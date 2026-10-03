using UnityEngine;

/// <summary>
/// Player options saved on the device (the AYARLAR screen): controls, fire modes, sensitivity,
/// gyro, graphics and sound. Apply() pushes them to Unity, post-processing and the touch controls.
/// </summary>
public static class GameSettings
{
    // Sensitivity
    public static float Sensitivity = 1f;        // camera (hip)        0.4 .. 2.0
    public static float AdsSensitivity = 0.8f;   // aiming down sights  0.3 .. 2.0
    public static float ScopeSensitivity = 0.6f; // 3x / 6x / sniper     0.3 .. 2.0
    public static int RotationMode = 1;          // 0 fixed, 1 acceleration
    public static int Acceleration = 120;        // 0 .. 200
    public static int Gyro;                      // 0 off, 1 while aiming, 2 always
    public static float GyroSensitivity = 1f;    // 0.3 .. 2.5

    // Weapon / controls
    public static bool AimAssist = true;
    public static int FirePreset;                // 0 tap to aim, 1 hip fire, 2 automatic, 3 custom
    public static readonly int[] CustomFire = { 0, 0, 1, 0, 1 };   // per WeaponType: 0 aim, 1 hip
    public static bool LeftFireButton = true;
    public static bool FireButtonLook = true;    // drag the right fire button to turn the camera
    public static bool FireButtonFollow;         // the fire button follows the thumb while held
    public static int JoystickMode = 1;          // 0 fixed, 1 dynamic (appears under the thumb)
    public static float ButtonOpacity = 1f;      // 0.3 .. 1 (on-screen buttons)

    // Graphics & sound
    public static int Quality = 1;               // 0 low, 1 medium, 2 high, 3 max
    public static int FrameRate = 1;             // 0 = 30, 1 = 60, 2 = max
    public static bool AntiAliasing = true;
    public static bool Shadows = true;
    public static bool Bloom = true;
    public static float Volume = 1f;             // 0 .. 1
    public static int Fov = 70;                  // 60 .. 90 (third-person view angle)
    public static int ViewDistance = 1;          // 0 near, 1 normal, 2 far, 3 max
    public static float RenderScale = 1f;        // 0.5 .. 1 (screen resolution)
    public static int GrassDensity = 2;          // 0 off, 1 low, 2 normal, 3 high
    public static int CrosshairColor;            // index into CrosshairColors
    public static bool DamageNumbers = true;
    public static bool Vibration = true;
    public static bool UiSounds = true;
    public static bool CameraShake = true;
    public static bool Loaded;

    public static readonly string[] ViewDistanceNames = { "YAKIN", "NORMAL", "UZAK", "MAKS." };
    public static readonly string[] GrassNames = { "KAPALI", "AZ", "NORMAL", "YOĞUN" };
    public static readonly string[] CrosshairNames = { "BEYAZ", "YEŞİL", "KIRMIZI", "SARI", "CAMGÖBEĞİ" };
    public static readonly Color[] CrosshairColors =
    {
        new Color(1f, 1f, 1f, 0.9f), new Color(0.35f, 1f, 0.35f, 0.95f), new Color(1f, 0.25f, 0.25f, 0.95f),
        new Color(1f, 0.85f, 0.2f, 0.95f), new Color(0.3f, 0.95f, 1f, 0.95f)
    };
    private static int nativeW, nativeH;

    public static readonly string[] QualityNames = { "DÜŞÜK", "ORTA", "YÜKSEK", "MAKS." };
    public static readonly string[] FrameRateNames = { "30 FPS", "60 FPS", "MAKS." };
    public static readonly string[] FirePresetNames = { "Tek Dokunuşla Nişangâh", "Nişan Almadan Atış", "Otomatik", "Kişisel" };
    public static readonly string[] GyroNames = { "KAPALI", "NİŞAN ALIRKEN", "HER ZAMAN" };

    public static void Load()
    {
        Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("zm_sens", 1f), 0.4f, 2f);
        AdsSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("zm_sens_ads", 0.8f), 0.3f, 2f);
        ScopeSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("zm_sens_scope", 0.6f), 0.3f, 2f);
        RotationMode = Mathf.Clamp(PlayerPrefs.GetInt("zm_rot_mode", 1), 0, 1);
        Acceleration = Mathf.Clamp(PlayerPrefs.GetInt("zm_rot_accel", 120), 0, 200);
        Gyro = Mathf.Clamp(PlayerPrefs.GetInt("zm_gyro", 0), 0, 2);
        GyroSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("zm_gyro_sens", 1f), 0.3f, 2.5f);

        AimAssist = PlayerPrefs.GetInt("zm_aim_assist", 1) == 1;
        FirePreset = Mathf.Clamp(PlayerPrefs.GetInt("zm_fire_preset", 0), 0, 3);
        for (int i = 0; i < CustomFire.Length; i++)
            CustomFire[i] = Mathf.Clamp(PlayerPrefs.GetInt("zm_fire_" + i, CustomFire[i]), 0, 1);
        LeftFireButton = PlayerPrefs.GetInt("zm_left_fire", 1) == 1;
        FireButtonLook = PlayerPrefs.GetInt("zm_fire_look", 1) == 1;
        FireButtonFollow = PlayerPrefs.GetInt("zm_fire_follow", 0) == 1;
        JoystickMode = Mathf.Clamp(PlayerPrefs.GetInt("zm_joystick", 1), 0, 1);
        ButtonOpacity = Mathf.Clamp(PlayerPrefs.GetFloat("zm_btn_alpha", 1f), 0.3f, 1f);

        Quality = Mathf.Clamp(PlayerPrefs.GetInt("zm_quality", DefaultQuality()), 0, 3);
        FrameRate = Mathf.Clamp(PlayerPrefs.GetInt("zm_fps", 1), 0, 2);
        AntiAliasing = PlayerPrefs.GetInt("zm_aa", 1) == 1;
        Shadows = PlayerPrefs.GetInt("zm_shadows", Quality == 0 ? 0 : 1) == 1;
        Bloom = PlayerPrefs.GetInt("zm_bloom", Quality == 0 ? 0 : 1) == 1;
        Volume = Mathf.Clamp01(PlayerPrefs.GetFloat("zm_volume", 1f));
        Fov = Mathf.Clamp(PlayerPrefs.GetInt("zm_fov", 70), 60, 90);
        ViewDistance = Mathf.Clamp(PlayerPrefs.GetInt("zm_view", Quality), 0, 3);
        RenderScale = Mathf.Clamp(PlayerPrefs.GetFloat("zm_render_scale", 1f), 0.5f, 1f);
        GrassDensity = Mathf.Clamp(PlayerPrefs.GetInt("zm_grass", Quality == 0 ? 0 : 2), 0, 3);
        CrosshairColor = Mathf.Clamp(PlayerPrefs.GetInt("zm_cross", 0), 0, CrosshairColors.Length - 1);
        DamageNumbers = PlayerPrefs.GetInt("zm_dmg_numbers", 1) == 1;
        Vibration = PlayerPrefs.GetInt("zm_vibration", 1) == 1;
        UiSounds = PlayerPrefs.GetInt("zm_ui_sounds", 1) == 1;
        CameraShake = PlayerPrefs.GetInt("zm_shake", 1) == 1;
        Loaded = true;
        Apply();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("zm_sens", Sensitivity);
        PlayerPrefs.SetFloat("zm_sens_ads", AdsSensitivity);
        PlayerPrefs.SetFloat("zm_sens_scope", ScopeSensitivity);
        PlayerPrefs.SetInt("zm_rot_mode", RotationMode);
        PlayerPrefs.SetInt("zm_rot_accel", Acceleration);
        PlayerPrefs.SetInt("zm_gyro", Gyro);
        PlayerPrefs.SetFloat("zm_gyro_sens", GyroSensitivity);
        PlayerPrefs.SetInt("zm_aim_assist", AimAssist ? 1 : 0);
        PlayerPrefs.SetInt("zm_fire_preset", FirePreset);
        for (int i = 0; i < CustomFire.Length; i++)
            PlayerPrefs.SetInt("zm_fire_" + i, CustomFire[i]);
        PlayerPrefs.SetInt("zm_left_fire", LeftFireButton ? 1 : 0);
        PlayerPrefs.SetInt("zm_fire_look", FireButtonLook ? 1 : 0);
        PlayerPrefs.SetInt("zm_fire_follow", FireButtonFollow ? 1 : 0);
        PlayerPrefs.SetInt("zm_joystick", JoystickMode);
        PlayerPrefs.SetFloat("zm_btn_alpha", ButtonOpacity);
        PlayerPrefs.SetInt("zm_quality", Quality);
        PlayerPrefs.SetInt("zm_fps", FrameRate);
        PlayerPrefs.SetInt("zm_aa", AntiAliasing ? 1 : 0);
        PlayerPrefs.SetInt("zm_shadows", Shadows ? 1 : 0);
        PlayerPrefs.SetInt("zm_bloom", Bloom ? 1 : 0);
        PlayerPrefs.SetFloat("zm_volume", Volume);
        PlayerPrefs.SetInt("zm_fov", Fov);
        PlayerPrefs.SetInt("zm_view", ViewDistance);
        PlayerPrefs.SetFloat("zm_render_scale", RenderScale);
        PlayerPrefs.SetInt("zm_grass", GrassDensity);
        PlayerPrefs.SetInt("zm_cross", CrosshairColor);
        PlayerPrefs.SetInt("zm_dmg_numbers", DamageNumbers ? 1 : 0);
        PlayerPrefs.SetInt("zm_vibration", Vibration ? 1 : 0);
        PlayerPrefs.SetInt("zm_ui_sounds", UiSounds ? 1 : 0);
        PlayerPrefs.SetInt("zm_shake", CameraShake ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
    }

    /// <summary>Back to the defaults for one settings page (0 basic, 1 controls, 2 graphics, 3 sensitivity).</summary>
    public static void ResetPage(int page)
    {
        switch (page)
        {
            case 0:
                AimAssist = true;
                FirePreset = 0;
                CrosshairColor = 0;
                DamageNumbers = true;
                CustomFire[0] = 0; CustomFire[1] = 0; CustomFire[2] = 1; CustomFire[3] = 0; CustomFire[4] = 1;
                break;
            case 1:
                LeftFireButton = true;
                FireButtonLook = true;
                FireButtonFollow = false;
                JoystickMode = 1;
                ButtonOpacity = 1f;
                Vibration = true;
                UiSounds = true;
                break;
            case 2:
                Quality = DefaultQuality();
                FrameRate = 1;
                AntiAliasing = true;
                Shadows = Quality > 0;
                Bloom = Quality > 0;
                Volume = 1f;
                ViewDistance = Quality == 0 ? 0 : 1;
                RenderScale = 1f;
                GrassDensity = Quality == 0 ? 0 : 2;
                break;
            default:
                Sensitivity = 1f;
                AdsSensitivity = 0.8f;
                ScopeSensitivity = 0.6f;
                RotationMode = 1;
                Acceleration = 120;
                Gyro = 0;
                GyroSensitivity = 1f;
                Fov = 70;
                CameraShake = true;
                break;
        }
        Save();
    }

    /// <summary>0 = tap the fire button to aim and shoot, 1 = hip fire, 2 = automatic fire.</summary>
    public static int FireModeFor(WeaponType type)
    {
        if (FirePreset == 3)
            return CustomFire[Mathf.Clamp((int)type, 0, CustomFire.Length - 1)];
        return FirePreset;
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
                QualitySettings.shadowDistance = 30f;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.lodBias = 0.6f;
                break;
            case 2:
                QualitySettings.shadowDistance = 70f;
                QualitySettings.shadowResolution = ShadowResolution.High;
                QualitySettings.lodBias = 1.5f;
                break;
            case 3:
                QualitySettings.shadowDistance = 90f;
                QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                QualitySettings.lodBias = 2f;
                break;
            default:
                QualitySettings.shadowDistance = 45f;
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                QualitySettings.lodBias = 1f;
                break;
        }
        QualitySettings.shadows = !Shadows ? ShadowQuality.Disable : (Quality >= 2 ? ShadowQuality.All : ShadowQuality.HardOnly);
        QualitySettings.shadowCascades = Quality >= 2 ? 2 : 1;
        QualitySettings.anisotropicFiltering = Quality == 0 ? AnisotropicFiltering.Disable : AnisotropicFiltering.ForceEnable;
        QualitySettings.globalTextureMipmapLimit = 0;   // full-resolution textures
        QualitySettings.skinWeights = Quality == 0 ? SkinWeights.TwoBones : SkinWeights.FourBones;
        QualitySettings.softParticles = false;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = FrameRate == 0 ? 30 : (FrameRate == 1 ? 60 : 120);
        Grass.SetQuality(GrassDensity);
        QualitySettings.realtimeReflectionProbes = Quality > 0;

        // Resolution scale (sharper vs. faster), relative to the phone's native screen.
        if (nativeW == 0)
        {
            // The game's own full-size window (landscape), before any scaling.
            nativeW = Mathf.Max(Screen.width, Screen.height);
            nativeH = Mathf.Min(Screen.width, Screen.height);
        }
        if (nativeW > 0 && Application.isMobilePlatform)
        {
            int w = Mathf.RoundToInt(nativeW * RenderScale), h = Mathf.RoundToInt(nativeH * RenderScale);
            if (Mathf.Abs(Mathf.Max(Screen.width, Screen.height) - w) > 2)
                Screen.SetResolution(w, h, true);
        }

        float fogEnd = ViewDistance == 0 ? 200f : (ViewDistance == 1 ? 320f : (ViewDistance == 2 ? 430f : 560f));
        RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.fogStartDistance = fogEnd * 0.3f;
        var cam = Camera.main;
        if (cam != null)
            cam.farClipPlane = fogEnd + 150f;

        var sun = RenderSettings.sun;
        if (sun != null)
            sun.shadows = !Shadows ? LightShadows.None : (Quality >= 2 ? LightShadows.Soft : LightShadows.Hard);

        PostFx.Apply(Quality, AntiAliasing, Bloom);
        World.ApplyQuality(Quality);
        if (TouchControls.Instance != null)
            TouchControls.Instance.ApplySettings();
    }
}
