using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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


// Syntactic sugar for OVRInput
public class RightController
{
    private static Vector2 _axes = new Vector2(0, 0);
    private static Vector2 _lastAxes;
    private static float _handTrigger;
    private static float _indexTrigger;
    private static float _handTriggerLast;
    private static float _indexTriggerLast;

    public static void Update()
    {
        _lastAxes = _axes;
        _axes = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
        _handTriggerLast = _handTrigger;
        _indexTriggerLast = _indexTrigger;
        _handTrigger = OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger);
        _indexTrigger = OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger);
    }
    public static bool HandTrigger
    {
        get => _handTrigger > 0;
    }
    public static bool IndexTrigger
    {
        get => _indexTrigger > 0;
    }
    public static bool BothTriggers
    {
        get => _indexTrigger > 0 && _handTrigger > 0;
    }
    public static bool HandTriggerStart
    {
        get => _handTrigger > 0 && _handTriggerLast == 0;
    }
    public static bool IndexTriggerStart
    {
        get => _indexTrigger > 0 && _indexTriggerLast == 0;
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
        get => OVRInput.GetDown(OVRInput.Button.One);
    }
    public static bool ButtonTwo
    {
        get => OVRInput.GetDown(OVRInput.Button.Two);
    }
    public static bool ThumbstickButton
    {
        get => OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick);
    }
    public static bool ThumbStickCentered
    {
        get => _axes.x == 0 && _axes.y == 0;
    }
    public static bool ThumbStickOblique // Hmm, maybe rename
    {
        get => _axes.x != 0 && _axes.y != 0;
    }
    public static bool ThumbStickMostlyX
    {
        get => !ThumbStickCentered && Mathf.Abs(_axes.x) > Mathf.Abs(_axes.y);
    }
    public static bool ThumbstickMostlyY
    {
        get => !ThumbStickCentered && Mathf.Abs(_axes.x) <= Mathf.Abs(_axes.y);
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
}
