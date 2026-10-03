using UnityEngine;

/// <summary>Player options saved on the device: look sensitivity, graphics quality, volume.</summary>
public static class GameSettings
{
    public static float Sensitivity = 1f;   // 0.4 .. 2.0
    public static int Quality = 1;          // 0 low, 1 medium, 2 high
    public static float Volume = 1f;        // 0 .. 1
    public static bool Loaded;

    public static readonly string[] QualityNames = { "Düşük", "Orta", "Yüksek" };

    public static void Load()
    {
        Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("zm_sens", 1f), 0.4f, 2f);
        Quality = Mathf.Clamp(PlayerPrefs.GetInt("zm_quality", DefaultQuality()), 0, 2);
        Volume = Mathf.Clamp01(PlayerPrefs.GetFloat("zm_volume", 1f));
        Loaded = true;
        Apply();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("zm_sens", Sensitivity);
        PlayerPrefs.SetInt("zm_quality", Quality);
        PlayerPrefs.SetFloat("zm_volume", Volume);
        PlayerPrefs.Save();
        Apply();
    }

    /// <summary>Low-memory phones start on the low preset.</summary>
    private static int DefaultQuality()
    {
        return SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 3000 ? 0 : 1;
    }

    public static void Apply()
    {
        AudioListener.volume = Volume;

        switch (Quality)
        {
            case 0:
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowDistance = 0f;
                Application.targetFrameRate = 30;
                QualitySettings.lodBias = 0.6f;
                break;
            case 2:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 70f;
                QualitySettings.shadowResolution = ShadowResolution.High;
                Application.targetFrameRate = 60;
                QualitySettings.lodBias = 1.5f;
                break;
            default:
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowDistance = 45f;
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                Application.targetFrameRate = 60;
                QualitySettings.lodBias = 1f;
                break;
        }
        QualitySettings.shadowCascades = 1;

        float fogEnd = Quality == 0 ? 170f : (Quality == 2 ? 320f : 260f);
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.fogStartDistance = fogEnd * 0.3f;
        var cam = Camera.main;
        if (cam != null)
            cam.farClipPlane = fogEnd + 150f;
    }
}
