using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Added this as a kludge for Debug mode:
public enum MetaMode
{
    None,
    Debug,
}

public enum ExtraMode
{
    None, // 0
    ZoomAndHorizontalOffset, // 1
    NudgeXY, // 2
    ResizeFactorAndResize, // 3
    SwatchPicker, // 4
    ZoomShiftXY, //5
    PlaybackSeekForward, // 6; thumbstick adjusts matte border, weirdly
    PlaybackSeekBackward, // 7; thumbstick adjusts matte border, also weirdly
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
    public MetaMode metaMode = MetaMode.Debug; // Currently, for dialing in DynThresh

    public InputController(ClipManager ctx)
    {
        _ctx = ctx;
    }

    // exitCondition was first added for use with swatchPicker, which
    // cycles through three modes; this way, it's possible to succinctly
    // call toggle, but stay in the mode unless a condition is met.
    void ToggleMode(ExtraMode mode, bool exitCondition = true)
    {
        extraMode = extraMode == mode && exitCondition ? ExtraMode.None : mode;
    }

    void ToggleMetaMode(MetaMode mode) => metaMode = metaMode == mode ? MetaMode.None : mode;

    public void Run()
    {
        RightController.Update();

        // First, perform upkeep that depends on a trigger or non-trigger state:
        if (RightController.NoTriggersStart) {
            extraMode = ExtraMode.None; // disable any submodes

            _ctx.DisableDebugGUI(true); // forceSwatchPickerOff == true

            // When nothing is pressed, trigger persistence:
            if (RightController.ThumbstickCentered)
                _ctx.UpdateDirtyConfig();
        }

        // Assign trigger combinations to enums. This was as terse as I could get it,
        // and only confusing if we think too hard about it:
        triggerMode =
            RightController.BothTriggers ? TriggerMode.TransparencyAndExposure :
            RightController.IndexTrigger ? TriggerMode.ZoomCompAndZoomAdjustComp :
            RightController.HandTrigger  ? TriggerMode.HorizontalOffsetCompAndSaturation :
            TriggerMode.None;

        if (RightController.EitherTriggerStart)
            _ctx.EnableDebugGUI();

        if (RightController.IndexTriggerStart)// || RightController.PinchStart)
            _ctx.InitHeadSync();
        if (RightController.OnlyIndexTrigger)// || RightController.Pinch)
        {
            if (RightController.HandTriggerStop)
                _ctx.InitHeadSync(); // prevents stale headSync offsets
            _ctx.UpdateHeadSync();
        }
        if (RightController.IndexTriggerStop)
            _ctx.UpdateDirtyConfig();

        // Next, deal with buttons and thumbstick, with or without triggers:
        var thumb = RightController.ThumbstickMagnitude;

        switch (triggerMode)
        {
            case TriggerMode.None:
                // First, deal with buttons, globally
                // ButtonTwo is on top, ButtonOne is underneath
                if (RightController.ButtonTwoSingleClick) _ctx.PlayNextClip();
                if (RightController.ButtonTwoDoubleClick) _ctx.PlayPrevClip();
                if (RightController.ButtonTwoTripleClick) _ctx.SeekAhead(-60);
                if (RightController.ButtonOneSingleClick) _ctx.TogglePlaying();
                if (RightController.ButtonOneDoubleClick) _ctx.SeekAhead(60);
                if (RightController.ButtonOneTripleClick) _ctx.SeekAhead(180);
                if (RightController.ThumbstickButtonSingleClick)
                {
                    ToggleMetaMode(MetaMode.Debug);
                    if (metaMode == MetaMode.Debug)
                        _ctx.EnableDebugGUI();
                    else if (triggerMode == TriggerMode.None)
                        _ctx.DisableDebugGUI();                        
                }
                if (RightController.ThumbstickButtonDoubleClick) _ctx.ResetProps();

                // If applicable, deal with extraMode specific buttons here.

                // Then, thumbstick
                if (RightController.ThumbstickAnyX)
                    //_ctx.OffsetProp("_RotationY", -thumb.x, -180, 180);
                    if (RightController.ThumbstickMostlyX)
                        if (metaMode == MetaMode.Debug)
                            _ctx.outliner.handDepthCompX = Mathf.Clamp(_ctx.outliner.handDepthCompX + thumb.x * 0.5f, -50.0f, 50.0f);
                            //_ctx.OffsetProp("_SampleDecay", thumb.x * 0.03f, 0, 2, Blitter.dynThreshBlitMat);
                        else 
                            _ctx.AdjustMaskMultiplier(thumb.x);
                            //_ctx.OffsetProp("_WeightMultiplier", thumb.x * 0.1f, 1, 30, Blitter.matteMaskAlphaBlitMat);
                if (RightController.ThumbstickAnyY)
                    //_ctx.OffsetProp("_RotationX", thumb.y, -180, 180);
                    if (RightController.ThumbstickMostlyY)
                        if (metaMode == MetaMode.Debug)
                            _ctx.OffsetProp("_OutputDecay", thumb.y * 0.03f, 0, 2, Blitter.dynThreshBlitMat);
                        else
                            _ctx.AdjustMaskPower(thumb.y);
                            //_ctx.OffsetProp("_WeightPower", thumb.y*0.1f, 1, 30, Blitter.matteMaskAlphaBlitMat);
                break;

            case TriggerMode.TransparencyAndExposure: // BOTH TRIGGERS
                // Global buttons
                if (RightController.ButtonTwoSingleClick) ToggleMode(ExtraMode.ZoomAndHorizontalOffset);
                if (RightController.ButtonOneSingleClick) ToggleMode(ExtraMode.ResizeFactorAndResize);

                if (RightController.ButtonTwoDoubleClick) _ctx.SetPlayStart();
                if (RightController.ButtonTwoTripleClick) _ctx.ClearPlayStart();

                if (RightController.ButtonOneDoubleClick) _ctx.SetPlayEnd();
                if (RightController.ButtonOneTripleClick) _ctx.ClearPlayEnd();

                // ExtraMode-specific buttons
                // Note: button behavior has the switch on the outside,
                // (Thumbstick pattern is the other way around)
                switch (extraMode)
                {
                    case ExtraMode.None:
                        if (RightController.ThumbstickButton)
                            _ctx.ToggleAutoShiftMode();
                        break;
                }

                // For top-level switches (kludgy, just for now):
                switch (metaMode)
                {
                    case MetaMode.None:
                        if (RightController.ThumbstickMostlyDiagonalRight)
                        {
                            switch (extraMode)
                            {
                                case ExtraMode.None:
                                    _ctx.OffsetProp("_Saturation", RightController.ThumbstickDiagonalMagnitude * 0.02f, 0, 1);
                                    break;
                            }
                        }
                        else if (RightController.ThumbstickMostlyDiagonalLeft)
                        {
                            switch (extraMode)
                            {
                                case ExtraMode.None:
                                    _ctx.OffsetProp("_Contrast", RightController.ThumbstickDiagonalMagnitude * 0.003f, 0, 2);
                                    break;
                            }
                        }
                        else if (RightController.ThumbstickMostlyX) // this will effectively lock to the axis
                        //if (RightController.ThumbstickMostlyX)
                            switch (extraMode) {
                                case ExtraMode.None:
                                    //_ctx.OffsetProp("_ColorDistMultThresh", thumb.x * 0.002f, 0, 0.1f, Blitter.dynThreshBlitMat);
                                    _ctx.OffsetProp("_Transparency", thumb.x * 0.02f, -1.5f, 1.5f);
                                    break;
                                case ExtraMode.ZoomAndHorizontalOffset:
                                    //_ctx.OffsetProp("_DistMultiplier", thumb.x * 0.1f, 1, 20, Blitter.dynThreshBlitMat);
                                    _ctx.OffsetProp("_HorizontalOffset", thumb.x * 0.01f, -1.5f, 1.5f);
                                    break;
                                case ExtraMode.ResizeFactorAndResize:
                                    //_ctx.OffsetProp("_InnerThreshMultiplier", thumb.x * 0.1f, 1, 20, Blitter.dynThreshBlitMat);
                                    _ctx.combinedZoomFactor = Mathf.Clamp(_ctx.combinedZoomFactor + thumb.x * 0.05f, -15.0f, 15.0f);
                                    break;
                            }
                        else if (RightController.ThumbstickMostlyY)
                            switch (extraMode)
                            {
                                case ExtraMode.None:
                                    //_ctx.OffsetProp("_ColorDistMultStrength", thumb.y * 0.5f, 0, 30, Blitter.dynThreshBlitMat);
                                    _ctx.OffsetProp("_Exposure", thumb.y * 0.02f, 0, 2);
                                    break;
                                case ExtraMode.ZoomAndHorizontalOffset:
                                    //_ctx.OffsetProp("_DistPower", thumb.y * 0.1f, 1, 20, Blitter.dynThreshBlitMat);
                                    _ctx.OffsetProp("_BaseZoom", thumb.y * 0.01f, -3.00f, 3.00f);
                                    break;
                                case ExtraMode.ResizeFactorAndResize:
                                    //_ctx.OffsetProp("_InnerThreshPower", thumb.y * 0.1f, 1, 20, Blitter.dynThreshBlitMat);
                                    // Argh, this should ESPECIALLY be defined somewhere:
                                    float step = 0.005f;
                                    _ctx.OffsetProp("_BaseZoom", thumb.y * step, -3.00f, 3.00f);
                                    _ctx.OffsetProp("_HorizontalOffset", thumb.y * step / _ctx.combinedZoomFactor, -3.00f / Mathf.Abs(_ctx.combinedZoomFactor), 3.00f / Mathf.Abs(_ctx.combinedZoomFactor));
                                    break;
                            }
                        break;
                    case MetaMode.Debug:
                        if (RightController.ThumbstickMostlyX) // this will effectively lock to the axis
                            switch (extraMode)
                            {
                                case ExtraMode.None:
                                    _ctx.OffsetProp("_ColorDistMultThresh", thumb.x * 0.002f, 0, 1.0f, Blitter.dynThreshBlitMat);
                                    break;
                                case ExtraMode.ZoomAndHorizontalOffset:
                                    _ctx.OffsetProp("_DistMultiplier", thumb.x * 0.1f, 0, 20, Blitter.dynThreshBlitMat);
                                    break;
                                case ExtraMode.ResizeFactorAndResize:
                                    _ctx.OffsetProp("_InnerThreshMultiplier", thumb.x * 0.1f, 1, 20, Blitter.dynThreshBlitMat);
                                    break;
                            }
                        else if (RightController.ThumbstickMostlyY)
                            switch (extraMode)
                            {
                                case ExtraMode.None:
                                    _ctx.OffsetProp("_ColorDistMultStrength", thumb.y * 0.5f, 0, 40, Blitter.dynThreshBlitMat);
                                    break;
                                case ExtraMode.ZoomAndHorizontalOffset:
                                    _ctx.OffsetProp("_DistPower", thumb.y * 0.1f, 0, 20, Blitter.dynThreshBlitMat);
                                    break;
                                case ExtraMode.ResizeFactorAndResize:
                                    _ctx.OffsetProp("_InnerThreshPower", thumb.y * 0.1f, 1, 20, Blitter.dynThreshBlitMat);
                                    break;
                            }
                        break;
                }
                break;

            case TriggerMode.ZoomCompAndZoomAdjustComp: // INDEX TRIGGER

                // Extra-mode specific
                switch (extraMode)
                {
                    case ExtraMode.None:
                    case ExtraMode.ZoomShiftXY:
                        if (RightController.ThumbstickButton) ToggleMode(ExtraMode.PlaybackSeekForward);
                        if (RightController.ButtonTwoSingleClick) ToggleMode(ExtraMode.NudgeXY);
                        if (RightController.ButtonTwoDoubleClick) _ctx.ToggleProp("_VerticalFlip");
                        if (RightController.ButtonOne) ToggleMode(ExtraMode.ZoomShiftXY);
                        break;
                    case ExtraMode.PlaybackSeekForward:
                    case ExtraMode.PlaybackSeekBackward:
                        var seekSign = extraMode == ExtraMode.PlaybackSeekForward ? 1 : -1;
                        if (RightController.ThumbstickButton) ToggleMode(ExtraMode.PlaybackSeekBackward);
                        if (RightController.ButtonTwo) _ctx.SeekAhead(seekSign*180);
                        if (RightController.ButtonOne) _ctx.SeekAhead(seekSign*60);
                        break;
                }
                
                if (RightController.ThumbstickMostlyX) // Disallow diagonals
                    switch (extraMode)
                    {
                        case ExtraMode.None:
                            //_ctx.OffsetProp("_ZoomNudgeFactor", thumb.x * 0.1f, 0, 6); // Zoom? I think this is horizontal offset.. Not used anymore?!
                            //_ctx.OffsetProp("_BlurX", thumb.x * 0.075f, 0, 5, Blitter.matteMaskAlphaBlurMat);
                            //_ctx.OffsetProp("_BlurX", thumb.x * 0.075f, 0, 5, Blitter.matteMaskThreshBlurMat);

                            _ctx.OffsetProp("_Strength", thumb.x * 0.01f, 0, 0.4f, Blitter.filmGrainMaterial);
                            break;
                        case ExtraMode.NudgeXY:
                            _ctx.OffsetProp("_NudgeFactorX", thumb.x * 0.01f, 0, 1);
                            break;
                        case ExtraMode.ZoomShiftXY:
                            _ctx.OffsetProp("_ZoomShiftX", thumb.x * 0.02f, -1, 1);
                            break;
                        case ExtraMode.PlaybackSeekForward:
                            _ctx.OffsetProp("_WeightMultiplier", thumb.x * 0.1f, 1, 30, Blitter.matteMaskAlphaBlitMat);
                            break;
                        case ExtraMode.PlaybackSeekBackward:
                            _ctx.OffsetProp("_BlurX", thumb.x * 0.075f, 0, 5, Blitter.matteMaskAlphaBlurMat);
                            break;
                    }


                if (RightController.ThumbstickMostlyY)
                    switch (extraMode)
                    {
                        case ExtraMode.None:
                            //_ctx.OffsetProp("_ZoomAdjustNudgeFactor", -thumb.y * 0.05f, 0, 1); // squashes down, so reversed
                            //_ctx.OffsetProp("_BlurY", thumb.y * 0.075f, 0, 5, Blitter.matteMaskAlphaBlurMat);
                            //_ctx.OffsetProp("_BlurY", thumb.y * 0.075f, 0, 5, Blitter.matteMaskThreshBlurMat);

                            _ctx.OffsetProp("_GrainBias", thumb.y * 0.01f, 0, 2, Blitter.compositingMaterial);
                            break;
                        case ExtraMode.NudgeXY:
                            _ctx.OffsetProp("_NudgeFactorY", thumb.y * 0.01f, 0, 1);
                            break;
                        case ExtraMode.ZoomShiftXY:
                            // Note: AutoShiftMode controls _ZoomShiftY and makes delicate calculations
                            // that are broken by user-augmentation of the term, so the same button is
                            // switched to RotationShift, which performs a similar function.
                            // It is currently NOT linked to ConfigSettings, however.
                            if (_ctx.useAutoShiftMode)
                                _ctx.OffsetProp("_RotationShiftX", -thumb.y, -180, 180);
                            else
                                _ctx.OffsetProp("_ZoomShiftY", thumb.y * 0.02f, -1, 1);
                            break;
                        case ExtraMode.PlaybackSeekForward:
                            _ctx.OffsetProp("_WeightPower", thumb.y*0.1f, 1, 30, Blitter.matteMaskAlphaBlitMat);
                            break;
                        case ExtraMode.PlaybackSeekBackward:
                            _ctx.OffsetProp("_BlurY", thumb.y * 0.075f, 0, 5, Blitter.matteMaskAlphaBlurMat);
                            break;
                    }
                break;

            case TriggerMode.HorizontalOffsetCompAndSaturation: // HAND TRIGGER
                // TriggerMode global buttons
                if (RightController.ButtonOne)
                {
                    _ctx.swatchDetector.Toggle();
                    var exitCondition = !_ctx.swatchDetector.IsDetectorActive();
                    ToggleMode(ExtraMode.SwatchPicker, exitCondition);
                } 

                // ExtraMode local buttons.
                switch(extraMode)
                {
                    case ExtraMode.None:
                        if (RightController.ButtonTwo) _ctx.CycleMaskModes(); // not an extra mode! debug only
                        if (RightController.ThumbstickButton)
                        {
                            //_ctx.outliner.handOffsetY = 0;
                            //_ctx.outliner.handOffsetX = 0;
                            _ctx.outliner.enabled = !_ctx.outliner.enabled;
                        }
                        break;
                    case ExtraMode.SwatchPicker:
                        if (RightController.ButtonTwo)
                            _ctx.swatchDetector.ToggleLight();
                        if (RightController.ThumbstickStopX)
                            _ctx.swatchDetector.Pause();
                        if (RightController.ThumbstickButton)
                            _ctx.swatchDetector.ResetCurrentMode();
                        break;
                }
                
                // ExtraMode-relative thumbstick behavior
                if (RightController.ThumbstickMostlyX)
                    switch (extraMode)
                    {
                        case ExtraMode.None:
                            if (metaMode == MetaMode.Debug)
                                _ctx.OffsetProp("_DecayDampThresh", thumb.x * 0.002f, 0, 0.2f, Blitter.dynThreshBlitMat);
                            else
                                _ctx.OffsetProp("_HorizontalOffsetNudgeFactor", thumb.x * 0.05f, 0, 1); // Horizontal Offset Factor
                            //_ctx.OffsetProp("_AutoShiftRotationXNudgeFactor", -thumb.x * 0.5f, -360, 360);
                            //_ctx.outliner.handOffsetX += thumb.x;
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
                            if (metaMode == MetaMode.Debug)
                                _ctx.OffsetProp("_DecayDampStrength", thumb.y * 0.5f, 0, 20, Blitter.dynThreshBlitMat);
                            else 
                                _ctx.OffsetProp("_Saturation", thumb.y * 0.02f, 0, 1);
                            //_ctx.outliner.handOffsetY += thumb.y;
                            break;
                        case ExtraMode.SwatchPicker:
                            _ctx.laser.maxLength += thumb.y * 0.02f;
                            break;
                    }
                break;
        }

        // Keyboard, for debugging:
        if (Input.GetKeyDown(KeyCode.F))
            _ctx.ToggleProp("_VerticalFlip");
        if (Input.GetKeyDown(KeyCode.I))
            _ctx.swatchDetector.MakeFreshSwatches();
        if (Input.GetKeyDown(KeyCode.O))
            _ctx.swatchDetector.Toggle(SwatchPickerMode.Exclusion);
        if (Input.GetKeyDown(KeyCode.P))
            _ctx.swatchDetector.Toggle(SwatchPickerMode.Inclusion);
        if (Input.GetKeyDown(KeyCode.L))
            _ctx.swatchDetector.Toggle(SwatchPickerMode.MaskDrawExclusion);
        if (Input.GetKeyDown(KeyCode.K))
            _ctx.swatchDetector.Toggle(SwatchPickerMode.MaskDrawDeleteExclusion);
        if (Input.GetKeyDown(KeyCode.M))
            ClipConfig.Save(_ctx.clipConfigs);
        if (Input.GetKeyDown(KeyCode.W))
            _ctx.TogglePlaying();
        if (Input.GetKeyDown(KeyCode.S))
            _ctx.PlayNextClip();
        if (Input.GetKeyDown(KeyCode.T))
            _ctx.CycleMaskModes();
        if (Input.GetKeyDown(KeyCode.N))
            _ctx.SeekAhead(30);
        if (Input.GetKeyDown(KeyCode.B))
        {
            _ctx.swatchDetector.Toggle(SwatchPickerMode.MaskDrawDeleteExclusion);
            _ctx.swatchDetector.UseAndPlaceLight();
        }
        if (Input.GetKeyDown(KeyCode.Z))
            _ctx.SetPlayStart();
        if (Input.GetKeyDown(KeyCode.X))
            _ctx.SetPlayEnd();
        if (Input.GetKeyDown(KeyCode.C))
            _ctx.ClearPlayStart();
        if (Input.GetKeyDown(KeyCode.V))
            _ctx.ClearPlayStart();
    }
}
