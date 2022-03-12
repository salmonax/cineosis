Shader "Unlit/MatteMaskThreshBlit"
{
    Properties
    {
        _MatteTex ("Texture", 2D) = "white" {}
        _MainTex ("Texture", 2D) = "white" {}
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
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _RgbThresh;
            float _LabThresh;

            float _Layout;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //return float4(1,0,0,1);
                half4 tex = tex2D(_MainTex, i.uv);
                half4 matte = tex2D(_MatteTex, i.uv);

                // < 0.06 for lab; 0.16 for lrgb.

                float wat = cie76(tex, matte);
                float watLab = cie76(rgb2lab(tex), rgb2lab(matte));
                //float watHSV = cie76(RGBtoHSV(tex), RGBtoHSV(matte));

                float4 output = float4(1,1,1,1);
                if (wat < _RgbThresh && watLab < _LabThresh)
                    output = float4(0,0,0,1);
                return output;
                // && watHSV < _TestX)
                //if (wat < _TestX && watLab < _TestY)// && watHSV < _TestX)

                //tex.a = pow((wat/_TestZ + watLab/_TestW)/2*_TestX, _TestY);

                //return tex;
            }
            ENDCG
        }
    }
}
