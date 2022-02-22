Shader "Unlit/ColorMaskGaussianBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Samples("Blur Amount", Range(0, 20)) = 5
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
//
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
            float _Samples;
     
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

                half4 tex = slowGaussianBlur(_MainTex, i.uv, _MainTex_TexelSize.xy, _Samples, 1);

                //tex.rgba = float4(0,0,0,1);

                return tex;
            }
            ENDCG
        }
    }
}
