using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public float musicVolume = 0.8f;
    public float sfxVolume = 0.8f;
    public int qualityLevel = 1;
    public bool vibration = true;

    public void ApplySettings()
    {
        AudioListener.volume = musicVolume;
        QualitySettings.SetQualityLevel(qualityLevel, true);
        if (vibration)
            Handheld.Vibrate();
    }

    public void SetGraphics(int level)
    {
        qualityLevel = Mathf.Clamp(level, 0, 5);
        ApplySettings();
    }
}
