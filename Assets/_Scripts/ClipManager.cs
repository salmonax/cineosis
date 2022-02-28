using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;


/* This class has too many responsibilities!
 *
 * Start separating them out! For starters:
 *  1. Controller input
 *  2. Shader manipulation/update logic
 *  3. Settings saving and loading [DONE, but needs more taken out]
 *
 *  4. Clip loading, switching, and playing
 *
 * Don't worry, it'll be easier than it looks.
 *
 */
public partial class ClipManager : MonoBehaviour
{
    private Camera camera;

    public ClipPool clipPool;
    private RenderTexture skyboxTex;
    public UnityEngine.UI.Text debugText;


    // INPUTCONTROLLER These are public because coupled to InputController
    // INPUTCONTROLLER - these belong everywhere
    public Material skyboxMat;
    public LaserPointer laser;
    public Outliner outliner;
    public GameObject sizingBar;
    public GameObject debugContainer;
    public SwatchPicker swatchDetector;
    public float combinedZoomFactor = -3.6f; // decent default

    // INPUTCONTROLLER - these belong here
    public ClipConfig[] clipConfigs;
    private long lastFrameIdx = 0;
    private bool shouldRender = true;
    public RenderTexture[] dsts = new RenderTexture[3];
    public RenderTexture combinedDynAlpha;
    public RenderTexture dynBlurredAlpha;
    public RenderTexture combinedAlpha;
    public RenderTexture blurredAlpha;


    private Material blitMat; // This definitely needs a more descriptive name

    private Material colorArrayBlitMat; // for a smaller _LastTex to generate an alpha
    public RenderTexture colorMaskAlpha;
    public Material colorMaskBlurBlit;
    public RenderTexture blurredColorMaskAlpha;
    public RenderTexture smallFrameTex;

    //private Material dynBlitMat;
    private Material dynBlurMat;
    private Material blurMat;

    private GameObject pointer;
    bool finishedInitialCapture = false;

    private InputController _inputController;

    private Material handMat;

    public bool useAutoShiftMode = false;

    private float _isDifferenceMaskEnabled; // overrides _UseDifferenceMask in shader

    private void Awake()
    {
        ClipProvider.CheckAndRequestPermissions();
        clipPool = new ClipPool(CoreConfig.clips);
    }

    void Start()
    {
        _inputController = new InputController(this);

        blitMat = Resources.Load<Material>("StaticMaskAlphaBlitMaterial");
        blurMat = Resources.Load<Material>("NaiveGaussianMaterial");
        dynBlurMat = Resources.Load<Material>("BoxKawaseBlitMaterial"); // currently, this does Gaussian despite name
        colorArrayBlitMat = Resources.Load<Material>("ColorArrayMaskBlitMaterial");
        colorMaskBlurBlit = Resources.Load<Material>("ColorMaskGaussianMaterialTwoPass");

        handMat = Resources.Load<Material>("BasicHandMaterialHSBE");

        skyboxTex = Resources.Load<RenderTexture>("SkyboxTexture");
        skyboxMat = RenderSettings.skybox;

        var kernel = GaussianKernel.Calculate(3, 12);
        colorMaskBlurBlit.SetFloatArray("_kernel", kernel);
        colorMaskBlurBlit.SetInt("_kernelWidth", kernel.Length);

        var diffKernel = GaussianKernel.Calculate(2, 6);
        //blurMat.SetFloatArray("_kernel", diffKernel);
        //blurMat.SetInt("_kernelWidth", diffKernel.Length);
        dynBlurMat.SetFloatArray("_kernel", diffKernel);
        dynBlurMat.SetInt("_kernelWidth", diffKernel.Length);


        pointer = GameObject.Find("LaserPointer");
        laser = pointer.GetComponent<LaserPointer>();


        // Reset _SwatchPickerMode, just in case it's changed in the material
        //skyboxMat.SetFloat("_UseSwatchPickerMode", 0);

        var centerEye = GameObject.Find("CenterEyeAnchor");
        camera = centerEye.GetComponent<Camera>();
        swatchDetector = centerEye.GetComponent<SwatchPicker>();
        outliner = centerEye.GetComponent<Outliner>();

        sizingBar = GameObject.Find("SizingBar");
        debugContainer = GameObject.Find("DebugText");

        debugText = (UnityEngine.UI.Text)debugContainer.GetComponent("Text");

        skyboxMat.SetInt("_VideoIndex", 0);


        InitClipConfigs();
        PullAndSetMaskState();
        DisableDebugGUI();

        // Hmm, the clipPool should be doing this:
        var clipOne = clipPool.clips[0];
        clipOne.targetTexture = skyboxTex;
        clipOne.Play();
    }

    void InitClipConfigs()
    {
        // -- START ClipConfig Loading
        ClipConfig[] existingClipConfigs = ClipConfig.Load();
        clipConfigs = new ClipConfig[clipPool.clips.Length];
        ClipConfig templateConfig = new ClipConfig();

        for (int i = 0; i < clipPool.clips.Length; i++)
        {
            if (i > existingClipConfigs.Length - 1 ||
                templateConfig.version != existingClipConfigs[i].version)
            {
                // Insert default if none exists OR if the version doesn't match
                clipConfigs[i] = new ClipConfig();
                Debug.Log(i + ". MADE NEW CONFIG");
            }
            else
            {
                // Otherwise, set the clipConfig to the one that was loaded:
                clipConfigs[i] = existingClipConfigs[i];
                Debug.Log(i + ". USED EXISTING CONFIG!");
            }
        }
        // Go ahead and save the newly merged configs
        ClipConfig.Save(clipConfigs);
        clipConfigs[0].ApplyToMaterial(skyboxMat);
        // -- END ClipConfig Loading
    }

    //Save save = new Save();
    //Save[] saves = new Save[] { new Save(), new Save() };

    // Call with false when updating from settings:
    void EnableMask(bool pushSetting = true)
    {
        if (pushSetting)
        {
            _isDifferenceMaskEnabled = (_isDifferenceMaskEnabled + 1) % 3;
            skyboxMat.SetFloat("_UseDifferenceMask", _isDifferenceMaskEnabled);
            clipConfigs[clipPool.index].SetFloatIfPresent("_UseDifferenceMask", _isDifferenceMaskEnabled);
        }
        var clip = getCurrentPlayer();
        clip.sendFrameReadyEvents = true;
        clip.frameReady += OnNewFrame;
        clip.loopPointReached += EndReached;
    }

    void DisableMask(bool pushSetting = true)
    {
        if (pushSetting)
        {
            _isDifferenceMaskEnabled = 0;
            skyboxMat.SetFloat("_UseDifferenceMask", 0);
            clipConfigs[clipPool.index].SetFloatIfPresent("_UseDifferenceMask", 0);
        }
        var clip = getCurrentPlayer();
        clip.sendFrameReadyEvents = false;
        clip.frameReady -= OnNewFrame;
        clip.loopPointReached -= EndReached;
    }

    void PullAndSetMaskState()
    {
        _isDifferenceMaskEnabled = skyboxMat.GetFloat("_UseDifferenceMask");
        if (_isDifferenceMaskEnabled > 0)
            EnableMask(false);
        else
            DisableMask(false);
    }


    // Video stuff MIGHT look like this:
    FrameSkipper _colorMaskSkipper = new FrameSkipper(1);
    public VideoPlayer lastSource;
    public void RenderColorMaskTick(VideoPlayer source)
    {
        if (source != lastSource) {
          lastSource = source;
        }
        if (_colorMaskSkipper.Skip()) return;
        if (!colorMaskAlpha && !smallFrameTex)
        {
            var scale = 0.25f;
            colorMaskAlpha = new RenderTexture((int)(source.width * scale), (int)(source.height * scale), 0);
            colorMaskAlpha.filterMode = FilterMode.Point;
            smallFrameTex = new RenderTexture((int)(source.width * scale), (int)(source.height * scale), 0);
            smallFrameTex.filterMode = FilterMode.Point;
        }
        RenderTexture.active = smallFrameTex;
        GL.Clear(true, true, Color.black);
        Graphics.Blit(source.texture, smallFrameTex);


        // @@@ START COLOR SELECTION
        RenderTexture.active = colorMaskAlpha;
        GL.Clear(true, true, Color.black);
        Graphics.Blit(source.texture, colorMaskAlpha, colorArrayBlitMat);

        //skyboxMat.SetTexture("_ColorMaskAlphaTex", colorMaskAlpha);
        skyboxMat.SetTexture("_SmallFrameTex", smallFrameTex);

        if (!blurredColorMaskAlpha)
        {
            blurredColorMaskAlpha = new RenderTexture((int)source.width / 4, (int)source.height / 4, 16);
        }

        RenderTexture.active = blurredColorMaskAlpha;
        GL.Clear(true, true, Color.black);
        Graphics.Blit(colorMaskAlpha, blurredColorMaskAlpha, colorMaskBlurBlit); // null because set to blitMat above
        skyboxMat.SetTexture("_ColorMaskAlphaTex", blurredColorMaskAlpha);

        // @@@@ END NEW COLOR SELECTION STUFF
        //return;
    }

    int[] frameSkips = new int[] { 1, 3, 5 };
    int frameSkipCursor = 0;
    int frameSkip
    {
        get {
            var current = frameSkips[frameSkipCursor];
            frameSkipCursor = (frameSkipCursor + 1) % frameSkips.Length;
            return current;
        }
    }
    void OnNewFrame(VideoPlayer source, long frameIdx)
    {
        if (_isDifferenceMaskEnabled == 0) return; // just in case the listener lingers; handle better
        if (_isDifferenceMaskEnabled == 2)
            RenderColorMaskTick(source); // not differentiating eyes, and brittle when called from swatchPicker!

        if (lastFrameIdx == 0 || frameIdx - lastFrameIdx > frameSkip) // was 18 for initial capture
        {
            if (lastFrameIdx == 0 || !shouldRender)
            {
                if (dsts[0]) // shunt forward; newest always at 0.
                {
                    var tmp = dsts[2];
                    dsts[2] = dsts[1];
                    dsts[1] = dsts[0];
                    dsts[0] = tmp;
                }
                // this will be true until all three textures exist,
                // ensuring that only three RenderTextures are created:
                if (!dsts[0])
                {
                    dsts[0] = new RenderTexture((int)source.width, (int)source.height, 16);
                    dsts[0].enableRandomWrite = true;
                    dsts[0].Create();
                }

                RenderTexture.active = dsts[0];
                GL.Clear(true, true, Color.black);
                Graphics.Blit(source.texture, dsts[0]);
            }

            if (lastFrameIdx == 0 || shouldRender)
            {
                // Note: this will fill each one out as they come in.
                if (_isDifferenceMaskEnabled == 1)
                {
                    skyboxMat.SetTexture("_LastTex", dsts[0]);
                    skyboxMat.SetTexture("_LastTex2", dsts[1]);
                    skyboxMat.SetTexture("_LastTex3", dsts[2]);
                }

                blitMat.SetTexture("_LastTex", dsts[0]);
                blitMat.SetTexture("_LastTex2", dsts[1]);
                blitMat.SetTexture("_LastTex3", dsts[2]);

                if (!combinedDynAlpha)
                {
                    combinedDynAlpha = new RenderTexture((int)source.width / 4, (int)source.height / 4, 16);
                    dynBlurredAlpha = new RenderTexture((int)source.width / 10, (int)source.height / 10, 16);
                }


                RenderTexture.active = combinedDynAlpha;
                GL.Clear(true, true, Color.black);
                Graphics.Blit(null, combinedDynAlpha, blitMat); // null because set to blitMat above


                RenderTexture.active = dynBlurredAlpha;
                GL.Clear(true, true, Color.black);
                Graphics.Blit(combinedDynAlpha, dynBlurredAlpha, dynBlurMat);

                if (_isDifferenceMaskEnabled == 1)
                    skyboxMat.SetTexture("_DynAlphaTex", dynBlurredAlpha);
                else if (_isDifferenceMaskEnabled == 2)
                    colorArrayBlitMat.SetTexture("_DynAlphaTex", dynBlurredAlpha);

                //skyboxMat.SetTexture("_DynAlphaTex", combinedDynAlpha);


                // Only run the following once, regardless of whether there's an early return above.
                if (dsts[2] && !finishedInitialCapture)
                {
                    finishedInitialCapture = true;

                    /* This section is for a static difference mask.
                     * The shader is currently ignoring it.
                     */

                    //blitMat.SetTexture("_LastTex", dsts[0]);
                    //blitMat.SetTexture("_LastTex2", dsts[1]);
                    //blitMat.SetTexture("_LastTex3", dsts[2]);

                    //// Can scale from here:
                    //if (!combinedAlpha)
                    //{
                    //    combinedAlpha = new RenderTexture((int)source.width / 10, (int)source.height / 10, 16);
                    //    blurredAlpha = new RenderTexture((int)source.width / 10, (int)source.height / 10, 16);
                    //}

                    //RenderTexture.active = combinedAlpha;
                    //GL.Clear(true, true, Color.black);
                    //Graphics.Blit(null, combinedAlpha, blitMat);


                    ////blurMat.SetTexture("_MainTex", combinedAlpha);-
                    //RenderTexture.active = blurredAlpha;
                    //GL.Clear(true, true, Color.black);
                    //Graphics.Blit(combinedAlpha, blurredAlpha, blurMat);

                    //skyboxMat.SetTexture("_AlphaTex", blurredAlpha);

                }
            }
            shouldRender = !shouldRender;
            lastFrameIdx = frameIdx;
        }
    }

    // Start is called before the first frame update
    string guiLabel(string label, int sigs)
    {
        return System.Math.Round(skyboxMat.GetFloat(label), sigs).ToString("0.00");
    }

    private Vector3 OneEightify(Vector3 eulerAngles)
    {
        System.Func<float, float> offset =
            (float n) => n - (n > 180 ? 360 : 0);
        return new Vector3(
            offset(eulerAngles.x),
            offset(eulerAngles.y),
            offset(eulerAngles.z)
        );
    }

    void Update()
    {
        // Sync hand exposure and saturation to skybox
        handMat.SetFloat("_Exposure", Mathf.Pow(skyboxMat.GetFloat("_Exposure"), 2));
        handMat.SetFloat("_Saturation", skyboxMat.GetFloat("_Saturation"));
        if (OVRManager.isHmdPresent == true)
        {
            //OVRPose head = OVRManager.tracker.GetPose();
            Vector3 head = GameObject.Find("CenterEyeAnchor").transform.position;
            Vector3 headRot = GameObject.Find("CenterEyeAnchor").transform.localEulerAngles;
            Vector3 headRot180 = OneEightify(headRot);

            float effectivePitch = headRot180.x + skyboxMat.GetFloat("_RotationX"); // not right, but works here
            var ePitchRad = effectivePitch * Mathf.PI / 180;

            var vRot = skyboxMat.GetFloat("_RotationX");
            var vRotRad = vRot * Mathf.PI / 180;
            
            var zc = Mathf.Sin(vRotRad) * head.y + Mathf.Cos(vRotRad) * head.z;
            var yc = Mathf.Cos(vRotRad) * head.y - Mathf.Sin(vRotRad) * head.z;

            //var zc = Mathf.Sin(ePitchRad) * head.y + Mathf.Cos(ePitchRad) * head.z;
            //var yc = Mathf.Cos(ePitchRad) * head.y - Mathf.Sin(ePitchRad) * head.z;

            // "Nudge" refers to head-tracking nudge.
            skyboxMat.SetFloat("_NudgeX", head.x * skyboxMat.GetFloat("_NudgeFactorX"));//skyboxMat.GetFloat("_ZoomNudgeFactor")); // 0.25 originally; left-right compensation
            skyboxMat.SetFloat("_NudgeY", -yc * skyboxMat.GetFloat("_NudgeFactorY"));//skyboxMat.GetFloat("_ZoomAdjustNudgeFactor")); // 0.25 or 0.5; up-down compensation

            // These should really be called ZoomeNudge and ZoomeAdjustNudge, and BaseZoom should just be "Zoom"
            var zoom = -zc * skyboxMat.GetFloat("_ZoomNudgeFactor") + skyboxMat.GetFloat("_BaseZoom");
            skyboxMat.SetFloat("_Zoom", zoom); // mul 4 originally, by itself

            // Note: there's no BaseZoomAdjustNudge, as it distorts the image.
            skyboxMat.SetFloat("_ZoomAdjust", Mathf.Max(zc * skyboxMat.GetFloat("_ZoomAdjustNudgeFactor"), 0)); // 0.5f originally


            // JUST A DEMO! This kind of sucks and might be doing a bit too much:
            // Also, because it's from dead-reckoning, it depends on the fake zc/yc above :~(

            // First dead-reckoning, based on absolute scene rotation, less precision about zooming to exactly -1.0. 
            //float autoShiftPartialFactor = -0.00623f * effectivePitch + 0.0745f; // 0.135 + autoShiftPartialFactor * 0.5
            // Second dead-reckoning, after "fixing" zc/zy, based on *effective rotation*:
            float autoShiftPartialFactor = (-0.006895f * effectivePitch + 0.09848f);

            // Third dead-reckoning, with low-end terms calibrated to less-exaggerated width:
            // (Note: pretty bogus with the new term; lots of bottom-stretch distortion)
            //float autoShiftPartialFactor = (-0.0066144f * effectivePitch + 0.11484f);

            if (useAutoShiftMode)
            {
                skyboxMat.SetFloat("_AutoShiftRotationXNudgeFactor", -93.375f * zoom + 3.719f);
                // Note: ZoomShiftY get "left" when leaving AutoShiftMode. It doubles as a calibrator, and it seems
                //  to work pretty well in that capacity.
                skyboxMat.SetFloat("_ZoomShiftY", autoShiftPartialFactor); // named like this because it's an unnecessary mangle
            } else
            {
                skyboxMat.SetFloat("_AutoShiftRotationXNudgeFactor", 0);
                skyboxMat.SetFloat("_RotationZoomShiftX", 0); // named like this because it's an unnecessary mangle
            }

            // Ugh, to deal with the flip. What a mess.
            if (clipPool.index != 1)
            {
                // NudgeZ should really be HorizontalOffsetNudge
                skyboxMat.SetFloat("_NudgeZ", -zc * skyboxMat.GetFloat("_HorizontalOffsetNudgeFactor")); // 0.5f originally, by itself
            }
            else
            {
                skyboxMat.SetFloat("_NudgeZ", zc * skyboxMat.GetFloat("_HorizontalOffsetNudgeFactor")); // 0.5f originally, by itself
            }

            //sizingBar.transform.
            //sizingBar.transform.position = new Vector3(sizingBar.transform.position.x, sizingBar.transform.position.y, 0.5212f + head.z);
            string extraLabelOld = (_inputController.extraMode != 0) ? " EXTRA-" + ((int)_inputController.extraMode).ToString() : "";
            string extraLabel = System.Enum.GetName(typeof(TriggerMode), _inputController.triggerMode) + " -> " + System.Enum.GetName(typeof(ExtraMode), _inputController.extraMode);

            LineRenderer line = pointer.GetComponent<LineRenderer>();
            var endPos = line.GetPosition(line.positionCount - 1);


            debugText.text =
                //System.Math.Round(hand.x, 3) + " " +
                //System.Math.Round(hand.y, 3) + " " +
                //System.Math.Round(hand.z, 3) + " | " +
                //ClipProvider.GetExternalFilesDir() + " | " +
                swatchDetector.screenSpaceIndexColorLeft + "|\n|" +
                System.Math.Round(laser.maxLength, 2).ToString("0.00") + "|" +
                System.Math.Round(zc, 2).ToString("0.00") + "," +
                System.Math.Round(yc, 2).ToString("0.00") + "|" +
                System.Math.Round(headRot180.x, 2).ToString("0.00") + " , " +
                System.Math.Round(headRot180.y, 2).ToString("0.00") + " , " +
                System.Math.Round(headRot180.z, 2).ToString("0.00") + " | " +
                System.Math.Round(effectivePitch, 2).ToString("0.00") + " |\n| " +
                //zoom * (0.135 + autoShiftPartialFactor * 0.5) + " |\n| " +
                guiLabel("_AutoShiftRotationXNudgeFactor", 2) + " |\n|" +
                guiLabel("_ZoomShiftX", 2) + ", " +
                guiLabel("_ZoomShiftY", 2) + " |\n| " +
                guiLabel("_RotationShiftX", 2) + " |\n| " +
                //System.Math.Round(lineEndScreen.x, 3) + " " +
                //System.Math.Round(lineEndScreen.y, 3) + " " +
                //System.Math.Round(lineEndScreen.z, 3) + " | " +
                guiLabel("_ZoomNudgeFactor", 2) + ", " + // Zoom
                guiLabel("_ZoomAdjustNudgeFactor", 2) + ", " + // ZoomAdjust
                guiLabel("_HorizontalOffsetNudgeFactor", 2) + " |\n| " + // HorizontalOffset Comp
                guiLabel("_HorizontalOffset", 3) + " " +
                guiLabel("_Zoom", 3) + " |\n| " +
                System.Math.Round(combinedZoomFactor, 3).ToString() + " |\n| " +
                //System.Math.Round(zc, 3).ToString() + " | " +
                guiLabel("_NudgeFactorX", 2) + ", " +
                guiLabel("_NudgeFactorY", 2) + " |\n|" +
                guiLabel("_RotationY", 2) + ", " + // ARGH, change these so X is SCREEN X!
                guiLabel("_RotationX", 2) + "|\n" +
                guiLabel("_VideoIndex", 2) + "|\n" +
                //vRot.ToString() + " " +
                extraLabel + "\n" +
                extraLabelOld;
            //yc.ToString() + " " +
            //head.z.ToString() + " " +
            //head.y.ToString() + " ";
        }
        _inputController.Run();
    }

    VideoPlayer getCurrentPlayer() => clipPool.current;
   
    void _resetFrameCapture(bool diffMaskOnly = true)
    {
        if (!diffMaskOnly || _isDifferenceMaskEnabled > 0)
        {
            finishedInitialCapture = false;
            //dsts = new RenderTexture[3];
            lastFrameIdx = 0;
            shouldRender = true;
        }
    }

    void EndReached(VideoPlayer vp) => _resetFrameCapture();

    ClipConfig _undoConfig = null;
}
