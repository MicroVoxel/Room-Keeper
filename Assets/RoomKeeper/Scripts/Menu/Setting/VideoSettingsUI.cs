using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class VideoSettingsUI : MonoBehaviour
{
    [Header("Quality Settings (Optional)")]
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("Performance Settings (Optional)")]
    [SerializeField] private TMP_Dropdown frameRateDropdown;

    // Keys for PlayerPrefs
    private const string KEY_QUALITY = "Setting_Quality";
    private const string KEY_FRAMERATE = "Setting_FrameRate";

    private void Start()
    {
        SetupQualityDropdown();
        SetupFrameRateDropdown();

        LoadSettings();
    }

    // ---------------- Quality ----------------

    private void SetupQualityDropdown()
    {
        // Safe Check: ถ้าไม่ได้ลาก Dropdown มา ก็ออกจากฟังก์ชันเลย
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();
        List<string> options = new List<string>(QualitySettings.names);
        qualityDropdown.AddOptions(options);

        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
    }

    private void OnQualityChanged(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
        PlayerPrefs.SetInt(KEY_QUALITY, index);
        PlayerPrefs.Save();
    }

    // ---------------- Frame Rate (Mobile Essential) ----------------

    private void SetupFrameRateDropdown()
    {
        // Safe Check
        if (frameRateDropdown == null) return;

        frameRateDropdown.ClearOptions();
        List<string> options = new List<string> { "30 FPS (Save Battery)", "60 FPS (Smooth)" };
        frameRateDropdown.AddOptions(options);

        frameRateDropdown.onValueChanged.AddListener(OnFrameRateChanged);
    }

    private void OnFrameRateChanged(int index)
    {
        int targetFPS = (index == 0) ? 30 : 60;
        SetFrameRate(targetFPS);
    }

    private void SetFrameRate(int targetFPS)
    {
        Application.targetFrameRate = targetFPS;

        // ปิด VSync เพื่อให้ targetFrameRate ทำงานได้บน Mobile
        QualitySettings.vSyncCount = 0;

        PlayerPrefs.SetInt(KEY_FRAMERATE, targetFPS);
        PlayerPrefs.Save();
    }

    // ---------------- Load Logic ----------------

    private void LoadSettings()
    {
        // 1. Load Quality
        int currentQuality = QualitySettings.GetQualityLevel();
        int savedQuality = PlayerPrefs.GetInt(KEY_QUALITY, currentQuality);

        // Safe Check: เช็คก่อนเรียกใช้
        if (qualityDropdown != null)
        {
            qualityDropdown.value = savedQuality;
            qualityDropdown.RefreshShownValue();
        }

        if (currentQuality != savedQuality)
        {
            QualitySettings.SetQualityLevel(savedQuality, true);
        }

        // 2. Load Frame Rate
        int savedFPS = PlayerPrefs.GetInt(KEY_FRAMERATE, 60);
        int dropdownIndex = (savedFPS == 60) ? 1 : 0;

        // Safe Check
        if (frameRateDropdown != null)
        {
            frameRateDropdown.value = dropdownIndex;
            frameRateDropdown.RefreshShownValue();
        }

        SetFrameRate(savedFPS);
    }
}