using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class Outliner : MonoBehaviour
{
    public Shader DrawAsSolidColor;
    public Shader Outline;
    Material _outlineMaterial;
    Material _compositingMaterial;

    Material _filmGrainMaterial;

    Camera _tempCam;
    Camera camera;

    GameObject rightHand;
    SkinnedMeshRenderer rightHandRenderer;
    MeshRenderer debugSphereRenderer;

    float[] kernel;

    // Start is called before the first frame update
    void Start()
    {
        _outlineMaterial = Resources.Load<Material>("OutlinerMaterial");

        // for outlined objects
        _tempCam = new GameObject().AddComponent<Camera>();
        _tempCam.enabled = false;

        //var centerEye = GameObject.Find("CenterEyeAnchor");
        //camera = centerEye.GetComponent<Camera>();
        kernel = GaussianKernel.Calculate(2, 8);

        _outlineMaterial.SetFloatArray("_kernel", kernel);
        _outlineMaterial.SetInt("_kernelWidth", kernel.Length);

        rightHand = GameObject.Find("RightControllerAnchor");
        rightHandRenderer = GameObject.Find("OVRHandPrefab_R").GetComponent<SkinnedMeshRenderer>();
        //debugSphereRenderer = GameObject.Find("Sphere").GetComponent<MeshRenderer>();

        rightHandRenderer.forceRenderingOff = true;

        //debugSphereRenderer.forceRenderingOff = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (OVRManager.isHmdPresent == true)
        {
            Vector3 rightPos = rightHand.transform.position;

            _outlineMaterial.SetFloat("_RightHandX", rightPos.x);
            _outlineMaterial.SetFloat("_RightHandY", Mathf.Max(rightPos.y, 0));

            float clampedZ = Mathf.Min(Mathf.Max(rightPos.z, 0), 0.4f);
            _outlineMaterial.SetFloat("_RightHandZ", clampedZ);
            _compositingMaterial.SetFloat("_RightHandZ", clampedZ);
        }
    }

    RenderTexture leftEye;
    RenderTexture rightEye;
    RenderTexture rawLeftEye;
    RenderTexture rawRightEye;
    RenderTexture featheredLeftEye;
    RenderTexture featheredRightEye;

    RenderTexture grainLeftEye;
    RenderTexture grainRightEye;

    // Note: these are just pointers to the left/right eye RTs above:
    RenderTexture blurredOutlineRt;
    RenderTexture rawOutlineRt;
    RenderTexture featheredOutlineRt;

    RenderTexture grainRt;

    int frameSkip = 1;
    int leftFrameIdx = 0;
    int rightFrameIdx = 0;

    // NOTE: these are the last screen-relative hand positions
    // TODO: change the names!
    Vector2 lastScreenRelativeHandPosLeft;
    Vector2 lastScreenRelativeHandPosRight;
    Vector2 lastScreenRelativeHandPos;

    private void OnPreRender()
    {
        int curFrame = -1; 

        _tempCam.CopyFrom(Camera.current);
        _tempCam.backgroundColor = Color.black;
        _tempCam.clearFlags = CameraClearFlags.Color;

        _tempCam.cullingMask = 1 << LayerMask.NameToLayer("Outline");

        RenderTextureDescriptor desc = VR.desc;
        
        if (VR.Left)
        {
            if (!leftEye)
            {
                desc.width /= 2;
                desc.height /= 2;
                grainLeftEye = new RenderTexture(desc);

                rawLeftEye = new RenderTexture(desc);
                //rawLeftEye.filterMode = FilterMode.Point;

                leftEye = new RenderTexture(desc);
                //leftEye.filterMode = FilterMode.Point;

                featheredLeftEye = new RenderTexture(desc);
                //leftEye.filterMode = FilterMode.Point;

            }
            rawOutlineRt = rawLeftEye;
            blurredOutlineRt = leftEye;
            featheredOutlineRt = featheredLeftEye;
            grainRt = grainLeftEye;

            leftFrameIdx = (leftFrameIdx + 1) % (frameSkip + 1);
            curFrame = leftFrameIdx;

            lastScreenRelativeHandPos = lastScreenRelativeHandPosLeft;
        } else if (VR.Right)
        {
            if (!rightEye)
            {
                desc.width /= 2;
                desc.height /= 2;
                grainRightEye = new RenderTexture(desc);

                rawRightEye = new RenderTexture(desc);
                //rawRightEye.filterMode = FilterMode.Point;

                rightEye = new RenderTexture(desc);
                //rightEye.filterMode = FilterMode.Point;

                featheredRightEye = new RenderTexture(desc);

            }
            rawOutlineRt = rawRightEye;
            blurredOutlineRt = rightEye;
            featheredOutlineRt = featheredRightEye;
            grainRt = grainRightEye;

            rightFrameIdx = (rightFrameIdx + 1) % (frameSkip + 1);
            curFrame = rightFrameIdx;

            lastScreenRelativeHandPos = lastScreenRelativeHandPosRight;
        } else
        {
            desc.width /= 2;
            desc.height /= 2;
            if (!blurredOutlineRt) blurredOutlineRt = new RenderTexture(desc);
            if (!rawOutlineRt) rawOutlineRt = new RenderTexture(desc);
            if (!featheredOutlineRt) featheredOutlineRt = new RenderTexture(desc);
            if (!grainRt) grainRt = new RenderTexture(desc);
        }

        //rt = RenderTexture.GetTemporary(desc);
        //rt.filterMode = FilterMode.Bilinear;
        // Hrm... following might be more efficient:
        //rt.width /= 2;
        //rt.height /= 2;

        _tempCam.targetTexture = rawOutlineRt;

        rightHandRenderer.forceRenderingOff = false;

        //debugSphereRenderer.forceRenderingOff = false;
        _tempCam.RenderWithShader(DrawAsSolidColor, "");

        rightHandRenderer.forceRenderingOff = true;


        //debugSphereRenderer.forceRenderingOff = true;

        // NOTE: mask should stay constantly updated!
        // Only the GRAB PASS BLUR is subject to delays!

        // For passthrough cutout OR outline mode:
        //Blitter.Clear(rawOutlineRt, featheredOutlineRt, Blitter.outlinerHandBlurMat);

        if (curFrame <= 0) // in case individual eye not triggered
        {
            /*
            if (VR.Left)
                lastScreenRelativeHandPos = lastScreenRelativeHandPosLeft =
                    Camera.current.WorldToScreenPoint(rightHand.transform.position, Camera.current.stereoActiveEye);
            else if (VR.Right)
                lastScreenRelativeHandPos = lastScreenRelativeHandPosRight =
                    Camera.current.WorldToScreenPoint(rightHand.transform.position, Camera.current.stereoActiveEye);
            */
            //RenderTexture rawOutlineRt = RenderTexture.GetTemporary(desc);

            //Graphics.Blit(rawOutlineRt, blurredOutlineRt, _outlineMaterial);
            //RenderTexture.ReleaseTemporary(rawOutlineRt);
        }

    }

    public float handOffsetX = 0;
    public float handDepthCompX = 0;
    public float handOffsetY = 0;
    public float drHandOffsetX = 0;
    public float drHandOffsetY = 0;
    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        var curScreen = Camera.current.WorldToScreenPoint(rightHand.transform.position, Camera.current.stereoActiveEye);
        var handDepthSign = 1;
        if (VR.Left)
        {
            ClipManager.hpLeft = curScreen;
            handDepthSign = -1;
        }
        else if (VR.Right) ClipManager.hpRight = curScreen;

        var handDeltaX = curScreen.x - lastScreenRelativeHandPos.x;
        var handDeltaY = curScreen.y - lastScreenRelativeHandPos.y;

        var _compositingMaterial = Blitter.compositingMaterial;
        // Turned frame skipping off, so always passing 0 delta.
        //if (frameSkip > 0)
        //{
            //_compositingMaterial.SetFloat("_RightHandDeltaX", handDeltaX);
            //_compositingMaterial.SetFloat("_RightHandDeltaY", handDeltaY);
        //}

        // Only do the clever hand-offset when not showing the controller model:
        //if (OVRInput.IsControllerConnected(OVRInput.Controller.Hands))
        //{
            drHandOffsetX = 0.051f * curScreen.x - 35;
            drHandOffsetY = 0.070f * curScreen.y - 27;
        //} else
            //drHandOffsetX = drHandOffsetY = 0;


        Blitter.Clear(src, grainRt, Blitter.filmGrainMaterial);
        //_compositingMaterial.SetFloat("_HandOffsetX", handOffsetX);
        //_compositingMaterial.SetFloat("_HandOffsetY", handOffsetY);
        _compositingMaterial.SetFloat("_HandOffsetX", drHandOffsetX + handOffsetX + handDepthCompX*handDepthSign);
        _compositingMaterial.SetFloat("_HandOffsetY", drHandOffsetY + handOffsetY);
        _compositingMaterial.SetTexture("_GrainTex", grainRt);
        _compositingMaterial.SetTexture("_SceneTex", src);
        //_compositingMaterial.SetTexture("_MaskTex", featheredOutlineRt);
        _compositingMaterial.SetTexture("_MaskTex", rawOutlineRt);

        //_outlineMaterial.SetTexture("_SceneTex", src);


        Graphics.Blit(blurredOutlineRt, dst, _compositingMaterial);

        //_tempCam.targetTexture = src; // is this actually necessary?

        //RenderTexture.ReleaseTemporary(rt);
    }

}
