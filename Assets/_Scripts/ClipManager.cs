using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Android;

/* ClipPool is manages the caching and preparing of clips
 * from a limited number of VideoPlayers.
 */
public class ClipPool
{
    VideoPlayer[] _clipPlayers;
    int _curClipCursor = 0;

    public int nextIndex(int increment = 1)
    {
        return (_curClipCursor + increment) % _clipPlayers.Length;
    }
    void incrementCursor() => _curClipCursor = nextIndex();

    public VideoPlayer[] clips
    {
        get => _clipPlayers;
    }
    public VideoPlayer current
    {
        get => _clipPlayers[_curClipCursor];
    }
    public int index
    {
        get => _curClipCursor;
    }

    public void Next(System.Action<VideoPlayer, int> onChange)
    {
        incrementCursor(); 
        if (current.isPrepared)
        {
            _clipPlayers[nextIndex(1)].Prepare();
            _clipPlayers[nextIndex(2)].Prepare();
            onChange(current, index);
        }
        else
        {
            current.prepareCompleted += (_) =>
            {
                _clipPlayers[nextIndex(1)].Prepare();
                _clipPlayers[nextIndex(2)].Prepare();
                onChange(current, index);
            };  
            current.Prepare();
        }
            
        //current.prepareCompleted += 



        // Do more business here
    }

    public ClipPool(string[] clipList)
    {
        _clipPlayers = new VideoPlayer[clipList.Length];
        for (int i = 0; i < clipList.Length; i++)
        {
            _clipPlayers[i] = ClipProvider.GetExternal(clipList[i]);
        }
        _clipPlayers[0].Prepare();
        _clipPlayers[nextIndex(1)].Prepare(); // won't fail on 1 video
    }
}

/* ClipProvider is currently just a simple way to encapsulate local vs. externally
 * loaded clips. It also wraps a permission check.
 * 
 * WARNING: the provided paths are just for debugging.
 */
public class ClipProvider
{
    private static string _androidBasePath = CoreConfig.deviceBasePath;
    private static string _editorBasePath = CoreConfig.editorBasePath;
    private static string _clipsPath = CoreConfig.clipsPath;

    private static bool _isAndroid = Application.platform == RuntimePlatform.Android;

    private static string platformClipsPath
    {
        get
        {
            if (_isAndroid) return _androidBasePath + _clipsPath;
            return _editorBasePath + _clipsPath;
        }
    }

    public static VideoPlayer GetLocal(string name)
    {
        return GameObject.Find(name).GetComponent<VideoPlayer>();
        
    }

    public static VideoPlayer GetExternal(string name)
    {
        VideoPlayer loadedPlayer = new GameObject().AddComponent<VideoPlayer>();

        // Applying (most) of the same defaults as the APK-loaded videos:
        // (waitForFirstFrame is irrelevant when not playing-on-awake)
        loadedPlayer.playOnAwake = false;
        loadedPlayer.isLooping = true;
        loadedPlayer.skipOnDrop = true;
        loadedPlayer.renderMode = VideoRenderMode.RenderTexture;
        loadedPlayer.url = platformClipsPath + name + ".mp4";

        return loadedPlayer;
    }

    public static void CheckAndRequestPermissions()
    {
        if (!_isAndroid) return;
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
            Permission.RequestUserPermission(Permission.ExternalStorageRead);
    }

    public static string GetFolder()
    {
        string path = "";

        if (Application.platform == RuntimePlatform.Android)
        {
            try {
                path = System.IO.Directory.GetFiles(CoreConfig.deviceBasePath + "Download")[0];
            } catch (System.Exception e)
            {
                path = "FAIL: " + e.Message;
            }
        } else
        {
            path = "NO ANDROID.";
        }
        return path;
    }
}





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
public class ClipManager : MonoBehaviour
{
    private Camera camera;
    private Material outlinerMat;
    private Material compositingMat;

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

    void Start()
    {
        ClipProvider.CheckAndRequestPermissions();

        clipPool = new ClipPool(CoreConfig.clips);

        //if (Application.platform == RuntimePlatform.Android)
        //{
        //    clips[0].url = "/storage/emulated/0/Clips/Work Clip.mp4";
        //} else
        //{
        //    clips[0].url = "/Users/salmonax/Downloads/@VR/Clips/Work Clip.mp4";
        //}

        _inputController = new InputController(this);
        blitMat = Resources.Load<Material>("StaticMaskAlphaBlitMaterial");
        blurMat = Resources.Load<Material>("NaiveGaussianMaterial");
        dynBlurMat = Resources.Load<Material>("BoxKawaseBlitMaterial"); // currently, this does Gaussian despite name
        colorArrayBlitMat = Resources.Load<Material>("ColorArrayMaskBlitMaterial");
        colorMaskBlurBlit = Resources.Load<Material>("ColorMaskGaussianMaterialTwoPass");

        var kernel = GaussianKernel.Calculate(3, 12);
        colorMaskBlurBlit.SetFloatArray("_kernel", kernel);
        colorMaskBlurBlit.SetInt("_kernelWidth", kernel.Length);

        var diffKernel = GaussianKernel.Calculate(2, 6);
        //blurMat.SetFloatArray("_kernel", diffKernel);
        //blurMat.SetInt("_kernelWidth", diffKernel.Length);
        dynBlurMat.SetFloatArray("_kernel", diffKernel);
        dynBlurMat.SetInt("_kernelWidth", diffKernel.Length);

        outlinerMat = Resources.Load<Material>("OutlinerMaterial");
        compositingMat = Resources.Load<Material>("CompositingMaterial");

        handMat = Resources.Load<Material>("BasicHandMaterialHSBE");

        pointer = GameObject.Find("LaserPointer");
        laser = pointer.GetComponent<LaserPointer>();

        skyboxTex = Resources.Load<RenderTexture>("SkyboxTexture");
        skyboxMat = RenderSettings.skybox;

        // Reset _SwatchPickerMode, just in case it's changed in the material
        //skyboxMat.SetFloat("_UseSwatchPickerMode", 0);


        // Move this into a method:
        //PlayerPrefs.DeleteAll();
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

        //typeof(ClipConfig).GetField("_BaseZoom").SetValue(clipConfigs[0], 1349);
        //Debug.Log("?!?!?" + clipConfigs[0]._BaseZoom);
        //.SetValue(clipConfigs[curClipIndex], newValue);

        var centerEye = GameObject.Find("CenterEyeAnchor");
        camera = centerEye.GetComponent<Camera>();
        swatchDetector = centerEye.GetComponent<SwatchPicker>();
        outliner = centerEye.GetComponent<Outliner>();


        // Really dumb kludge to get all videos pre-loading.
        // Assumes only the first clip autoplays on load. Fix later
        var clipOne = clipPool.clips[0];
        clipOne.targetTexture = skyboxTex;
        clipOne.Play();

        sizingBar = GameObject.Find("SizingBar");
        debugContainer = GameObject.Find("DebugText");
        sizingBar.SetActive(false);
        debugContainer.SetActive(false);

        debugText = (UnityEngine.UI.Text)debugContainer.GetComponent("Text");

        skyboxMat.SetInt("_VideoIndex", 0);

        // Video frame related stuff:

        //clipOne.sendFrameReadyEvents = true;
        //clipOne.frameReady += OnNewFrame;
        //clipOne.loopPointReached += EndReached;

        PullAndSetMaskState();
    }

    public void ToggleAutoShiftMode()
    {
        useAutoShiftMode = !useAutoShiftMode;
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
        {
            // decrement, since EnableMask kludgily increments atm:
            //_isDifferenceMaskEnabled--;
            EnableMask(false);
        }
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
            var scale = 0.2f;
            colorMaskAlpha = new RenderTexture((int)(source.width * scale), (int)(source.height * scale), 16);
            colorMaskAlpha.filterMode = FilterMode.Point;
            smallFrameTex = new RenderTexture((int)(source.width * scale), (int)(source.height * scale), 16);
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
    void OnNewFrame(VideoPlayer source, long frameIdx)
    {
        if (_isDifferenceMaskEnabled == 2)
            RenderColorMaskTick(source); // not differentiating eyes, and brittle when called from swatchPicker!

        if (_isDifferenceMaskEnabled != 1) return;
        //if (finishedInitialCapture) return; // comment out to enable running frame collection
        //if (!wat)
        //{
        //RenderTexture src = source.texture as RenderTexture
        //if (lastFrameIdx == 0)

        if (lastFrameIdx == 0 || frameIdx - lastFrameIdx > 1) // was 18 for initial capture
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
                //Graphics.Blit(source.texture, dsts[0], new Vector2(30.0f, 30.0f), new Vector2(0, 0
                Graphics.Blit(source.texture, dsts[0]);



                //blitMat.SetTexture("_LastTex", source.texture);
                //Graphics.Blit(source.texture, dsts[0], blitMat);
            }
            if (lastFrameIdx == 0 || shouldRender)
            {
                // Note: this will fill each one out as they come in.
                skyboxMat.SetTexture("_LastTex", dsts[0]);
                skyboxMat.SetTexture("_LastTex2", dsts[1]);
                skyboxMat.SetTexture("_LastTex3", dsts[2]);

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

                skyboxMat.SetTexture("_DynAlphaTex", dynBlurredAlpha);

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

    public void togglePlayerThrottle(float amount)
    {
        if (clipPool.current.playbackSpeed == amount)
        {
            clipPool.current.playbackSpeed = 1;
            return;
        }
        clipPool.current.playbackSpeed = amount;
    }

    // Update is called once per frame
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

            float effectivePitch = headRot.x - (headRot.x > 180 ? 360 : 0) + skyboxMat.GetFloat("_RotationX"); // not right, but works here
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


            Vector3 lineEndScreen = camera.WorldToScreenPoint(endPos);

            //var rhdx = compositingMat.GetFloat("_RightHandDeltaX");
            //var rhdy = compositingMat.GetFloat("_RightHandDeltaY");

            debugText.text =
                //rhdx " " + rhdy + " | " +
                //System.Math.Round(hand.x, 3) + " " +
                //System.Math.Round(hand.y, 3) + " " +
                //System.Math.Round(hand.z, 3) + " | " +
                //ClipProvider.GetExternalFilesDir() + " | " +
                ClipProvider.GetFolder() + " |\n|" +
                System.Math.Round(laser.maxLength, 2).ToString("0.00") + "|" +
                System.Math.Round(zc, 2).ToString("0.00") + "," +
                System.Math.Round(yc, 2).ToString("0.00") + "|" +
                System.Math.Round(headRot.x, 2).ToString("0.00") + " | " +
                System.Math.Round(effectivePitch, 2).ToString("0.00") + " |\n| " +
                zoom * (0.135 + autoShiftPartialFactor * 0.5) + " |\n| " +
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
        if (!diffMaskOnly || _isDifferenceMaskEnabled == 1)
        {
            finishedInitialCapture = false;
            //dsts = new RenderTexture[3];
            lastFrameIdx = 0;
            shouldRender = true;
        }
    }

    void EndReached(UnityEngine.Video.VideoPlayer vp)
    {
        _resetFrameCapture();
    }

    // START Public Methods!
    // Because coupled to InputController!
    public void offsetProp(string prop, float offset, float min, float max)
    {
        float currentValue = skyboxMat.GetFloat(prop);
        float newValue = Mathf.Clamp(currentValue + offset, min, max);
        skyboxMat.SetFloat(prop, newValue);
        clipConfigs[clipPool.index].SetFloatIfPresent(prop, newValue);

        // Use gross C# reflection to set the field dynamically from the prop

        // Maybe add a method like so:
        //  clipConfigs[curClipIndex].UpdateValidField(prop, newValue);

        //var maybeField = typeof(ClipConfig).GetField(prop);
        //if (maybeField != null)
        //{
        //    maybeField.SetValue(clipConfigs[curClipIndex], newValue);
        //    clipConfigs[curClipIndex].needsUpdate = true;
        //}
    }

    public void togglePlaying()
    {
        var clip = getCurrentPlayer();
        if (clip.isPlaying)
        {
            clip.Pause();
        }
        else
        {
            _resetFrameCapture();
            clip.Play();
        }
    }
    public void playNextClip()
    {
        var exitingClip = clipPool.current;
        exitingClip.Pause();
        clipPool.Next((VideoPlayer enteringClip, int newIndex) =>
        {
            // Next will eventually help shuffle videoplayers across the pool.
            // It should also prepare the player after the entering one
            enteringClip.targetTexture = skyboxTex;
            exitingClip.targetTexture = null;
            skyboxMat.SetInt("_VideoIndex", newIndex);

            undoConfig = null;
            clipConfigs[newIndex].ApplyToMaterial(skyboxMat);

            PullAndSetMaskState();
            _resetFrameCapture(false); // don't check current mode

            enteringClip.Play();
        });

        //var nextClip = clipPool.GetNext();
        //nextClip.targetTexture = skyboxTex; // will PROBABLY have clipPool manage this

        //undoConfig = null;
        //clipConfigs[clip.index].ApplyToMaterial(skyboxMat);





        //var clip = getCurrentPlayer();
        //curClipIndex = (curClipIndex + 1) % clipPool.clips.Length;
        //var nextClip = getCurrentPlayer();

        //clip.Pause();
        //clip.targetTexture = null;

        //// Pass it into the shader
        //skyboxMat.SetFloat("_VideoIndex", curClipIndex);

        //clip = getCurrentPlayer();
        //clip.targetTexture = skyboxTex;

        //// Responsibilities leaking here, but leaving for now:
        //undoConfig = null;
        //clipConfigs[curClipIndex].ApplyToMaterial(skyboxMat);

        //PullAndSetMaskState();
        //if (_isDifferenceMaskEnabled > 0)
        //{
        //    _resetFrameCapture();
        //    // Note: following shoulod be taken care of by PullAndSetMaskState():
        //    //clip.sendFrameReadyEvents = true;
        //    //clip.frameReady += OnNewFrame;
        //}
        //clip.Play();
    }

    ClipConfig undoConfig = null;
    public void resetProps()
    {
        // TODO: Integrate this into persistence!
        // Something like:
        ClipConfig freshConfig;
        if (undoConfig == null)
        {
            undoConfig = clipConfigs[clipPool.index];
            freshConfig = new ClipConfig();
            // Argh, leave this the same:
            freshConfig._RotationX = undoConfig._RotationX;
            freshConfig._RotationY = undoConfig._RotationY;
        }
        else
        {
            freshConfig = undoConfig;
            undoConfig = null;
        }
        // save the new config when all buttons are released::
        clipConfigs[clipPool.index] = freshConfig;
        ClipConfig.Save(clipConfigs); // do it manually.
        clipConfigs[clipPool.index].ApplyToMaterial(skyboxMat);

        //skyboxMat.SetFloat("_RotationX", 0);
        //skyboxMat.SetFloat("_RotationY", 0);


        /*
        skyboxMat.SetFloat("_HorizontalOffset", 0);
        skyboxMat.SetFloat("_BaseZoom", 0);
        */

        //skyboxMat.SetFloat("_NudgeX", 0);
        //skyboxMat.SetFloat("_NudgeY", 0);
        //skyboxMat.SetFloat("_NudgeZ", 0);
        //skyboxMat.SetFloat("_Zoom", 0);
        //skyboxMat.SetFloat("_ZoomAdjust", 0);

        /*
        skyboxMat.SetFloat("_ZoomNudgeFactor", 2.2f);
        skyboxMat.SetFloat("_ZoomAdjustNudgeFactor", 0.12f);
        skyboxMat.SetFloat("_HorizontalOffsetNudgeFactor", 0.24f);
        skyboxMat.SetFloat("_NudgeFactorX", 0.25f);
        skyboxMat.SetFloat("_NudgeFactorY", 0.25f);
        */

        // This is hard-coded, so not sure why I put it here.
        // Oh yeah, it's because I change it. Shit!
        //combinedZoomFactor = -3.6f;
    }

    public void ToggleMask()
    {
        if (_isDifferenceMaskEnabled == 2) // cycling!
            DisableMask();
        else
            EnableMask();
    }
}


//public class Save
//{
//    public bool number = true;
//}


/*
 * This class is in charge of blitting the RenderTextures for color matching.
 * It'll be called by both the ClipManager and SwatchPicker classes to make
 * sure that changes to the color array used for color filtering are reflected
 * in the Skybox texture.
 */

//public class SwatchBlitter
//{
//    _frameSkip = new FrameSkipper(2);

//    void ClearBlit(RenderTexture source, RenderTexture dest, float scale)
//    {
//        var tmp = RenderTexture.active;
//        RenderTexture.active = dest;
//        GL.Clear(true, true, Color.black);
//        Graphics.Blit(source, dest, 16);
//        RenderTexture.active = tmp;
//    }
//    void


//    void Dispatch()
//    {
//        if (_colorMaskSkipper.Skip()) return;
//        if (!colorMaskAlpha && !smallFrameTex)
//        {
//            var scale = 0.2f;
//            colorMaskAlpha = new RenderTexture((int)(source.width * scale), (int)(source.height * scale), 16);
//            colorMaskAlpha.filterMode = FilterMode.Point;
//            smallFrameTex = new RenderTexture((int)(source.width * scale), (int)(source.height * scale), 16);
//            smallFrameTex.filterMode = FilterMode.Point;
//        }

//        ClearBlit(fullFrame, smallFrame);
//        ClearBlit(fullFrame, colorMaskAlpha, colorArrayBlitMat);
//        ClearBlit(colorMaskAlpha, blurredColorMaskAlpha, colorMaskBlurBlit)


//        //skyboxMat.SetTexture("_ColorMaskAlphaTex", colorMaskAlpha);
//        skyboxMat.SetTexture("_SmallFrameTex", smallFrameTex);

//        if (!blurredColorMaskAlpha)
//        {
//            blurredColorMaskAlpha = new RenderTexture((int)source.width / 4, (int)source.height / 4, 16);
//        }

//        RenderTexture.active = blurredColorMaskAlpha;
//        GL.Clear(true, true, Color.black);
//        Graphics.Blit(colorMaskAlpha, blurredColorMaskAlpha, colorMaskBlurBlit); // null because set to blitMat above
//        skyboxMat.SetTexture("_ColorMaskAlphaTex", blurredColorMaskAlpha);


//}
