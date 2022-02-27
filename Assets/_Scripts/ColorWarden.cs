using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lower-case to mimic the shader style, which is mostly why this class exists.
public class ColorSmith
{
    // Uh, unity has Color:linear, so not actually needed, but leaving for now.
    public static Color rgb2lrgb(Color c)
    {
        Color lrgb = new Color();
        lrgb.r = (c.r > 0.04045f) ? Mathf.Pow((c.r + 0.055f) / 1.055f, 2.4f) : c.r / 12.92f;
        lrgb.g = (c.g > 0.04045f) ? Mathf.Pow((c.g + 0.055f) / 1.055f, 2.4f) : c.g / 12.92f;
        lrgb.b = (c.b > 0.04045f) ? Mathf.Pow((c.b + 0.055f) / 1.055f, 2.4f) : c.b / 12.92f;
        return lrgb;
    }
    public static Color rgb2hsv(Color c)
    {
        float H, S, V;
        Color.RGBToHSV(c, out H, out S, out V);
        return new Color(H, S, V);
    }
}

public class ColorWarden
{
    int _segmentLength;
    int _maxFreqWeight; // The maximum weight to give extant values

    int _sampleLength;
    float _span;

    Color[] _inclusions;
    int[] _inclusionsFreq;
    Color[] _exclusions;
    int[] _exclusionsFreq;

    Color[] _sortedColors;
    float[] _sortedFreqs;

    Color[] _outputColors;

    Color ToHSV(Color c)
    {
        // Unity normally seems to handle lrgb conversion on the way
        // to the GPU; since we'll be sending them in HSV, we need to
        // perform the conversion here.


        return ColorSmith.rgb2hsv(c.linear);
    }

    int HueToIndex(Color hsvColor)
    {
        return Mathf.FloorToInt(hsvColor.r / _span);
        //return _inclusionsCursor;
    }

    void _Insert(Color color, Color[] samples, int[] samplesFreq, int addedFreq = 1)
    {
        Color hsvColor = ToHSV(color);
        int hueIndex = HueToIndex(hsvColor);

        float clampedFreq = System.Math.Min(samplesFreq[hueIndex], _maxFreqWeight);

        float averageHue = (samples[hueIndex].r * clampedFreq + hsvColor.r) / (clampedFreq + 1);
        float averageSat = (samples[hueIndex].g * clampedFreq + hsvColor.g) / (clampedFreq + 1);
        float averageVal = (samples[hueIndex].b * clampedFreq + hsvColor.b) / (clampedFreq + 1);
        hsvColor.r = averageHue;
        hsvColor.g = averageSat;
        hsvColor.b = averageVal;

        samples[hueIndex] = hsvColor;

        // This is to help with hair and other dark colors, for inclusions only:
        if (samples == _inclusions)
        {
            if (hsvColor.b < 0.2) addedFreq *= 2;
            _exclusionsFreq[hueIndex] = Mathf.Max(_exclusionsFreq[hueIndex] - 1, 0);
        }
        else
        {
            _inclusionsFreq[hueIndex] = Mathf.Max(_inclusionsFreq[hueIndex] - 1, 0);
        }

        samplesFreq[hueIndex] += addedFreq;

        //Debug.Log("Hover Color: " + hsvColor);
    }

    public void Include(Color color) =>
        _Insert(color, _inclusions, _inclusionsFreq);

    public void Exclude(Color color) =>
        _Insert(color, _exclusions, _exclusionsFreq);

    public void Reset()
    {
        Debug.Log("Trying to make fresh swatches, I guess.");
        for (int i = 0; i < _sampleLength; i++)
        {
            _inclusions[i] = new Color(-1, -1, -1); // purposely initialize as invalid.
            _inclusionsFreq[i] = 0;
            _exclusions[i] = new Color(-1, -1, -1);
            _exclusionsFreq[i] = 0;
        }
    }

    public Color[] Inclusions
    {
        get => GetOutputSamples(_inclusions, _inclusionsFreq);
    }
    public Color[] Exclusions
    {
        get => GetOutputSamples(_exclusions, _exclusionsFreq);
    }

    public Color[] GetOutputSamples(Color[] samples, int[] samplesFreq)
    {
        samplesFreq.CopyTo(_sortedFreqs, 0);
        samples.CopyTo(_sortedColors, 0);
        System.Array.Sort(_sortedFreqs, _sortedColors, 0, _sampleLength);
        System.Array.Copy(
            _sortedColors,
            _sampleLength - _segmentLength,
            _outputColors,
            0,
            _segmentLength
        );
        //Debug.Log(_outputColors[0] + " " + _outputColors[39]);
        //Debug.Log(_sortedFreqs[_sampleLength - _segmentLength] + " " + _sortedFreqs[_sampleLength - _segmentLength / 2] + " " + _sortedFreqs[_sampleLength - 1]);
        return _outputColors;
    }

    public ColorWarden(int segmentLength, int sampleLength = 512, int maxFreqWeight = 10)
    {
        _segmentLength = segmentLength;
        _sampleLength = sampleLength;
        _maxFreqWeight = maxFreqWeight;

        // To calculate index from color:
        _span = 1 / ((float)sampleLength - 1);

        _inclusions = new Color[sampleLength];
        _inclusionsFreq = new int[sampleLength];

        _exclusions = new Color[sampleLength];
        _exclusionsFreq = new int[sampleLength];

        _sortedColors = new Color[sampleLength];
        _sortedFreqs = new float[sampleLength];

        _outputColors = new Color[segmentLength];

        Reset();
    }
}
