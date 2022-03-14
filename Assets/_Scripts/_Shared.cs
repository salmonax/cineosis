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
        get => !Hands && OVRInput.GetDown(OVRInput.Button.One);
    }
    public static bool ButtonTwo
    {
        get => OVRInput.GetDown(OVRInput.Button.Two);
    }
    public static bool ThumbstickButton
    {
        get => OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick);
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