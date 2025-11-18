using UnityEngine;

/// <summary>
/// LevelProgressManager - Unity 6.2 Optimized
/// จัดการระบบ Save/Load สำหรับด่าน (Level Unlock & Star Rating)
/// ใช้ PlayerPrefs เพื่อประสิทธิภาพสูงสุดบน Mobile
/// </summary>
public class LevelProgressManager : MonoBehaviour
{
    public static LevelProgressManager Instance { get; private set; }

    private const string KEY_LEVEL_STARS = "Level_{0}_Stars";     // Key สำหรับเก็บดาว (เช่น Level_1_Stars)
    private const string KEY_LEVEL_UNLOCKED = "Level_{0}_Unlocked"; // Key สำหรับเก็บสถานะปลดล็อก

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // ให้ Object นี้คงอยู่ข้าม Scene
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // ด่าน 1 ต้องปลดล็อกเสมอ
        UnlockLevel(1);
    }

    /// <summary>
    /// บันทึกผลการเล่นเมื่อจบด่าน
    /// </summary>
    /// <param name="levelID">เลขด่านที่เพิ่งเล่นจบ</param>
    /// <param name="starsEarned">จำนวนดาวที่ได้ (1-3)</param>
    public void SaveLevelResult(int levelID, int starsEarned)
    {
        // 1. บันทึกดาว (เฉพาะถ้าได้มากกว่าเดิม)
        int currentBestStars = GetLevelStars(levelID);
        if (starsEarned > currentBestStars)
        {
            PlayerPrefs.SetInt(string.Format(KEY_LEVEL_STARS, levelID), starsEarned);
        }

        // 2. ปลดล็อกด่านถัดไปอัตโนมัติ (ถ้าเพิ่งผ่านด่านนี้เป็นครั้งแรก หรือเล่นซ้ำก็ให้ย้ำสถานะ Unlock)
        if (starsEarned > 0)
        {
            UnlockLevel(levelID + 1);
        }

        PlayerPrefs.Save();
        Debug.Log($"Saved Level {levelID}: {starsEarned} Stars. Next Level ({levelID + 1}) Unlocked.");
    }

    /// <summary>
    /// ปลดล็อกด่านที่กำหนด
    /// </summary>
    public void UnlockLevel(int levelID)
    {
        if (!IsLevelUnlocked(levelID))
        {
            PlayerPrefs.SetInt(string.Format(KEY_LEVEL_UNLOCKED, levelID), 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// ตรวจสอบว่าด่านนี้ปลดล็อกหรือยัง
    /// </summary>
    public bool IsLevelUnlocked(int levelID)
    {
        // ด่าน 1 ปลดล็อกเสมอ
        if (levelID == 1) return true;

        // 1 = True (Unlocked), 0 = False (Locked)
        return PlayerPrefs.GetInt(string.Format(KEY_LEVEL_UNLOCKED, levelID), 0) == 1;
    }

    /// <summary>
    /// ดึงจำนวนดาวสูงสุดที่เคยทำได้ในด่านนั้น
    /// </summary>
    public int GetLevelStars(int levelID)
    {
        return PlayerPrefs.GetInt(string.Format(KEY_LEVEL_STARS, levelID), 0);
    }

    // --- Debug / Testing Tools ---

    [ContextMenu("Reset All Level Progress")]
    public void ResetAllProgress()
    {
        PlayerPrefs.DeleteAll();
        UnlockLevel(1);
        Debug.Log("All Level Progress Reset!");
    }
}