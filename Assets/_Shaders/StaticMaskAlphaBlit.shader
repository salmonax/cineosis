Shader "Unlit/StaticMaskAlphaBlit"
{
    Properties
    {
        _LastTex ("Texture", 2D) = "white" {}
        _LastTex2 ("Texture", 2D) = "white" {}
        _LastTex3 ("Texture", 2D) = "white" {}
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
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "./cginc/rgb2lab.cginc"
            #include "./cginc/blur.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _LastTex;
            float4 _LastTex_ST;
            float4 _LastTex_TexelSize;
            sampler2D _LastTex2;
            float4 _LastTex2_ST;
            float4 _LastTex2_TexelSize;
            sampler2D _LastTex3;
            float4 _LastTex3_ST;
            float4 _LastTex3_TexelSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _LastTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 tex = float4(0,0,0,1);

                
                half4 last = tex2D(_LastTex, i.uv);
                half4 last2 = tex2D(_LastTex2, i.uv);
                half4 last3 = tex2D(_LastTex3, i.uv);

                float3 lastLab = rgb2lab(last);
                float3 lastLab2 = rgb2lab(last2);
                float3 lastLab3 = rgb2lab(last3);

                float eyeFactor = 0;
                if (i.uv.x > 0.5)
                    eyeFactor = 0.5;

                //float stX = pow(tc.x - (0.25 + eyeFactor), 2)*25; // was 25.    
                //float stY = pow(tc.y, 1.5); // was 1.5, no shift term

                float stX = pow(i.uv.x - (0.25 + eyeFactor), 2)*40; // was 25.    
                float stY = pow(i.uv.y, 0.6) - 0.35; // was 1.5, no shift term
 
                float screenThresh = (stX + stY)/2;


                float d4 = cie94(lastLab, lastLab2);
                float d5 = cie94(lastLab2, lastLab3);
                float d6 = cie94(lastLab, lastLab3);

                if (pow(d4, 0.33) > screenThresh)
                    tex.rgb = float4(1,1,1,1);
                if (pow(d5, 0.33) > screenThresh)
                    tex.rgb = float4(1,1,1,1);
                if (pow(d6, 0.33) > screenThresh)
                    tex.rgb = float4(1,1,1,1);

//
                //tex.rgba = float4(screenThresh, screenThresh, screenThresh, 1);


                // fixed4 col = slowGaussianBlur(_LastTex, i.uv, _LastTex_TexelSize.xy, 35, 2);
                //col.rgb = float3(0,0,0);

                // apply fog

                return tex;
            }
            ENDCG
        }
    }
}
