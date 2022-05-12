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
    public GameObject debugGuiCanvas;
    public SwatchPicker swatchDetector;
    public float combinedZoomFactor = -3.6f; // decent default

    // INPUTCONTROLLER - these belong here
    public ClipConfig[] clipConfigs;

    private GameObject pointer;

    private InputController _inputController;

    private Material handMat;

    public bool useAutoShiftMode = false;

    ClipConfig _undoConfig = null;

    GameObject _rightHand;
    GameObject _smoothDampAnchor;
    GameObject _headDampAnchor;

    public static Vector2 hpLeft = new Vector2(0, 0);
    public static Vector2 hpRight = new Vector2(0, 0);

    public MaskManager maskManager;

    private void Awake()
    {
        Blitter.Init(); // currently just applies Gaussian kernels
        ClipProvider.CheckAndRequestPermissions();
        clipPool = new ClipPool(CoreConfig.clips);
        // MASKMANAGER TODO: Move these definitions from Start() and init MaskManager:
        skyboxMat = RenderSettings.skybox;
        InitClipConfigs();
        maskManager = new MaskManager(skyboxMat, clipPool, clipConfigs); // ruh roh
    }

    private static GameObject[] _swatches;
    private static GameObject[] _exclusionSwatches;
    public static void MakeSwatches(int rows, int cols)
    {
        _swatches = new GameObject[rows * cols];
        GameObject swatchTemplate = GameObject.Find("SwatchSource");
        GameObject swatchGridAnchor = GameObject.Find("SwatchGridAnchor");
        var scale = 0.1f;
        var offsetX = 0.4f;
        var offsetY = -0.70f;

        for (int i = 0; i < rows;  i++)
        {
            for (int j = 0; j < cols; j++)
            {
                var swatch =
                    Instantiate(
                        swatchTemplate,
                        swatchTemplate.transform.position + new Vector3(offsetX + i * 0.1f*scale, offsetY + j * -0.1f*scale, 0.52f),
                        swatchTemplate.transform.rotation
                    );
                swatch.GetComponent<MeshRenderer>().enabled = true;
                swatch.transform.SetParent(swatchGridAnchor.transform, true);
                _swatches[j*rows + i] = swatch;
            }
           
        }
    }
    public static void MakeExclusionSwatches(int rows, int cols)
    {
        _exclusionSwatches = new GameObject[rows * cols];
        GameObject swatchTemplate = GameObject.Find("SwatchSource");
        GameObject swatchGridAnchor = GameObject.Find("SwatchGridAnchor");
        var scale = 0.1f;
        var offsetX = 0.4f;
        var offsetY = -0.70f;

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                var swatch =
                    Instantiate(
                        swatchTemplate,
                        swatchTemplate.transform.position + new Vector3(offsetX + 1.25f*scale + i * 0.1f*scale, offsetY + j * -0.1f*scale, 0.52f),
                        swatchTemplate.transform.rotation
                    );
                swatch.GetComponent<MeshRenderer>().enabled = true;
                swatch.transform.SetParent(swatchGridAnchor.transform, true);
                _exclusionSwatches[j * rows + i] = swatch;
            }

        }
    }

    public static void SetSwatches(Color[] colors, GameObject[] swatches = null)
    {
        if (swatches == null) swatches = _swatches;
        for (int i = 0; i < colors.Length && i < swatches.Length; i++)
        {
            var c = colors[i];
            swatches[i].GetComponent<MeshRenderer>().material.color = Color.HSVToRGB(c.r, c.g, c.b);
        }
    }
    public static void SetExclusionSwatches(Color[] colors) => SetSwatches(colors, _exclusionSwatches);

    Vector3 guiOffsetEuler = new Vector3(0, 20, 0);
    void Start()
    {
        _head = GameObject.Find("CenterEyeAnchor");
        _headDampAnchor = GameObject.Find("HeadDampAnchor");
        _headDampAnchor.transform.position = _head.transform.position;
        _headDampAnchor.transform.rotation = _head.transform.rotation * Quaternion.Euler(guiOffsetEuler);

        _rightHand = GameObject.Find("RightHandAnchor");
        _smoothDampAnchor = GameObject.Find("SmoothDampAnchor");

        _inputController = new InputController(this);

        handMat = Resources.Load<Material>("BasicHandMaterialHSBE");

        skyboxTex = Resources.Load<RenderTexture>("SkyboxTexture");

        if (OVRManager.isHmdPresent)
            OVRManager.display.RecenteredPose += ResetHeadSync;

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
        debugGuiCanvas = GameObject.Find("DebugGuiCanvas");
        debugGuiCanvas.transform.SetParent(_headDampAnchor.transform, true);

        debugText = (UnityEngine.UI.Text)debugContainer.GetComponent("Text");

        DisableDebugGUI();
        PlayClipAtIndexOffset(0);

        //InitClipConfigs();
        //Blitter.SetCurrentMatte(clipPool.currentMatte);
        //PullAndSetMaskState();
        //DisableDebugGUI();

        //// Hmm, the clipPool should be doing this (it's basically just PlayNextClip):
        //var clipOne = clipPool.current;
        //clipOne.prepareCompleted += SetLayoutFromResolution;
        //clipOne.targetTexture = skyboxTex;
        //clipOne.Play();
    }

    void SetLayoutFromResolution(VideoPlayer preparedPlayer)
    {
        var ratio = preparedPlayer.width / preparedPlayer.height;
        preparedPlayer.aspectRatio = VideoAspectRatio.Stretch;
        // If it's square, assume top-bottom (ie. 2); otherwise, SBS.
        var layout = (ratio == 1) ? 2 : 1;
        skyboxMat.SetInt("_Layout", layout);
        // Oof... not a fan, but doing it for now; these all use
        // an "eyeFactor" term:
        Blitter.diffMaskBlitMat.SetInt("_Layout", layout);
        Blitter.colorMaskBlitMat.SetInt("_Layout", layout);
        Blitter.lightingMat.SetInt("_Layout", layout);
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

        // Note: want to get rid of this call site (having PlayClipAtIndexOffset(0) do it instead):
        //clipConfigs[0].ApplyToMaterial(skyboxMat);
        //clipConfigs[0].ApplyToMaterial(Blitter.dynThreshBlitMat, true); // useDynThreshFields = true
        // -- END ClipConfig Loading
    }

    //Save save = new Save();
    //Save[] saves = new Save[] { new Save(), new Save() };

    // Start is called before the first frame update
    string guiLabel(string label, int sigs, Material targetMat = null)
    {
        if (!targetMat) targetMat = skyboxMat;
        return System.Math.Round(targetMat.GetFloat(label), sigs).ToString("0." + new string('0', sigs));
    }

    string guiRound(float num)
    {
        return System.Math.Round(num, 2).ToString("0.00");
    }

    private Vector3 OneEightify(Vector3 eulerAngles)
    {
        System.Func<float, float> offset =
            (float n) => (n % 360) - ((n % 360) > 180 ? 360 : 0);
        return new Vector3(
            offset(eulerAngles.x),
            offset(eulerAngles.y),
            offset(eulerAngles.z)
        );
    }

    private Vector3 _headPositionBasis = new Vector3(0, 0, 0);
    private Vector3 _headPositionAtStart;

    private Vector3 _handRotationAtStart; // not a "basis" in the same sense as head
    private Vector3 _handPositionAtStart;
    //private Vector3 _handSyncRotationDelta = new Vector3(0, 0, 0);

    private Vector3 _skyboxRotationAtStart;
    private Vector3 _smoothHandSyncRotation;
    private Vector3 _handSyncPositionDelta;

    private float _baseZoomAtStart;
    //private Vector3 _headAtHandHeight;
    private float _handDistanceAtStart;
    
    private GameObject _head;

    private bool _useSmoothDamp = true;
    private bool _useScreenSpaceHand = false; // cool, but buggy
    private GameObject _handOrAnchor;

    // Warning: nothing is enforcing that UpdateHeadSync() only be called after InitHeadSync()
    public void InitHeadSync()
    {
        _smoothDampAnchor.transform.position = _rightHand.transform.position;
        _smoothDampAnchor.transform.rotation = _rightHand.transform.rotation;

        _handOrAnchor = _useSmoothDamp ? _smoothDampAnchor : _rightHand;
        /* Proceed in three steps:
         * 1. Deal with the head, by initializing and modifying a head offset while in sync mode
         * 2. Deal with the hand rotation, init/modifying its rotational delta and applying it to rotation
         * 3. Deal with zooming, initializing the skybox's BaseZoom and hand-to-head distance and applying the delta
         */

        // 1
        _headPositionAtStart = _head.transform.position - _headPositionBasis;

        // 2
        _handRotationAtStart = OneEightify(_handOrAnchor.transform.localEulerAngles);
        _handPositionAtStart = _handOrAnchor.transform.position; // new

        _skyboxRotationAtStart = _smoothHandSyncRotation = new Vector3(
            skyboxMat.GetFloat("_RotationX"), // Relative to X-axis, so vertical shift
            skyboxMat.GetFloat("_RotationY"), // Relative to Y-axis, so horizontal shift
            0 // ignored, just to simplify operators in UpdateHeadSync()
         );

        // 3
        _baseZoomAtStart = skyboxMat.GetFloat("_BaseZoom");
        _handDistanceAtStart = Vector3.Distance(_head.transform.position, _handOrAnchor.transform.position);
    }
    public void UpdateHeadSync()
    {
        _headPositionBasis = _head.transform.position - _headPositionAtStart;

        if (_useScreenSpaceHand)
        {
            _handSyncPositionDelta =
                Camera.current.WorldToScreenPoint(_handOrAnchor.transform.position, Camera.MonoOrStereoscopicEye.Left) -
                Camera.current.WorldToScreenPoint(_handPositionAtStart, Camera.MonoOrStereoscopicEye.Left); // normalize to left
            var newHandSyncRotationY =
                (_handSyncPositionDelta.x / UnityEngine.XR.XRSettings.eyeTextureWidth) * Camera.current.fieldOfView;
            var newHandSyncRotationX =
                -(_handSyncPositionDelta.y / UnityEngine.XR.XRSettings.eyeTextureHeight) * Camera.current.fieldOfView;

            SetProp("_RotationX", _skyboxRotationAtStart.x - newHandSyncRotationX, -180, 180);
            SetProp("_RotationY", _skyboxRotationAtStart.y - newHandSyncRotationY, -180, 180);
        } else // use normal rotation-based handsync
        {
            var handSyncRotationDelta = (OneEightify(_handOrAnchor.transform.localEulerAngles) - _handRotationAtStart);
            //_smoothHandSyncRotation = (_smoothHandSyncRotation + _skyboxRotationAtStart - handSyncRotationDelta) / 2;
            //skyboxMat.SetFloat("_RotationX", _smoothHandSyncRotation.x);
            //skyboxMat.SetFloat("_RotationY", _smoothHandSyncRotation.y);
            var newHandSyncRotation = _skyboxRotationAtStart - handSyncRotationDelta;
            SetProp("_RotationX", newHandSyncRotation.x, -180, 180);
            SetProp("_RotationY", newHandSyncRotation.y, -180, 180);
        }


        var handDistanceDelta = _handDistanceAtStart - Vector3.Distance(_head.transform.position, _handOrAnchor.transform.position);
        SetProp("_BaseZoom", _baseZoomAtStart - 4.5788f * handDistanceDelta, -3.00f, 3.00f);
        //skyboxMat.SetFloat("_BaseZoom", _baseZoomAtStart - 4.5788f * handDistanceDelta); // dead-reckoned magic number
    }
    // Run this on recenter:
    private void ResetHeadSync()
    {
        _headPositionBasis = new Vector3(0, 0, 0);
    }

    private Vector3 _dampPosVel = Vector3.zero;
    private Quaternion _dampRotVel = Quaternion.identity;
    private Vector3 _headDampPosVel = Vector3.zero;
    private Quaternion _headDampRotVel = Quaternion.identity;
   
    void Update()
    {
        // Sync hand exposure and saturation to skybox
        handMat.SetFloat("_Exposure", Mathf.Pow(skyboxMat.GetFloat("_Exposure"), 2));
        handMat.SetFloat("_Saturation", skyboxMat.GetFloat("_Saturation"));
        //SmoothDamp this in a second:
        _headDampAnchor.transform.position =
            Vector3.SmoothDamp(_headDampAnchor.transform.position, _head.transform.position, ref _headDampPosVel, 0.5f);
            //_head.transform.position;
        _headDampAnchor.transform.rotation =
            Util.SmoothDampQuaternion(_headDampAnchor.transform.rotation, _head.transform.rotation, ref _headDampRotVel, 0.5f);
            //_head.transform.rotation;

        if (OVRManager.isHmdPresent == true)
        {
            Vector3 head = _head.transform.position - _headPositionBasis;
            Vector3 headRot = _head.transform.localEulerAngles;
            Vector3 headRot180 = OneEightify(headRot);

            var posTime = RightController.Hands ? 0.3f : 0.2f; // er, the same for now
            var rotTime = RightController.Hands ? 0.3f : 0.2f;

            _smoothDampAnchor.transform.position =
                Vector3.SmoothDamp(_smoothDampAnchor.transform.position, _rightHand.transform.position, ref _dampPosVel, posTime);
            _smoothDampAnchor.transform.rotation =
                Util.SmoothDampQuaternion(_smoothDampAnchor.transform.rotation, _rightHand.transform.rotation, ref _dampRotVel, rotTime);


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
            var foo = GameObject.Find("OVRHandPrefab_R").GetComponent<OVRHand>();



            debugText.text =
                "Grain Strength: " + guiLabel("_Strength", 2, Blitter.filmGrainMaterial) + "|\n|" +
                "Grain Bias: " + guiLabel("_GrainBias", 2, Blitter.compositingMaterial) + "|\n|" +

                ((_inputController.metaMode == MetaMode.Debug) ?
                    "<color=yellow>" +
                    "HAND DEPTH: " + System.Math.Round(outliner.handDepthCompX, 2).ToString("0.00") + " |\n| " +
                    "SampleDecay: " + guiLabel("_SampleDecay", 3, Blitter.dynThreshBlitMat) + "|\n|" +
                    "OutputDecay: " + guiLabel("_OutputDecay", 3, Blitter.dynThreshBlitMat) + "|\n|" +
                    "ColorDistMultThresh: " + guiLabel("_ColorDistMultThresh", 3, Blitter.dynThreshBlitMat) + "|\n|" +
                    "ColorDistMultStrength: " + guiLabel("_ColorDistMultStrength", 3, Blitter.dynThreshBlitMat) + "|\n|" +
                    "DecayDampThresh: " + guiLabel("_DecayDampThresh", 3, Blitter.dynThreshBlitMat) + "|\n|" +
                    "DecayDampStrength: " + guiLabel("_DecayDampStrength", 3, Blitter.dynThreshBlitMat) + "|\n|" +
                    "DistMultiplier: " + guiLabel("_DistMultiplier", 3, Blitter.dynThreshBlitMat) + "|\n|" +
                    "DistPower: " + guiLabel("_DistPower", 3, Blitter.dynThreshBlitMat) + "|\n|" +
                    "InnerThreshMultiplier: " + guiLabel("_InnerThreshMultiplier", 3, Blitter.dynThreshBlitMat) + "|\n|" +
                    "InnerThreshPower: " + guiLabel("_InnerThreshPower", 3, Blitter.dynThreshBlitMat) + "|\n" +
                    "</color>" :

                //System.Math.Round(hand.x, 3) + " " +
                //System.Math.Round(hand.y, 3) + " " +
                //System.Math.Round(hand.z, 3) + " | " +
                //ClipProvider.GetExternalFilesDir() + " | " +
                //"index: " + foo.GetFingerPinchStrength(OVRHand.HandFinger.Index) + "," +
                //    foo.GetFingerConfidence(OVRHand.HandFinger.Index) + "|\n|" + 
                //"middle: " + foo.GetFingerPinchStrength(OVRHand.HandFinger.Middle) + "," +
                //    foo.GetFingerConfidence(OVRHand.HandFinger.Middle) + "|\n|" +
                //"ring: " + foo.GetFingerPinchStrength(OVRHand.HandFinger.Ring) + "," +
                //    foo.GetFingerConfidence(OVRHand.HandFinger.Ring) + "|\n|" +
                guiLabel("_MatteAlphaMultiplier", 2) + ", " +
                guiLabel("_MatteAlphaPower", 2) + " |\n| " +

                // Rename to _MatteThreshMultiplier, etc.:
                guiLabel("_WeightMultiplier", 2, Blitter.matteMaskAlphaBlitMat) + ", " +
                guiLabel("_WeightPower", 2, Blitter.matteMaskAlphaBlitMat) + " |\n| " +
                //guiLabel("_BlurX", 2, Blitter.matteMaskThreshBlurMat) + ", " +
                //guiLabel("_BlurY", 2, Blitter.matteMaskThreshBlurMat) + " |\n| " +

                guiLabel("_BlurX", 2, Blitter.matteMaskAlphaBlurMat) + ", " +
                guiLabel("_BlurY", 2, Blitter.matteMaskAlphaBlurMat) + " |\n| " +
                "|LEFT EYE: " + hpLeft + "|\n|" +
                "RIGHT EYE: " + hpRight + "|\n|" +
                "HandOffsets: " + (outliner.handOffsetX + outliner.drHandOffsetX) + "," + (outliner.handOffsetY + outliner.drHandOffsetY) + "|\n|" +
                //"HandOffsets: " + outliner.handOffsetX + "," + outliner.handOffsetY + "|\n|" +
                Vector3.Distance(_rightHand.transform.position, new Vector3(_head.transform.position.x, _rightHand.transform.position.y, _head.transform.position.z)) + "|\n|" +
                //                _handSyncRotationDelta + "|\n|" +
                skyboxMat.GetInt("_Layout") + "|\n|" +
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
                guiLabel("_VideoIndex", 2) + "|\n") +
                //vRot.ToString() + " " +
                extraLabel + "\n" + extraLabelOld;
            ;
            //yc.ToString() + " " +
            //head.z.ToString() + " " +
            //head.y.ToString() + " ";
        }
        _inputController.Run();
    }

    VideoPlayer getCurrentPlayer() => clipPool.current;

    void PlayClipAtIndexOffset(int offset)
    {
        Debug.Log("PLAY CLIP AT INDEX OFFSET CALLED: " + offset);
        var exitingClip = clipPool.current;
        maskManager.DisableFrameCapture(exitingClip); // same as no arg, but just to be clear
        if (offset != 0) exitingClip.Pause();
        clipPool.Next((VideoPlayer enteringClip, int newIndex) =>
        {
            // This callback runs after entering video is prepared.
            // (small detail: clipPool also prepares the next two videos FIRST)
            enteringClip.targetTexture = skyboxTex;
            if (offset != 0 && exitingClip != enteringClip) exitingClip.targetTexture = null;
            skyboxMat.SetInt("_VideoIndex", newIndex);
            SetLayoutFromResolution(enteringClip);

            _undoConfig = null;


            var playStart = clipConfigs[newIndex].playStart;
            // Check playStart in case the video has changed.
            if (playStart <= (long)enteringClip.frameCount && playStart > 0 && enteringClip.frame == 0)
                enteringClip.frame = playStart;

            clipConfigs[newIndex].ApplyToMaterial(skyboxMat);
            // Not necessary, but it'll surface any bugs related to globalizing the DynThresh fields.
            clipConfigs[newIndex].ApplyToMaterial(Blitter.dynThreshBlitMat, true); // useDynThreshFields = true

            Blitter.SetCurrentMatte(clipPool.currentMatte); // ignores if null
            PullAndSetMaskState();
            _resetFrameCapture(false); // don't check current mode
            enteringClip.Play();

            // Presentation debug
            //skyboxMat.SetFloat("_RotationY", 134);
            //skyboxMat.SetFloat("_ZoomShiftY", 0.8f);
        }, offset);
    }

    void SetProp(string prop, float value, float min, float max, Material targetMat = null)
    {
        if (!targetMat) targetMat = skyboxMat;
        float newValue = Mathf.Clamp(value, min, max);
        targetMat.SetFloat(prop, newValue);
        // TODO: eliminate this awful dynThreshBlitMat kludge!
        if (targetMat == skyboxMat || targetMat == Blitter.dynThreshBlitMat)
            clipConfigs[clipPool.index].SetFloatIfPresent(prop, newValue);
        if (targetMat == Blitter.dynThreshBlitMat)
            ClipConfig.GlobalizeDynThresh(clipConfigs, clipPool.index);
    }

}
