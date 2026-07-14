using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    [Header("Speed")]
    [Tooltip("The Speed slider offsets this bird's thrust relative to whatever it was set to at startup.")]
    [SerializeField] private FlightController flightController;

    [Header("Graphics (this project currently only has 2 Quality levels configured: 0 = Mobile, 1 = PC)")]
    [SerializeField] private int lowQualityLevel = 0;
    [SerializeField] private int mediumQualityLevel = 0;
    [SerializeField] private int highQualityLevel = 1;

    private float baseThrust;
    private float lastNonMutedVolume = 1f;

    private void Awake()
    {
        if (flightController != null)
            baseThrust = flightController.thrust;

        lastNonMutedVolume = AudioListener.volume;
    }

    // Wired to the Volume slider's OnValueChanged (0-100).
    public void OnVolumeChanged(float sliderValue)
    {
        lastNonMutedVolume = sliderValue / 100f;
        AudioListener.volume = lastNonMutedVolume;
    }

    // Wired to the Mute toggle's OnValueChanged.
    public void OnMuteChanged(bool isMuted)
    {
        AudioListener.volume = isMuted ? 0f : lastNonMutedVolume;
    }

    // Wired to the Speed slider's OnValueChanged (-50 to +50).
    public void OnSpeedChanged(float offset)
    {
        if (flightController == null) return;
        flightController.thrust = baseThrust + offset;
    }

    // Wired to the three Graphics buttons.
    public void SetGraphicsLow() => QualitySettings.SetQualityLevel(lowQualityLevel, true);
    public void SetGraphicsMedium() => QualitySettings.SetQualityLevel(mediumQualityLevel, true);
    public void SetGraphicsHigh() => QualitySettings.SetQualityLevel(highQualityLevel, true);
}
