Shader "Custom/PostOutline"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "black" {}
        _SceneTex("Scene Texture", 2D) = "black" {}

        _kernel("Gauss Kernel", Vector) = (0,0,0,0)
        _kernelWidth("Gauss Kernel Width", Float) = 1

        _RightHandX ("Right Hand X", Range (-0.4, 0.4)) = 0
        _RightHandY ("Right Hand Y", Range (0, 0.4)) = 0
        _RightHandZ ("Right Hand Z", Range (0, 0.4))  = 0
        _BlurScale ("Blur Scale", Range(1, 10)) = 0
    }
    SubShader
    {
        ZTest Always
//        Cull Off ZWrite Off

//        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            float _kernel[8];
            float _kernelWidth;
            sampler2D _MainTex;
            sampler2D _SceneTex;
            float2 _MainTex_TexelSize;

            float _BlurScale;
            float _RightHandZ;
            float _RightHandY;

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uvs : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                // maybe fix UVs
                #if UNITY_UV_STARTS_AT_TOP
                float scale = 1.0;
                #else
                float scale = -1.0;
                #endif

                o.uvs = float2(o.pos.x, o.pos.y*scale)/ 2 + 0.5;

                return o;
            }

            float4 frag (v2f i) : COLOR
            {
                //return float4(1,1,1,1);
                //return tex2D(_SceneTex, i.uvs.xy);
                // discard if under fragemnt

                int iterations = _kernelWidth; // arbitrary

                float tx = _MainTex_TexelSize.x;
                float ty = _MainTex_TexelSize.y;
                float intensityRadius = 0;

                for (int k=0; k < iterations; k++) {           
                    intensityRadius += _kernel[k]*tex2D(
                        _MainTex,
                        float2(
                            i.uvs.x + (k - iterations*0.5)*tx*((1 - _RightHandZ/0.4)*16+3), /* Z is for blur, not shifting here */
                            i.uvs.y + _RightHandY*(0.05 + (1-(_RightHandZ/0.4))*0.50) /* Note the Z term for shifting! */
                            //0,
                            //0
                            // Tweaked from 2/7 but didn't quite like (smaller minimum shift, minimum blur): 
                            //i.uvs.x + (k - iterations/2)*tx*((1 - _RightHandZ/0.4)*16+1), // Z is for blur, not shifting here
                            //i.uvs.y + _RightHandY*(0.02 + (1-(_RightHandZ/0.4))*0.50) /* Note the Z term for shifting! */
                        )
                    ).r;
                }

                //intensityRadius *= 0.010;

                //half4 color = tex2D(_SceneTex, i.uvs.xy) + intensityRadius * half4(0,1,1,1);

                // fix? maybe unnecessary
                //color.r = max(tex2D(_SceneTex, i.uvs.xy).r-intensityRadius, 0);


                return intensityRadius;
                
            }
            ENDCG
        } // end pass

        GrabPass{}

        Pass
        {
            CGPROGRAM
            float _kernel[8];
            float _kernelWidth;
            sampler2D _MainTex;
            sampler2D _SceneTex;

            sampler2D _GrabTexture;
            float2 _GrabTexture_TexelSize;

            float _BlurScale;
            float _RightHandX;
            float _RightHandZ;

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uvs : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                // maybe fix UVs
                o.uvs = o.pos.xy / 2 + 0.5;
                return o;
            }

            float4 frag (v2f i) : COLOR
            {
                //return float4(1,1,1,1);
                //return tex2D(_SceneTex, i.uvs.xy);
                // discard if under fragemnt

                float tx = _GrabTexture_TexelSize.x;
                float ty = _GrabTexture_TexelSize.y;

                float4 outlineTex = tex2D(_MainTex, i.uvs.xy);
                float4 sceneTex = tex2D(_SceneTex, i.uvs.xy);

                // 0.5 allows for some leeway if filtering
                if (outlineTex.r > 0.5) {
                    return sceneTex;
                }

                int iterations = _kernelWidth; // arbitrary
                float4 intensityRadius = 0;

                /* This creates an illusion that the shadow recedes as Z approaches 0 */
                float eyeTerm = 0.20;
                if (unity_StereoEyeIndex == 1)
                    eyeTerm = -0.20;

                for (int k=0; k < iterations; k++) {           
                    intensityRadius += _kernel[k] * tex2D(
                        _GrabTexture,
                        float2(
                            i.uvs.x + _RightHandX*(0.05 + (1-(_RightHandZ/0.4))*(0.50+eyeTerm)), /* magnification on Z term */
                            1-i.uvs.y + (k - iterations*0.5)*ty*((1 - _RightHandZ/0.4)*16+3)
                            //0,
                            //0
                            // Tweaked from 2/7, but didn't quite like (note convoluted clamping and RightHandX-narrowing):
                            //i.uvs.x + max(-0.6,min(_RightHandX/0.3, 0.6))*(0.02 + (1-(_RightHandZ/0.4))*(0.50+eyeTerm)), /* magnification on Z term */
                            //1-i.uvs.y + (k - iterations/2)*ty*((1 - _RightHandZ/0.4)*16+2)
                        )
                        
                            
                        
                    );
                }

                //intensityRadius *= 0.010;

                if (sceneTex.r < 0.01)
                    intensityRadius = 0;


                float redRatio = ((sceneTex.g + sceneTex.b)/2)/sceneTex.r; // make scene-relative redness
                half4 color = sceneTex - intensityRadius * half4(sceneTex.r*max(0.7,0.8*min(1,redRatio)),sceneTex.g*0.8,sceneTex.b*0.8,0)*_RightHandZ/0.4;
                // For visual testing:
                //half4 color = sceneTex + intensityRadius * half4(1,1,1,1);

                // fix? maybe unnecessary
                //color.r = max(tex2D(_SceneTex, i.uvs.xy).r-intensityRadius, 0);


                return color;
                
            }
            ENDCG

        } // end pass
    } // end sub
} // end shader
