using UnityEngine;
using UnityEngine.UI;

public class SoundSettingsUI : MonoBehaviour
{
    [Header("Sliders (Optional - Can be Null)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Toggles & Icons (Optional - Can be Null)")]
    [SerializeField] private Button musicToggleButton;
    [SerializeField] private Image musicIcon;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;

    [SerializeField] private Button sfxToggleButton;
    [SerializeField] private Image sfxIcon;
    [SerializeField] private Sprite sfxOnSprite;
    [SerializeField] private Sprite sfxOffSprite;

    // Keys for PlayerPrefs
    private const string KEY_MASTER = "Vol_Master";
    private const string KEY_MUSIC = "Vol_Music";
    private const string KEY_SFX = "Vol_SFX";
    private const string KEY_MUTE_MUSIC = "Mute_Music";
    private const string KEY_MUTE_SFX = "Mute_SFX";

    // Mixer Parameters (ต้องตรงกับที่ตั้งใน AudioMixer Group)
    private const string MIXER_MASTER = "MasterVolume";
    private const string MIXER_MUSIC = "MusicVolume";
    private const string MIXER_SFX = "SFXVolume";

    private bool _isMusicMuted;
    private bool _isSfxMuted;

    private void Start()
    {
        // โหลดค่าที่บันทึกไว้ และเริ่มการทำงาน
        LoadSettings();
        SetupListeners();
    }

    private void SetupListeners()
    {
        // Safe Check: ตรวจสอบ Component ก่อน AddListener เพื่อป้องกัน Error หากไม่ได้ลากใส่ Inspector
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterValChanged);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicValChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSFXValChanged);

        if (musicToggleButton != null) musicToggleButton.onClick.AddListener(OnMusicToggle);
        if (sfxToggleButton != null) sfxToggleButton.onClick.AddListener(OnSFXToggle);
    }

    private void LoadSettings()
    {
        float masterVol = PlayerPrefs.GetFloat(KEY_MASTER, 0.75f);
        float musicVol = PlayerPrefs.GetFloat(KEY_MUSIC, 0.75f);
        float sfxVol = PlayerPrefs.GetFloat(KEY_SFX, 0.75f);

        // Update UI Elements
        if (masterSlider != null) masterSlider.value = masterVol;
        if (musicSlider != null) musicSlider.value = musicVol;
        if (sfxSlider != null) sfxSlider.value = sfxVol;

        _isMusicMuted = PlayerPrefs.GetInt(KEY_MUTE_MUSIC, 0) == 1;
        _isSfxMuted = PlayerPrefs.GetInt(KEY_MUTE_SFX, 0) == 1;

        UpdateIcons();

        // ส่งค่าไปที่ Mixer ผ่าน AudioManager
        // หมายเหตุ: ต้องมั่นใจว่า AudioManager init เสร็จแล้ว (ซึ่งปกติ Script Order จะทำงานถูกต้องถ้า AudioManager เป็น DontDestroyOnLoad)
        SetMixerVolume(MIXER_MASTER, masterVol);
        ApplyMuteStates();
    }

    // ------------------ Sliders Callbacks ------------------

    private void OnMasterValChanged(float val)
    {
        SetMixerVolume(MIXER_MASTER, val);
        PlayerPrefs.SetFloat(KEY_MASTER, val);
    }

    private void OnMusicValChanged(float val)
    {
        // ถ้าเลื่อน Slider แสดงว่าผู้เล่นต้องการเปิดเสียง -> ยกเลิก Mute อัตโนมัติ
        if (_isMusicMuted)
        {
            _isMusicMuted = false;
            UpdateIcons();
            PlayerPrefs.SetInt(KEY_MUTE_MUSIC, 0);
        }

        SetMixerVolume(MIXER_MUSIC, val);
        PlayerPrefs.SetFloat(KEY_MUSIC, val);
    }

    private void OnSFXValChanged(float val)
    {
        if (_isSfxMuted)
        {
            _isSfxMuted = false;
            UpdateIcons();
            PlayerPrefs.SetInt(KEY_MUTE_SFX, 0);
        }

        SetMixerVolume(MIXER_SFX, val);
        PlayerPrefs.SetFloat(KEY_SFX, val);
    }

    // ------------------ Toggles Callbacks ------------------

    private void OnMusicToggle()
    {
        _isMusicMuted = !_isMusicMuted;
        ApplyMuteStates();
        UpdateIcons();
        PlayerPrefs.SetInt(KEY_MUTE_MUSIC, _isMusicMuted ? 1 : 0);
    }

    private void OnSFXToggle()
    {
        _isSfxMuted = !_isSfxMuted;
        ApplyMuteStates();
        UpdateIcons();
        PlayerPrefs.SetInt(KEY_MUTE_SFX, _isSfxMuted ? 1 : 0);
    }

    // ------------------ Core Logic ------------------

    private void ApplyMuteStates()
    {
        // ดึงค่าปัจจุบันจาก Slider หรือถ้าไม่มี Slider ให้ดึงจาก Prefs
        float currentMusicVal = (musicSlider != null) ? musicSlider.value : PlayerPrefs.GetFloat(KEY_MUSIC, 0.75f);
        float currentSfxVal = (sfxSlider != null) ? sfxSlider.value : PlayerPrefs.GetFloat(KEY_SFX, 0.75f);

        // ถ้า Mute ให้ส่ง 0 (ซึ่งจะแปลงเป็น -80dB) ถ้าไม่ Mute ให้ส่งค่า Volume เดิม
        SetMixerVolume(MIXER_MUSIC, _isMusicMuted ? 0 : currentMusicVal);
        SetMixerVolume(MIXER_SFX, _isSfxMuted ? 0 : currentSfxVal);
    }

    private void SetMixerVolume(string paramName, float linearValue)
    {
        if (AudioManager.Instance == null) return;

        // แปลง Linear (0-1) เป็น Decibel (-80 ถึง 0)
        // 0.0001f คือค่าต่ำสุดเพื่อป้องกัน Log(0) = -Infinity
        float dbValue = linearValue <= 0.0001f ? -80f : Mathf.Log10(linearValue) * 20;
        AudioManager.Instance.SetMixerVolume(paramName, dbValue);
    }

    private void UpdateIcons()
    {
        if (musicIcon != null)
        {
            Sprite targetSprite = _isMusicMuted ? musicOffSprite : musicOnSprite;
            if (targetSprite != null) musicIcon.sprite = targetSprite;
        }

        if (sfxIcon != null)
        {
            Sprite targetSprite = _isSfxMuted ? sfxOffSprite : sfxOnSprite;
            if (targetSprite != null) sfxIcon.sprite = targetSprite;
        }
    }

    private void OnDisable()
    {
        // บันทึกค่าลง Disk เมื่อปิดหน้านี้
        PlayerPrefs.Save();
    }
}