Shader "Custom/StencilTest"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Name "Forward1"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            Stencil
            {
                Ref 1          // value to write
                Comp Always    // always pass
                Pass Replace   // replace stencil with Ref
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

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

                // Convert the vertex position into clip space for rendering
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                // Convert the object space normal into world space for lighting later
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normalize(normInputs.normalWS);

                // Store view direction so the fragment shader can do rim shading
                OUT.viewDirWS = normalize(GetWorldSpaceViewDir(posInputs.positionWS));

                return OUT;
            }

            half4 frag(Varyings IN, half faceSign : VFACE) : SV_Target
            {
                if(IN.positionWS.x > 0)
                {
                    clip(-1);
                }
                return float4(1,0,0,1);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Forward2"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            Stencil
            {
                Ref 1
                Comp NotEqual  // only pass where stencil != 1
                Pass Keep      // don’t modify stencil
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

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

                // Convert the vertex position into clip space for rendering
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                // Convert the object space normal into world space for lighting later
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normalize(normInputs.normalWS);

                // Store view direction so the fragment shader can do rim shading
                OUT.viewDirWS = normalize(GetWorldSpaceViewDir(posInputs.positionWS));

                return OUT;
            }

            half4 frag(Varyings IN, half faceSign : VFACE) : SV_Target
            {
                return float4(0,1,0,1);
            }

            ENDHLSL
        }
    }
}
