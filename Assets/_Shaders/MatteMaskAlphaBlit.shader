Shader "Unlit/MatteMaskAlphaBlit"
{
    Properties
    {
        _MatteTex ("Matte Texture", 2D) = "white" {}
        _ThreshTex ("Threshold Texture", 2D) = "white" {}
        _MainTex ("Frame Texture (Main)", 2D) = "white" {}
        _WeightMultiplier ("Weight Multiplier (3.03)", Range(0.5, 30)) = 3.03
        _WeightPower ("Weight Power (3.61)", Range(0.5, 30)) = 3.61
        _RgbThresh ("RGB Threshold (0.13)", Range(0, 1)) = 0.13
        _LabThresh ("Lab Threshold (0.06)", Range(0, 0.5)) = 0.06
        [Enum(None, 0, Side by Side, 1, Over Under, 2)] _Layout("3D Layout", Float) = 0
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

            sampler2D _MatteTex;
            sampler2D _ThreshTex; /* product of a very-blurred matte */

            sampler2D _MainTex; /* video frame */
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            float _RgbThresh;
            float _LabThresh;

            float _Layout;

            // magic numbers from skyboxMat TestX/Y tweaking:
            float _WeightMultiplier;
            float _WeightPower;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                half4 matte = tex2D(_MatteTex, i.uv);
                half4 thresh = tex2D(_ThreshTex, i.uv);

                //return thresh;

                float weight = pow(thresh*_WeightMultiplier, _WeightPower).r; // from skybox TestX/Y nums

                float rgbDist = cie76(tex, matte);
                float labDist = cie76(rgb2lab(tex), rgb2lab(matte));

                float4 output = float4(1,1,1,1);
                if (rgbDist < _RgbThresh/weight && labDist < _LabThresh/weight)
                    output = float4(0,0,0,1);
                return output;
            }
            ENDCG
        }
    }
}
