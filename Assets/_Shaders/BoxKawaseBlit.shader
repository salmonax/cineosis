Shader "Unlit/BoxKawaseBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
            #include "./cginc/blur.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {            
                //half4 tex = tex2D(_MainTex, i.uv);

                // This was temporary, to test two kinds of blit, but misnormer:
                half4 tex = slowGaussianBlur(_MainTex, i.uv, _MainTex_TexelSize.xy, 12, 1);

                //half4 tex = (kawaseBlur(_MainTex, i.uv, _MainTex_TexelSize.xy, 1) +
                //boxBlur(_MainTex, i.uv, _MainTex_TexelSize.xy))/2;

                //tex.rgba = float4(0,0,0,1);

                return tex;
            }
            ENDCG
        }
    }
}
