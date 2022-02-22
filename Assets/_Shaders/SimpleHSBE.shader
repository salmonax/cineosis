Shader "Surface/Bulgeable Hand" {
Properties {
    _MainTex ("Texture", 2D) = "white" {}
    _Bulge ("Bulge", Range(0,0.1)) = 0
    _Hue("Hue", Range(340, 380)) = 0
    _Saturation ("Saturation", Range(0,2)) = 1
    _Exposure ("Exposure", Range(0,3)) = 1
    _Saturation_Offset("Saturation Offset", Range(-1,1)) = 0
    _Exposure_Offset("Exposure Offset", Range(-1,1)) = 0
}
SubShader {
    //Tags {"Queue" = "Transparent" "RenderType"="Transparent" }
    Tags {"RenderType"="Opaque" }

    CGPROGRAM
    #pragma surface surf Lambert vertex:vert
    //#pragma surface surf Lambert vertex:vert alpha:fade
    #include "./cginc/hsbe.cginc"
    struct Input {
        float2 uv_MainTex;
    };
    float _Bulge;
    float _Hue;
    float _Saturation;
    float _Exposure;
    float _Saturation_Offset;
    float _Exposure_Offset;
    void vert (inout appdata_full v) {
        v.vertex.xyz += v.normal * _Bulge;
    }
    sampler2D _MainTex;
    void surf (Input IN, inout SurfaceOutput o) {
        float4 tex = tex2D (_MainTex, IN.uv_MainTex);
        hue(tex, _Hue);
        exposure(tex, _Exposure+_Exposure_Offset);
        saturation(tex, _Saturation+_Saturation_Offset);
        o.Emission = tex.rgb;
        clip(tex.a - 0.5);

    }
    ENDCG
} 
Fallback "Diffuse"
}
