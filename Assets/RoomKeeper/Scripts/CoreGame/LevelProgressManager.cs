using UnityEngine;
using UnityEngine.InputSystem;

public class LevelProgressManager : MonoBehaviour
{
    public static LevelProgressManager Instance { get; private set; }
    [SerializeField] private bool enableDebug = false;

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
        UnlockLevel(1); // Always unlock Level 1
    }

#if UNITY_EDITOR
    private void Update()
    {
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

    [ContextMenu("Reset All")]
    public void ResetAllProgress()
    {
        PlayerPrefs.DeleteAll();
        UnlockLevel(1);
        Debug.LogWarning("🧹 Reset All Progress");
    }
}