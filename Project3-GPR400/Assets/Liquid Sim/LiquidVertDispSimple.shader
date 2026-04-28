Shader "Custom/LiquidVertDispSimple"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.8, 1.0, 0.75)

        _ClipCenterX ("Clip Center X", Float) = 0
        _ClipCenterZ ("Clip Center Z", Float) = 0
        _ClipRadiusX ("Clip Radius X", Float) = 0.5
        _ClipRadiusZ ("Clip Radius Z", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM

            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            fixed4 _Color;

            float _ClipCenterX;
            float _ClipCenterZ;
            float _ClipRadiusX;
            float _ClipRadiusZ;

            struct vertexData
            {
                float3 position;
            };

            StructuredBuffer<vertexData> verts;

            struct appdata
            {
                float4 vertex : POSITION;
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                float4 vertPos : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f o;

                vertexData v = verts[input.vertexID];

                o.localPos = v.position;
                o.vertPos = UnityObjectToClipPos(float4(v.position, 1.0));

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float x = i.localPos.x - _ClipCenterX;
                float z = i.localPos.z - _ClipCenterZ;

                float normalizedX = x / _ClipRadiusX;
                float normalizedZ = z / _ClipRadiusZ;

                float ellipseValue = normalizedX * normalizedX + normalizedZ * normalizedZ;

                // Keep pixels inside the ellipse.
                // Discard pixels outside.
                clip(1.0 - ellipseValue);

                return _Color;
            }

            ENDCG
        }
    }
}