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

    Camera _tempCam;
    Camera camera;

    GameObject rightHand;


    float[] kernel;

    // Start is called before the first frame update
    void Start()
    {
        _outlineMaterial = Resources.Load<Material>("OutlinerMaterial");
        _compositingMaterial = Resources.Load<Material>("CompositingMaterial");
        // for outlined objects
        _tempCam = new GameObject().AddComponent<Camera>();
        _tempCam.enabled = false;

        //var centerEye = GameObject.Find("CenterEyeAnchor");
        //camera = centerEye.GetComponent<Camera>();
        kernel = GaussianKernel.Calculate(2, 8);

        _outlineMaterial.SetFloatArray("_kernel", kernel);
        _outlineMaterial.SetInt("_kernelWidth", kernel.Length);

        rightHand = GameObject.Find("RightControllerAnchor");
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
    RenderTexture blurredOutlineRt;
    RenderTexture rawOutlineRt;
    int frameSkip = 0;
    int leftFrameIdx = 0;
    int rightFrameIdx = 0;

    // NOTE: these are the last screen-relative hand positions
    // TODO: change the names!
    Vector2 lastScreenLeft;
    Vector2 lastScreenRight;
    Vector2 lastScreen;

    private void OnPreRender()
    {
        int curFrame = -1; 

        _tempCam.CopyFrom(Camera.current);
        _tempCam.backgroundColor = Color.black;
        _tempCam.clearFlags = CameraClearFlags.Color;

        _tempCam.cullingMask = 1 << LayerMask.NameToLayer("Outline");

        RenderTextureDescriptor desc;
        if (XRSettings.enabled)
            desc = XRSettings.eyeTextureDesc;
        else
            desc = new RenderTextureDescriptor(Screen.width, Screen.height);

        //desc.width /= 2;
        //desc.height /= 2;

        if (Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
        {
            if (!leftEye)
            {
                rawLeftEye = new RenderTexture(desc);
                rawLeftEye.filterMode = FilterMode.Point;

                leftEye = new RenderTexture(desc);
                leftEye.filterMode = FilterMode.Point;
            }
            rawOutlineRt = rawLeftEye;
            blurredOutlineRt = leftEye;

            leftFrameIdx = (leftFrameIdx + 1) % (frameSkip + 1);
            curFrame = leftFrameIdx;

            lastScreen = lastScreenLeft;
        } else if (Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right)
        {
            if (!rightEye)
            {
                rawRightEye = new RenderTexture(desc);
                rawRightEye.filterMode = FilterMode.Point;

                rightEye = new RenderTexture(desc);
                rightEye.filterMode = FilterMode.Point;
            }
            rawOutlineRt = rawRightEye;
            blurredOutlineRt = rightEye;

            rightFrameIdx = (rightFrameIdx + 1) % (frameSkip + 1);
            curFrame = rightFrameIdx;

            lastScreen = lastScreenRight;
        } else
        {
            if (!blurredOutlineRt) blurredOutlineRt = new RenderTexture(desc);
            if (!rawOutlineRt) rawOutlineRt = new RenderTexture(desc);
        }

        //rt = RenderTexture.GetTemporary(desc);
        //rt.filterMode = FilterMode.Bilinear;
        // Hrm... following might be more efficient:
        //rt.width /= 2;
        //rt.height /= 2;

        _tempCam.targetTexture = rawOutlineRt;
        _tempCam.RenderWithShader(DrawAsSolidColor, "");

            
        // NOTE: mask should stay constantly updated!
        // Only the GRAB PASS BLUR is subject to delays!

        if (curFrame <= 0) // in case individual eye not triggered
        {

            if (Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left)
            {
                lastScreen = lastScreenLeft = Camera.current.WorldToScreenPoint(rightHand.transform.position, Camera.current.stereoActiveEye);
            }
            else if (Camera.current.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right)
            {
                lastScreen = lastScreenRight = Camera.current.WorldToScreenPoint(rightHand.transform.position, Camera.current.stereoActiveEye);
            }
                //RenderTexture rawOutlineRt = RenderTexture.GetTemporary(desc);

            Graphics.Blit(rawOutlineRt, blurredOutlineRt, _outlineMaterial);
            //RenderTexture.ReleaseTemporary(rawOutlineRt);
        }

    }

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        var curScreen = Camera.current.WorldToScreenPoint(rightHand.transform.position, Camera.current.stereoActiveEye);
        var handDeltaX = curScreen.x - lastScreen.x;
        var handDeltaY = curScreen.y - lastScreen.y;

        // Turned frame skipping off, so always passing 0 delta.
        //_compositingMaterial.SetFloat("_RightHandDeltaX", handDeltaX);
        //_compositingMaterial.SetFloat("_RightHandDeltaY", handDeltaY);
        _compositingMaterial.SetTexture("_SceneTex", src);
        _compositingMaterial.SetTexture("_MaskTex", rawOutlineRt);

        //_outlineMaterial.SetTexture("_SceneTex", src);

        Graphics.Blit(blurredOutlineRt, dst, _compositingMaterial);

        //_tempCam.targetTexture = src; // is this actually necessary?

        //RenderTexture.ReleaseTemporary(rt);
    }

}
