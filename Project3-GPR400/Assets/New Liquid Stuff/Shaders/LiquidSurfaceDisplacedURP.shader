// Benjamin Politzer - shader that reads the buffer from the compute and then moves each vertex vertically, 
// rebuilds an approximate normal from neighbors height, and shades the result as a transparent liquid surface.
Shader "Custom/LiquidSurfaceDisplacedURP"
{
    Properties
    {
        _SurfaceColor("Surface Color", Color) = (0.08, 0.42, 0.95, 0.42)
        _UnderColor("Underside Color", Color) = (0.01, 0.16, 0.35, 0.35)
        _HighlightColor("Highlight Color", Color) = (1, 1, 1, 1)
        _FresnelPower("Fresnel Power", Float) = 4
        _FresnelStrength("Fresnel Strength", Float) = 0.55
        _SpecularStrength("Specular Strength", Float) = 0.75
        _SpecularPower("Specular Power", Float) = 64
        _EdgeRimStrength("Edge Rim Strength", Float) = 0.1
        _ContainerRadiusX("Container Radius X", Float) = 0.48
        _ContainerRadiusZ("Container Radius Z", Float) = 0.48
        _EdgeSoftness("Edge Softness", Float) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "LiquidSurface"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            Cull Off

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct VertexState
            {
                float3 restPosOS;
                float heightOffset;
                float heightVelocity;
            };


            StructuredBuffer<VertexState> _LiquidState;

            int _GridWidth;
            int _GridHeight;

            float _GridSizeX;
            float _GridSizeZ;

            float4 _SurfaceColor;
            float4 _UnderColor;
            float4 _HighlightColor;

            float _FresnelPower;
            float _FresnelStrength;
            float _SpecularStrength;
            float _SpecularPower;
            float _EdgeRimStrength;

            float _ContainerRadiusX;
            float _ContainerRadiusZ;
            float _EdgeSoftness;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;

                // Original rest position before displacement
                float3 restPosOS : TEXCOORD3;
            };

            uint CoordToIndex(int x, int y)
            {
                return (uint)(y * _GridWidth + x);
            }

            // Ellipse signed distance approximation
            // Returns:
                // 1 near center
                // 0 at edge
                // <0 outside ellipse
            float ContainerSignedDistance01(float3 p)
            {
                float rx = max(_ContainerRadiusX, 0.0001);
                float rz = max(_ContainerRadiusZ, 0.0001);

                float2 q = float2(p.x / rx, p.z / rz);

                return 1.0 - length(q);
            }

            bool IsInsideContainer(float3 p)
            {
                return ContainerSignedDistance01(p) >= 0.0;
            }

            // Reads a neighboring height from compute buffer
                // If neighbor is outside grid or outside container mask, use fallbackHeight instead
            float GetHeightOrWall(int x, int y, float fallbackHeight)
            {
                if (x < 0 || x >= _GridWidth || y < 0 || y >= _GridHeight)
                    return fallbackHeight;

                uint index = CoordToIndex(x, y);
                VertexState ns = _LiquidState[index];

                if (!IsInsideContainer(ns.restPosOS))
                    return fallbackHeight;

                return ns.heightOffset;
            }

            Varyings vert(Attributes IN, uint vertexID : SV_VertexID)
            {
                Varyings OUT;

                // Get this vertexs simulated state from compute buffer
                VertexState s = _LiquidState[vertexID];

                // Convert vertex ID into grid coordinates
                int x = (int)(vertexID % (uint)_GridWidth);
                int y = (int)(vertexID / (uint)_GridWidth);

                // Start from original rest position
                float3 posOS = s.restPosOS;

                // Apply vertical displacement from compute simulation
                posOS.y += s.heightOffset;

                // Current height for this vertex
                float hC = s.heightOffset;

                // Neighbor heights used to estimate displaced normal
                float hL = GetHeightOrWall(x - 1, y, hC);
                float hR = GetHeightOrWall(x + 1, y, hC);
                float hD = GetHeightOrWall(x, y - 1, hC);
                float hU = GetHeightOrWall(x, y + 1, hC);

                // Grid spacing
                float dx = _GridSizeX / max(_GridWidth - 1, 1);
                float dz = _GridSizeZ / max(_GridHeight - 1, 1);

                // Build approximate tangents from neighboring heights
                float3 tangentX = normalize(float3(2.0 * dx, hR - hL, 0.0));
                float3 tangentZ = normalize(float3(0.0, hU - hD, 2.0 * dz));

                // Cross product gives a normal for displaced surface
                float3 normalOS = normalize(cross(tangentZ, tangentX));

                // Convert displaced position to clip/world space
                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normalize(TransformObjectToWorldNormal(normalOS));
                OUT.viewDirWS = normalize(GetWorldSpaceViewDir(posInputs.positionWS));
                OUT.restPosOS = s.restPosOS;

                return OUT;
            }

            half4 frag(Varyings IN, half faceSign : VFACE) : SV_Target
            {
                // Clip surface to container footprint
                float ellipse = ContainerSignedDistance01(IN.restPosOS);
                clip(ellipse);

                // Soft fade near the edge.
                float edgeAlpha = smoothstep(0.0, max(_EdgeSoftness, 0.0001), ellipse);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // front or back side
                bool backFace = faceSign < 0.0;

                if (backFace)
                    N = -N;

                // Get main directional light from URP
                Light mainLight = GetMainLight();

                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);

                // Simple direct lighting
                float ndotl = saturate(dot(N, L));
                float lighting = 0.25 + ndotl * 0.75;

                // Fresnel is stronger at grazing angles
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // Blinn-Phong style specular highlight
                float specular = pow(saturate(dot(N, H)), _SpecularPower) * _SpecularStrength;

                // Edge rim gets stronger near clipped boundary
                float edgeRim = 1.0 - edgeAlpha;
                edgeRim *= edgeRim;
                edgeRim *= _EdgeRimStrength;

                // Use a different color for top and underside
                float4 baseColor = backFace ? _UnderColor : _SurfaceColor;

                float3 color = baseColor.rgb * lighting;
                color += _HighlightColor.rgb * fresnel * _FresnelStrength;
                color += _HighlightColor.rgb * specular;
                color += _HighlightColor.rgb * edgeRim;

                float alpha = baseColor.a * saturate(edgeAlpha + 0.15);

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}