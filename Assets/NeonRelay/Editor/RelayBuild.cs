using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonRelay.Editor
{
    public static class RelayBuild
    {
        [MenuItem("Neon Relay/Create or refresh game scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Orbital Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera"; camera.GetComponent<Camera>().orthographic = true;
            camera.transform.position = new Vector3(11, 3, -10);
            new GameObject("Neon Relay", typeof(RelayGame));
            Directory.CreateDirectory("Assets/NeonRelay/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/NeonRelay/Scenes/NeonRelay.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/NeonRelay/Scenes/NeonRelay.unity", true) };
            PlayerSettings.companyName = "Orbital Courier"; PlayerSettings.productName = "Neon Relay";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Neon Relay/Build Windows player")]
        public static void Windows()
        {
            CreateScene();
            Directory.CreateDirectory("Builds/NeonRelay");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/NeonRelay/Scenes/NeonRelay.unity" },
                locationPathName = "Builds/NeonRelay/NeonRelay.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Player build failed: " + report.summary.result);
            Debug.Log("NEON BUILD PASS: " + report.summary.totalSize + " bytes");
        }
    }
}
