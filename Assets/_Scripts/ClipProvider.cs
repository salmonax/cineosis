using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Android;

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
            try
            {
                path = System.IO.Directory.GetFiles(CoreConfig.deviceBasePath + "Download")[0];
            }
            catch (System.Exception e)
            {
                path = "FAIL: " + e.Message;
            }
        }
        else
        {
            path = "NO ANDROID.";
        }
        return path;
    }
}