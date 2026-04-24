// https://www.patreon.com/posts/fake-liquid-urp-75665057
// Victor - using the other shader as a base
Shader "Custom/FakeLiquidMarched"
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
            Cull Front

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
                // Build a tilted up direction for the liquid surface
                // _WobbleX and _WobbleZ come from script (used to fake liquid movement/displacement)
                float3 liquidUpWS = normalize(float3(_WobbleX, 1.0, _WobbleZ));

                // Position of current pixel relative to the liquid surface origin in world space
                float3 relativeWS = IN.positionWS - _SurfaceOriginWS.xyz;

                // Increase ripple amplitude by wobble amount when bottle is in motion
                float wobbleAmount = saturate(length(float2(_WobbleX, _WobbleZ)) * 8.0);
                float dynamicAmp = lerp(_WaveAmp * 0.35, _WaveAmp, wobbleAmount);

                // Animate the wave motion over time
                float t = _Time.y * _WaveSpeed;

                float4 baseCol = _LiquidColor;
                
                float3 camPos = GetCameraPositionWS();
                float3 camRay = normalize(IN.positionWS - camPos);
                float3 currentPos = IN.positionWS;

                for(int i = 0; i < 100; i++)
                {
                    relativeWS = currentPos - _SurfaceOriginWS;
                    float wave1 = sin(relativeWS.x * _WaveFreq + t) * dynamicAmp;
                    float wave2 = sin(relativeWS.z * (_WaveFreq * 0.85) + t * 1.17) * (dynamicAmp * 0.55);
                    float wave3 = sin((relativeWS.x + relativeWS.z) * (_WaveFreq * 0.6) + t * 0.73) * (dynamicAmp * 0.4);
                    float wave4 = sin((relativeWS.x - relativeWS.z) * (_WaveFreq * 1.35) - t * 1.41) * (dynamicAmp * 0.2);
                    float wave = wave1 + wave2 + wave3 + wave4;
                    wave *= 3;

                    float sdf = dot(relativeWS, liquidUpWS) - wave;
                        
                    if(sdf > 0)
                    {
                        clip(-1);
                        return float4(0,0,0,0);
                    }

                    if(sdf > -0.01 && sdf < 0)
                    {
                        float4 col = float4(saturate(relativeWS), 1);
                        col = float4(-wave,-wave,-wave,1);
                        //col = float4(0,1,0,1);
                        return col;
                    }
                    
                    // if((distance(camPos, currentPos) > distance(camPos, IN.positionWS)))
                    // {
                    //     clip(-1);
                    //     return float4(0,0,0,0);
                    // }
                        
                    currentPos += camRay * sdf;
                }

                return float4(1,0,0,1);

            }

            ENDHLSL
        }
        
        //GrabPass { "_BackFaceTex" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            sampler2D _BackFaceTex;

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
                // Build a tilted up direction for the liquid surface
                // _WobbleX and _WobbleZ come from script (used to fake liquid movement/displacement)
                float3 liquidUpWS = normalize(float3(_WobbleX, 1.0, _WobbleZ));

                // Position of current pixel relative to the liquid surface origin in world space
                float3 relativeWS = IN.positionWS - _SurfaceOriginWS.xyz;

                // Increase ripple amplitude by wobble amount when bottle is in motion
                float wobbleAmount = saturate(length(float2(_WobbleX, _WobbleZ)) * 8.0);
                float dynamicAmp = lerp(_WaveAmp * 0.35, _WaveAmp, wobbleAmount);

                // Animate the wave motion over time
                float t = _Time.y * _WaveSpeed;

                float4 baseCol = _LiquidColor;
                
                float3 camPos = GetCameraPositionWS();
                float3 camRay = normalize(IN.positionWS - camPos);
                float3 currentPos = IN.positionWS;

                for(int i = 0; i < 100; i++)
                {
                    relativeWS = currentPos - _SurfaceOriginWS;
                    float wave1 = sin(relativeWS.x * _WaveFreq + t) * dynamicAmp;
                    float wave2 = sin(relativeWS.z * (_WaveFreq * 0.85) + t * 1.17) * (dynamicAmp * 0.55);
                    float wave3 = sin((relativeWS.x + relativeWS.z) * (_WaveFreq * 0.6) + t * 0.73) * (dynamicAmp * 0.4);
                    float wave4 = sin((relativeWS.x - relativeWS.z) * (_WaveFreq * 1.35) - t * 1.41) * (dynamicAmp * 0.2);
                    float wave = wave1 + wave2 + wave3 + wave4;
                    wave *= 3;

                    float sdf = dot(relativeWS, liquidUpWS) - wave;
                    
                    if(sdf < 0)
                    {
                        return baseCol;
                        clip(-1);
                        return float4(0,0,0,0);
                    }

                    if(sdf < 0.01 && sdf > 0)
                    {
                        float4 col = float4(saturate(relativeWS), 1);
                        col = float4(-wave,-wave,-wave,1);
                        //col = float4(0,1,0,1);
                        return col;
                    }
                    
                    if((distance(camPos, currentPos) < distance(camPos, IN.positionWS)))
                    {
                        clip(-1);
                        return float4(0,0,0,0);
                    }

                    currentPos += camRay * sdf;
                }

                return float4(1,0,0,1);

            }

            ENDHLSL
        }
    }
}