Shader "Skybox/CineosisPanoramic" {
Properties {
    UseDifferenceMask("Use Difference Mask", Float) = 0
    _TestX ("TestX", Range(0, 5)) = 0
    _TestY ("TestY", Range(0, 50)) = 0
    _TestZ ("TestZ", Range(0, 5)) = 0
    _TestW ("TestW", Range(0, 5)) = 0
    [MaterialToggle] _UseSwatchPickerMode("Use Swatch Picker Mode", Float) = 0
    //_LeftColorExclusionArray("Left Color Exclusion Array", Color) = (0,0,0)
    //_RightColorExclusionArray("Right Color Exclusion Array", Color) = (0,0,0)
    _ColorTest("Color Test", Color)= (0,0,0, 1)
    _MatteThresholdR("Matte Threshold R", Range(0, 1)) = 0.02
    _MatteThresholdG("Matte Threshold G", Range(0, 1)) = 1
    _MatteThresholdB("Matte Threshold B", Range(0, 1)) = 1
    _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
    _Transparency ("Transparency", Range(0, 1)) = 0.2
    _Saturation ("Saturation", Range(0, 1)) = 0.0
    [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
    _Contrast ("Contrast", Range(0, 2)) = 1.0
    _RotationX ("Rotation X", Range(-180, 180)) = 0
    _RotationY ("Rotation Y", Range(-180, 180)) = 0
    _HorizontalOffset ("Horizontal Offset", Range(-1.5, 1.5)) = 0
    _NudgeX ("NudgeX", Range(-1.5, 1.5)) = 0
    _NudgeY ("NudgeY", Range(-1.5, 1.5)) = 0
    _NudgeFactorX ("NudgeFactorX", Range(0, 1)) = 0
    _NudgeFactorY ("NudgeFactorY", Range(0, 1)) = 0
    _ZoomShiftX ("_ZoomShiftX", Range(-1, 1)) = 1
    _ZoomShiftY ("_ZoomShiftY", Range(-1000, 1000)) = 1
    _AutoShiftRotationXNudgeFactor ("AutoShiftRotationXNudgeFactor", Range(-360, 360)) = 0
    _NudgeZ ("NudgeZ", Range(-1, 1)) = 0
    _Zoom ("Zoom", Range(-1.5, 6)) = 0
     BaseZoom ("BaseZoom", Range(-1.5, 1.5)) = 0
    _ZoomAdjust ("ZoomAdjust", Range(-1.0, 1.0)) = 0 // range is 
    _ZoomNudgeFactor ("ZoomNudgeFactor", Range(0, 6)) = 0
    _ZoomAdjustNudgeFactor ("ZoomAdjustNudgeFactor", Range(0, 1)) = 0
    _HorizontalOffsetNudgeFactor ("HorizontalOffsetNudgeFactor", Range(0, 1)) = 0
     VideoIndex("VideoIndex", Range(0, 2)) = 0
    [NoScaleOffset] _Tex ("Spherical  (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _MatteTex ("Spherical Matte Texture (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _MatteMaskAlphaTex ("Spherical Matte Texture (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _LastTex ("LAST Spherical Texture (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _LastTex2 ("LAST Spherical Texture (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _LastTex3 ("LAST Spherical Texture (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _AlphaTex ("Combined Spherical Texture (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _DynAlphaTex ("Combined Spherical Texture (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _ExclusionDrawMaskTex ("Combined Spherical Texture (HDR)", 2D) = "white" {}
    [NoScaleOffset] _LightingTex ("Combined Spherical Texture (HDR)", 2D) = "white" {}
    [NoScaleOffset] _TestTex ("Combined Spherical Texture (HDR)", 2D) = "white" {}

    [NoScaleOffset]  SmallFrameTex ("Small Frame Tex Spherical Texture (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _ColorMaskAlphaTex ("Color Mask Spherical Texture (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _ScreenSpaceHelperTex ("ScreenHelper Spherical Texture (HDR)", 2D) = "grey" {}

    [KeywordEnum(6 Frames Layout, Latitude Longitude Layout)] _Mapping("Mapping", Float) = 1
    [Enum(360 Degrees, 0, 180 Degrees, 1)] _ImageType("Image Type", Float) = 0
    [Toggle] _MirrorOnBack("Mirror on Back", Float) = 0
    [Enum(None, 0, Side by Side, 1, Over Under, 2)] _Layout("3D Layout", Float) = 0
}

SubShader {
    Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
    Cull Off ZWrite Off
    Blend SrcAlpha OneMinusSrcAlpha

    Pass {

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 2.0
        #pragma multi_compile __ _MAPPING_6_FRAMES_LAYOUT

        #include "UnityCG.cginc"
        #include "./cginc/rgb2lab.cginc"
        #include "./cginc/blur.cginc"
        #include "./cginc/hsbe.cginc"
        #include "./cginc/masking.cginc"
        #include "./cginc/shapes.cginc"

        float4 _ColorTest;
        float _TestX;
        float _TestY;
        float _TestZ;
        float _TestW;
        float2 _LaserCoord;
        float _AutoShiftRotationXNudgeFactor; // multiplied against ShiftY; should be 0 if autoshift is off
        float3 _ColorTestArray[1];
        int _ColorArrayLength;
        float3 _LeftColorExclusionArray[10];
        float3 _RightColorExclusionArray[10];
        float _UseDifferenceMask;
        float _UseSwatchPickerMode;
        sampler2D _Tex;
        sampler2D _MatteTex;
        sampler2D _LastTex;
        sampler2D _LastTex2;
        sampler2D _LastTex3;
        sampler2D _AlphaTex;
        sampler2D _DynAlphaTex;
        sampler2D _ColorMaskAlphaTex;
        sampler2D _SmallFrameTex;
        sampler2D _ScreenSpaceHelperTex;
        sampler2D _ExclusionDrawMaskTex;
        sampler2D _LightingTex;
        sampler2D _TestTex;
        sampler2D _MatteMaskAlphaTex;
        float4 _Tex_TexelSize;
        float4 _LastTex_TexelSize;
        float4 _AlphaTex_TexelSize;
        float4 _DynAlphaTex_TexelSize;
        half4 _Tex_HDR;
        half4 _Tint;
        float _Transparency;
        float _Contrast;
        half _Exposure;
        float _RotationX;
        float _RotationY;
        float _HorizontalOffset;
        float _Zoom;
        float _ZoomAdjust;
        float _Saturation;
        float _ZoomNudgeFactor;
        float _ZoomAdjustNudgeFactor;
        float _ZoomShiftX;
        float _ZoomShiftY;
        // NOTE: this one only gets used in AutoShiftMode:

        float _RotationShiftX; // No prop for this one; didn't forget.
        float _HorizontalOffsetNudgeFactor;
        float _NudgeX;
        float _NudgeY;
        float _NudgeFactorX;
        float _NudgeFactorY;
        float _NudgeZ;
        int _VideoIndex;
        float _MatteThresholdR;
        float _MatteThresholdG;
        float _MatteThresholdB;
        bool _UseScreenSpaceHelper;
        bool _UseLight = 0;
        int _Layout;
#ifndef _MAPPING_6_FRAMES_LAYOUT
        bool _MirrorOnBack;
        int _ImageType;
#endif

#ifndef _MAPPING_6_FRAMES_LAYOUT
        inline float2 ToRadialCoords(float3 coords)
        {
            float3 normalizedCoords = normalize(coords);
            float latitude = acos(normalizedCoords.y);
            float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
            float2 sphereCoords = float2(longitude, latitude) * float2(0.5/UNITY_PI, 1.0/UNITY_PI);
            return float2(0.5,1.0) - sphereCoords;
        }
#endif

#ifdef _MAPPING_6_FRAMES_LAYOUT
        inline float2 ToCubeCoords(float3 coords, float3 layout, float4 edgeSize, float4 faceXCoordLayouts, float4 faceYCoordLayouts, float4 faceZCoordLayouts)
        {
            // Determine the primary axis of the normal
            float3 absn = abs(coords);
            float3 absdir = absn > float3(max(absn.y,absn.z), max(absn.x,absn.z), max(absn.x,absn.y)) ? 1 : 0;
            // Convert the normal to a local face texture coord [-1,+1], note that tcAndLen.z==dot(coords,absdir)
            // and thus its sign tells us whether the normal is pointing positive or negative
            float3 tcAndLen = mul(absdir, float3x3(coords.zyx, coords.xzy, float3(-coords.xy,coords.z)));
            tcAndLen.xy /= tcAndLen.z;
            // Flip-flop faces for proper orientation and normalize to [-0.5,+0.5]
            bool2 positiveAndVCross = float2(tcAndLen.z, layout.x) > 0;
            tcAndLen.xy *= (positiveAndVCross[0] ? absdir.yx : (positiveAndVCross[1] ? float2(absdir[2],0) : float2(0,absdir[2]))) - 0.5;
            // Clamp values which are close to the face edges to avoid bleeding/seams (ie. enforce clamp texture wrap mode)
            tcAndLen.xy = clamp(tcAndLen.xy, edgeSize.xy, edgeSize.zw);
            // Scale and offset texture coord to match the proper square in the texture based on layout.
            float4 coordLayout = mul(float4(absdir,0), float4x4(faceXCoordLayouts, faceYCoordLayouts, faceZCoordLayouts, faceZCoordLayouts));
            tcAndLen.xy = (tcAndLen.xy + (positiveAndVCross[0] ? coordLayout.xy : coordLayout.zw)) * layout.yz;
            return tcAndLen.xy;
        }
#endif
        #define Blend(base, blend, funcf) 		float3(funcf(base.r, blend.r), funcf(base.g, blend.g), funcf(base.b, blend.b))
        #define BlendOverlayf(base, blend) 	(base < 0.5 ? (2.0 * base * blend) : (1.0 - 2.0 * (1.0 - base) * (1.0 - blend)))
        #define BlendOverlay(base, blend) 		Blend(base, blend, BlendOverlayf)
        #define BlendHardLight(base, blend) 	BlendOverlay(blend, base)

        float3 HSVtoRGB(float3 input) {
            float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
            float3 p = abs(frac(input.xxx + K.xyz) * 6.0 - K.www);

            return input.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), input.y);
        }

        float dist(float2 p0, float2 pf) {
            return sqrt((pf.x-p0.x)*(pf.x-p0.x)+(pf.y-p0.y)*(pf.y-p0.y));
        }

        float ColorDistance(float3 e1, float3 e2)
        {
            float rmean = ( e1.r + e2.r ) / 2;
            float r = e1.r - e2.r;
            float g = e1.g - e2.g;
            float b = e1.b - e2.b;
            return sqrt((((512+rmean)*r*r)/256) + 4*g*g + (((767-rmean)*b*b)/256));
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

        float3 RotateAroundXInDegrees (float3 vertex, float degrees)
        {
            float alpha = degrees * UNITY_PI / 180.0;
            float sina, cosa;
            sincos(alpha, sina, cosa);
            float2x2 m = float2x2(cosa, -sina, sina, cosa);
            return float3(vertex.x, mul(m, vertex.zy)).xzy;
        }

        float3 RotateAroundYInDegrees (float3 vertex, float degrees)
        {
            float alpha = degrees * UNITY_PI / 180.0;
            float sina, cosa;
            sincos(alpha, sina, cosa);
            float2x2 m = float2x2(cosa, -sina, sina, cosa);
            return float3(mul(m, vertex.xz), vertex.y).xzy;
        }

        struct appdata_t {
            float4 vertex : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f {
            float4 vertex : SV_POSITION;
            float3 texcoord : TEXCOORD0;
#ifdef _MAPPING_6_FRAMES_LAYOUT
            float3 layout : TEXCOORD1;
            float4 edgeSize : TEXCOORD2;
            float4 faceXCoordLayouts : TEXCOORD3;
            float4 faceYCoordLayouts : TEXCOORD4;
            float4 faceZCoordLayouts : TEXCOORD5;
#else
            float2 image180ScaleAndCutoff : TEXCOORD1;
            float4 layout3DScaleAndOffset : TEXCOORD2;
#endif
            UNITY_VERTEX_OUTPUT_STEREO
        };

        v2f vert (appdata_t v)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float layoutRotationY = 0;
            float3 rotated;
            if (_Layout == 2) {
                layoutRotationY = 90;
                rotated = RotateAroundYInDegrees(v.vertex, _RotationY + layoutRotationY);
                rotated = RotateAroundXInDegrees(rotated, _RotationX + _ZoomShiftY*_AutoShiftRotationXNudgeFactor + _Zoom*_RotationShiftX);
            } else {
                rotated = RotateAroundXInDegrees(v.vertex, _RotationX + _ZoomShiftY*_AutoShiftRotationXNudgeFactor + _Zoom*_RotationShiftX);
                rotated = RotateAroundYInDegrees(rotated, _RotationY + layoutRotationY);
            }
            o.vertex = UnityObjectToClipPos(rotated);
            o.texcoord = v.vertex.xyz;
#ifdef _MAPPING_6_FRAMES_LAYOUT
            // layout and edgeSize are solely based on texture dimensions and can thus be precalculated in the vertex shader.
            float sourceAspect = float(_Tex_TexelSize.z) / float(_Tex_TexelSize.w);

            // Use the halfway point between the 1:6 and 3:4 aspect ratios of the strip and cross layouts to
            // guess at the correct format.
            bool3 aspectTest =
                sourceAspect >
                float3(1.0, 1.0f / 6.0f + (3.0f / 4.0f - 1.0f / 6.0f) / 2.0f, 6.0f / 1.0f + (4.0f / 3.0f - 6.0f / 1.0f) / 2.0f);
            // For a given face layout, the coordinates of the 6 cube faces are fixed: build a compact representation of the
            // coordinates of the center of each face where the first float4 represents the coordinates of the X axis faces,
            // the second the Y, and the third the Z. The first two float componenents (xy) of each float4 represent the face
            // coordinates on the positive axis side of the cube, and the second (zw) the negative.
            // layout.x is a boolean flagging the vertical cross layout (for special handling of flip-flops later)
            // layout.yz contains the inverse of the layout dimensions (ie. the scale factor required to convert from
            // normalized face coords to full texture coordinates)
            if (aspectTest[0]) // horizontal
            {
                if (aspectTest[2])
                { // horizontal strip
                    o.faceXCoordLayouts = float4(0.5,0.5,1.5,0.5);
                    o.faceYCoordLayouts = float4(2.5,0.5,3.5,0.5);
                    o.faceZCoordLayouts = float4(4.5,0.5,5.5,0.5);
                    o.layout = float3(-1,1.0/6.0,1.0/1.0);
                }
                else
                { // horizontal cross
                    o.faceXCoordLayouts = float4(2.5,1.5,0.5,1.5);
                    o.faceYCoordLayouts = float4(1.5,2.5,1.5,0.5);
                    o.faceZCoordLayouts = float4(1.5,1.5,3.5,1.5);
                    o.layout = float3(-1,1.0/4.0,1.0/3.0);
                }
            }
            else
            {
                if (aspectTest[1])
                { // vertical cross
                    o.faceXCoordLayouts = float4(2.5,2.5,0.5,2.5);
                    o.faceYCoordLayouts = float4(1.5,3.5,1.5,1.5);
                    o.faceZCoordLayouts = float4(1.5,2.5,1.5,0.5);
                    o.layout = float3(1,1.0/3.0,1.0/4.0);
                }
                else
                { // vertical strip
                    o.faceXCoordLayouts = float4(0.5,5.5,0.5,4.5);
                    o.faceYCoordLayouts = float4(0.5,3.5,0.5,2.5);
                    o.faceZCoordLayouts = float4(0.5,1.5,0.5,0.5);
                    o.layout = float3(-1,1.0/1.0,1.0/6.0);
                }
            }
            // edgeSize specifies the minimum (xy) and maximum (zw) normalized face texture coordinates that will be used for
            // sampling in the texture. Setting these to the effective size of a half pixel horizontally and vertically
            // effectively enforces clamp mode texture wrapping for each individual face.
            o.edgeSize.xy = _Tex_TexelSize.xy * 0.5 / o.layout.yz - 0.5;
            o.edgeSize.zw = -o.edgeSize.xy;
#else // !_MAPPING_6_FRAMES_LAYOUT
            // Calculate constant horizontal scale and cutoff for 180 (vs 360) image type
            if (_ImageType == 0)  // 360 degree
                o.image180ScaleAndCutoff = float2(1.0, 1.0);
            else  // 180 
                //o.image180ScaleAndCutoff = float2(2.0, _MirrorOnBack ? 1.0 : 0.5);
                // 1) Zoomish thing ONE:
                o.image180ScaleAndCutoff = float2(2.0 + _Zoom, _MirrorOnBack ? 1.0 : 1/(0.0067*pow(_Zoom,3)-0.0911*pow(_Zoom,2)+0.5474*_Zoom+2));
            // Calculate constant scale and offset for 3D layouts
            if (_Layout == 0) // No 3D layout
                o.layout3DScaleAndOffset = float4(0,0,1,1);
            else if (_Layout == 1) // Side-by-Side 3D layout
                //o.layout3DScaleAndOffset = float4(unity_StereoEyeIndex,0,0.5,1);
                // 2) Zoomish thing TWO:

                o.layout3DScaleAndOffset = float4(unity_StereoEyeIndex,0,0.5,1 + round(_ZoomAdjust * 1000)/1000);
            else // Over-Under 3D layout
                o.layout3DScaleAndOffset = float4(0, 1-unity_StereoEyeIndex,1,0.5); // no _ZoomAdjust for now... kind of antiquated
#endif
            return o;
        }

        fixed4 frag (v2f i) : SV_Target
        {
#ifdef _MAPPING_6_FRAMES_LAYOUT
            float2 tc = ToCubeCoords(i.texcoord, i.layout, i.edgeSize, i.faceXCoordLayouts, i.faceYCoordLayouts, i.faceZCoordLayouts);
#else
            float2 tc = ToRadialCoords(i.texcoord);

            float layoutZoomShiftX = 0;
            if (_Layout != 2) {
                if (tc.x > i.image180ScaleAndCutoff[1])
                    return half4(0,0,0,0);
                if (tc.x < 0.48-i.image180ScaleAndCutoff[1])
                    return half4(0,0,0,0);
                tc.x = fmod(tc.x*i.image180ScaleAndCutoff[0], 6);

            } else {
                layoutZoomShiftX = 0.70*0.5;
            }

            // Note: below is mine
            // I might not actually need this.

            tc.y = fmod(tc.y*i.image180ScaleAndCutoff[0]/2, 6); // add scale to y; needs centering

            // what does this do? Can I get zoom to work here?

            tc = (tc + i.layout3DScaleAndOffset.xy) * i.layout3DScaleAndOffset.zw;

            if (_Layout == 2)
                tc.x *= 1+(_Zoom*0.5);

            // Flip
            // use first value to do horizontal offset
            // only works with side-by-side for now

            if (unity_StereoEyeIndex == 1)
                if (_VideoIndex != 1)
                    tc.x -= (_HorizontalOffset + _NudgeZ) * 0.02;
                else 
                    tc.x = 1.00 - tc.x - (_HorizontalOffset + _NudgeZ) * 0.02;
            if (unity_StereoEyeIndex == 0)
                if (_VideoIndex != 1)
                    tc.x += (_HorizontalOffset + _NudgeZ) * 0.02;
                else
                    tc.x = 1.00 - tc.x + (_HorizontalOffset + _NudgeZ) * 0.02;


            // Maybe make these shift based on vertical camera rotation?

            // New default: 9 on _ZoomShiftX, 4 on _ZoomShiftY, range between 1 and 10

            if (_VideoIndex == 1)
                tc.x -= _NudgeX - round(_Zoom*(0.14285+_ZoomShiftX*0.5) * 1000)/1000; // headset movement compensation, with centering term! (was 7, ~0.14285)
             else
                tc.x += _NudgeX - round(_Zoom*(0.11111+(_ZoomShiftX+layoutZoomShiftX)*0.5) * 1000)/1000; // headset movement compensation, with centering term! (was 9, ~0.11111)

            if (_VideoIndex == 1)
                tc.y -= _NudgeY + round(_Zoom*(0.2+(_ZoomShiftY)*0.5) * 1000)/1000; // NOTE: _Zoom/4 is a centering term (was 5, 0.2)
//            else if (_VideoIndex == 0)
//              tc.y -= _NudgeY + round(_Zoom*(0.25+_ZoomShiftX*0.5) * 1000)/1000; // NOTE: _Zoom/4 is a centering term (was 4, 0.25)
            else
                // I think this should about around 0.135, based on the Ryoukan clip
                tc.y -= _NudgeY + round(_Zoom*(0.135+(_ZoomShiftY)*0.5) * 1000)/1000; // NOTE: _Zoom/4 is a centering term (was 8, 0.25)

            if (_VideoIndex == 1)
               tc.y = 1.00 - tc.y;
               //tc.x = 1.00 - tc.x;
#endif

            half4 tex = tex2D (_Tex, tc);
            half4 colorMask = tex2D(_ColorMaskAlphaTex, tc);
            half4 smallFrame = tex2D(_SmallFrameTex, tc);

            //half4 tex = tex2Dlod(_Tex, float4(tc.xy, 0, 0));
            //half4 matte = tex2D(_MatteTex, tc);
            //half4 foo = tex2D(_Tex, tc);

            //float f = 1.0/30.0;

            float f = 1.0; // unnecessary.
            float3 foo = float3(f,f,f);
            half4 last = tex2D(_LastTex, tc/foo);
            half4 last2 = tex2D(_LastTex2, tc/foo);
            half4 last3 = tex2D(_LastTex3, tc/foo);

            half4 alpha = tex2D(_AlphaTex, tc/foo);
            half4 dynAlpha = tex2D(_DynAlphaTex, tc/foo);
            //half4 alpha = slowGaussianBlur(_AlphaTex, tc, _AlphaTex_TexelSize.xy, 8, 1);
            //half4 alpha = (kawaseBlur(_AlphaTex, _AlphaTex_TexelSize.xy, tc, 1) +
                //boxBlur(_AlphaTex, _AlphaTex_TexelSize.xy, tc))/2;

            //half4 matteBlur = slowGaussianBlur( _MatteTex, tc, _Tex_TexelSize.xy, 25, 3);
           //half4 texBlur = slowGaussianBlur( _Tex, tc, _Tex_TexelSize.xy, 30, 2);
            //half4 lastBlur = slowGaussianBlur( _LastTex, tc, _Tex_TexelSize.xy, 30, 2);


            float cutoff = 0.93; // mask cutoff
            float maxAlpha = _Transparency;
            //tex.a = 0;


            //float3 texHCV = RGBtoHSV(tex);
            //float3 lastHCV = RGBtoHSV(last);

            //float3 texLab = rgb2lab(lrgb2rgb(texBlur));
            //float3 lastLab = rgb2lab(lrgb2rgb(lastBlur));

            float3 texLab = rgb2lab(tex);
            float3 lastLab = rgb2lab(last);
            float3 lastLab2 = rgb2lab(last2);
            float3 lastLab3 = rgb2lab(last3);

            //float3 matteLab = rgb2lab(matte);

            //if (abs(tex.r - last.r) < _MatteThresholdR)
            //float distance = cie94(texLab, lastLab);


            //float screenThresh = abs(tc.x - 0.25)*5;
            //float screenThresh = pow(tc.x - 0.25, 0.5)*2;


           float screenThresh = getScreenThresh(_Layout, tc);

           tex.a = maxAlpha;

            //float screenThresh = BlendHardLight(float3(stX, stX, stX), float3(stY, stY, stY)).x;
               
            //tex = float4(screenThresh, screenThresh, screenThresh, 1);

/*
            float distance = (cie94(texLab, lastLab) +
                cie94(texLab, lastLab2) +
                cie94(texLab, lastLab3) +
                cie94(lastLab, lastLab2) +
                cie94(lastLab2, lastLab3) +
                cie94(lastLab, lastLab3))/6;
\
            if (pow(distance, 0.3) > screenThresh)
                tex.a = maxAlpha;
*/

/*
            float meanDistanceInitial = (
                cie94(lastLab, lastLab2) +
                cie94(lastLab2, lastLab3) +
                cie94(lastLab, lastLab3)
            )/3;

            float meanDistanceCurrent = (
                cie94(texLab, lastLab) +
                cie94(texLab, lastLab2) +
                cie94(texLab, lastLab3)
            )/3;

            if (pow(meanDistanceCurrent, 0.32) > screenThresh)
                tex.a = maxAlpha;
            if (pow(meanDistanceInitial, 0.32) > screenThresh)
                tex.a = maxAlpha;
*/

           
            if (_UseDifferenceMask == 1) {
                float d1 = cie94(texLab, lastLab);
                float d2 = cie94(texLab, lastLab2);
                float d3 = cie94(texLab, lastLab3);
                //float d4 = cie94(lastLab, lastLab2);
                //float d5 = cie94(lastLab2, lastLab3);
                //float d6 = cie94(lastLab, lastLab3);


                tex.a = maxAlpha * dynAlpha * 3;// + pow(alpha, 0.6);

                // Direct comparison terms; turn off for blur-only.

                if (pow(d1, 0.33) > screenThresh)
                    tex.a += maxAlpha/6;
                if (pow(d2, 0.33) > screenThresh)
                    tex.a += maxAlpha/6;
                if (pow(d3, 0.33) > screenThresh)
                    tex.a += maxAlpha/6;
                /*if (pow(d4, 0.33) > screenThresh)
                    tex.a += maxAlpha/6;
                if (pow(d5, 0.33) > screenThresh)
                    tex.a += maxAlpha/6;
                if (pow(d6, 0.33) > screenThresh)
                    tex.a += maxAlpha/6;*/
            }
                
            // Assumes left-right

/*
            if (cie76(lastLab, lastLab2) > _MatteThresholdR/13)
                tex.a = 1;
            if (cie76(lastLab2, lastLab3) > _MatteThresholdR/13)
                tex.a = 1;
            if (cie76(lastLab, lastLab3) > _MatteThresholdR/13)
                tex.a = 1; */

            
                //tex.a = pow((distance - _MatteThresholdR)/(1 - _MatteThresholdR), 1);




            //float diffX = pow(abs(texLab.z - matteLab.z), 0.5);
            //float diffY = pow(abs(texLab.x - matteLab.x), 0.5);
            //float diffZ = pow(abs(texLab.x - matteLab.x), 0.5);

            //float diffZ = sqrt(pow(texLab.y - matteLab.y, 2) + pow(texLab.y - matteLab.y,2) + pow(texLab.z - matteLab.z, 2));
            //float diffZ = abs(tex.b - matte.b);

            /*
            if (diffX > _MatteThresholdR)
                tex.a = maxAlpha;

            if (diffY > _MatteThresholdG)
                tex.a = maxAlpha;
            if (diffZ > _MatteThresholdB
              tex.a = maxAlpha;
                //tex.a = pow((totalDiff - _MatteThresholdR)/(1 - _MatteThresholdR), 0.4);

            //if (abs(tex.r) - abs(tex.g + tex.b)/2 > _MatteThresholdG)
               // tex.a = 1;
            */
            if (_UseDifferenceMask == 90) {
                tex.a = 0;
                //if (tex.r - tex.b >= 0.02)
                    //    tex.a = 1;
                //if (tex.r - tex.g >= 0.02)
                      //  tex.a = 1;

                //tex.a = 0;
                float3 foo = LinearToSRGB(tex.rgb);
                float3 foo2 = RGBtoHSV(foo);
                //if (foo.r > 0.373 && foo.g > 0.157 && foo.b > 0.0784)
                  //if (max(foo.r,max(foo.g,foo.b))-min(foo.r,min(foo.g,foo.b)) > 0.0588)
                if (foo.r > _TestX && foo.g > _TestY && foo.b > _TestZ && max(foo.r,max(foo.g,foo.b))-min(foo.r,min(foo.g,foo.b)) > 0.0588)
                    if (abs(foo.r - foo.g) > _TestW && foo.r > foo.g && foo.r > foo.b)

                    //if (abs(foo.r - foo.g) > 0.0588 && foo.r > foo.g && foo.r > foo.b)
                        if (foo2.x < 0.196)
                            tex.a = 1;
            }

            if (_UseDifferenceMask == 3) {
                tex.a = pow(tex2D(_MatteMaskAlphaTex, tc)*_TestX, _TestY)*maxAlpha;

                //tex.a = tex2D(_MatteMaskAlphaTex, tc) * maxAlpha;
                //tex.a = 0;

/*
                half4 matteAlpha = tex2D(_MatteMaskAlphaTex, tc);
                return matteAlpha;
                float weight = pow(matteAlpha*_TestX, _TestY).r;
                //tex.a = pow(matteAlpha*_TestX, _TestY).r;
*/

                /*
                float d1 = cie94(texLab, lastLab);
                float d2 = cie94(texLab, lastLab2);
                float d3 = cie94(texLab, lastLab3);

                if (pow(d1, 0.33) > _TestZ)
                    tex.a = 1;
                if (pow(d2, 0.33) > _TestZ)
                    tex.a = 1;
                if (pow(d3, 0.33) > _TestZ)
                    tex.a = 1;*/


                //tex.a = matteAlpha.r;


                //return tex2D(_MatteMaskAlphaTex, tc);

                half4 matte = tex2D(_MatteTex, tc);

                // < 0.06 for lab; 0.16 for lrgb.
                

                //float wat = cie76(tex, matte);
                //float watLab = cie76(rgb2lab(tex), rgb2lab(matte));
               // float watHSV = cie76(RGBtoHSV(tex), RGBtoHSV(matte));

                //if (wat < 0.13/weight && watLab < 0.06/weight)// && watHSV < _TestX)
                   //tex.a = 0;

                //if (wat < _TestX && watLab < _TestY)// && watHSV < _TestX)

                //tex.a = pow((wat/_TestZ + watLab/_TestW)/2*_TestX, _TestY);
                
            }


            if (_UseDifferenceMask == 10) {
                if (_VideoIndex == 0)
                    if (tex.r - tex.b < 0.02)
                        if (i.texcoord.x > 0.15)
                            tex.a = 0;
                if (_VideoIndex == 0)
                    if (tex.r - tex.g < 0.02)
                        if (i.texcoord.x < -0.19)
                            tex.a = 0;
                if (_VideoIndex == 0)
                    if (tex.r - tex.g < 0.02)
                        if (i.texcoord.x > 0.31)
                            tex.a = 0;
                if (_VideoIndex == 0)
                    if (tex.r - tex.g < 0.01)
                        if (i.texcoord.y > 0.46)
                            tex.a = 0;
                if (_VideoIndex == 0)
                    if (tex.b - tex.g < -0.2)
                            tex.a = maxAlpha;
                if (_VideoIndex == 0)
                    if (i.texcoord.x < -0.39)
                        if (i.texcoord.y > -0.31)
                            tex.a = 0;
                if (_VideoIndex == 0)
                    if (i.texcoord.x > 0.37)
                        if (i.texcoord.y > -0.31)
                            tex.a = 0;
                /*if (_VideoIndex == 0)
                    if (i.texcoord.x > -0.17)
                        if (i.texcoord.x < 0.16)
                            if (i.texcoord.y > 0.36)
                               if ((tex.r + tex.g + tex.b)/6.0 < 0.5)
                                    tex.a = maxAlpha;*/
                if (_VideoIndex == 0)
                    if (i.texcoord.x > -0.3)
                        if (i.texcoord.x < 0.16)
                            if (i.texcoord.y > 0)
                                if (i.texcoord.y < 0.92)
                                    if ((tex.r + tex.g + tex.b) < 0.3)
                                      tex.a = maxAlpha;
                          
                                  //tex.r = 1;
            
    
                //if (_VideoIndex == 2)
                //    tex.a = 0;

                            // For White Sky
                if (_VideoIndex == 1)           
                    if (tex.r > cutoff)
                        if (tex.g > cutoff)
                            if (tex.b > cutoff)
                                tex.a = pow(max(0.98-(tex.r+tex.g+tex.b)/3.0, 0.0)/cutoff, 0.4) * maxAlpha;
           

                
                // For Bed Clip
                if (_VideoIndex == 2)
                    if (tex.b >= tex.r)
                        tex.a = 0;


                if (_VideoIndex == 2)
                    if (i.texcoord.x < -.47)
                        tex.a = 0;
                if (_VideoIndex == 2)
                    if (i.texcoord.y > -0.02)
                        tex.a = 0;
                if (_VideoIndex == 2)
                    if (i.texcoord.x > 0.50)
                        tex.a = 0;
                if (_VideoIndex == 2)
                    if (i.texcoord.x > 0.44)
                        if (i.texcoord.y > -0.14)
                            tex.a = 0;
                if (_VideoIndex == 2)
                    if (i.texcoord.x < -0.2)
                        if (tc.y > 0.38)
                            tex.a = 0;
                if (_VideoIndex == 2)
                    if (i.texcoord.x < -0.08)
                        if (tc.y > 0.398)
                            tex.a = 0;
            }
            

            
            //tex.rgb *= _Exposure;

            if (_UseSwatchPickerMode == 1)
                return smallFrame;

            if (_UseScreenSpaceHelper == 1) {
                return tex2D(_ScreenSpaceHelperTex, tc);
            }

            //return tex2D(_ScreenSpaceHelperTex, tc);
            

            exposure(tex, _Exposure);
            contrast(tex, _Contrast);
            saturation(tex, _Saturation);
                /* NOTE: it turns out that Hue and Chroma seem to
                 * work better for filtering */



                /*float4 color = _ColorTest;*/
            
            /*    for (int i = 0; i < _ColorArrayLength; i++) {
                    if (_LeftColorExclusionArray[i].r >= 0) {
                        float3 color = rgb2lrgb(_LeftColorExclusionArray[i]);
                        //float swatchDist = ColorDistance(color, tex);
                        //float swatchDist = abs(RGBtoHCV(color).x - RGBtoHCV(tex).x);
                        float swatchDist = cie76(rgb2lab(color), texLab);
                        if (swatchDist <= pow(screenThresh*1.5, 3)) // higher power increases inner resistance
                            tex.rgb = float3(0,0,0);
                            //tex.a = 0;
                    }
                }

*/
                //tex.rgb = color2;
//                if (color.r == color2.r)
  //                 tex.a = float3(1,1,1);

                    /*tex.rgb = tex.rgb*pow((1-screenThresh), 5);//float3(0,0,0);*/
                    /*tex.rgb = tex.rgb * (0.05-swatchDist)/0.05;*/
                //if (_LeftColorExclusionArray[0].r > 0)
                    //tex.rgb = float3(1,1,1);



          //return colorMask;
          /* Example of combined filter:*/
          //if (_UseDifferenceMask == 3) {
          //  tex.a = (colorMask-(1-dynAlpha))*2 * maxAlpha;
          //  return tex;
          //}



          if (_UseDifferenceMask == 2) {
              /* was 1.5, 2.5 for a while: */
              tex.a = min(pow(colorMask*1.25, 1.25),1) * maxAlpha; // was 1.75, 2.25 forever; 1.25 1.75, then 1.5 1.5
              //tex.a = min(colorMask*3,1) * maxAlpha;// pow(colorMask*1.5, 1.5) * maxAlpha; // was 1.75, 2.25 forever; 1.25 1.75, then 1.5 1.5
              tex.rgb *= tex.a;

              //tex.rgb = HSVtoRGB(_ColorTestArray[0]);
               //return colorMask;
          }

          //if (i.texcoord.x < 0.25 && i.texcoord.x > 0.25-_Tex_TexelSize.x*1000 && i.texcoord.y < 0.25 && i.texcoord.y > 0.25-_Tex_TexelSize.x*1000)
            //tex.rgb = 1;
          //float pointLight = ((1-dist(float2(_LaserCoord.x/9.5,_LaserCoord.y/9.5), float2(i.texcoord.x*1.25, i.texcoord.y*1.25)))+0.5);

          //float pointLight = circle(tc, _LaserCoord, 0.1, 0);

          //return tex2D(_ScreenSpaceHelperTex, tc);

          float pointLight = tex2D(_LightingTex, tc).r;
          if (_UseLight == 1)
            //return float4(0,0,0,1);
            //return tex2D(_LightingTex, tc);
            //return pointLight;
            tex = float4(tex.rgb*(pow(pointLight/pow(_Exposure+0.05, 0.2), 7)+1), tex.a);

          tex.a *= tex2D(_ExclusionDrawMaskTex, tc).r;


          return tex;
          //return colorMask;
          //tex.a = pow((colorMask+dynAlpha/2)*(1-screenThresh),1.5);

            
            //return slowGaussianBlur( _Tex, tc, _Tex_TexelSize.xy, 35, 2);

            /* Leaving this just in case I need to re-enable HDR:
                half3 c = DecodeHDR (tex, _Tex_HDR);
                c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb;
                // Perfect value is 0.39 for White Sky
                c *= _Exposure;
                return half4(c, 1);
            */
        }
        ENDCG
    }
}


CustomEditor "SkyboxPanoramicBetaShaderGUI"
Fallback Off

}
