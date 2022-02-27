Shader "Unlit/ColorArrayMaskBlit"
{
    Properties
    {
        _MainTex ("Mini Panorama", 2D) = "white" {}
        _DynAlphaTex("Dynamic Alpha Tex", 2D) = "white" {}
        _TestX("Matte Threshold X", Range(0, 1)) = 0.01
        _TestY("Matte Threshold Y", Range(0, 1)) = 1
         TestZ("Matte Threshold Z", Range(0.2, 8)) = 0.3
//         TestU("Matte Threshold U", Range(0, 0.2)) = 0.01
  //      _TestV("Matte Threshold V", Range(0, 1)) = 1
    //     TestW("Matte Threshold W", Range(0.2, 8)) = 0.3
        // For now, just doing one eye, to see.
        //_LeftColorExclusionArray("Left Color Exclusion Array", Color) = (0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
//
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _DynAlphaTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float3 _LeftColorExclusionArray[40];
            float3 _LeftColorInclusionArray[40];
            float _ColorArrayLength;
            float _TestX;
            float _TestY;
            float _TestZ;
            //float _TestU;
           // float _TestV;
            //float _TestW;

            float Epsilon = 1e-10;
 
        float3 RGBtoHCV(in float3 RGB)
        {
            // Based on work by Sam Hocevar and Emil Persson
            float4 P = (RGB.g < RGB.b) ? float4(RGB.bg, -1.0, 2.0/3.0) : float4(RGB.gb, 0.0, -1.0/3.0);
            float4 Q = (RGB.r < P.x) ? float4(P.xyw, RGB.r) : float4(RGB.r, P.yzx);
            float C = Q.x - min(Q.w, Q.y);
            float H = abs((Q.w - Q.y) / (6 * C + Epsilon) + Q.z);
            return float3(H, C, Q.x);
        }

        
        float3 RGBtoHSV(in float3 RGB)
        {
            float3 HCV = RGBtoHCV(RGB);
            float S = HCV.y / (HCV.z + Epsilon);
            return float3(HCV.x, S, HCV.z);
        }


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 dynAlpha = tex2D(_DynAlphaTex, i.uv);
                float3 texLab = rgb2lab(tex);
            
//              return tex;
                //return float4(rgb2lrgb(_LeftColorExclusionArray[0].rgb), 1);
                ////return tex;

                float eyeFactor = 0;
                if (i.uv.x > 0.5)
                    eyeFactor = 0.5;

                //float stX = pow(tc.x - (0.25 + eyeFactor), 2)*25; // was 25.    
                //float stY = pow(tc.y, 1.5); // was 1.5, no shift term

                float stX = pow(i.uv.x - (0.25 + eyeFactor), 2)*40; // was 25.    
                float stY = pow(i.uv.y, 0.6) - 0.35; // was 1.5, no shift term
 
                float screenThresh = (stX + stY)/2;


                float4 output = float4(0,0,0,1);
                float3 texHSV = RGBtoHSV(tex);
                for (int i = 0; i < _ColorArrayLength; i++) {
                    if (_LeftColorInclusionArray[i].r >= 0) {
                        float3 includeColor = _LeftColorInclusionArray[i];

                        //float includeSwatchDist = ColorDistance(includeColor, tex);
                        //float includeSwatchDist = abs(RGBtoHCV(includeColor).x - RGBtoHCV(tex).x);

                        float includeSwatchDist = pow(includeColor.x - texHSV.x,2);
                        //float satDist = pow(includeColor.y - texHSV.y, 2);

                        float valueDist = pow(includeColor.z - texHSV.z, 2);

                        //float miracleCure = sqrt(includeSwatchDist + valueDist*0.004)*min((includeColor.z+texHSV.z*4)/5/0.1, 1);

                        float miracleCure = sqrt(includeSwatchDist)*min((includeColor.z+texHSV.z*4)/5/0.2, 1);
                        //float includeSwatchDist = cie76(rgb2lab(includeColor), texLab);
                        //min(pow((includeColor.z+texHSV.z)/2/0.2, 8), 1)

                        if (miracleCure < _TestX*pow(1-screenThresh, 8) ||
                            miracleCure < dynAlpha.r/10 // was 20 for a second
                        )
                            output.rgb += float3(_TestY,_TestY,_TestY);
                        //output.rgb = float3(1,1,1);
                    }   
                    if (_LeftColorExclusionArray[i].r >= 0) {
                        float3 excludeColor = _LeftColorExclusionArray[i];

                        //float includeSwatchDist = ColorDistance(includeColor, tex);
                        //float includeSwatchDist = abs(RGBtoHCV(includeColor).x - RGBtoHCV(tex).x);

                        float excludeSwatchDist = pow(excludeColor.x - texHSV.x,2);
                        float valueDist = pow(excludeColor.z - texHSV.z, 2);
                        float miracleCure = sqrt(excludeSwatchDist + valueDist*0.005);
                        //float excludeSwatchDist = cie76(rgb2lab(excludeColor), texLab);
                        //min(pow((excludeColor.z+texHSV.z)/2/0.2, 8), 1)

                        if (miracleCure < _TestX*pow(screenThresh*2,1.25) ||
                            miracleCure < (1-dynAlpha.r)/10
                        )
                            output.rgb -= float3(_TestY,_TestY,_TestY);


                        //float3 excludeColor = rgb2lrgb(_LeftColorExclusionArray[i]);

                        //float excludeSwatchDist = abs(RGBtoHSV(excludeColor).x - RGBtoHSV(tex).x);

                        //float excludeSwatchDist = cie76(rgb2lab(excludeColor), texLab);

                        //if (excludeSwatchDist < 0.06)

                        //if (excludeSwatchDist <= pow(screenThresh*1.5, 3)) /* higher power increases inner resistance */
                            //output.rgb = max(output.rgb - float3(0.5,0.5,0.5), float3(0,0,0));
                    }
                }
                return output;
            }
            ENDCG
        }
    }
}
