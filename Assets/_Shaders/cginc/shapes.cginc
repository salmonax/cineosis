float circle(float2 uv,float2 pos,float radius,float feather)
{
    float2 uvDist=uv-pos;
    return 1.0-smoothstep(radius-feather,radius+feather, length(uvDist));
}