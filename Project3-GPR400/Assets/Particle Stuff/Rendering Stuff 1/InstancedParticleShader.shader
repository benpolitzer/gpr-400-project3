Shader "Unlit/InstancedParticleShader"
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
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #include "UnityCG.cginc"

            //must be the same as compute and scripts
            struct vertexData
            {
                float3 position;
            };

            //buffer from compute
            StructuredBuffer<vertexData> verts;

            struct appdata
            {
                uint vertexID : SV_VertexID;
            };

            struct v2g
            {
                float4 vertPos : POSITION;
            };

            struct g2f
            {
                float2 uv : TEXCOORD0;
                float4 vertPos : POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Size;

            v2g vert (appdata input)
            {
                v2g o;
                //sends out data gotten from the buffer sampling based on vertex id
                vertexData v = verts[input.vertexID];
                o.vertPos = UnityObjectToClipPos(v.position);
                return o;
            }

            [maxvertexcount(6)]
            void geom(point v2g input[1], inout TriangleStream<g2f> triStream)
            {
                //adding a point in each corner
                float4 pos = input[0].vertPos;

                float4 camRight = float4(1, 0, 0, 0);
                float4 camUp    = float4(0, 1, 0, 0);

                float halfSize = _Size * 0.5;

                float4 right = camRight * halfSize;
                float4 up    = camUp * halfSize;

                float4 corners[4];
                corners[0] = pos - right - up;
                corners[1] = pos + right - up;
                corners[2] = pos - right + up;
                corners[3] = pos + right + up;

                //setting UV data to match our corners with texture location
                float2 uvs[4] = {
                    float2(0,1),
                    float2(1,1),
                    float2(0,0),
                    float2(1,0)
                };

                //adding a quad by adding 4 points to the triangle strip
                g2f o;

                o.vertPos = corners[0]; o.uv = uvs[0]; triStream.Append(o);
                o.vertPos = corners[1]; o.uv = uvs[1]; triStream.Append(o);
                o.vertPos = corners[2]; o.uv = uvs[2]; triStream.Append(o);
                o.vertPos = corners[3]; o.uv = uvs[3]; triStream.Append(o);
                triStream.RestartStrip();
            }

            fixed4 frag (g2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                //fixed4 col = fixed4(i.uv.r, i.uv.g, 0, 1);
                return col;
            }
            ENDCG
        }
    }
}
