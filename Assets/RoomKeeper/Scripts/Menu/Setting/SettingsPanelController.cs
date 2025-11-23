using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    [Header("Tabs Content")]
    [SerializeField] private GameObject audioContent;
    [SerializeField] private GameObject videoContent;

    [Header("Tab Buttons")]
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button videoTabButton;

    [Header("Button Colors")]
    [SerializeField] private Color activeTabColor = Color.white;
    [SerializeField] private Color inactiveTabColor = Color.gray;

    [Header("Navigation")]
    [SerializeField] private Button closeButton;

    private void Start()
    {
        SetupButtons();
    }

    // เรียกทุกครั้งที่เปิดหน้า Setting เพื่อ Reset กลับมาหน้า Audio เสมอ
    private void OnEnable()
    {
        ShowAudioTab();
    }

    private void SetupButtons()
    {
        if (audioTabButton != null) audioTabButton.onClick.AddListener(ShowAudioTab);
        if (videoTabButton != null) videoTabButton.onClick.AddListener(ShowVideoTab);

        // [FIX] เช็คก่อนว่ามี GameUIManager หรือไม่ (คืออยู่ในฉากเล่นเกม)
        // ถ้ามี -> ให้ SettingsPanel คุมปุ่ม Close เอง
        // ถ้าไม่มี (อยู่หน้า MainMenu) -> ไม่ต้องทำอะไร ปล่อยให้ MainMenuController เป็นคนคุมปุ่ม Close
        if (closeButton != null && GameUIManager.Instance != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                GameUIManager.Instance.OnToggleSettings();
            });
        }
    }

    private void ShowAudioTab()
    {
        if (audioContent) audioContent.SetActive(true);
        if (videoContent) videoContent.SetActive(false);
        UpdateButtonColors(audioTabButton, videoTabButton);
    }

    private void ShowVideoTab()
    {
        if (audioContent) audioContent.SetActive(false);
        if (videoContent) videoContent.SetActive(true);
        UpdateButtonColors(videoTabButton, audioTabButton);
    }

    private void UpdateButtonColors(Button activeBtn, Button inactiveBtn)
    {
        if (activeBtn != null && activeBtn.image != null)
            activeBtn.image.color = activeTabColor;

        if (inactiveBtn != null && inactiveBtn.image != null)
            inactiveBtn.image.color = inactiveTabColor;
    }
}