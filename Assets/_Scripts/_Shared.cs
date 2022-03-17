using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/* 
 * This is a stop-gap file for things on their way
 * to a better place.
 */

public static class GaussianKernel
{
    public static float[] Calculate(float sigma, int size)
    {
        float[] ret = new float[size];
        double sum = 0;
        int half = size / 2;
        for (int i = 0; i < size; i++)
        {
            ret[i] = (1 / (Mathf.Sqrt(2 * Mathf.PI) * sigma) * Mathf.Exp(-(i - half) * (i - half) / (2 * sigma * sigma)));
            sum += ret[i];
        }
        return ret;
    }
}

public class FrameSkipper
{
    int _frameIdx = -1;
    int _framesToSkip = 0;

    public FrameSkipper(int framesToSkip)
    {
        _framesToSkip = Mathf.Max(framesToSkip, 1);
    }
    public bool Skip()
    {
        _frameIdx = (_frameIdx + 1) % (_framesToSkip + 1);
        return _frameIdx > 0;
    }
    public void Set(int framesToSkip)
    {
        _framesToSkip = Mathf.Max(framesToSkip, 0);
    }
}


public enum ThumbstickMode
{
    Undefined,
    MostlyDiagonalLeft,
    MostlyDiagonalRight,
    MostlyX,
    MostlyY,
    Centered
}

/*
public class DelayedButton
{
    OVRInput.Button _button;
    float _lastPress = float.MinValue;
    float _beforeLastPress = float.MinValue;
    bool _triggeredSingle = false;
    bool _triggeredDouble = false;

    public DelayedButton(OVRInput.Button button) => _button = button;

    public bool SingleClicked(float beginWindow = 0.4f, float endWindow = 0.5f)
    {
        if (_triggeredSingle) return false;
        bool inWindow = !OVRInput.Get(_button) && Time.time - _lastPress >= beginWindow && Time.time - _lastPress < endWindow;
        if (inWindow) _triggeredSingle = true;
        return inWindow;
    }
    public bool DoubleClicked(float beginWindow = 0, float endWindow = 0.4f)
    {
        if (_triggeredDouble) return false;
        bool inWindow = !OVRInput.Get(_button) && Time.time - _beforeLastPress >= beginWindow && Time.time - _beforeLastPress < endWindow;
        if (inWindow) _triggeredSingle =_triggeredDouble = true;
        return inWindow;
    }

    // The below method will return a bool, but encapsulates a Reset() to effectively wipe out
    // the queue, thereby preventing stale/double firing.
    //
    // WARNING: this requires the caller (eg. InputController) to be somewhat careful with its conditionals.
    // For example, if it checks for both ButtonTwoSingleClick and ButtonTwo in the same control path,
    // ButtonTwoSingleClick will never fire. To prevent this, never combine a delayed button press check
    // with an immediate press check.
    //
    // However, this fixes erroneous double-firing along *separate* control paths, since it essentially
    // detects whether a conditional has returned true and cancels all pending delayed clicks.
    // The alternative involved canceling on trigger-state changes, which would swallow ExtraMode clicks
    // the user releases the trigger too quickly.
    public bool ClickedThisFrame(bool shouldReset = true)
    {
        bool wasPressedThisFrame = OVRInput.GetDown(_button);
        if (shouldReset && wasPressedThisFrame) Reset();
        return wasPressedThisFrame;
    }

    public void RegisterClick()
    {
        _beforeLastPress = _lastPress;
        _lastPress = Time.time;
        if (_triggeredDouble) _triggeredDouble = false;
        if (_triggeredSingle) _triggeredSingle = false;
    }

    public void Reset()
    {
        _lastPress = _beforeLastPress = float.MinValue;
        _triggeredSingle = _triggeredDouble = false;
    }
}*/
public class DelayedButton
{
    OVRInput.Button _button;

    float _latestClickTime;
    float _clickCounter;
    float _waitPerPress;
    float _waitPadding;

    public DelayedButton(OVRInput.Button button, float waitPerPress = 0.3f, float waitPadding = 0.2f)
    {
        _button = button;
        _waitPerPress = waitPerPress;
        _waitPadding = waitPadding;

        Reset();
    }
    public bool MultiClickedPro(int clickCount)
    {
        if (_clickCounter == clickCount &&
            Time.time - _latestClickTime >= _waitPerPress &&
            Time.time - _latestClickTime < _waitPerPress + _waitPadding
        )
        {
            Reset();
            return true;
        }
        return false;
    }

    public bool SingleClicked() => MultiClickedPro(1);
    public bool DoubleClicked() => MultiClickedPro(2);


    public bool ClickedThisFrame(bool shouldReset = true)
    {
        bool wasPressedThisFrame = OVRInput.GetDown(_button);
        if (shouldReset && wasPressedThisFrame) Reset();
        return wasPressedThisFrame;
    }

    public void RegisterClick()
    {
        if (Time.time - _latestClickTime > _waitPerPress + _waitPadding && _clickCounter > 0)
            _clickCounter = 0;

        _latestClickTime = Time.time;
        _clickCounter++;
    }
    public void Reset()
    {
        _latestClickTime = float.MinValue;
        _clickCounter = 0;
    }
}



// Syntactic sugar for OVRInput
public class RightController
{
    private static Vector2 _axes = new Vector2(0, 0);
    private static float _angle;
    private static Vector2 _lastAxes;
    private static float _handTrigger = 0;
    private static float _indexTrigger = 0;
    private static float _handTriggerLast = 0;
    private static float _indexTriggerLast = 0;

    private static ThumbstickMode _thumbstickMode = ThumbstickMode.Centered;

    private static DelayedButton _delayedButtonOne = new DelayedButton(OVRInput.Button.One);
    private static DelayedButton _delayedButtonTwo = new DelayedButton(OVRInput.Button.Two);
    private static DelayedButton _delayedThumbstickButton = new DelayedButton(OVRInput.Button.SecondaryThumbstick);

    public static void Update()
    {
        _lastAxes = _axes;
        _axes = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        _angle = Mathf.Atan(_axes.y / _axes.x) * 180 / Mathf.PI;
        _handTriggerLast = _handTrigger;
        _indexTriggerLast = _indexTrigger;
        _handTrigger = OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger);
        _indexTrigger = OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger);

        // Initiate and keep a thumbstick mode until the next time the thumbstick is
        // centered:
        if (ThumbstickCentered)
            _thumbstickMode = ThumbstickMode.Centered;
        else if (_thumbstickMode == ThumbstickMode.Centered)
            _thumbstickMode =
                _ThumbstickMostlyDiagonalRight ? ThumbstickMode.MostlyDiagonalRight :
                _ThumbstickMostlyDiagonalLeft ? ThumbstickMode.MostlyDiagonalLeft :
                _ThumbstickMostlyX ? ThumbstickMode.MostlyX :
                _ThumbstickMostlyY ? ThumbstickMode.MostlyY :
                ThumbstickMode.Undefined; // It should never be this.

        if (Hands) return;
        if (_delayedButtonOne.ClickedThisFrame(false)) // shouldReset = false
            _delayedButtonOne.RegisterClick();
        if (_delayedButtonTwo.ClickedThisFrame(false))
            _delayedButtonTwo.RegisterClick();
        if (_delayedThumbstickButton.ClickedThisFrame(false))
            _delayedThumbstickButton.RegisterClick();
     }
    public static bool HandTrigger
    {
        get => _handTrigger > 0;
    }
    public static bool OnlyHandTrigger
    {
        get => HandTrigger && !IndexTrigger;
    }
    public static bool IndexTrigger
    {
        get => _indexTrigger > 0;
    }
    public static bool OnlyIndexTrigger
    {
        get => IndexTrigger && !HandTrigger;
    }
    public static bool BothTriggers
    {
        get => _indexTrigger > 0 && _handTrigger > 0;
    }
    public static bool HandTriggerStart
    {
        get => _handTrigger > 0 && _handTriggerLast == 0;
    }
    public static bool HandTriggerStop
    {
        get => _handTriggerLast > 0 && _handTrigger == 0;
    }
    public static bool IndexTriggerStart
    {
        get => _indexTrigger > 0 && _indexTriggerLast == 0;
    }
    public static bool IndexTriggerStop
    {
        get => _indexTriggerLast > 0 && _indexTrigger == 0;
    }
    public static bool BothTriggersStart
    {
        get => (_indexTrigger > 0 && _handTrigger > 0) &&
                (IndexTriggerStart || HandTriggerStart);
    }

    public static bool NoTriggersStart
    {
        get => (_handTriggerLast > 0 || _indexTriggerLast > 0) && NoTriggers;
    }

    public static bool EitherTriggerStart
    {
        get => (_handTriggerLast == 0 && _indexTriggerLast == 0) && EitherTrigger;
    }

    public static bool EitherTriggerStop
    {
        get => IndexTriggerStop || HandTriggerStop;
    }

    public static bool EitherTrigger
    {
        get => _indexTrigger > 0 || _handTrigger > 0;
    }
    public static bool NoTriggers
    {
        get => _indexTrigger == 0 && _handTrigger == 0;
    }

    public static bool ButtonOne
    {
        get => !Hands && _delayedButtonOne.ClickedThisFrame();
    }

    public static bool ButtonOneSingleClick
    {
        get => _delayedButtonOne.MultiClickedPro(1);
    }
    public static bool ButtonOneDoubleClick
    {
        get => _delayedButtonOne.MultiClickedPro(2);
    }
    public static bool ButtonOneTripleClick
    {
        get => _delayedButtonOne.MultiClickedPro(3);
    }
    
    public static bool ButtonTwo
    {
        get => _delayedButtonTwo.ClickedThisFrame();
    }
    public static bool ButtonTwoSingleClick
    {
        get => _delayedButtonTwo.MultiClickedPro(1);
    }
    public static bool ButtonTwoDoubleClick
    {
        get => _delayedButtonTwo.MultiClickedPro(2);
    }
    public static bool ButtonTwoTripleClick
    {
        get => _delayedButtonTwo.MultiClickedPro(3);
    }

    public static bool ThumbstickButton
    {
        get => _delayedThumbstickButton.ClickedThisFrame();
    }
    public static bool ThumbstickButtonSingleClick
    {
        get => _delayedThumbstickButton.SingleClicked();
    }
    public static bool ThumbstickButtonDoubleClick
    {
        get => _delayedThumbstickButton.DoubleClicked();
    }

    public static bool ThumbstickCentered
    {
        get => _axes.x == 0 && _axes.y == 0;
    }
    public static bool ThumbstickOblique // Hmm, maybe rename
    {
        get => _axes.x != 0 && _axes.y != 0;
    }
   
    // The diagonal angle thresholds below are picked arbitrarily. A perfectly divided
    // circle would yield 45/2 = 22.5, but it makes sense to give the diagonals a small amount
    // of bias, since it tends to be easier to move the thumbstick at right angles.
    private static bool _ThumbstickMostlyDiagonalRight
    {
        get => !ThumbstickCentered && _angle > 0 && Mathf.Abs(Mathf.Abs(_angle) - 45) < 26;
    }
    private static bool _ThumbstickMostlyDiagonalLeft
    {
        get => !ThumbstickCentered && _angle < 0 && Mathf.Abs(Mathf.Abs(_angle) - 45) < 26;
    }
    private static bool _ThumbstickMostlyX
    {
        get => !ThumbstickCentered && Mathf.Abs(_axes.x) > Mathf.Abs(_axes.y);
    }
    private static bool _ThumbstickMostlyY
    {
        get => !ThumbstickCentered && Mathf.Abs(_axes.x) <= Mathf.Abs(_axes.y);
    }

    // The Update() loop makes thumbstick directions "sticky", such that the public
    // versions of these getters should only check against the assigned enum:
    public static bool ThumbstickMostlyDiagonalRight
    {
        get => _thumbstickMode == ThumbstickMode.MostlyDiagonalRight;
    }
    public static bool ThumbstickMostlyDiagonalLeft
    {
        get => _thumbstickMode == ThumbstickMode.MostlyDiagonalLeft;
    }
    public static bool ThumbstickMostlyX
    {
        get => _thumbstickMode == ThumbstickMode.MostlyX;
    }
    public static bool ThumbstickMostlyY
    {
        get => _thumbstickMode == ThumbstickMode.MostlyY;
    }

    public static bool ThumbstickStartX
    {
        get => _axes.x != 0 && _lastAxes.x == 0;
    }
    public static bool ThumbstickStartY
    {
        get => _axes.y != 0 && _lastAxes.y == 0;
    }
    public static bool ThumbstickStopX
    {
        get => _axes.x == 0 && _lastAxes.x != 0;
    }
    public static bool ThumbstickStopY
    {
        get => _axes.y == 0 && _lastAxes.y != 0;
    }
    public static bool ThumbstickAnyX
    {
        get => _axes.x != 0;
    }
    public static bool ThumbstickAnyY
    {
        get => _axes.y != 0;
    }
    public static Vector2 ThumbstickMagnitude
    {
        get => _axes;
    }
    public static float ThumbstickDiagonalMagnitude
    {
        get => Mathf.Sign(_axes.y) * Mathf.Sqrt(Mathf.Pow(_axes.x, 2) + Mathf.Pow(_axes.y, 2));
    }

    public static bool Hands
    {
        get => OVRInput.IsControllerConnected(OVRInput.Controller.Hands);
    }
    public static bool Pinch
    {
        get => Hands && OVRInput.Get(OVRInput.Button.One);
    }
    public static bool PinchStart
    {
        get => Hands && OVRInput.GetDown(OVRInput.Button.One);
    }
}

public static class VR
{
    // Syntactic sugar to produce a platform-dependent RenderTexture screen desc:
    public static RenderTextureDescriptor desc
    {
        get => XRSettings.enabled ?
            XRSettings.eyeTextureDesc :
            new RenderTextureDescriptor(Screen.width, Screen.height);           
    }
    public static bool Left {
        get => Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left;
    }
    public static bool Right
    {
        get => Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right;
    }
    public static bool Mono
    {
        get => Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Mono;
    }
}


public class Util
{
    /*
        Copyright 2016 Max Kaufmann (max.kaufmann@gmail.com)
        Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
        The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
    */
    public static Quaternion SmoothDampQuaternion(Quaternion rot, Quaternion target, ref Quaternion deriv, float time)
    {
        if (Time.deltaTime < Mathf.Epsilon) return rot;
        // account for double-cover
        var Dot = Quaternion.Dot(rot, target);
        var Multi = Dot > 0f ? 1f : -1f;
        target.x *= Multi;
        target.y *= Multi;
        target.z *= Multi;
        target.w *= Multi;
        // smooth damp (nlerp approx)
        var Result = new Vector4(
            Mathf.SmoothDamp(rot.x, target.x, ref deriv.x, time),
            Mathf.SmoothDamp(rot.y, target.y, ref deriv.y, time),
            Mathf.SmoothDamp(rot.z, target.z, ref deriv.z, time),
            Mathf.SmoothDamp(rot.w, target.w, ref deriv.w, time)
        ).normalized;

        // ensure deriv is tangent
        var derivError = Vector4.Project(new Vector4(deriv.x, deriv.y, deriv.z, deriv.w), Result);
        deriv.x -= derivError.x;
        deriv.y -= derivError.y;
        deriv.z -= derivError.z;
        deriv.w -= derivError.w;

        return new Quaternion(Result.x, Result.y, Result.z, Result.w);
    }
}