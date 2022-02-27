using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public enum SwatchPickerMode
{
    Disabled,
    Inclusion,
    Exclusion,
    Paused,
    // Not a huge fan of this, but works for now:
    MaskPaused,
    MaskDrawExclusion,
    MaskDrawDeleteExclusion,
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

    private RenderTexture _screenSpaceHelperTex;
    private RenderTexture _exclusionTex;

    void RenderScreenSpaceHelper(Material renderMat)
    {
        var curVidTex = clipManager.clipPool.current.texture;
        _screenSpaceHelperTex = new RenderTexture(2048, 1024, 16);
        RenderTexture.active = _screenSpaceHelperTex;
        GL.Clear(true, true, Color.white);
        Graphics.Blit(null, _screenSpaceHelperTex, renderMat);
        skyboxMat.SetTexture("_ScreenSpaceHelperTex", _screenSpaceHelperTex);
    }

    Material exclusionMat;

    void InitExclusionMask()
    {
        if (!_exclusionTex)
            _exclusionTex = new RenderTexture(1440, 720, 0); // no depth map dammit!
        RenderTexture.active = _exclusionTex;
        GL.Clear(true, true, Color.white); // won't use color, but sig needs it
        Graphics.Blit(null, _exclusionTex, Resources.Load<Material>("DummyMaterial"));
        exclusionMat.SetTexture("_LastTex", _exclusionTex);
        RenderTexture.active = null;
    }

    void RenderExclusionMaskTick()
    {
        RenderTexture.active = _exclusionTex;
        GL.Clear(false, false, Color.white); // won't use color, but sig needs it
        exclusionMat.SetFloat("_DeleteMode", _mode == SwatchPickerMode.MaskDrawDeleteExclusion ? 1 : 0);
        Graphics.Blit(null, _exclusionTex, exclusionMat);
        exclusionMat.SetTexture("_LastTex", _exclusionTex);
        RenderTexture.active = null;
    }

    void Start()
    {
        _colorWarden = new ColorWarden(40);

        camera = GameObject.Find("CenterEyeAnchor").GetComponent<Camera>();

        colorMaskMat = Resources.Load<Material>("ColorArrayMaskBlitMaterial");
        exclusionMat = Resources.Load<Material>("ExclusionMaskBlitMaterial");

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

        Material screenSpaceHelperMat = Resources.Load<Material>("ScreenSpaceHelperMaterial");

        clipManager.clipPool.current.prepareCompleted += (_) => RenderScreenSpaceHelper(screenSpaceHelperMat);

        Disable(); // start off!
        InitExclusionMask();
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
            return new Vector3(Mathf.Clamp(mouse.x, 0, Screen.width - 1), Mathf.Clamp(mouse.y, 0, Screen.height - 4));
        }
    }

    public void Disable()
    {
        _mode = SwatchPickerMode.Disabled;
        line.enabled = false;
        RenderTexture.active = null;
        swatchContainer.SetActive(false);
        // maybe release preRenderTex here

        // these are only rendered once per activation,
        // so null them out until next time:
//        _renderedHelperCount = 0;
        _useLightMode = _useLightMode > 0 ? 3 : 0;
    }

    public void Enable(SwatchPickerMode startingMode = SwatchPickerMode.Paused)
    {
        _mode = startingMode;
        if (_mode > SwatchPickerMode.Paused) // Change color for Mask mode.
        {
            line.startColor = new Color(0, 1, 0, 0);
            line.endColor = new Color(0, 1, 0, 1);

        } else
        {
            line.startColor = new Color(1, 0, 1, 0);
            line.endColor = new Color(1, 0, 1, 1);
        }

        line.enabled = true;

        clipManager.sizingBar.SetActive(false);
        //clipManager.debugContainer.SetActive(false);

        swatchContainer.SetActive(true);
    }

    public void Toggle(SwatchPickerMode startingMode = SwatchPickerMode.Paused) // this param is deprecated!
    {
        if (_mode != startingMode)
            Enable(startingMode);
        // Only cycle from swatch mode if the startingMode param
        // is set to the default; this allows an on-off behavior when passed
        // specific modes:
        else if (startingMode == SwatchPickerMode.Paused && _mode == SwatchPickerMode.Paused)
            Enable(SwatchPickerMode.MaskPaused);
        else
        {
            if (line.enabled) Disable();
            else Enable(startingMode);
        }
    }

    public bool IsDetectorActive()
    {
        return line.enabled; // NOTE: Paused is considered "active".
    }

    public void UseInclusionMode()
    {
        if (_mode <= SwatchPickerMode.Paused)
            _mode = SwatchPickerMode.Inclusion;
        else
            _mode = SwatchPickerMode.MaskDrawDeleteExclusion;
    }

    public void UseExclusionMode()
    {
        if (_mode <= SwatchPickerMode.Paused)
            _mode = SwatchPickerMode.Exclusion;
        else
            _mode = SwatchPickerMode.MaskDrawExclusion;
    }
        

    public void Pause()
    {
        if (!line.enabled) return;
        if (_mode > SwatchPickerMode.Paused)
        {
            _mode = SwatchPickerMode.MaskPaused;
            return;
        }
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

    public void ResetCurrentMode()
    {
        if (_mode > SwatchPickerMode.Paused) // urgh, do something better here
            InitExclusionMask();
        else
            MakeFreshSwatches();
    }

    // Update is called once per frame
    void Update()
    {

    }

    RenderTexture leftEye;
    RenderTexture rightEye;
    // for ScreenSpaceHelper:
    RenderTexture leftEyeHelper;
    RenderTexture rightEyeHelper;
    RenderTexture currentEyeHelper;
    RenderTexture currentEye;

    //private int _renderedHelperCount = 0;
    private void OnPreRender()
    {
        if (IsDetectorActive())
        {
            _tempCam.CopyFrom(Camera.current);

            // TODO: I use this pattern a lot, so I should abstract it somewhere
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

                if (!leftEyeHelper)
                {
                    leftEyeHelper = new RenderTexture(desc);
                    leftEyeHelper.filterMode = FilterMode.Point;
                }
                currentEyeHelper = leftEyeHelper;

            }
            else if (Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right)
            {
                if (!rightEye)
                {
                    rightEye = new RenderTexture(desc);
                    rightEye.filterMode = FilterMode.Point;
                }
                currentEye = rightEye;

                if (!rightEyeHelper)
                {
                    rightEyeHelper = new RenderTexture(desc);
                    rightEyeHelper.filterMode = FilterMode.Point;
                }
                currentEyeHelper = rightEyeHelper;
            }
            else
            {
                if (!currentEye) currentEye = new RenderTexture(desc);
                if (!currentEyeHelper) currentEyeHelper = new RenderTexture(desc);
            }
            _tempCam.targetTexture = currentEye;

            // TODO: Work out a more general way to do these prop pushes
            skyboxMat.SetFloat("_UseSwatchPickerMode", 1);
            line.forceRenderingOff = true;
            /* Might want to do other stuf:
             * 1. Disable GUI
             * 2. Actually use the small texture as the source
             */
            _tempCam.Render();
            skyboxMat.SetFloat("_UseSwatchPickerMode", 0);

            if (_mode > SwatchPickerMode.MaskPaused)
            {
                _tempCam.targetTexture = currentEyeHelper;
                skyboxMat.SetFloat("_UseScreenSpaceHelper", 1);
                _tempCam.Render();
                skyboxMat.SetFloat("_UseScreenSpaceHelper", 0);
                //_renderedHelperCount++;
            }
            line.forceRenderingOff = false;
            //if (line.enabled) line.enabled = true;
        }
    }

    //private void OnRenderImage(RenderTexture src, RenderTexture dst)
    //{
    //    Graphics.Blit(src, dst);
    //}

    // Can probably move the following to OnRenderImage()

    int curFrame = -1;
    int frameSkip = 4;

    public Color screenSpaceIndexColorLeft = new Color(0, 0, 0);
    public Color screenSpaceIndexColorRight = new Color(0, 0, 0);

    int _useLightMode  = 0; // 0 is disabled, 1 is use-and-place, 2 is use
    public void ToggleLight() {
        if (_useLightMode == 3) // Three is like 2, but forces it back to 0-2.
            _useLightMode = 1;
        else
            _useLightMode = (_useLightMode + 1) % 3;
        skyboxMat.SetFloat("_UseLight", _useLightMode > 0 ? 1 : 0);
    }


    private void OnPostRender()
    {
        if (IsDetectorActive()) // false when SwatchPickerMode.Paused
        {
            curFrame = (curFrame + 1) % (frameSkip + 1); // wraps around

            var pointerPos = GetPointerScreenPos();

            int x = Mathf.RoundToInt(pointerPos.x);
            int y = Mathf.RoundToInt(pointerPos.y);

            //RenderTexture.active = preRenderTex;
            RenderTexture.active = currentEye;
            pixelTexture.ReadPixels(new Rect(x, y, 1, 1), 0, 0, false);
            Color colorAtPointer = pixelTexture.GetPixel(0, 0);

            RenderTexture.active = currentEyeHelper;
            pixelTexture.ReadPixels(new Rect(x, y, 1, 1), 0, 0, false);
            Color screenSpaceIndexColor = pixelTexture.GetPixel(0, 0);


            RenderTexture.active = null;

            //if (curFrame != 0) return;

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
                swatchMaterialLeft.color = colorAtPointer;
                screenSpaceIndexColorLeft = screenSpaceIndexColor.linear;

                if (_mode == SwatchPickerMode.Inclusion && curFrame == 0)
                {
                    _colorWarden.Include(swatchMaterialLeft.color);
                    colorMaskMat.SetColorArray("_LeftColorInclusionArray", _colorWarden.Inclusions);
                }
                else if (_mode == SwatchPickerMode.Exclusion && curFrame == 0) // just for clarity
                {
                    _colorWarden.Exclude(swatchMaterialLeft.color);
                    colorMaskMat.SetColorArray("_LeftColorExclusionArray", _colorWarden.Exclusions);
                }

                if (_mode > SwatchPickerMode.MaskPaused)
                {
                    exclusionMat.SetVector("_LaserCoord", new Vector2(screenSpaceIndexColorLeft.r, screenSpaceIndexColorLeft.g));
                    RenderExclusionMaskTick();
                    // Hmm, I should only have to do this once per two-eye cycle, especially
                    // since there's no frameskip.
                    //skyboxMat.SetTexture("_TestTex", _exclusionTex);
                }

                // Only do this once per two-eye render cycle; left eye is fine:
                if (clipManager.clipPool.current.isPaused)
                    clipManager.RenderColorMaskTick(clipManager.lastSource);

                // Note: this is bunk. See DrawMaskBlit for how to do it right.
                // This is also one-eye only, though it's anybody's guess why it even
                // works.
                var lightPosition = new Vector2(
                    (screenSpaceIndexColorLeft.r - 0.25f) * 60, // for SBS
                    (screenSpaceIndexColorLeft.g - 0.50f) * 30
                );

                // Wait a minute... how the hell is this working in the second eye?
                if (_useLightMode == 1)
                    skyboxMat.SetVector("_LaserCoord", lightPosition);

            } else
            {
                swatchMaterialRight.color = colorAtPointer;
                screenSpaceIndexColorRight = screenSpaceIndexColor.linear;
                if (_mode == SwatchPickerMode.Inclusion && curFrame == 0)
                {
                    _colorWarden.Include(swatchMaterialRight.color);
                    colorMaskMat.SetColorArray("_LeftColorInclusionArray", _colorWarden.Inclusions);
                } else if (_mode == SwatchPickerMode.Exclusion && curFrame == 0) // for clarity
                {
                    _colorWarden.Exclude(swatchMaterialRight.color);
                    colorMaskMat.SetColorArray("_LeftColorExclusionArray", _colorWarden.Exclusions);
                }
            }

            if (_mode > SwatchPickerMode.MaskPaused)
            {
                exclusionMat.SetVector("_LaserCoord", new Vector2(screenSpaceIndexColorRight.r, screenSpaceIndexColorRight.g));
                RenderExclusionMaskTick();
                skyboxMat.SetTexture("_TestTex", _exclusionTex);
            }

            if (camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Mono)
            {
                // Just render screenSpaceIndex on the right, since we can't do anything else:
                swatchMaterialRight.color = screenSpaceIndexColor;
                //Debug.Log("PAYDIRT: " + screenSpaceIndexColorLeft);

                //Debug.Log("FUCK FUCK FUCK: " + fuck.y);
                
            }
        }
    }
}
