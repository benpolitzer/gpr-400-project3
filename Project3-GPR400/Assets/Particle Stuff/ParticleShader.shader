Shader "Unlit/ParticleShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Size ("Size", Float) = 0.5
    }
    SubShader
    {
        Tags {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2g
            {
                float4 vertex : POSITION;
            };

            struct g2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Size;

            v2g vert (appdata v)
            {
                v2g o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            [maxvertexcount(6)]
            void geom(point v2g input[1], inout TriangleStream<g2f> triStream)
            {
                float4 pos = input[0].vertex;

                float4 camRight = float4(1, 0, 0, 0);
                float4 camUp    = float4(0, 1, 0, 0);

                float halfSize = _Size * 0.5;

                float4 right = camRight * halfSize;
                float4 up    = camUp * halfSize;

                float4 corners[4];
                corners[0] = pos - right - up;
                corners[1] = pos + right - up;
                corners[2] = pos + right + up;
                corners[3] = pos - right + up;

                float2 uvs[4] = {
                    float2(0,0),
                    float2(1,0),
                    float2(1,1),
                    float2(0,1)
                };


                g2f o;

                o.vertex = corners[0]; o.uv = uvs[0]; triStream.Append(o);
                o.vertex = corners[1]; o.uv = uvs[1]; triStream.Append(o);
                o.vertex = corners[2]; o.uv = uvs[2]; triStream.Append(o);
                triStream.RestartStrip();

                o.vertex = corners[0]; o.uv = uvs[0]; triStream.Append(o);
                o.vertex = corners[2]; o.uv = uvs[2]; triStream.Append(o);
                o.vertex = corners[3]; o.uv = uvs[3]; triStream.Append(o);
                triStream.RestartStrip();
            }

            fixed4 frag (g2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                return col;
            }
            ENDCG
        }
    }
}
