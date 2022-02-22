Shader "Unlit/TwoPassGaussian"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "black" {}

        _kernelWidth("Gauss Kernel Width", Float) = 1
        _kernel("Gauss Kernel", Vector) = (0,0,0,0)

        _BlurX ("Blur X", Range(0, 5)) = 0.2
        _BlurY ("Blur Y", Range(0, 5)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZWrite Off
        //LOD 100
        //ZTest Always
//        Cull Off ZWrite Off

//        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            float _kernel[16];
            float _kernelWidth;

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _BlurX;
            float _BlurY;
    


           v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
/*
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;

                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
*/
                // maybe fix UVs
                #if UNITY_UV_STARTS_AT_TOP
                float scale = 1.0;
                #else
                float scale = -1.0;
                #endif

                o.uv = float2(o.vertex.x, o.vertex.y*scale)/ 2 + 0.5;

                return o;
            }

            float4 frag (v2f i) : COLOR
            {
                //return float4(1,1,1,1);
                //return tex2D(_SceneTex, i.uv.xy);
                // discard if under fragemnt

                int iterations = _kernelWidth; // arbitrary

                float tx = _MainTex_TexelSize.x;
                float ty = _MainTex_TexelSize.y;
                float blurredAlpha = 0;

                for (int k=0; k < iterations; k++) {           
                    blurredAlpha += _kernel[k]*tex2D(
                        _MainTex,
                        float2(
                            i.uv.x + (k - iterations*0.5)*tx*_BlurX, /* Z is for blur, not shifting here */
                            i.uv.y  /* Note the Z term for shifting! */
                        )
                    ).r;
                }
                return blurredAlpha;
            }
            ENDCG
        } // end pass

        GrabPass{}

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            float _kernel[16];
            float _kernelWidth;

            sampler2D _GrabTexture;
            float4 _GrabTexture_ST;
            float4 _GrabTexture_TexelSize;
            float _BlurX;
            float _BlurY;
    


           v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _GrabTexture);
                return o;
            }

            float4 frag (v2f i) : COLOR
            {
                float tx = _GrabTexture_TexelSize.x;
                float ty = _GrabTexture_TexelSize.y;

                int iterations = _kernelWidth; // arbitrary
                float4 blurredAlpha = 0;

                for (int k=0; k < iterations; k++) {           
                    blurredAlpha += _kernel[k] * tex2D(
                        _GrabTexture,
                        float2(
                            i.uv.x,
                            1-i.uv.y + (k - iterations*0.5)*ty*_BlurY
                        )
                    );
                }
                
                return blurredAlpha;
            }
            ENDCG

        } // end pass
    } // end sub
} // end shader
