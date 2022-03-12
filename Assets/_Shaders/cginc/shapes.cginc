float circle(float2 uv,float2 pos,float radius,float feather, float layout = 1)
{
    float2 uvDist=uv-pos;
    if (layout == 1)
        uvDist = float2(uvDist.x, uvDist.y/2);
    return 1.0-smoothstep(radius-feather,radius+feather, length(uvDist));
}