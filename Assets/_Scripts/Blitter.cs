using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Blitter
{
    public static readonly Material dummyMaterial = Resources.Load<Material>("DummyMaterial");

    /* Clip Manager */

    // Note: ignoring the former presence of static diffMasks; these are dynamic:
    public static readonly Material diffMaskBlitMat = Resources.Load<Material>("StaticMaskAlphaBlitMaterial");
    public static readonly Material diffMaskBlurMat = Resources.Load<Material>("BoxKawaseBlitMaterial"); // currently, this does Gaussian despite name
    public static readonly RenderTexture diffMaskCrispAlpha = Tex(720, 360); // as current, no FilterMode declared
    public static readonly RenderTexture diffMaskBlurredAlpha = Tex(288, 144); // as current, questionably

    // This is the magical cie76 decay threshold, to replace screenThresh:
    public static readonly Material dynThreshBlitMat = Resources.Load<Material>("DynThreshBlitMaterial");
    public static readonly RenderTexture dynThreshCrispAlpha = Tex(128, 128, FilterMode.Point); // full texture, for color sampling
    public static readonly RenderTexture dynThreshInternalBlur = Tex(128, 128);
    public static readonly RenderTexture dynThreshCrispAlphaWithColorBias = Tex(512, 512, FilterMode.Point); // black-and-white, just alpha value


    public static readonly RenderTexture dynThreshMiniAlphaWithColorBias = Tex(128, 128, FilterMode.Point);


    public static readonly Texture2D dynThreshColorSource = new Texture2D(512, 512, TextureFormat.RGBA32, false);
    public static readonly ColorWarden dynThreshWarden = new ColorWarden(40, 512); // Arbitrarily clamp moving average to sqrt of pixel count.
    public static readonly Material dynThreshInternalBlurMat = Resources.Load<Material>("DynThreshInternalBlurMaterial"); // small recursive blur
    public static readonly Material dynThreshBlurMat = Resources.Load<Material>("DynThreshBlurMaterial");
    public static readonly RenderTexture dynThreshBlurred = Tex(512, 512);


    // A static difference mask might use the following material, with a more expensive blur:
    //public static readonly Material blurMat = Resources.Load<Material>("NaiveGaussianMaterial"); 

    public static readonly Material colorMaskBlitMat = Resources.Load<Material>("ColorArrayMaskBlitMaterial");
    public static readonly Material colorMaskBlurMat = Resources.Load<Material>("ColorMaskGaussianMaterialTwoPass");
    public static readonly RenderTexture colorMaskCrispAlpha = Tex(720, 360, FilterMode.Point); // Should be Temp?
    public static readonly RenderTexture colorMaskBlurredAlpha = Tex(720, 360);

    public static readonly Material matteMaskThreshBlitMat = Resources.Load<Material>("MatteMaskThreshBlitMaterial");
    public static readonly Material matteMaskAlphaBlitMat = Resources.Load<Material>("MatteMaskAlphaBlitMaterial");
    public static readonly Material matteMaskThreshBlurMat = Resources.Load<Material>("MatteMaskThreshBlurMaterial");
    public static readonly Material matteMaskAlphaBlurMat = Resources.Load<Material>("MatteMaskAlphaBlurMaterial");
    public static readonly RenderTexture matteMaskCrispThresh = Tex(1024, 1024, FilterMode.Point);
    public static readonly RenderTexture matteMaskCrispAlpha = Tex(1024, 1024, FilterMode.Point);
    public static readonly RenderTexture matteMaskBlurredThresh = Tex(512, 512);
    public static readonly RenderTexture matteMaskBlurredAlpha = Tex(512, 512);

    public static readonly RenderTexture smallFrame = Tex(720, 360, FilterMode.Point);


    // Just have to remember to call this! If a new Gaussian material is added, make sure to
    // apply a kernal here
    public static void Init()
    {
        // Argh, these have ballooned out; at least memoize in GaussianKernel class
        ApplyKernel(diffMaskBlurMat, 2, 6);
        ApplyKernel(colorMaskBlurMat, 3, 12); // not really sure why this one is higher
        ApplyKernel(matteMaskThreshBlurMat, 2, 8);
        ApplyKernel(matteMaskAlphaBlurMat, 2, 8); // was 2, 10 for a minute
        ApplyKernel(outlinerHandBlurMat, 1, 5);
        ApplyKernel(dynThreshBlurMat, 2, 8);
        ApplyKernel(dynThreshInternalBlurMat, 2, 8);
        ClipManager.MakeSwatches(10, 4);
        ClipManager.MakeExclusionSwatches(10, 4);
    }

    public static void ApplyKernel(Material mat, int sigma, int size)
    {
        var kernel = GaussianKernel.Calculate(sigma, size);
        mat.SetFloatArray("_kernel", kernel);
        mat.SetInt("_kernelWidth", kernel.Length);
    }


    public static void SetRunningTextures(Material material, RenderTexture[] dsts)
    {
        material.SetTexture("_LastTex", dsts[0]);
        material.SetTexture("_LastTex2", dsts[1]);
        material.SetTexture("_LastTex3", dsts[2]);
    }

    public static void ApplyDynThresh(
        Material mat, Texture tex,
        bool shouldSampleColors = true,
        string dstSlot = "_DynThreshTex",
        string lastSlot = "_LastTex",
        string mainSlot = "_MainTex", string threshSlot = "_ThreshTex",
        string innerThreshSlot = "_InnerThreshTex"
    )
    {
        //var exitingTex = dynThreshBlitMat.GetTexture(mainSlot);
        //dynThreshBlitMat.SetTexture(lastSlot, exitingTex); // probably don't do this
        dynThreshBlitMat.SetTexture(mainSlot, tex);

        // Hmm... could run this blit ONCE without color info... setting it to ignore it
        dynThreshBlitMat.SetFloat("_UseColorBias", 0);
        dynThreshBlitMat.SetTexture(threshSlot, dynThreshCrispAlpha);
        Clear(null, dynThreshCrispAlpha, dynThreshBlitMat, Color.black, false);

        if (shouldSampleColors)
        {
            RenderTexture.active = dynThreshCrispAlpha;
            dynThreshColorSource.ReadPixels(new Rect(0, 0, dynThreshCrispAlpha.width, dynThreshCrispAlpha.height), 0, 0);
            RenderTexture.active = null;
            var byteArray = dynThreshColorSource.GetRawTextureData();
            for (var i = 0; i < byteArray.Length; i += 4)
            {
                var color = new Color32(byteArray[i], byteArray[i + 1], byteArray[i + 2], byteArray[i + 3]);
                if (color.a == 0)
                    dynThreshWarden.Exclude(color);
                if (color.a / 255 > 0.8)
                    dynThreshWarden.Include(color, int.MaxValue); // zero-out the corresponding exclusion
            }

            //var hsv = dynThreshWarden.Inclusions[19];
            //GameObject.Find("ColorSwatchLeft").GetComponent<Renderer>().material.color = Color.HSVToRGB(hsv.r, hsv.g, hsv.b);

            var inclusions = dynThreshWarden.Inclusions;
            var exclusions = dynThreshWarden.Exclusions;
            ClipManager.SetSwatches(inclusions);
            ClipManager.SetExclusionSwatches(exclusions);

            //Run it again WITH color info after GetRawTextureData call and colorwarden filter..
            dynThreshBlitMat.SetColorArray("_ColorInclusionArray", inclusions);
            dynThreshBlitMat.SetColorArray("_ColorExclusionArray", exclusions);
            dynThreshBlitMat.SetFloat("_ColorArrayLength", inclusions.Length);
        }
        dynThreshBlitMat.SetTexture(threshSlot, dynThreshCrispAlphaWithColorBias);
        dynThreshBlitMat.SetFloat("_UseColorBias", 1);

        //dynThreshBlitMat.SetFloat("_UseInnerThresh", 0);
        //Clear(null, dynThreshMiniAlphaWithColorBias, dynThreshBlitMat, Color.black, false);
        //Clear(dynThreshMiniAlphaWithColorBias, dynThreshInternalBlur, dynThreshInternalBlurMat);
        //dynThreshBlitMat.SetTexture(innerThreshSlot, dynThreshInternalBlur);

        //dynThreshBlitMat.SetFloat("_UseInnerThresh", 1);
        Clear(null, dynThreshCrispAlphaWithColorBias, dynThreshBlitMat, Color.black, false);

        /* Before switching to no-feedback innerThresh: */
        Clear(dynThreshCrispAlphaWithColorBias, dynThreshInternalBlur, dynThreshInternalBlurMat);
        dynThreshBlitMat.SetTexture(innerThreshSlot, dynThreshInternalBlur);

        /* For Debugging: */
        //mat.SetTexture(dstSlot, dynThreshInternalBlur);
        //mat.SetTexture(dstSlot, dynThreshCrispAlphaWithColorBias);

        Clear(dynThreshCrispAlphaWithColorBias, dynThreshBlurred, dynThreshBlurMat);
        mat.SetTexture(dstSlot, dynThreshBlurred);
    }


    public static void ApplyDifferenceMask(Material mat, RenderTexture[] dsts, string slot = "_DynAlphaTex")
    {
        SetRunningTextures(diffMaskBlitMat, dsts);
        Clear(null, diffMaskCrispAlpha, diffMaskBlitMat);
        Clear(diffMaskCrispAlpha, diffMaskBlurredAlpha, diffMaskBlurMat);
        mat.SetTexture(slot, diffMaskBlurredAlpha);
    }

    public static void ApplyColorMask(Material mat, Texture tex, string slot = "_ColorMaskAlphaTex")
    {
        Clear((RenderTexture)tex, colorMaskCrispAlpha, colorMaskBlitMat);
        Clear(colorMaskCrispAlpha, colorMaskBlurredAlpha, colorMaskBlurMat);
        mat.SetTexture(slot, colorMaskBlurredAlpha);
    }

    // This feels a bit weird to do, but should only be done once when a video
    // is loaded.
    public static void SetCurrentMatte(Texture2D matte, string matteSlot = "_MatteTex") {
        if (!matte) return;
        matteMaskThreshBlitMat.SetTexture(matteSlot, matte);
        matteMaskAlphaBlitMat.SetTexture(matteSlot, matte);
    }

    public static void ApplyMatteThresh(
        Material mat, Texture tex, Texture2D matte,
        string slot = "_MatteMaskAlphaTex",
        string matteSlot = "_MatteTex")
    {
        //Clear(matte, matteMaskSmallMatte);
        //matteMaskThreshBlitMat.SetTexture(matteSlot, matte);// matteMaskSmallMatte);
        //Clear((RenderTexture)tex, matteMaskSmallFrame);

        //Clear(matteMaskSmallFrame, matteMaskCrispAlpha, matteMaskThreshBlitMat);
        Clear(tex, matteMaskCrispThresh, matteMaskThreshBlitMat);

        Clear(matteMaskCrispThresh, matteMaskBlurredThresh, matteMaskThreshBlurMat);

        mat.SetTexture(slot, matteMaskBlurredThresh);
    }

    public static void ApplyMatteAlpha(
        Material mat, Texture tex, Texture2D matte,
        bool shouldRefreshThresh = false,
        string slot = "_MatteMaskAlphaTex",
        string matteSlot = "_MatteTex", string threshSlot = "_ThreshTex")
    {
        // TODO: Bleh, only do this on video load:

        // generate thresh and apply it to the same material as above:
        if (shouldRefreshThresh)
            ApplyMatteThresh(matteMaskAlphaBlitMat, tex, matte, "_ThreshTex");
        //else // only do this step when not refreshing the thresh:
        //{
        //matteMaskAlphaBlitMat.SetTexture(matteSlot, matte);
        Clear(tex, matteMaskCrispAlpha, matteMaskAlphaBlitMat);
        Clear(matteMaskCrispAlpha, matteMaskBlurredAlpha, matteMaskAlphaBlurMat);
        mat.SetTexture(slot, matteMaskBlurredAlpha);
        //}
        //matteMaskAlphaBlitMat.SetTexture(matteSlot, )
        // set the thresh texture to 
    }

    public static void ApplySmallFrame(Material mat, Texture tex, string slot = "_SmallFrameTex")
    {
        Clear((RenderTexture)tex, smallFrame);
        mat.SetTexture(slot, smallFrame);
    }


    /* Swatch Picker */

    public static readonly Material screenSpaceHelperMat = Resources.Load<Material>("ScreenSpaceHelperMaterial");
    public static readonly RenderTexture screenSpaceHelperTex = Tex(2048, 1024);

    public static readonly RenderTexture exclusionTex = Tex(1440, 720);
    public static readonly Material exclusionMat = Resources.Load<Material>("ExclusionMaskBlitMaterial");

    public static readonly RenderTexture lightingTex = Tex(1440, 720);
    public static readonly Material lightingMat = Resources.Load<Material>("LightingBlitMaterial");


    public static void ApplyScreenSpaceHelper(Material mat, string slot = "_ScreenSpaceHelperTex")
    {
        Clear(null, screenSpaceHelperTex, screenSpaceHelperMat, Color.white);
        mat.SetTexture(slot, screenSpaceHelperTex);
    }


    public static void InitExclusionMask(string slot = "_LastTex")
    {
        Clear(null, exclusionTex, dummyMaterial);
        exclusionMat.SetTexture(slot, exclusionTex);
    }

    public static void RenderExclusionMaskTick(
        Vector2 laserCoord, bool shouldDelete, Material? maybeTargetMat = null,
        string coordSlot = "_LaserCoord", string modeSlot = "_DeleteMode",
        string lastTexSlot = "_LastTex", string compositingMatSlot = "_ExclusionDrawMaskTex")
    {
        exclusionMat.SetVector(coordSlot, laserCoord);
        exclusionMat.SetFloat(modeSlot, shouldDelete ? 1 : 0);

        // Perform the draw and set the texture
        Clear(null, exclusionTex, exclusionMat, Color.white, false);
        exclusionMat.SetTexture(lastTexSlot, exclusionTex);

        if (maybeTargetMat != null)
            maybeTargetMat.SetTexture(compositingMatSlot, exclusionTex);
    }

    public static void RenderLightingTick(
        Vector2 laserCoordLeft, Vector2 laserCoordRight, Material? maybeTargetMat = null,
        string coordSlotLeft = "_LaserCoordLeft", string coordSlotRight = "_LaserCoordRight",
        string compositingMatSlot = "_LightingTex"
    )
    {
        lightingMat.SetVector(coordSlotLeft, laserCoordLeft);
        lightingMat.SetVector(coordSlotRight, laserCoordRight);
        Clear(null, lightingTex, lightingMat);
        if (maybeTargetMat != null)
            maybeTargetMat.SetTexture(compositingMatSlot, lightingTex);
    }

    /* Outliner (No methods yet, but moving materials over) */

    public static readonly Material outlinerHandBlurMat = Resources.Load<Material>("OutlinerHandBlurMaterial");
    public static readonly Material filmGrainMaterial = Resources.Load<Material>("FilmGrainMaterial");
    public static readonly Material compositingMaterial = Resources.Load<Material>("CompositingMaterial");

    // Not sure why I need to manually clear each blit, but it was the early blitting style, so leaving it for now:
    public static void Clear(Texture source, RenderTexture target, Material mat = null, Color? color = null, bool clear = true)
    {
        if (color == null) color = Color.black;
        RenderTexture.active = target;
        GL.Clear(clear, clear, (Color)color);
        if (mat == null)
            Graphics.Blit(source, target);
        else
            Graphics.Blit(source, target, mat);
        RenderTexture.active = null;
    }

    private static RenderTexture Tex(int width, int height, FilterMode filterMode = FilterMode.Bilinear)
    {
        var tex = new RenderTexture(width, height, 0);
        tex.filterMode = filterMode;
        return tex;
    }
}
