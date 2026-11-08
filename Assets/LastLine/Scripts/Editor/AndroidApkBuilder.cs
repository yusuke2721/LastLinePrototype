using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastLine.Editor
{
    // Direct-install test APK, signed with Unity's local debug keystore.
    public static class AndroidApkBuilder
    {
        private const string Version = "0.1.4";
        private const int VersionCode = 5;
        private const string Identifier = "com.yusuke2721.lastline";

        [MenuItem("Last Line/Build Android Test APK", false, 31)]
        public static void BuildCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (UnityEngine.Object.FindAnyObjectByType<GameController>() == null || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Open the saved LastLine scene first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(scene.path);
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                const string scene = "Assets/LastLine/Generated/Minimal/LastLine.unity";
                EditorSceneManager.OpenScene(scene);
                Build(scene);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Build(string scene)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support, SDK/NDK and OpenJDK in Unity Hub, then restart Unity.");
            ConfigureExternalTools();

            PlayerSettings.companyName = "Yusuke2721";
            PlayerSettings.productName = "LAST LINE";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, Identifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.splitApplicationBinary = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ETC2;
            AssetDatabase.SaveAssets();

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Android"));
            Directory.CreateDirectory(folder);
            string output = Path.Combine(folder, "LastLine-" + Version + "-arm64.apk");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene }, target = BuildTarget.Android,
                locationPathName = output, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output))
                throw new InvalidOperationException("Android APK build failed: " + report.summary.result);
            File.WriteAllText(Path.Combine(folder, "build-info.txt"),
                "LAST LINE / friend test build\nVersion: " + Version + " (" + VersionCode + ")\nPackage: " + Identifier +
                "\nAndroid: 8.0+ / ARM64 / OpenGL ES 3\nSigning: local debug keystore\n" +
                "UTC: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") +
                "\nBytes: " + new FileInfo(output).Length + "\nScene: " + scene + "\n");
            Debug.Log("LAST LINE APK BUILD SUCCESS: " + output);
        }

        private static void ConfigureExternalTools()
        {
            string root = Environment.GetEnvironmentVariable("LASTLINE_ANDROID_TOOLS");
            if (string.IsNullOrEmpty(root)) return;
            Type settings = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                settings = assembly.GetType("UnityEditor.Android.AndroidExternalToolsSettings");
                if (settings != null) break;
            }
            if (settings == null) throw new InvalidOperationException("Android external tool settings could not be loaded.");
            foreach (string tool in new[] { "SDK", "NDK", "JDK" })
            {
                string path = Path.Combine(root, tool);
                if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
                var property = settings.GetProperty(tool.ToLowerInvariant() + "RootPath");
                if (property == null) throw new MissingMemberException(settings.FullName, tool + "RootPath");
                property.SetValue(null, path);
            }
            Debug.Log("Using Android tools from: " + root);
        }
    }
}
