using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ClipConfig
{
    // Manually increment this when changing the format!
    public int version = 32;

    // Note on the properties:
    //  1) The default values will override the Skybox Material properties
    //      unless we specifically write code to load them.
    //  2) They are currently doubled up in resetProp(), which is easily fixed.
    //

    // Note: all _Capitalized props are named after material props and currently
    // synced individually from offsetProp().
    //
    // Raw Zoom
    public float _BaseZoom = 0;
    public float _HorizontalOffset = 0;

    // Headset-tracking compensation
    public float _ZoomNudgeFactor = 2.2f;
    public float _ZoomAdjustNudgeFactor = 0.12f;
    public float _HorizontalOffsetNudgeFactor = 0.24f;
    // TODO: make the hard-coded vertical-shift-on-zoom value adjustable!
    public float _ZoomShiftX = 0;
    public float _ZoomShiftY = 0;

    public float _NudgeFactorX = 0.25f;
    public float _NudgeFactorY = 0.25f;

    // View rotation
    // NOTE: these are UV coords, so X is vertical, Y horizontal!
    // (fix please)
    public float _RotationX = 0;
    public float _RotationY = 0;

    // Color settings
    public float _Saturation = 1;
    public float _Exposure = 0.6f;
    public float _Contrast = 1;

    public float _Transparency = 1.5f; // urgh...

    public float _UseDifferenceMask = 0;

    public float _VerticalFlip = 0;


    // Argh, kludge; just for this debug period:
    [System.NonSerialized]
    static string[] dynThreshFields = {
        "_SampleDecay",
        "_OutputDecay",
        "_ColorDistMultThresh",
        "_ColorDistMultStrength",
        "_DecayDampThresh",
        "_DecayDampStrength",
        "_DistMultiplier",
        "_DistPower",
        "_InnerThreshMultiplier",
        "_InnerThreshPower",
    };

    // DynThresh values;
    // Note: this will save unique values for each clip.
    // For kludge purposes, pass the current index to GlobalizeDynThresh to copy them across
    // all ClipConfigs.
    public float _SampleDecay = 0.5f;
    public float _OutputDecay = 0.5f;
    public float _ColorDistMultThresh = 0;
    public float _ColorDistMultStrength = 0;
    public float _DecayDampThresh = 0;
    public float _DecayDampStrength = 0;
    public float _DistMultiplier = 1;
    public float _DistPower = 1;
    public float _InnerThreshMultiplier = 1;
    public float _InnerThreshPower = 1;

    // used to trigger a save when modified
    [System.NonSerialized]
    public bool needsUpdate = false;


    // Used for loading settings to the Skybox.
    // WARNING: it follows a brittle convention and doesn't
    //  bother to verify that the target has the properties!
    public void ApplyToMaterial(Material material, bool useDynThreshFields = false)
    {
        var fields = typeof(ClipConfig).GetFields();
        for (int i = 0; i < fields.Length; i++)
        {
            var name = fields[i].Name;

            var dynThreshSwitch = System.Array.IndexOf(dynThreshFields, name) == -1; // NOT in the list
            if (useDynThreshFields) dynThreshSwitch = !dynThreshSwitch;

            //Debug.Log("APPLY TO MATERIAL CALLED: " + name);
            if (name[0] == '_' && char.IsUpper(name[1]) && dynThreshSwitch)
            {
                //Debug.Log("@ INSIDE CONDITIONAL: " + name);
                material.SetFloat(name, (float)fields[i].GetValue(this));
            }
        }
    }

    // This is a kludge to copy the dynThresh values at a given index to ALL
    // clipConfigs.
    public static void GlobalizeDynThresh(ClipConfig[] clipConfigs, int index)
    {
        var fields = typeof(ClipConfig).GetFields();
        for (int i = 0; i < clipConfigs.Length; i++)
        {
            for (int j = 0; j < fields.Length; j++)
            {
                var name = fields[j].Name;
                if (System.Array.IndexOf(dynThreshFields, name) > -1)
                {
                    var fieldInfo = typeof(ClipConfig).GetField(name);
                    fieldInfo.SetValue(clipConfigs[i], fieldInfo.GetValue(clipConfigs[index]));
                }
            }
        }
    }

    public void SetFloatIfPresent(string prop, float value)
    {
        var maybeField = typeof(ClipConfig).GetField(prop);
        if (maybeField == null) return;

        maybeField.SetValue(this, value);
        needsUpdate = true;
    }

    public static void Save(ClipConfig[] clipConfigsToSave)
    {
        // Probably, this will be what gets called during ControllerInput
        string saveStringArray = "";
        for (int i = 0; i < clipConfigsToSave.Length; i++)
        {
            saveStringArray += JsonUtility.ToJson(clipConfigsToSave[i]);
            if (i < clipConfigsToSave.Length - 1) saveStringArray += "|"; // whatever.
        }
        Debug.Log(saveStringArray);
        PlayerPrefs.SetString("ClipConfig", saveStringArray);
    }

    public static ClipConfig[] Load()
    {
        if (PlayerPrefs.HasKey("ClipConfig"))
        {
            string saveString = PlayerPrefs.GetString("ClipConfig");
            Debug.Log("ClipConfig.Load(): " + saveString);
            string[] loadedStringArray = saveString.Split('|');
            ClipConfig[] loadedSaves = new ClipConfig[loadedStringArray.Length];
            for (int i = 0; i < loadedStringArray.Length; i++)
            {
                loadedSaves[i] = JsonUtility.FromJson<ClipConfig>(loadedStringArray[i]);
            }
            return loadedSaves;
        }
        return new ClipConfig[0];
    }
}