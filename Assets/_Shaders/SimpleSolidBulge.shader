Shader "Surface/SimpleSolidBulge" {
SubShader {
    Tags {"RenderType"="Opaque" }
    CGPROGRAM
    #pragma surface surf Lambert vertex:vert
    struct Input {
        float2 uv_MainTex;
    };
    float _Bulge = 0.0015; /* really 0.0015 */
    void vert (inout appdata_full v) {
        v.vertex.xyz += v.normal * _Bulge;
    }
    sampler2D _MainTex;
    void surf (Input IN, inout SurfaceOutput o) {
        float4 tex = tex2D (_MainTex, IN.uv_MainTex);
        o.Emission = fixed4(1,1,1,1);
        clip(tex.a - 0.5);
    }
    ENDCG
} 
Fallback "Diffuse"
}
