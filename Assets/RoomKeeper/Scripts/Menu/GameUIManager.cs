using UnityEngine;
using TMPro;
using UnityEngine.UI;

public enum UIState
{
    Gameplay,   // โชว์ HUD, ซ่อน BG
    Settings,   // โชว์ Setting, โชว์ BG
    Victory,    // โชว์ Victory, โชว์ BG
    GameOver,   // โชว์ GameOver, โชว์ BG
    Ads         // ซ่อนทุกอย่าง
}

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject backgroundPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("HUD Elements")]
    [SerializeField] private Slider mainProgressBar;
    [SerializeField] private TextMeshProUGUI timeText;
    [Tooltip("ใส่ภาพดาวใน HUD (เรียงจากดาว 1 -> 3)")]
    [SerializeField] private GameObject[] starFills;

    [Header("Victory Elements")]
    [Tooltip("ใส่ภาพดาวในหน้า Victory (เรียงจากดาว 1 -> 3)")]
    [SerializeField] private GameObject[] victoryStars;
    [SerializeField] private TextMeshProUGUI victoryTimeText;

    [Header("Game Over Elements")]
    [Tooltip("ลากปุ่มที่จะใช้กดดูโฆษณาเพื่อชุบชีวิตมาใส่ตรงนี้")]
    [SerializeField] private Button reviveAdsButton;

    private UIState _currentState;
    private UIState _lastStateBeforeAds;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        SwitchUIState(UIState.Gameplay);

        if (reviveAdsButton != null)
        {
            reviveAdsButton.onClick.RemoveAllListeners();
            reviveAdsButton.onClick.AddListener(() =>
            {
                if (GameCoreManager.Instance != null)
                {
                    GameCoreManager.Instance.OnClickReviveWithAd();
                }
            });
        }
    }

    public void SwitchUIState(UIState newState)
    {
        // 1. จำสถานะก่อนหน้าถ้าเป็น Ads
        if (newState == UIState.Ads && _currentState != UIState.Ads)
        {
            _lastStateBeforeAds = _currentState;
        }

        _currentState = newState;

        // 2. ถ้าไม่ใช่หน้า Gameplay ให้สั่งปิด Task ที่ค้างอยู่ทิ้งให้หมด
        if (newState != UIState.Gameplay)
        {
            if (GameCoreManager.Instance != null)
            {
                GameCoreManager.Instance.CloseAllOpenTasks();
            }
        }

        // 3. จัดการ Background 
        bool showBG = newState != UIState.Gameplay;
        if (backgroundPanel) backgroundPanel.SetActive(showBG);

        // 4. จัดการ Panels หลัก
        if (hudPanel) hudPanel.SetActive(newState == UIState.Gameplay);
        if (settingsPanel) settingsPanel.SetActive(newState == UIState.Settings);
        if (victoryPanel) victoryPanel.SetActive(newState == UIState.Victory);
        if (gameOverPanel) gameOverPanel.SetActive(newState == UIState.GameOver);

        // 5. จัดการเวลาในเกม
        if (newState != UIState.Ads)
        {
            Time.timeScale = (newState == UIState.Gameplay) ? 1f : 0f;
        }

        // 6. จัดการปุ่ม Revive
        if (newState == UIState.GameOver && reviveAdsButton != null)
        {
            reviveAdsButton.interactable = true;
        }
    }

    public void ResumeFromAds(bool rewardClaimed)
    {
        if (rewardClaimed)
        {
            // ReviveGame() จะสั่ง SwitchUIState(Gameplay) ให้เอง
        }
        else
        {
            SwitchUIState(_lastStateBeforeAds);
        }
    }

    public void OnToggleSettings()
    {
        if (_currentState == UIState.Ads) return;

        bool isOpening = !settingsPanel.activeSelf;
        SwitchUIState(isOpening ? UIState.Settings : UIState.Gameplay);
    }

    public void UpdateHUD(float progress, float time, int starCount)
    {
        if (mainProgressBar) mainProgressBar.value = progress;

        if (timeText)
        {
            System.TimeSpan t = System.TimeSpan.FromSeconds(Mathf.Max(0, time));
            timeText.text = string.Format("{0:0}:{1:00}", t.Minutes, t.Seconds);
        }

        // --- FIXED LOGIC ---
        // เปลี่ยนเงื่อนไขเป็น i < starCount (เพราะ Array เริ่มที่ 0)
        // ตัวอย่าง: ได้ 1 ดาว (starCount=1) -> i=0 (<1 จริง เปิด), i=1 (<1 เท็จ ปิด) -> ถูกต้อง
        for (int i = 0; i < starFills.Length; i++)
        {
            if (starFills[i]) starFills[i].SetActive(i < starCount);
        }
    }

    public void SetupVictoryScreen(int starsEarned, float timeLeft)
    {
        SwitchUIState(UIState.Victory);

        // --- FIXED LOGIC ---
        for (int i = 0; i < victoryStars.Length; i++)
        {
            if (victoryStars[i]) victoryStars[i].SetActive(i < starsEarned);
        }

        if (victoryTimeText)
        {
            System.TimeSpan t = System.TimeSpan.FromSeconds(Mathf.Max(0, timeLeft));
            victoryTimeText.text = string.Format("{0:0}:{1:00}", t.Minutes, t.Seconds);
        }
    }

    public void ShowGameOver()
    {
        SwitchUIState(UIState.GameOver);
    }
}