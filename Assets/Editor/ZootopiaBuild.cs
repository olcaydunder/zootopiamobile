using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click project setup so the game can be built without opening the editor.
/// GitHub Actions (GameCI): buildMethod ZootopiaBuild.BuildAndroid (see .github/workflows/android-apk.yml)
/// Unity Build Automation: set "Pre-Export Method" to ZootopiaBuild.PreExport
/// </summary>
public static class ZootopiaBuild
{
    public const string ScenePath = "Assets/Scenes/Main.unity";
    public const string CompanyName = "Olcay Yasin Dünder";
    public const string ProductName = "Zootopia Mobile";
    public const string AndroidPackage = "com.olcayasindunder.zootopiamobile";
    public const string Version = "0.2.0";
    public const string IconPath = "Assets/Icon/AppIcon.png";

    /// <summary>Called by Unity Build Automation before every build.</summary>
    public static void PreExport()
    {
        EnsureScene();
        ApplyPlayerSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[ZootopiaBuild] Project prepared for " + AndroidPackage);
    }

    [MenuItem("Zootopia/Projeyi Hazırla (sahne + ayarlar)")]
    public static void SetupFromMenu()
    {
        PreExport();
    }

    /// <summary>
    /// GitHub Actions (GameCI) entry point: buildMethod: ZootopiaBuild.BuildAndroid
    /// Prepares the project, then builds an APK using GameCI's command-line arguments
    /// (-customBuildPath, -buildVersion, -androidVersionCode, -androidKeystore*).
    /// </summary>
    private static readonly List<string> buildErrors = new List<string>();

    private static void CaptureLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert ||
            (type == LogType.Warning && message.Contains("Shader")))
            buildErrors.Add("[" + type + "] " + message + (type == LogType.Exception ? "\n" + stack : ""));
    }

    /// <summary>Errors of this build in build/zm_build_errors.txt (the CI workflow shows them).</summary>
    private static void WriteErrors(string extra)
    {
        try
        {
            Directory.CreateDirectory("build");
            File.WriteAllText("build/zm_build_errors.txt", extra + "\n" + string.Join("\n", buildErrors.ToArray()));
        }
        catch (System.Exception) { }
    }

    public static void BuildAndroid()
    {
        Application.logMessageReceived += CaptureLog;
        WriteErrors("build method started");
        try
        {
            PreExport();
            var args = ReadCommandLine();

            string version = Arg(args, "buildVersion", "");
            if (version.Length > 0 && version != "none")
                PlayerSettings.bundleVersion = version;

            int code;
            if (int.TryParse(Arg(args, "androidVersionCode", ""), out code) && code > 0)
                PlayerSettings.Android.bundleVersionCode = code;

            EditorUserBuildSettings.buildAppBundle = false;

            string keystore = Arg(args, "androidKeystoreName", "");
            if (keystore.Length > 0 && File.Exists(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystore);
                PlayerSettings.Android.keystorePass = Arg(args, "androidKeystorePass", "");
                PlayerSettings.Android.keyaliasName = Arg(args, "androidKeyaliasName", "");
                PlayerSettings.Android.keyaliasPass = Arg(args, "androidKeyaliasPass", "");
            }

            string output = Arg(args, "customBuildPath", "build/Android/ZootopiaMobile.apk");
            string dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[ZootopiaBuild] Result: " + report.summary.result + ", size: " + report.summary.totalSize + " bytes, output: " + output);
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        buildErrors.Add("[step " + step.name + "] " + m.content);
            WriteErrors("result: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ZootopiaBuild] Build failed: " + e);
            WriteErrors("exception: " + e);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// GitHub Actions (GameCI) entry point for the online game server: buildMethod ZootopiaBuild.BuildServer
    /// (targetPlatform StandaloneLinux64). A normal Linux player (Mono) that the server starts with
    /// -batchmode -nographics -server ...; a VERSION file with the build's commit goes next to it
    /// (the phones must have the same one, see NetGame.BuildVersion).
    /// </summary>
    public static void BuildServer()
    {
        Application.logMessageReceived += CaptureLog;
        WriteErrors("server build method started");
        try
        {
            PreExport();
            var args = ReadCommandLine();
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground = true;

            string output = Arg(args, "customBuildPath", "build/StandaloneLinux64/ZootopiaServer.x86_64");
            if (!output.EndsWith(".x86_64"))
                output += ".x86_64";
            string dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[ZootopiaBuild] Server result: " + report.summary.result + ", size: " + report.summary.totalSize + " bytes, output: " + output);
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                        buildErrors.Add("[step " + step.name + "] " + m.content);
            WriteErrors("server result: " + report.summary.result);
            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
                return;
            }
            string version = File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : "dev";
            File.WriteAllText(Path.Combine(string.IsNullOrEmpty(dir) ? "." : dir, "VERSION"), version.Length > 0 ? version : "dev");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ZootopiaBuild] Server build failed: " + e);
            WriteErrors("exception: " + e);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Written by the CI workflow (online compatibility version) before building; read at run time as Resources/zm_version.</summary>
    public const string VersionFile = "Assets/Resources/zm_version.txt";

    private static Dictionary<string, string> ReadCommandLine()
    {
        var result = new Dictionary<string, string>();
        string[] raw = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < raw.Length; i++)
        {
            if (!raw[i].StartsWith("-"))
                continue;
            string key = raw[i].TrimStart('-');
            string value = (i + 1 < raw.Length && !raw[i + 1].StartsWith("-")) ? raw[i + 1] : "";
            result[key] = value;
        }
        return result;
    }

    private static string Arg(Dictionary<string, string> args, string key, string fallback)
    {
        string value;
        return args.TryGetValue(key, out value) && value != null ? value : fallback;
    }

    /// <summary>Also prepares the project automatically the first time it is opened in the editor.</summary>
    [InitializeOnLoadMethod]
    private static void AutoSetupOnLoad()
    {
        if (Application.isBatchMode)
            return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ScenePath))
                PreExport();
        };
    }

    private static void EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            // Empty scene: GameBootstrap creates everything at runtime.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Fog is switched on at runtime; with "Automatic" fog stripping Unity only keeps the
            // fog shader variants if a scene in the build uses fog, so enable it here too.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 260f;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    private static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;
        PlayerSettings.bundleVersion = Version;

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AndroidPackage);
        if (PlayerSettings.Android.bundleVersionCode < 1)
            PlayerSettings.Android.bundleVersionCode = 1;

        // Google Play requirements: IL2CPP + 64-bit, recent target API.
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

        // Sharper textures: ASTC compression (better quality than ETC2 at the same size; Android 7+ GPUs have it).
        // The game draws at the screen's own resolution (GameSettings: render scale 100%, ULTRA always native).
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

        // Landscape only, like other mobile shooters.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        // Online play: the matchmaker is plain HTTP on the game server (no domain / certificate),
        // so allow http:// requests, and always ask for the Internet permission.
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        PlayerSettings.Android.forceInternetPermission = true;

        // App icon (used for every size and platform).
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon != null)
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
    }
}
