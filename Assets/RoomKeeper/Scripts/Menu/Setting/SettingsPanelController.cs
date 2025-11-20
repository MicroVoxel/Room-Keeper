using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    [Header("Main Panel")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Tabs Content")]
    [SerializeField] private GameObject audioContent;
    [SerializeField] private GameObject videoContent;

    [Header("Tab Buttons")]
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button videoTabButton;

    [Header("Button Colors")]
    [SerializeField] private Color activeTabColor = Color.white;
    [SerializeField] private Color inactiveTabColor = Color.gray;

    [Header("Close Button")]
    [SerializeField] private Button closeButton;

    private void Start()
    {
        SetupButtons();

        // Default State
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // เริ่มต้นที่ Audio Tab เสมอ
        ShowAudioTab();
    }

    private void SetupButtons()
    {
        // Safe Check: เช็คว่าปุ่มมีของไหมก่อน AddListener
        if (audioTabButton != null) audioTabButton.onClick.AddListener(ShowAudioTab);
        if (videoTabButton != null) videoTabButton.onClick.AddListener(ShowVideoTab);
        if (closeButton != null) closeButton.onClick.AddListener(HideSettingsPanel);
    }

    public void ShowSettingsPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);

        // Optional: Pause Game
        // Time.timeScale = 0f; 
    }

    public void HideSettingsPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Optional: Resume Game
        // Time.timeScale = 1f;
    }

    public void ToggleSettingsPanel()
    {
        if (settingsPanel != null)
        {
            if (settingsPanel.activeSelf) HideSettingsPanel();
            else ShowSettingsPanel();
        }
    }

    private void ShowAudioTab()
    {
        if (audioContent != null) audioContent.SetActive(true);
        if (videoContent != null) videoContent.SetActive(false);

        UpdateButtonColors(audioTabButton, videoTabButton);
    }

    private void ShowVideoTab()
    {
        if (audioContent != null) audioContent.SetActive(false);
        if (videoContent != null) videoContent.SetActive(true);

        UpdateButtonColors(videoTabButton, audioTabButton);
    }

    private void UpdateButtonColors(Button activeBtn, Button inactiveBtn)
    {
        // Safe Check: เช็คทั้งตัวปุ่ม และ Image component ของปุ่ม
        if (activeBtn != null && activeBtn.image != null)
            activeBtn.image.color = activeTabColor;

        if (inactiveBtn != null && inactiveBtn.image != null)
            inactiveBtn.image.color = inactiveTabColor;
    }
}