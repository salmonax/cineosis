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
        float currentValue = targetMat.GetFloat(prop);
        float newValue = Mathf.Clamp(currentValue + offset, min, max);
        targetMat.SetFloat(prop, newValue);
        if (targetMat == skyboxMat)
            clipConfigs[clipPool.index].SetFloatIfPresent(prop, newValue);
    }

    public void EnableDebugGUI()
    {
        sizingBar.SetActive(true);
        debugContainer.SetActive(true);
    }

    public void DisableDebugGUI(bool forceSwatchPickerOff = false)
    {
        sizingBar.SetActive(false);
        debugContainer.SetActive(false);
        if (forceSwatchPickerOff) swatchDetector.Disable();
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

    public void PlayNextClip()
    {
        var exitingClip = clipPool.current;
        exitingClip.Pause();
        clipPool.Next((VideoPlayer enteringClip, int newIndex) =>
        {
            // This callback runs after entering video is prepared.
            // (small detail: clipPool also prepares the next two videos FIRST)
            enteringClip.targetTexture = skyboxTex;
            exitingClip.targetTexture = null;
            skyboxMat.SetInt("_VideoIndex", newIndex);
            SetLayoutFromResolution(enteringClip);

            _undoConfig = null;
            clipConfigs[newIndex].ApplyToMaterial(skyboxMat);

            Blitter.SetCurrentMatte(clipPool.currentMatte); // ignores if null
            PullAndSetMaskState();
            _resetFrameCapture(false); // don't check current mode
            enteringClip.Play();
        });
    }

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
        ClipConfig freshConfig;
        if (_undoConfig == null)
        {
            _undoConfig = clipConfigs[clipPool.index];
            freshConfig = new ClipConfig();
            // Argh, leave this the same:
            freshConfig._RotationX = _undoConfig._RotationX;
            freshConfig._RotationY = _undoConfig._RotationY;
        }
        else
        {
            freshConfig = _undoConfig;
            _undoConfig = null;
        }
        // save the new config when all buttons are released:
        clipConfigs[clipPool.index] = freshConfig;
        ClipConfig.Save(clipConfigs); // do it manually.
        clipConfigs[clipPool.index].ApplyToMaterial(skyboxMat);

        combinedZoomFactor = -3.6f;
    }

    public void CycleMaskModes()
    {
        if (_isDifferenceMaskEnabled == _differenceMaskCount) // cycling!
            DisableMask();
        else
            EnableMask();
    }

    // This performs poorly and isn't used; maybe delete
    public void TogglePlayerThrottle(float amount)
    {
        clipPool.current.playbackSpeed =
        (clipPool.current.playbackSpeed == amount) ?
             1 : amount;
    }

    public void SeekAhead(int seconds) =>
        clipPool.current.time += seconds;

    public void ToggleAutoShiftMode() =>
        useAutoShiftMode = !useAutoShiftMode;

}
