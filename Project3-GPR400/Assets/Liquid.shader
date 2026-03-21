Shader "Custom/FakeLiquidURP_Section"
{
    Properties
    {
        _LiquidColor("Liquid Color", Color) = (0.1, 0.5, 1.0, 0.75)
        _TopColor("Top / Section Color", Color) = (0.85, 0.95, 1.0, 0.95)
        _RimColor("Rim Color", Color) = (1,1,1,1)

        _SurfaceOriginWS("Surface Origin WS", Vector) = (0,0,0,0)

        _WobbleX("Wobble X", Range(-1,1)) = 0
        _WobbleZ("Wobble Z", Range(-1,1)) = 0

        _WaveAmp("Wave Amplitude", Range(0,0.1)) = 0.02
        _WaveFreq("Wave Frequency", Range(0,20)) = 8
        _WaveSpeed("Wave Speed", Range(0,10)) = 2

        _EdgeWidth("Edge Width", Range(0.001,0.1)) = 0.03
        _RimPower("Rim Power", Range(0.5,8)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

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

            CBUFFER_START(UnityPerMaterial)
                float4 _LiquidColor;
                float4 _TopColor;
                float4 _RimColor;
                float4 _SurfaceOriginWS;
                float _WobbleX;
                float _WobbleZ;
                float _WaveAmp;
                float _WaveFreq;
                float _WaveSpeed;
                float _EdgeWidth;
                float _RimPower;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normalize(normInputs.normalWS);
                OUT.viewDirWS = normalize(GetWorldSpaceViewDir(posInputs.positionWS));

                return OUT;
            }

            half4 frag(Varyings IN, half faceSign : VFACE) : SV_Target
            {
                float3 liquidUpWS = normalize(float3(_WobbleX, 1.0, _WobbleZ));
                float3 relativeWS = IN.positionWS - _SurfaceOriginWS.xyz;

                float wave =
                    sin(relativeWS.x * _WaveFreq + _Time.y * _WaveSpeed) * _WaveAmp +
                    sin(relativeWS.z * (_WaveFreq * 0.8) + _Time.y * (_WaveSpeed * 1.2)) * (_WaveAmp * 0.5);

                float surface = dot(relativeWS, liquidUpWS) - wave;

                clip(-surface);

                float edgeBand = smoothstep(0.0, _EdgeWidth, abs(surface));

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower);

                bool isFrontFace = faceSign > 0;

                float4 baseCol = isFrontFace ? _LiquidColor : _TopColor;

                baseCol.rgb = lerp(baseCol.rgb, _TopColor.rgb, 1.0 - edgeBand);

                if (isFrontFace)
                {
                    baseCol.rgb += _RimColor.rgb * fresnel * 0.15;
                }

                return baseCol;
            }
            ENDHLSL
        }
    }
}