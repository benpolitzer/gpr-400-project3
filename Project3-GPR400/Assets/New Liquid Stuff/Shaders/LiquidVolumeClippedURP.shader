Shader "Custom/LiquidVolumeClippedURP"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.04, 0.32, 0.85, 0.28)

        _DeepColor("Deep Color", Color) = (0.005, 0.08, 0.26, 0.62)

        _LiquidPlaneWS("Liquid Plane WS", Vector) = (0, 1, 0, 0)

        _SurfaceOverlap("Surface Overlap", Float) = 0.025

        _TopFadeWidth("Top Fade Width", Float) = 0.08

        _DepthDarkening("Depth Darkening", Float) = 1.4

        _RimPower("Rim Power", Float) = 3
        _RimStrength("Rim Strength", Float) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent-20"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "LiquidVolume"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _BaseColor;
            float4 _DeepColor;

            float4 _LiquidPlaneWS;

            float _SurfaceOverlap;
            float _TopFadeWidth;
            float _DepthDarkening;
            float _RimPower;
            float _RimStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            // Data passed from vertex shader to fragment shader
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Convert object space position into all useful coordinate spaces
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                // Convert object space normal into world space
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normalize(normalInputs.normalWS);

                // Direction from fragment position toward camera
                OUT.viewDirWS = normalize(GetWorldSpaceViewDir(posInputs.positionWS));

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Unpack world space liquid plane
                float3 planeNormalWS = normalize(_LiquidPlaneWS.xyz);
                float planeDistance = _LiquidPlaneWS.w;

                // Positive value means this pixel is below liquid surface
                // Negative value means this pixel is above surface
                float belowSurface = planeDistance - dot(planeNormalWS, IN.positionWS);

                // Discard anything above liquid plane
                // SurfaceOverlap lets volume extend slightly past top surface
                clip(belowSurface + _SurfaceOverlap);

                // Used to fade volume near top surface
                float topBlend = saturate((belowSurface + _SurfaceOverlap) / max(_TopFadeWidth, 0.0001));

                // Used to darken liquid as it gets farther below top surface
                float depthFactor = saturate(belowSurface * _DepthDarkening);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // Fresnel/rim effect: stronger at grazing view angles
                float fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower);

                // Blend between shallow and deep colors
                float3 color = lerp(_BaseColor.rgb, _DeepColor.rgb, depthFactor);

                // Add a small rim highlight
                color += fresnel * _RimStrength;

                // Blend alpha between shallow and deep opacity
                float alpha = lerp(_BaseColor.a, _DeepColor.a, depthFactor);

                // Slightly fade near top seam so volume and top surface blend better
                alpha *= lerp(0.65, 1.0, topBlend);

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}