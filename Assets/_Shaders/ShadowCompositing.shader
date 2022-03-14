Shader "Custom/ShadowCompositing"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "black" {}
        _SceneTex("Scene Texture", 2D) = "black" {}

        _kernel("Gauss Kernel", Vector) = (0,0,0,0)
        _kernelWidth("Gauss Kernel Width", Float) = 1

        /* Delta since last blur capture */
        _RightHandDeltaX ("Right Hand Delta X", Range (-0.4, 0.4)) = 0
        _RightHandDeltaY ("Right Hand Delta Y", Range (0, 0.4)) = 0

        _RightHandZ ("Right Hand Z", Range (0, 0.4))  = 0
        _BlurScale ("Blur Scale", Range(1, 10)) = 0

        _GrainBias("Grain Bias (0.6)", Range(0, 2)) = 0
    }
    SubShader
    {
        ZTest Always
//        Cull Off ZWrite Off

//        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            sampler2D _MainTex; // blurred, displaced, delayed tex
            float2 _MainTex_TexelSize;
            sampler2D _MaskTex; // original mask
            sampler2D _SceneTex;
            sampler2D _GrainTex;

            float _GrainBias;

            float _RightHandDeltaX;
            float _RightHandDeltaY;
            float _RightHandZ;

            float _HandOffsetX = 0;
            float _HandOffsetY = 0;

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

                //o.uvs = o.pos.xy;
                return o;
            }

            float4 frag (v2f i) : COLOR
            {
                float tx = _MainTex_TexelSize.x;
                float ty = _MainTex_TexelSize.y;
                float4 blurTex = tex2D(_MainTex, float2(i.uvs.x - _RightHandDeltaX*tx - _HandOffsetX*tx, i.uvs.y - _RightHandDeltaY*ty - _HandOffsetY*ty));
                float4 maskTex = tex2D(_MaskTex, float2(i.uvs.x - _HandOffsetX*tx, i.uvs.y - _HandOffsetY*ty));
                //float4 maskTex = tex2D(_MaskTex, float2(i.uvs.x - _RightHandDeltaX*tx, i.uvs.y - _RightHandDeltaY*ty));
                float4 sceneTex = tex2D(_SceneTex, i.uvs.xy);

                //return 1-maskTex*2;
                /* 0.5 would allow for some leeway if I decide to Blit this at a smaller size: */
                if (maskTex.r > 0) {
                    // For passthrough cutout:
                    //sceneTex.a = max((1-maskTex.r*1.75)*sceneTex.a, 0);
                    //sceneTex.rgb *= max((1-maskTex.r*1.75), 0);

                    // For outlined shadow-hand (ugly)
                    if (maskTex.r > 0.25 && maskTex.r < 0.5)
                        //sceneTex.rgb = float3(1, 0, 0.5);
                        sceneTex.rgb = float3(0.42, 0.06, 0.92);
                    else 
                        sceneTex.rgb *= max((1-maskTex.r*1.75), 0.5);



                    // For just the model:
                    //return sceneTex;
                }

                /* Ignore if very dark, which includes the masked parts of the scene: */
                if (sceneTex.r < 0.01)
                    blurTex.r = 0;

                //return sceneTex;

                float redRatio = ((sceneTex.g + sceneTex.b)/2)/sceneTex.r; /* make scene-relative redness */
                half4 color = sceneTex;// - blurTex.r * half4(sceneTex.r*max(0.7,0.8*min(1,redRatio)),sceneTex.g*0.8,sceneTex.b*0.8,0)*_RightHandZ/0.4;
                // For visual testing:
                //half4 color = sceneTex + intensityRadius * half4(1,1,1,1);

                // fix? maybe unnecessary
                //color.r = max(tex2D(_SceneTex, i.uvs.xy).r-intensityRadius, 0);

                /* Note: higher power multiplier biases towards higher grain at higher exposures: */
                return float4(color.rgb - tex2D(_GrainTex, i.uvs)*pow(color.rgb*7,_GrainBias), color.a);
            }
            ENDCG

        } // end pass
    } // end sub
} // end shader
