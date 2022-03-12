Shader "Unlit/LightingBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "./cginc/shapes.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _MainTex_ST;
            float2 _LaserCoordLeft = float2(0,0);
            float2 _LaserCoordRight = float2(0,0);
            float _Layout = 0;
     
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //return float4(0,0,0,1);
              float left = 1-circle(i.uv.xy, _LaserCoordLeft, 0.1, 0.07, _Layout);
              float right = 1-circle(i.uv.xy, _LaserCoordRight, 0.1, 0.07, _Layout);
              return 1-left*right;
            }
            ENDCG
        }
    }
}

