using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

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

        float currentHue = samples[hueIndex].r;
        float clampedFreq = System.Math.Min(samplesFreq[hueIndex], _maxFreqWeight);

        float averageHue = (currentHue * clampedFreq + hsvColor.r) / (clampedFreq + 1);
        hsvColor.r = averageHue;

        samples[hueIndex] = hsvColor;

        // This is to help with hair and other dark colors, for inclusions only:
        if (samples == _inclusions)
        {
            if (hsvColor.b < 0.25) addedFreq *= 2;
            _exclusionsFreq[hueIndex] = 0;
        }
        else
        {
            _inclusionsFreq[hueIndex] = 0;
        }

        samplesFreq[hueIndex] += addedFreq;

        Debug.Log("Hover Color: " + hsvColor);
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
        Debug.Log(_sortedFreqs[_sampleLength -_segmentLength] + " " + _sortedFreqs[_sampleLength - _segmentLength/2]  +  " " + _sortedFreqs[_sampleLength-1]);
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

public enum SwatchPickerMode
{
    Disabled,
    Inclusion,
    Exclusion,
    Paused,
}

public class SwatchPicker : MonoBehaviour
{
    Camera _tempCam;
    Camera camera;

    Material skyboxMat; // argh, get this somewhere centrally

    LineRenderer line;
    ClipManager clipManager;
    RenderTexture preRenderTex; // null when not enabled
    Texture2D pixelTexture;
    Material swatchMaterialLeft; // move elsewhere
    Material swatchMaterialRight; // move elsewhere
    GameObject swatchContainer;
    UnityEngine.UI.Text debugText;
    // Start is called before the first frame update

    SwatchPickerMode _mode = SwatchPickerMode.Disabled;

    Color[] leftInclusionColors;
    Color[] leftExclusionColors;

    ColorWarden _colorWarden;

    void Start()
    {
        _colorWarden = new ColorWarden(40);

        camera = GameObject.Find("CenterEyeAnchor").GetComponent<Camera>();
        colorMaskMat = Resources.Load<Material>("ColorArrayMaskBlitMaterial");

        clipManager = GameObject.Find("Core").GetComponent<ClipManager>();

        skyboxMat = RenderSettings.skybox;

        MakeFreshSwatches();


        _tempCam = new GameObject().AddComponent<Camera>();
        _tempCam.enabled = false;


        line = GameObject.Find("LaserPointer").GetComponent<LineRenderer>();
        swatchContainer = GameObject.Find("ColorSwatches");
        pixelTexture = new Texture2D(1, 1, TextureFormat.RGB24, false);

        //preRenderTex = new RenderTexture(Screen.width, Screen.height, 24);
        //preRenderTex.Create();

        swatchMaterialLeft = GameObject.Find("ColorSwatchLeft").GetComponent<Renderer>().material;
        swatchMaterialRight = GameObject.Find("ColorSwatchRight").GetComponent<Renderer>().material;

        debugText = GameObject.Find("DebugText").GetComponent<UnityEngine.UI.Text>();

        Disable(); // start off!
    }


    Vector3 GetPointerScreenPos()
    {
        if (OVRManager.isHmdPresent == true)
        {
            var endPos = line.GetPosition(line.positionCount - 1);
            return camera.WorldToScreenPoint(endPos, camera.stereoActiveEye);
        } else
        {
            // Get mouse position
            var mouse = Input.mousePosition;
            return new Vector3(Mathf.Clamp(mouse.x, 0, Screen.width-1), Mathf.Clamp(mouse.y, 0, Screen.height-4));
        }
    }

    public void Disable()
    {
        _mode = SwatchPickerMode.Disabled;
        line.enabled = false;
        RenderTexture.active = null;
        swatchContainer.SetActive(false);
        // maybe release preRenderTex here
    }

    public void Enable(SwatchPickerMode startingMode = SwatchPickerMode.Paused)
    {
        _mode = startingMode;
        //_isIncludeMode = shouldUseInclusion;
        line.enabled = true;
        clipManager.sizingBar.SetActive(false);
        clipManager.debugContainer.SetActive(false);
        swatchContainer.SetActive(true);
        //camera.targetTexture = preRenderTex;
        //camera.Render();

        //camera.targetTexture = null;
    }

    public void Toggle(SwatchPickerMode startingMode = SwatchPickerMode.Paused) // this param is deprecated!
    {
        if (_mode != startingMode)
        {
            Enable(startingMode);            
        } else
        {
            if (line.enabled) Disable();
            else Enable(startingMode);
        }
    }

    bool IsDetectorActive()
    {
        return line.enabled; // NOTE: Paused is considered "active".
    }

    public void UseInclusionMode() =>
        _mode = SwatchPickerMode.Inclusion;
    public void UseExclusionMode() =>
        _mode = SwatchPickerMode.Exclusion;

    public void Pause()
    {
        if (!line.enabled) return;
        _mode = SwatchPickerMode.Paused;
    }

    Material colorMaskMat;

    public void MakeFreshSwatches()
    {
        _colorWarden.Reset();
        colorMaskMat.SetFloat("_ColorArrayLength", _colorWarden.Inclusions.Length);
        colorMaskMat.SetColorArray("_LeftColorInclusionArray", _colorWarden.Inclusions);
        colorMaskMat.SetColorArray("_LeftColorExclusionArray", _colorWarden.Exclusions);
        //if (clipManager.clipPool.current.isPaused)
          //  clipManager.RenderColorMaskTick(clipManager.lastSource);   
    }


    // Update is called once per frame
    void Update()
    {

    }

    RenderTexture leftEye;
    RenderTexture rightEye;
    RenderTexture currentEye;

    private void OnPreRender()
    {
        if (IsDetectorActive())
        {
            _tempCam.CopyFrom(Camera.current);

            RenderTextureDescriptor desc;
            if (XRSettings.enabled)
                desc = XRSettings.eyeTextureDesc;
            else
                desc = new RenderTextureDescriptor(Screen.width, Screen.height);

            if (Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
            {
                if (!leftEye)
                {
                    leftEye = new RenderTexture(desc);
                    leftEye.filterMode = FilterMode.Point;
                }
                currentEye = leftEye;
            }
            else if (Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right)
            {
                if (!rightEye)
                {
                    rightEye = new RenderTexture(desc);
                    rightEye.filterMode = FilterMode.Point;
                }
                currentEye = rightEye;
            }
            else
            {
                if (!currentEye) currentEye = new RenderTexture(desc);
            }

            _tempCam.targetTexture = currentEye;

            // TODO: Work out a more general way to do these prop pushes
            skyboxMat.SetFloat("_UseSwatchPickerMode", 1);
            /* Might want to do other stuf:
             * 1. Disable GUI
             * 2. Actually use the small texture as the source
             */
            _tempCam.Render();
            skyboxMat.SetFloat("_UseSwatchPickerMode", 0);
        }
    }

    //private void OnRenderImage(RenderTexture src, RenderTexture dst)
    //{
    //    Graphics.Blit(src, dst);
    //}

    // Can probably move the following to OnRenderImage()

    int curFrame = -1;
    int frameSkip = 4;

    private void OnPostRender()
    {
        if (IsDetectorActive()) // false when SwatchPickerMode.Paused
        {
            curFrame = (curFrame + 1) % (frameSkip + 1); // wraps around

            var pointerPos = GetPointerScreenPos();

            int x = Mathf.RoundToInt(pointerPos.x);
            int y = Mathf.RoundToInt(pointerPos.y + 2);

            //RenderTexture.active = preRenderTex;
            RenderTexture.active = currentEye;
            pixelTexture.ReadPixels(new Rect(x, y, 1, 1), 0, 0, false);
            RenderTexture.active = null;

            if (curFrame != 0) return;

            // Ugh... too much clipManager stuff here. Put its business and ours
            // in the same place.
            //if (clipManager.clipPool.current.isPaused &&
            //    (!clipManager.lastSourceLeft || !clipManager.lastSourceRight)) {
            //    StartCoroutine(WaitForLastSources());
            //}


            // Not using the right eye; if we begin to collect them both, change the following:
            if (camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left ||
                camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Mono)
            {
                swatchMaterialLeft.color = pixelTexture.GetPixel(0, 0);
                if (_mode == SwatchPickerMode.Inclusion)
                {
                    _colorWarden.Include(swatchMaterialLeft.color);
                    colorMaskMat.SetColorArray("_LeftColorInclusionArray", _colorWarden.Inclusions);
                }
                else if (_mode == SwatchPickerMode.Exclusion) // just for clarity
                {
                    _colorWarden.Exclude(swatchMaterialLeft.color);
                    // On purpose for now:
                    colorMaskMat.SetColorArray("_LeftColorExclusionArray", _colorWarden.Exclusions);
                }

                // Only do this once per two-eye render cycle; left eye is fine:
                if (clipManager.clipPool.current.isPaused)
                    clipManager.RenderColorMaskTick(clipManager.lastSource);
            } else 
            {
                swatchMaterialRight.color = pixelTexture.GetPixel(0, 0);
                if (_mode == SwatchPickerMode.Inclusion)
                {
                    _colorWarden.Include(swatchMaterialRight.color);
                    colorMaskMat.SetColorArray("_LeftColorInclusionArray", _colorWarden.Inclusions);
                } else if (_mode == SwatchPickerMode.Exclusion) // for clarity
                {
                    _colorWarden.Exclude(swatchMaterialRight.color);
                    colorMaskMat.SetColorArray("_LeftColorExclusionArray", _colorWarden.Exclusions);
                }

            }
        }
    }
}
