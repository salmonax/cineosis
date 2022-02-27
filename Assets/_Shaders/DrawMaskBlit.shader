Shader "Unlit/DrawMaskBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LastTex ("Texture", 2D) = "white" {}
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
            #include "./cginc/shapes.cginc"

            sampler2D _LastTex;
            float _DeleteMode = 0;
            
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
            float2 _LaserCoord = float2(0,0);
            bool _IsDrawing;
     
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //return float4(0,1,0,1);
                //float drawPoint = dist(float2(i.uv.x/2, i.uv.y), float2(0.01,0.01));

               //
                // float drawPoint = ((1-dist(float2(_LaserCoord.x*2,_LaserCoord.y*2), float2(i.uv.x*20, i.uv.y*20)))+0.5);
                float tex = tex2D(_LastTex, i.uv);
                float drawPoint = 1-circle(i.uv.xy, _LaserCoord, 0.01, 0.01);
                // dist(float2(i.uv.x, i.uv.y), float2(i.uv.x, i.uv.y));
                if (_DeleteMode == 0) /* Do a multiple */
                    tex *= drawPoint;
                else /* Do a screen */
                    tex += (1-drawPoint);
                    //tex = 1 - (1 - tex) / drawPoint;
                return tex;
                //return float4(drawPoint, drawPoint, drawPoint, 1);
            }
            ENDCG
        }
    }
}

