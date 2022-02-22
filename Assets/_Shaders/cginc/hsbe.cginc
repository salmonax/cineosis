// HSBE = Hue, Saturation, Brightness, Contrast, Exposure

/* (Exposure is a bit silly, but just so that it's consistent across shaders) */
void exposure(inout float4 color, float _exposure) {
    color.rgb *= _exposure;
}

/* See Forum thread for other implementations */
void saturation(inout float4 color, float _saturation) {
    float3 intensity = dot(color.rgb, float3(0.299, 0.587, 0.114));
    color.rgb = lerp(intensity, color.rgb, _saturation);
}

void hue(inout float4 color, float hue)
{
    float angle = radians(hue);
    float3 k = float3(0.57735, 0.57735, 0.57735);
    float cosAngle = cos(angle);
    //Rodrigues' rotation formula
    color.rgb = color * cosAngle + cross(k, color) * sin(angle) + k * dot(k, color) * (1 - cosAngle);
}