using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Blitter
{
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

    public static readonly RenderTexture smallFrame = Tex(720, 360, FilterMode.Point);


    public static void SetRunningTextures(Material material, RenderTexture[] dsts)
    {
        material.SetTexture("_LastTex", dsts[0]);
        material.SetTexture("_LastTex2", dsts[1]);
        material.SetTexture("_LastTex3", dsts[2]);
    }

    public static void ApplyDifferenceMask(Material mat, RenderTexture[] dsts, string name = "_DynAlphaTex")
    {
        SetRunningTextures(diffMaskBlitMat, dsts);
        Clear(null, diffMaskCrispAlpha, diffMaskBlitMat);
        Clear(diffMaskCrispAlpha, diffMaskBlurredAlpha, diffMaskBlurMat);
        mat.SetTexture(name, diffMaskBlurredAlpha);
    }

    public static void ApplyColorMask(Material mat, Texture tex, string name = "_ColorMaskAlphaTex")
    {
        Clear((RenderTexture)tex, colorMaskCrispAlpha, colorMaskBlitMat);
        Clear(colorMaskCrispAlpha, colorMaskBlurredAlpha, colorMaskBlurMat);
        mat.SetTexture(name, colorMaskBlurredAlpha);
    }


    public static void ApplySmallFrame(Material mat, Texture tex, string name = "_SmallFrameTex")
    {
        Clear((RenderTexture)tex, smallFrame);
        mat.SetTexture(name, smallFrame);
    }

    // Not sure why I need to manually clear each blit, but it was the early blitting style, so leaving it for now:
    public static void Clear(RenderTexture source, RenderTexture target, Material mat = null, bool clear = true)
    {
        RenderTexture.active = target;
        GL.Clear(clear, clear, Color.black);
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
