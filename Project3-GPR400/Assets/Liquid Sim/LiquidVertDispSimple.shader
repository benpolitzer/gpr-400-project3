Shader "Custom/LiquidVertDispSimple"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Pass{
            CGPROGRAM
            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

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

            struct v2f
            {
                float4 vertPos : SV_POSITION;
            };

        
            v2f vert (appdata input)
            {
                v2f o;
                //sends out data gotten from the buffer sampling based on vertex id
                vertexData v = verts[input.vertexID];
                o.vertPos = float4(v.position, 1.0);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = fixed4(1.0,1.0,1.0,1.0);
                return col;
            }

            ENDCG
        }
    }
    FallBack "Diffuse"
}
