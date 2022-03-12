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
        ApplyKernel(matteMaskAlphaBlurMat, 2, 10);
        ApplyKernel(outlinerHandBlurMat, 2, 8);
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
