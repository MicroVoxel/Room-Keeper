using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SettingsController : MonoBehaviour
{
    #region Inspector
    [Header("UI References")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    
    [Header("BGM Settings")]
    [SerializeField] private Button bgmButton;
    [SerializeField] private Image bgmIcon;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;
    [SerializeField] private AudioSource bgmSource;
    #endregion

    private const string PrefQuality = "quality_level";
    private const string PrefBgm = "bgm_enabled";

    private bool _isBgmOn = true;

    #region Unity Events
    private void Start()
    {
        if (settingsPanel != null) 
            settingsPanel.SetActive(false);

        SetupQualityDropdown();
        LoadQualitySetting();
        
        LoadBgmSetting();
        SetupBgmButton();
    }
    #endregion

    #region Quality Settings
    private void SetupQualityDropdown()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();

        var options = new System.Collections.Generic.List<string> { "Low", "Medium", "High" };
        qualityDropdown.AddOptions(options);

        qualityDropdown.onValueChanged.RemoveAllListeners();
        qualityDropdown.onValueChanged.AddListener(OnQualityDropdownChanged);
    }

    private void LoadQualitySetting()
    {
        int saved = PlayerPrefs.GetInt(PrefQuality, 1);
        ApplyQuality(saved);

        if (qualityDropdown != null)
        {
            qualityDropdown.value = saved;
            qualityDropdown.RefreshShownValue();
        }
    }

    private void OnQualityDropdownChanged(int index)
    {
        ApplyQuality(index);
        PlayerPrefs.SetInt(PrefQuality, index);
        PlayerPrefs.Save();
        
        Debug.Log($"Quality changed to index {index}");
    }

    private void ApplyQuality(int index)
    {
        int maxIndex = QualitySettings.names.Length - 1;
        index = Mathf.Clamp(index, 0, maxIndex);

        QualitySettings.SetQualityLevel(index, true);
    }
    #endregion
    
    #region BGM Settings
    private void SetupBgmButton()
    {
        if (bgmButton == null) return;
        bgmButton.onClick.RemoveAllListeners();
        bgmButton.onClick.AddListener(OnBgmButtonClicked);
    }

    private void LoadBgmSetting()
    {
        int saved = PlayerPrefs.GetInt(PrefBgm, 1); // 1 = เปิด, 0 = ปิด
        _isBgmOn = saved == 1;
        ApplyBgm(_isBgmOn);
        UpdateBgmIcon();
    }

    private void OnBgmButtonClicked()
    {
        _isBgmOn = !_isBgmOn;
        ApplyBgm(_isBgmOn);
        UpdateBgmIcon();

        PlayerPrefs.SetInt(PrefBgm, _isBgmOn ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"BGM {(_isBgmOn ? "enabled" : "muted")}");
    }

    private void ApplyBgm(bool enabled)
    {
        if (bgmSource == null) return;
    
        if (enabled)
        {
            if (!bgmSource.isPlaying)
                bgmSource.Play();
        }
        else
        {
            bgmSource.Pause();
        }
    }

    private void UpdateBgmIcon()
    {
        if (bgmIcon != null)
            bgmIcon.sprite = _isBgmOn ? musicOnSprite : musicOffSprite;
        else
            Debug.LogWarning("BGM Icon not assigned in Inspector.");
    }
    #endregion

    #region Panel Controls
    public void ShowSettingsPanel()
    {
        if (settingsPanel != null) 
            settingsPanel.SetActive(true);
    }

    public void HideSettingsPanel()
    {
        if (settingsPanel != null) 
            settingsPanel.SetActive(false);
    }
    #endregion
}
