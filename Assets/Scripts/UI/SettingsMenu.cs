using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    private const string VolumeKey = "MasterVolume";
    private const string MuteKey = "MasterMuted";

    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Toggle soundToggle;
    [SerializeField] private Slider volumeSlider;

    // Áp dụng âm lượng đã lưu ngay khi game bắt đầu, kể cả khi Pause Menu đang tắt.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedAudio()
    {
        Apply(PlayerPrefs.GetFloat(VolumeKey, 1f), PlayerPrefs.GetInt(MuteKey, 0) == 1);
    }

    private static void Apply(float volume, bool muted)
    {
        AudioListener.volume = muted ? 0f : volume;
    }

    private void Awake()
    {
        if (soundToggle != null) soundToggle.onValueChanged.AddListener(OnSoundToggled);
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnEnable()
    {
        // Mỗi lần mở pause menu: đóng Settings và đồng bộ giao diện với giá trị đã lưu.
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (soundToggle != null) soundToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(MuteKey, 0) == 0);
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumeKey, 1f));
    }

    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void OnSoundToggled(bool soundOn)
    {
        PlayerPrefs.SetInt(MuteKey, soundOn ? 0 : 1);
        PlayerPrefs.Save();
        Apply(PlayerPrefs.GetFloat(VolumeKey, 1f), !soundOn);
    }

    private void OnVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();
        Apply(value, PlayerPrefs.GetInt(MuteKey, 0) == 1);
    }
}