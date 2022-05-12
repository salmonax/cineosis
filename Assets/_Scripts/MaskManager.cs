using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class MaskManager
{
    ClipPool clipPool;
    Material skyboxMat;
    ClipConfig[] clipConfigs;

    public MaskManager(Material skybox, ClipPool clips, ClipConfig[] configs)
    {
        clipPool = clips;
        skyboxMat = skybox;
        clipConfigs = configs;
    }

    bool finishedInitialCapture = false;

    // Temporary, for refactor:
    public float _isDifferenceMaskEnabled; // overrides _UseDifferenceMask in shader

    private int _differenceMaskCount = 4; // used to mod across modes

    private long lastFrameIdx = 0;
    private bool shouldRender = true;

    //int[] frameSkips = new int[] { 1, 3, 5, 3 };
    int[] frameSkips = new int[] { 1 };
    int frameSkipCursor = 0;
    int frameSkip
    {
        get
        {
            var current = frameSkips[frameSkipCursor];
            frameSkipCursor = (frameSkipCursor + 1) % frameSkips.Length;
            return current;
        }
    }

    private long lastColorIdx = 0;
    private int colorSkip = 5;

    public RenderTexture[] dsts = new RenderTexture[3];

    FrameSkipper _colorMaskSkipper = new FrameSkipper(1);
    public VideoPlayer lastSource;
    public void RenderColorMaskTick(VideoPlayer source = null)
    {
        // Note: lastSource is a swatchPicker thing, so this needs to be refactored
        // to not be weird
        if (source == null)
        {
            if (lastSource == null) return;
            source = lastSource;
        }
        else if (source != lastSource) lastSource = source;
        if (_colorMaskSkipper.Skip()) return;

        Blitter.ApplySmallFrame(skyboxMat, source.texture);
        Blitter.ApplyColorMask(skyboxMat, source.texture);
    }

    public void OnNewFrame(VideoPlayer source, long frameIdx)
    {
        if (_isDifferenceMaskEnabled == 0) return; // just in case the listener lingers; handle better
        if (frameIdx < lastFrameIdx)
            lastFrameIdx = lastColorIdx = frameIdx;
        if (_isDifferenceMaskEnabled == 3)
        {
            if (clipPool.currentMatte)
                Blitter.ApplyMatteAlpha(skyboxMat, source.texture, clipPool.currentMatte, frameIdx % 2 == 0);
            else
                CycleMaskModes(); // Just get out of the mode! Real easy-like.
        }
        else if (_isDifferenceMaskEnabled == 2)
            RenderColorMaskTick(source); // not differentiating eyes, and brittle when called from swatchPicker!

        else if (_isDifferenceMaskEnabled == 2 || _isDifferenceMaskEnabled == 1)
            RenderClassicDiffMaskTick(source, frameIdx);

        else if (_isDifferenceMaskEnabled == 4)
            RenderDynThreshTick(source, frameIdx);
        HandleTimeBounds(frameIdx);
    }

    // Temporary; logic needs re-thinking
    void HandleTimeBounds(long frameIdx)
    {

        var config = clipConfigs[clipPool.index];
        if ((config.playStart > 0 && frameIdx < config.playStart) ||
            (config.playEnd > 0 && frameIdx >= config.playEnd))
        {
            clipPool.current.frame = config.playStart;
            _resetFrameCapture(true, false);
        }
    }

    void ShiftDiffTexes(VideoPlayer source)
    {
        if (dsts[0]) // shunt forward; newest always at 0.
        {
            var tmp = dsts[2];
            dsts[2] = dsts[1];
            dsts[1] = dsts[0];
            dsts[0] = tmp;
        }
        // this will be true until all three textures exist,
        // ensuring that only three RenderTextures are created:
        if (!dsts[0])
        {
            dsts[0] = new RenderTexture((int)source.width, (int)source.height, 16);
            dsts[0].enableRandomWrite = true;
            dsts[0].Create();
        }
    }

    void RenderDynThreshTick(VideoPlayer source, long frameIdx)
    {
        ShiftDiffTexes(source);

        bool shouldReadColors = frameIdx - lastColorIdx >= colorSkip;
        Blitter.SetRunningTextures(Blitter.dynThreshBlitMat, dsts);
        Blitter.SetRunningTextures(skyboxMat, dsts);
        Blitter.ApplyDynThresh(skyboxMat, source.texture, shouldReadColors);
        if (shouldReadColors) lastColorIdx = frameIdx;

        Blitter.Clear(source.texture, dsts[0]);
    }

    void RenderClassicDiffMaskTick(VideoPlayer source, long frameIdx)
    {
        if (lastFrameIdx == 0 || frameIdx - lastFrameIdx > frameSkip) // was 18 for initial capture
        {
            if (lastFrameIdx == 0 || !shouldRender)
            {
                ShiftDiffTexes(source);
                // Can probably do this one and dsts[0] differently:
                Blitter.Clear((RenderTexture)source.texture, dsts[0]);
            }

            if (lastFrameIdx == 0 || shouldRender)
            {
                // Note: this will fill each one out as they come in.
                if (_isDifferenceMaskEnabled == 1)
                    Blitter.SetRunningTextures(skyboxMat, dsts);

                // Update the difference mask textures regardless of mode
                Blitter.SetRunningTextures(Blitter.diffMaskBlitMat, dsts);

                if (_isDifferenceMaskEnabled == 1)
                    Blitter.ApplyDifferenceMask(skyboxMat, dsts);
                else if (_isDifferenceMaskEnabled == 2)
                    Blitter.ApplyDifferenceMask(Blitter.colorMaskBlitMat, dsts);

                // Only run the following once, regardless of whether there's an early return above.
                if (dsts[2] && !finishedInitialCapture)
                {
                    finishedInitialCapture = true;

                    /* This section is for a static difference mask.
                     * The shader is currently ignoring it.
                     */

                    //diffBlitMat.SetTexture("_LastTex", dsts[0]);
                    //diffBlitMat.SetTexture("_LastTex2", dsts[1]);
                    //diffBlitMat.SetTexture("_LastTex3", dsts[2]);

                    //// Can scale from here:
                    //if (!combinedAlpha)
                    //{
                    //    combinedAlpha = new RenderTexture((int)source.width / 10, (int)source.height / 10, 16);
                    //    blurredAlpha = new RenderTexture((int)source.width / 10, (int)source.height / 10, 16);
                    //}

                    //RenderTexture.active = combinedAlpha;
                    //GL.Clear(true, true, Color.black);
                    //Graphics.Blit(null, combinedAlpha, blitMat);


                    ////blurMat.SetTexture("_MainTex", combinedAlpha);-
                    //RenderTexture.active = blurredAlpha;
                    //GL.Clear(true, true, Color.black);
                    //Graphics.Blit(combinedAlpha, blurredAlpha, blurMat);

                    //skyboxMat.SetTexture("_AlphaTex", blurredAlpha);

                }
            }
            shouldRender = !shouldRender;
            lastFrameIdx = frameIdx;
        }
    }

    void EnableMask(bool pushSetting = true)
    {
        if (pushSetting)
        {
            _isDifferenceMaskEnabled = (_isDifferenceMaskEnabled + 1) % (_differenceMaskCount + 1);
            skyboxMat.SetFloat("_UseDifferenceMask", _isDifferenceMaskEnabled);
            clipConfigs[clipPool.index].SetFloatIfPresent("_UseDifferenceMask", _isDifferenceMaskEnabled);
        }
        EnableFrameCapture();
    }

    void DisableMask(bool pushSetting = true)
    {
        if (pushSetting)
        {
            _isDifferenceMaskEnabled = 0;
            skyboxMat.SetFloat("_UseDifferenceMask", 0);
            clipConfigs[clipPool.index].SetFloatIfPresent("_UseDifferenceMask", 0);
        }
        DisableFrameCapture();
    }

    public void EnableFrameCapture(VideoPlayer clip = null)
    {
        // Assume that this is the only place that sendFrameReadyEvents can be enabled:
        if (clip == null) clip = getCurrentPlayer();
        if (clip.sendFrameReadyEvents != false) return; 
        clip.sendFrameReadyEvents = true;
        clip.frameReady += OnNewFrame;
        clip.loopPointReached += EndReached;
    }

    public void DisableFrameCapture(VideoPlayer clip = null)
    {
        if (clip == null) clip = getCurrentPlayer();
        clip.sendFrameReadyEvents = false;
        clip.frameReady -= OnNewFrame;
        clip.loopPointReached -= EndReached;
    }

    public void PullAndSetMaskState()
    {
        _isDifferenceMaskEnabled = skyboxMat.GetFloat("_UseDifferenceMask");
        if (_isDifferenceMaskEnabled > 0)
            EnableMask(false);
        else
            DisableMask(false);
    }

    public void _resetFrameCapture(bool diffMaskOnly = true, bool clearDsts = true)
    {
        if (!diffMaskOnly || _isDifferenceMaskEnabled > 0)
        {
            finishedInitialCapture = false;
            if (clearDsts)
                for (int i = 0; i < dsts.Length; i++)
                    dsts[i] = null;
            lastFrameIdx = lastColorIdx = 0;
            shouldRender = true;
        }
    }
    void EndReached(VideoPlayer vp) => _resetFrameCapture(true, false);

    VideoPlayer getCurrentPlayer() => clipPool.current;

    public void CycleMaskModes()
    {
        if (_isDifferenceMaskEnabled == _differenceMaskCount) // cycling!
            DisableMask();
        else
            EnableMask();
    }
}