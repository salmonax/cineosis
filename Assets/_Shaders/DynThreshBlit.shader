Shader "Unlit/DynThreshBlit"
{
    Properties
    {
        _MainTex ("Entering Frame (_MainTex)", 2D) = "white" {}
        _LastTex ("LastTex", 2D) = "grey" {}
        _LastTex2 ("LastTex2", 2D) = "grey" {}
        _LastTex3 ("LastTex3", 2D) = "grey" {}
        _InnerThreshTex ("Inner Thresh", 2D) = "black" {} /* highly blurred mini-thresh, to help with holes */
        _ThreshTex ("Running Thresh (_ThreshTex)", 2D) = "black" {}
        _SampleDecay ("SampleDecay", Range(0, 2)) = 0
        _OutputDecay ("OutputDecay", Range(0, 2)) = 0
        _DistMultiplier ("DistMultiplier", Range(0, 25)) = 1
        _DistPower ("DistPower", Range(0, 5)) = 1

        _ColorDistMultThresh ("ColorDistMultThresh", Range(0, 0.1)) = 0
        _ColorDistMultStrength ("ColorDistMultStrength", Range(0, 30)) = 0
        _ColorDistMultMax ("ColorDistMultMax", Range(0, 40)) = 0

        _DecayDampThresh ("DecayDampThresh", Range(0, 0.2)) = 0
        _DecayDampStrength ("DecayDampStrength", Range(0, 20)) = 0

        [MaterialToggle] _UseHueValueInclude("Use Hue-Value (Include)", Float) = 0
        [MaterialToggle] _UseHueValueExclude("Use Hue-Value (Exclude)", Float) = 0
        _UseColorBias("UseColorBias", float) = 0

        _TestZ ("TestZ", Range(0, 5)) = 0
        _TestW ("TestW", Range(0, 5)) = 0
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
            #include "./cginc/rgb2lab.cginc"
            #include "./cginc/masking.cginc"


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
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            sampler2D _LastTex;
            sampler2D _LastTex2;
            sampler2D _LastTex3;
            sampler2D _InnerThreshTex; // from color biased output
            float _SampleDecay;
            float _OutputDecay;
            float _DistMultiplier;
            float _DistPower;
            float _DecayDampThresh;
            float _DecayDampStrength;
            float _ColorDistMultThresh;
            float _ColorDistMultStrength;
            float _ColorDistMultMax;
            float _ColorArrayLength;
            float3 _ColorInclusionArray[40];
            float3 _ColorExclusionArray[40];
            bool _UseColorBias = 0;
            bool _UseHueValueInclude;
            bool _UseHueValueExclude;
            float _TestZ;
            float _TestW;
            
            sampler2D _ThreshTex;
     
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

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
            float3 HSVtoRGB(float3 input) {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(input.xxx + K.xyz) * 6.0 - K.www);

                return input.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), input.y);
            }

            float hueValueDist(float3 first, float3 second) {
                return pow(pow(first.r - second.r, 2)+ pow(first.b - second.b, 2), 0.5);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 runningThresh = tex2D(_ThreshTex, i.uv);

                half4 tex = tex2D(_MainTex, i.uv);
                half3 texHSV = RGBtoHSV(tex);
                half4 last = tex2D(_LastTex, i.uv);
                half4 last2 = tex2D(_LastTex2, i.uv);
                half4 last3 = tex2D(_LastTex3, i.uv);


                float3 texLab = rgb2lab(tex);
                float3 lastLab = rgb2lab(last);
                float3 lastLab2 = rgb2lab(last2);
                float3 lastLab3 = rgb2lab(last3);

                float screenThresh = getScreenThresh(2, i.uv);

                float innerThresh = pow(tex2D(_InnerThreshTex, i.uv).r*_TestZ, _TestW);

                float d1 = cie76(texLab, lastLab);
                float d2 = cie76(texLab, lastLab2);
                float d3 = cie76(texLab, lastLab3);
                float d4 = cie76(lastLab, lastLab2);
                float d5 = cie76(lastLab2, lastLab3);
                float d6 = cie76(lastLab, lastLab3);

                float cieDistSum = (d1+d2+d3+d4+d5+d6);

                float colorDecayDamping = 1;
                float colorDistMultiplier = 0;
                float decay = _SampleDecay;

                if (_UseColorBias == 1) {
                    decay = _OutputDecay;
                    float includeColor, excludeColor;

                    for (int i = 0; i < _ColorArrayLength; i++) {
                        includeColor = _ColorInclusionArray[i];
                        excludeColor = _ColorExclusionArray[i];

                        if (includeColor.r >= 0) {
                            float hsvIncludeDist = _UseHueValueInclude ?
                                hueValueDist(includeColor, texHSV) : 
                                abs(includeColor.r - texHSV.r);

                            float hsvExcludeDist = _UseHueValueExclude ?
                                hueValueDist(excludeColor, texHSV) :
                                abs(excludeColor.r - texHSV.r);

                            colorDistMultiplier = min(colorDistMultiplier + (1 - min(hsvIncludeDist/_ColorDistMultThresh, 1))*_ColorDistMultStrength*innerThresh, _ColorDistMultMax);
                            colorDecayDamping += (1 - min(hsvExcludeDist/_DecayDampThresh, 1))*_DecayDampStrength*max(_TestZ-innerThresh, 0);

                        }
                    }
                    return runningThresh.r + pow(cieDistSum*(_DistMultiplier+colorDistMultiplier*innerThresh), _DistPower) - max(decay*colorDecayDamping, 0);
                }
                tex.a = runningThresh.a + pow(cieDistSum*(_DistMultiplier+colorDistMultiplier), _DistPower) - max(decay*colorDecayDamping, 0);
                return tex;
            }
            ENDCG
        }
    }
}

