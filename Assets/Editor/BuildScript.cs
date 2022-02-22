using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class BuildScript
{
    static void PerformBuild()
    {
        string[] defaultScene = { "Assets/My Scene.unity" };
        BuildPipeline.BuildPlayer(defaultScene, "/Users/salmonax/My project/build/game.apk",
            BuildTarget.Android, BuildOptions.None);
    }
}
