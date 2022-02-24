using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ExtraMode
{
    None, // 0
    ZoomAndHorizontalOffset, // 1
    NudgeXY, // 2
    ResizeFactorAndResize, // 3
    SwatchPicker, // 4
    ZoomShiftXY, //5
    PlaybackThrottle,
}

// Not using yet, but probably will; useful to document
public enum TriggerMode
{
    None, // mostly superfluous, but whatever
    TransparencyAndExposure,// Both
    ZoomCompAndZoomAdjustComp, // Index
    HorizontalOffsetCompAndSaturation, // Hand
}

public class InputController
{
    ClipManager _ctx;
    public ExtraMode extraMode = ExtraMode.None;
    public TriggerMode triggerMode = TriggerMode.None;

    public InputController(ClipManager ctx)
    {
        _ctx = ctx;
    }
    
    void ToggleMode(ExtraMode mode)
    {
        extraMode = extraMode == mode ? ExtraMode.None : mode;
    }

    public void Run()
    {
        RightController.Update();

        // First, perform upkeep that depends on a trigger or non-trigger state:
        if (RightController.NoTriggersStart) {
            extraMode = ExtraMode.None; // disable any submodes

            // TODO: move this to a bar controller!
            _ctx.sizingBar.SetActive(false);
            _ctx.debugContainer.SetActive(false);

            _ctx.swatchDetector.Disable();

            if (RightController.ThumbStickCentered)
            {
                // Nothing pressed at all; trigger persistence
                // NOTE: holy crap, inputControll should NOT have to know about clipPool!
                if (_ctx.clipConfigs[_ctx.clipPool.index].needsUpdate)
                {
                    ClipConfig.Save(_ctx.clipConfigs); // WARNING: saves ALL of them.
                    _ctx.clipConfigs[_ctx.clipPool.index].needsUpdate = false;
                }
            }
        }

        // Assign trigger combinations to enums. This was as terse as I could get it,
        // and only confusing if I think too hard about it:
        triggerMode =
            RightController.BothTriggers ? TriggerMode.TransparencyAndExposure :
            RightController.IndexTrigger ? TriggerMode.ZoomCompAndZoomAdjustComp :
            RightController.HandTrigger  ? TriggerMode.HorizontalOffsetCompAndSaturation :
            TriggerMode.None;

        if (RightController.EitherTriggerStart) {
            _ctx.sizingBar.SetActive(true);
            _ctx.debugContainer.SetActive(true);
        }

        // Next, deal with buttons and thumbstick, with or without triggers:
        var thumb = RightController.ThumbstickMagnitude;

        switch (triggerMode)
        {
            case TriggerMode.None:
                // First, deal with buttons, globally
                // ButtonTwo is on top, ButtonOne is underneath
                if (RightController.ButtonTwo) _ctx.playNextClip();
                if (RightController.ButtonOne)_ctx.togglePlaying();
                if (RightController.ThumbstickButton) _ctx.resetProps();

                // If applicable, deal with extraMode specific buttons here.

                // Then, thumbstick
                if (RightController.ThumbstickAnyX) _ctx.offsetProp("_RotationY", -thumb.x, -180, 180);
                if (RightController.ThumbstickAnyY) _ctx.offsetProp("_RotationX", thumb.y, -180, 180);
                break;

            case TriggerMode.TransparencyAndExposure: // BOTH TRIGGERS
                // Global buttons
                if (RightController.ButtonTwo) ToggleMode(ExtraMode.ZoomAndHorizontalOffset);
                if (RightController.ButtonOne) ToggleMode(ExtraMode.ResizeFactorAndResize);


                // ExtraMode-specific buttons
                // Note: button behavior has the switch on the outside
                // (Thumbstick behavior is the other way around)
                switch (extraMode)
                {
                    case ExtraMode.None:
                        if (RightController.ThumbstickButton)
                            _ctx.ToggleAutoShiftMode();
                        break;
                }

                if (RightController.ThumbStickMostlyX) // this will effectively lock to the axis
                    switch (extraMode) {
                        case ExtraMode.None:
                            _ctx.offsetProp("_Transparency", thumb.x * 0.02f, -1.5f, 1.5f);
                            break;
                        case ExtraMode.ZoomAndHorizontalOffset:
                            _ctx.offsetProp("_HorizontalOffset", thumb.x * 0.01f, -1.5f, 1.5f);
                            break;
                        case ExtraMode.ResizeFactorAndResize:
                            _ctx.combinedZoomFactor = Mathf.Clamp(_ctx.combinedZoomFactor + thumb.x * 0.05f, -15.0f, 15.0f);
                            break;
                    }
                if (RightController.ThumbstickMostlyY)
                    switch (extraMode)
                    {
                        case ExtraMode.None:
                            _ctx.offsetProp("_Exposure", thumb.y * 0.02f, 0, 1);
                            break;
                        case ExtraMode.ZoomAndHorizontalOffset:
                            _ctx.offsetProp("_BaseZoom", thumb.y * 0.01f, -3.00f, 3.00f);
                            break;
                        case ExtraMode.ResizeFactorAndResize:
                            // Argh, this should ESPECIALLY be defined somewhere:
                            float step = 0.005f;
                            _ctx.offsetProp("_BaseZoom", thumb.y * step, -1.5f, 1.5f);
                            _ctx.offsetProp("_HorizontalOffset", thumb.y * step / _ctx.combinedZoomFactor, -1.5f / Mathf.Abs(_ctx.combinedZoomFactor), 1.5f / Mathf.Abs(_ctx.combinedZoomFactor));
                            break;
                    }
                break;

            case TriggerMode.ZoomCompAndZoomAdjustComp: // INDEX TRIGGER
                if (RightController.ThumbstickButton) ToggleMode(ExtraMode.PlaybackThrottle);

                // Extra-mode specific
                switch (extraMode)
                {
                    case ExtraMode.None:
                    case ExtraMode.ZoomShiftXY:
                        if (RightController.ButtonTwo) ToggleMode(ExtraMode.NudgeXY);
                        if (RightController.ButtonOne) ToggleMode(ExtraMode.ZoomShiftXY);
                        break;
                    case ExtraMode.PlaybackThrottle:
                        if (RightController.ButtonTwo) _ctx.togglePlayerThrottle(-1.5f);
                        if (RightController.ButtonOne) _ctx.togglePlayerThrottle(1.5f);
                        break;
                }
                
                if (RightController.ThumbStickMostlyX) // Disallow diagonals
                    switch (extraMode)
                    {
                        case ExtraMode.None:
                            _ctx.offsetProp("_ZoomNudgeFactor", thumb.x * 0.1f, 0, 6); // Zoom? I think this is horizontal offset.. Not used anymore?!
                            break;
                        case ExtraMode.NudgeXY:
                            _ctx.offsetProp("_NudgeFactorX", thumb.x * 0.01f, 0, 1);
                            break;
                        case ExtraMode.ZoomShiftXY:
                            _ctx.offsetProp("_ZoomShiftX", thumb.x * 0.02f, -1, 1);
                            break;
                    }


                if (RightController.ThumbstickMostlyY)
                    switch (extraMode)
                    {
                        case ExtraMode.None:
                            _ctx.offsetProp("_ZoomAdjustNudgeFactor", -thumb.y * 0.05f, 0, 1); // squashes down, so reversed
                            break;
                        case ExtraMode.NudgeXY:
                            _ctx.offsetProp("_NudgeFactorY", thumb.y * 0.01f, 0, 1);
                            break;
                        case ExtraMode.ZoomShiftXY:
                            // Note: AutoShiftMode controls _ZoomShiftY and makes delicate calculations
                            // that are broken by user-augmentation of the term, so the same button is
                            // switched to RotationShift, which performs a similar function.
                            // It is currently NOT linked to ConfigSettings, however.
                            if (_ctx.useAutoShiftMode)
                                _ctx.offsetProp("_RotationShiftX", -thumb.y, -180, 180);
                            else
                                _ctx.offsetProp("_ZoomShiftY", thumb.y * 0.02f, -1, 1);
                            break;
                    }
                break;

            case TriggerMode.HorizontalOffsetCompAndSaturation: // HAND TRIGGER
                // TriggerMode global buttons
                if (RightController.ButtonOne)
                {
                    ToggleMode(ExtraMode.SwatchPicker);
                    _ctx.swatchDetector.Toggle();
                }
                if (RightController.ButtonTwo) _ctx.ToggleMask(); // not an extra mode! debug only

                // ExtraMode local buttons.
                switch(extraMode)
                {
                    case ExtraMode.None:
                        if (RightController.ThumbstickButton)
                            _ctx.outliner.enabled = !_ctx.outliner.enabled;
                        break;
                    case ExtraMode.SwatchPicker:
                        if (RightController.ThumbstickStopX)
                            _ctx.swatchDetector.Pause();
                        if (RightController.ThumbstickButton)
                            _ctx.swatchDetector.MakeFreshSwatches();
                        break;
                }
                
                // ExtraMode-relative thumbstick behavior
                if (RightController.ThumbStickMostlyX)
                    switch (extraMode)
                    {
                        case ExtraMode.None:
                            //_ctx.offsetProp("_AutoShiftRotationXNudgeFactor", -thumb.x * 0.5f, -360, 360);
                            _ctx.offsetProp("_HorizontalOffsetNudgeFactor", thumb.x * 0.05f, 0, 1); // Horizontal Offset Factor
                            break;
                        case ExtraMode.SwatchPicker:
                            if (RightController.ThumbstickStartX)
                                if (RightController.ThumbstickMagnitude.x > 0)
                                    _ctx.swatchDetector.UseInclusionMode();
                                else
                                    _ctx.swatchDetector.UseExclusionMode();
                            break;
                    }
                if (RightController.ThumbstickMostlyY)
                    switch (extraMode)
                    {
                        case ExtraMode.None:
                            _ctx.offsetProp("_Saturation", thumb.y * 0.02f, 0, 1);    
                            break;
                        case ExtraMode.SwatchPicker:
                            _ctx.laser.maxLength += thumb.y * 0.02f;
                            break;
                    }
                break;
        }

        // Keyboard, for debugging:
        if (Input.GetKeyDown(KeyCode.I))
            _ctx.swatchDetector.MakeFreshSwatches();
        if (Input.GetKeyDown(KeyCode.O))
            _ctx.swatchDetector.Toggle(SwatchPickerMode.Exclusion);
        if (Input.GetKeyDown(KeyCode.P))
            _ctx.swatchDetector.Toggle(SwatchPickerMode.Inclusion);
        if (Input.GetKeyDown(KeyCode.M))
            ClipConfig.Save(_ctx.clipConfigs);
        if (Input.GetKeyDown(KeyCode.W))
            _ctx.togglePlaying();
        if (Input.GetKeyDown(KeyCode.S))
            _ctx.playNextClip();
        if (Input.GetKeyDown(KeyCode.T))
            _ctx.ToggleMask();
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            float foo = _ctx.skyboxMat.GetFloat("_Exposure");
            _ctx.skyboxMat.SetFloat("_Exposure", foo + (float)0.1);
        }
    }
}
