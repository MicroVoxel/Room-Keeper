using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // เพิ่มเพื่อใช้ Reload Scene

public class LevelProgressManager : MonoBehaviour
{
    public static LevelProgressManager Instance { get; private set; }
    [SerializeField] private bool enableDebug = true; // เปิดไว้เทส

    private const string KEY_LEVEL_STARS = "Level_{0}_Stars";
    private const string KEY_LEVEL_UNLOCKED = "Level_{0}_Unlocked";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    private void Start()
    {
        UnlockLevel(1); // Unlock เลเวล 1 เสมอ
    }

#if UNITY_EDITOR
    private void Update()
    {
        // กด Ctrl + Shift + R เพื่อรีเซ็ต
        if (enableDebug && Keyboard.current != null)
        {
            if ((Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed) &&
                (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed) &&
                Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetAllProgress();
            }
        }
    }
#endif

    // -------------------- Save / Load --------------------

    public void SaveLevelResult(int levelID, int starsEarned)
    {
        starsEarned = Mathf.Clamp(starsEarned, 0, 3);
        int current = GetLevelStars(levelID);

        if (starsEarned > current)
            PlayerPrefs.SetInt(string.Format(KEY_LEVEL_STARS, levelID), starsEarned);

        if (starsEarned > 0)
            UnlockLevel(levelID + 1);

        PlayerPrefs.Save();
    }

    public int GetLevelStars(int levelID)
    {
        return PlayerPrefs.GetInt(string.Format(KEY_LEVEL_STARS, levelID), 0);
    }

    public void UnlockLevel(int levelID)
    {
        PlayerPrefs.SetInt(string.Format(KEY_LEVEL_UNLOCKED, levelID), 1);
        PlayerPrefs.Save();
    }

    public bool IsLevelUnlocked(int levelID)
    {
        if (levelID == 1) return true;
        return PlayerPrefs.GetInt(string.Format(KEY_LEVEL_UNLOCKED, levelID), 0) == 1;
    }

    [ContextMenu("Reset All Progress (Levels + IAP)")]
    public void ResetAllProgress()
    {
        // 1. ล้างข้อมูลทั้งหมด (รวมถึง VIP และ Remove Ads ที่เก็บใน PlayerPrefs)
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // 2. ปลดล็อคเลเวล 1 ใหม่อีกครั้ง (เพราะโดนลบไปแล้ว)
        UnlockLevel(1);

        // 3. จัดการ IAP และ Ads ให้กลับมาสภาพเดิม
        if (AdsManager.Instance != null)
        {
            // บังคับให้โหลดแบนเนอร์กลับมา (เพราะสถานะ Remove Ads หายไปแล้ว)
            AdsManager.Instance.LoadBannerAds();
        }

        Debug.LogWarning("🧹 Reset All Progress Complete: Levels locked, IAP local data cleared.");

        // 4. รีโหลด Scene ปัจจุบัน เพื่อให้ UI (ปุ่มซื้อของ) อัปเดตสถานะทันที
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}