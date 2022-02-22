float4 boxBlur(sampler2D sample, float2 texelSize, float2 uv) 
{
    float x = texelSize.x;
    float y = texelSize.y;
    float4 sum = tex2D(sample, uv) + tex2D(sample, float2(uv.x - x, uv.y - y)) + tex2D(sample, float2(uv.x, uv.y - y))
                    + tex2D(sample, float2(uv.x + x, uv.y - y)) + tex2D(sample, float2(uv.x - x, uv.y))
                    + tex2D(sample, float2(uv.x + x, uv.y)) + tex2D(sample, float2(uv.x - x, uv.y + y))
                    + tex2D(sample, float2(uv.x, uv.y + y)) + tex2D(sample, float2(uv.x + x, uv.y + y));
    return sum / 9;
}


float4 kawaseBlur(sampler2D sample, float2 texelSize, float2 uv, int pixelOffset)
{
	float4 o = 0;
	o += tex2D(sample, uv + (float2(pixelOffset + 0.5,pixelOffset + 0.5) * texelSize)) * 0.25;
	o += tex2D(sample, uv + (float2(-pixelOffset - 0.5,pixelOffset + 0.5) * texelSize))* 0.25;
	o += tex2D(sample, uv + (float2(-pixelOffset - 0.5,-pixelOffset - 0.5) * texelSize)) * 0.25;
	o += tex2D(sample, uv + (float2(pixelOffset + 0.5,-pixelOffset - 0.5) * texelSize)) * 0.25;
	return o;
}

// Naive and slow
float4 slowGaussianBlur(sampler2D sp, float2 U, float2 scale, int samples, int LOD)
{
    float4 O = (float4) 0;
    int sLOD = 1 << LOD;
    float sigma = float(samples) * 0.25;
    int s = samples/sLOD;  
    for (int i = 0; i < s*s; i++)
    {
        float2 d = float2(i%(uint)s, i/(uint)s) * float(sLOD) - float(samples)/2.;
        float2 t = d;
        O += exp(-0.5* dot(t/=sigma,t) ) / ( 6.28 * sigma*sigma ) * tex2Dlod( sp, float4(U + scale * d, 0, LOD));
    }
    return O / O.a;
}