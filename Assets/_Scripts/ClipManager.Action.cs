using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
/* 
 * These are the user-directed public methods of ClipManager.
 * 
 * 1. These are all primarily coupled to InputController
 * 2. The intention is that they have a limited amount of internal logic,
 *    but some of them have more than I'd like, eg. PlayNextClip()
 * 3. If they keep member variables that ONLY they use, I've brought them here.
 * 4. There's definitely a better way to do this, but I wagered that this
 *    is better than nothing, for now.
 */
public partial class ClipManager
{
    // Used for the majority of shader-driven tweaks
    public void OffsetProp(string prop, float offset, float min, float max, Material targetMat = null)
    {
        if (!targetMat) targetMat = skyboxMat;
        SetProp(prop, targetMat.GetFloat(prop) + offset, min, max, targetMat);
    }
    public void ToggleProp(string prop, Material targetMat = null)
    {
        if (!targetMat) targetMat = skyboxMat;
        SetProp(prop, targetMat.GetFloat(prop) == 0 ? 1 : 0, 0, 1, targetMat);
    }

    public void EnableDebugGUI()
    {
        //sizingBar.SetActive(true);
        //debugContainer.SetActive(true);
        debugGuiCanvas.SetActive(true);
    }

    public void DisableDebugGUI(bool forceSwatchPickerOff = false)
    {
        if (forceSwatchPickerOff) swatchDetector.Disable();
        if (_inputController.metaMode == MetaMode.Debug) return;
        debugGuiCanvas.SetActive(false);
        //sizingBar.SetActive(false);
        //debugContainer.SetActive(false);
    }

    public void AdjustMaskMultiplier(float amount)
    {
        if (_isDifferenceMaskEnabled == 3)
            OffsetProp("_MatteAlphaMultiplier", amount * 0.1f, 1.0f, 30);
        else if (_isDifferenceMaskEnabled == 4)
            OffsetProp("_DynThreshMultiplier", amount * 0.1f, 1.0f, 30);

    }
    public void AdjustMaskPower(float amount)
    {
        if (_isDifferenceMaskEnabled == 3)
            OffsetProp("_MatteAlphaPower", amount * 0.1f, 1.0f, 30);
        else if (_isDifferenceMaskEnabled == 4)
            OffsetProp("_DynThreshPower", amount * 0.1f, 1.0f, 30);
    }

    // Re-save configs if a user-update has caused needsUpdate to be true
    // (This is invoked on idle, so not quite an input)
    public void UpdateDirtyConfig()
    {
        if (clipConfigs[clipPool.index].needsUpdate)
        {
            ClipConfig.Save(clipConfigs); // WARNING: saves ALL of them.
            clipConfigs[clipPool.index].needsUpdate = false;
        }
    }

    public void PlayNextClip() => PlayClipAtIndexOffset(1);
    public void PlayPrevClip() => PlayClipAtIndexOffset(-1);
    

    public void TogglePlaying()
    {
        var clip = getCurrentPlayer();
        if (clip.isPlaying)
        {
            clip.Pause();
        }
        else
        {
            _resetFrameCapture();
            clip.Play();
        }
    }

    // TODO: Argh, this doesn't work quite right; check into it!
    public void ResetProps()
    {
        // TODO: Integrate this into persistence!
        // Something like:
        ClipConfig freshOrUndoConfig;
        if (_undoConfig == null)
        {
            _undoConfig = clipConfigs[clipPool.index];
            freshOrUndoConfig = new ClipConfig();
            // Argh, leave this the same:
            freshOrUndoConfig._RotationX = _undoConfig._RotationX;
            freshOrUndoConfig._RotationY = _undoConfig._RotationY;
        }
        else
        {
            freshOrUndoConfig = _undoConfig;
            _undoConfig = null;
        }
        // save the new config when all buttons are released:
        clipConfigs[clipPool.index] = freshOrUndoConfig;
        ClipConfig.GlobalizeDynThresh(clipConfigs, clipPool.index);
        ClipConfig.Save(clipConfigs); // do it manually.
        clipConfigs[clipPool.index].ApplyToMaterial(skyboxMat);
        clipConfigs[clipPool.index].ApplyToMaterial(Blitter.dynThreshBlitMat, true); // useDynThreshFields = true

        combinedZoomFactor = -3.6f;
    }

    public void CycleMaskModes()
    {
        maskManager.CycleMaskModes();
        //if (_isDifferenceMaskEnabled == _differenceMaskCount) // cycling!
        //    DisableMask();
        //else
        //    EnableMask();
    }

    // This performs poorly and isn't used; maybe delete
    public void TogglePlayerThrottle(float amount)
    {
        clipPool.current.playbackSpeed =
        (clipPool.current.playbackSpeed == amount) ?
             1 : amount;
    }

    public void SetPlayEnd()
    {
        var endFrame = clipPool.current.frame;
        var config = clipConfigs[clipPool.index];
        if (config.playStart >= endFrame)
            config.playStart = 0;
        config.playEnd = endFrame;
        ClipConfig.Save(clipConfigs);
    }
        
    public void SetPlayStart()
    {
        var startFrame = clipPool.current.frame;
        var config = clipConfigs[clipPool.index];
        if (config.playEnd <= startFrame)
            config.playEnd = 0;
        config.playStart = startFrame;
        ClipConfig.Save(clipConfigs);
    }

    public void ClearPlayStart()
    {
        clipConfigs[clipPool.index].playStart = 0;
        ClipConfig.Save(clipConfigs);
    }

    public void ClearPlayEnd()
    {
        clipConfigs[clipPool.index].playEnd = 0;
        ClipConfig.Save(clipConfigs);
    }

    public void SeekAhead(int seconds) =>
        clipPool.current.time += seconds;

    public void ToggleAutoShiftMode() =>
        useAutoShiftMode = !useAutoShiftMode;

}
