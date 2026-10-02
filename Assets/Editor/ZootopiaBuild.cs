using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click project setup so the game can be built without opening the editor.
/// Unity Build Automation: set "Pre-Export Method" to ZootopiaBuild.PreExport
/// </summary>
public static class ZootopiaBuild
{
    public const string ScenePath = "Assets/Scenes/Main.unity";
    public const string CompanyName = "Olcay Yasin Dünder";
    public const string ProductName = "Zootopia Mobile";
    public const string AndroidPackage = "com.olcayasindunder.zootopiamobile";
    public const string Version = "0.1.0";

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

        // Landscape only, like other mobile shooters.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
    }
}
