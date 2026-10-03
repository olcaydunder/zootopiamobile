using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Camera post-processing (Post Processing Stack v2) and anti-aliasing, scaled by the quality setting:
/// Low = FXAA only. Medium = MSAA 2x, bloom, colour grading, vignette.
/// High = MSAA 4x, the same with stronger bloom and ambient occlusion.
/// </summary>
public static class PostFx
{
    private static PostProcessLayer layer;
    private static PostProcessVolume volume;
    private static Bloom bloom;
    private static ColorGrading grading;
    private static Vignette vignette;
    private static AmbientOcclusion ao;
    private static bool failed;

    public static bool Failed { get { return failed; } }

    public static void Setup(Camera cam)
    {
        if (cam == null || layer != null || failed)
            return;

        var holder = Resources.Load<PostFxResourcesHolder>("PostFxResources");
        if (holder == null || holder.resources == null)
        {
            failed = true;
            Debug.LogWarning("[PostFx] Post-processing resources missing; running without post-processing.");
            return;
        }

        try
        {
            // Add while inactive so the layer has its resources before OnEnable runs.
            var go = cam.gameObject;
            bool wasActive = go.activeSelf;
            go.SetActive(false);
            layer = go.AddComponent<PostProcessLayer>();
            layer.Init(holder.resources);
            layer.volumeTrigger = cam.transform;
            layer.volumeLayer = 1 << 0;
            layer.stopNaNPropagation = false;
            go.SetActive(wasActive);

            bloom = ScriptableObject.CreateInstance<Bloom>();
            bloom.enabled.Override(true);
            bloom.threshold.Override(0.88f);
            bloom.softKnee.Override(0.6f);
            bloom.diffusion.Override(6f);
            bloom.fastMode.Override(true);

            grading = ScriptableObject.CreateInstance<ColorGrading>();
            grading.enabled.Override(true);
            grading.gradingMode.Override(GradingMode.LowDefinitionRange);
            grading.temperature.Override(6f);
            grading.saturation.Override(12f);
            grading.contrast.Override(14f);
            grading.brightness.Override(2f);

            vignette = ScriptableObject.CreateInstance<Vignette>();
            vignette.enabled.Override(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.4f);

            ao = ScriptableObject.CreateInstance<AmbientOcclusion>();
            ao.enabled.Override(false);
            ao.mode.Override(AmbientOcclusionMode.ScalableAmbientObscurance);
            ao.intensity.Override(0.6f);
            ao.radius.Override(0.6f);
            ao.quality.Override(AmbientOcclusionQuality.Low);

            volume = PostProcessManager.instance.QuickVolume(0, 100f, bloom, grading, vignette, ao);
            Object.DontDestroyOnLoad(volume.gameObject);
        }
        catch (System.Exception e)
        {
            failed = true;
            Debug.LogWarning("[PostFx] Disabled: " + e.Message);
            if (layer != null)
                Object.Destroy(layer);
            layer = null;
        }

        Apply(GameSettings.Quality, GameSettings.AntiAliasing, GameSettings.Bloom);
    }

    /// <summary>quality 0 low .. 3 max; antiAliasing = "Düzgünleştirme", bloom = "Parlaklık".</summary>
    public static void Apply(int quality, bool antiAliasing, bool bloomOn)
    {
        var cam = Camera.main;
        bool msaa = antiAliasing && quality > 0;
        if (cam != null)
        {
            cam.allowMSAA = msaa;
            cam.allowHDR = false;
        }
        QualitySettings.antiAliasing = !msaa ? 0 : (quality == 1 ? 2 : 4);

        if (layer == null)
            return;

        // Low quality (or MSAA off): cheap FXAA keeps edges smooth.
        bool effects = quality > 0;
        layer.enabled = effects || (antiAliasing && !msaa);
        layer.antialiasingMode = antiAliasing && !msaa ? PostProcessLayer.Antialiasing.FastApproximateAntialiasing : PostProcessLayer.Antialiasing.None;
        layer.fastApproximateAntialiasing.fastMode = true;
        if (volume != null)
            volume.weight = effects ? 1f : 0f;

        if (bloom != null)
        {
            bloom.enabled.Override(bloomOn);
            bloom.intensity.Override(quality >= 2 ? 1.6f : 1.1f);
            bloom.fastMode.Override(quality < 2);
        }
        if (ao != null)
            ao.enabled.Override(quality >= 2);
        if (ao != null)
            ao.quality.Override(quality >= 3 ? AmbientOcclusionQuality.Medium : AmbientOcclusionQuality.Low);
        if (cam != null)
        {
            if (quality >= 2)
                cam.depthTextureMode |= DepthTextureMode.Depth;
            else
                cam.depthTextureMode &= ~DepthTextureMode.Depth;
        }
    }
}
