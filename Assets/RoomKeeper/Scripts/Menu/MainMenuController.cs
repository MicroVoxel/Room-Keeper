using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class LevelEntry
{
    public string displayName;

    [SerializeField, HideInInspector] private string sceneName;

#if UNITY_EDITOR
    [Tooltip("ลาก Scene Asset มาใส่ที่นี่")]
    [SerializeField] private Object sceneAsset;

    public void OnValidate()
    {
        sceneName = sceneAsset != null ? sceneAsset.name : "";
    }
#endif

    public string SceneName => sceneName;
}

public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }

    [Header("General Settings")]
    [SerializeField, HideInInspector] private string mainMenuSceneName = "MainMenu";
    public string MainMenuSceneName => mainMenuSceneName;

#if UNITY_EDITOR
    [Tooltip("ลาก Scene Asset สำหรับหน้า Main Menu มาใส่ที่นี่")]
    [SerializeField] private Object mainMenuSceneAsset;
#endif

    [Header("Panels")]
    [SerializeField] private GameObject levelsPanel;
    [SerializeField] private Transform levelsContainer;

    [Header("Buttons (Optional)")]
    [Tooltip("ลากปุ่ม Close ใน LevelPanel มาใส่ที่นี่")]
    [SerializeField] private Button closeLevelsPanelButton;

    [Header("Prefabs")]
    [SerializeField] private GameObject levelButtonPrefab;

    [Header("Level List")]
    [SerializeField] private List<LevelEntry> levelEntries;
    public List<LevelEntry> LevelEntries => levelEntries;

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (mainMenuSceneAsset != null)
        {
            mainMenuSceneName = mainMenuSceneAsset.name;
        }
        if (levelEntries != null)
        {
            foreach (var entry in levelEntries) entry.OnValidate();
        }
#endif
    }

    private void Awake()
    {
        // แก้ไข Singleton Logic:
        // ถ้ามี Instance เดิมอยู่แล้ว ให้ทำลายตัวเดิมทิ้ง! 
        // และให้ตัวใหม่ (this) เป็น Instance แทน
        // เหตุผล: เพราะปุ่ม UI ใน Scene นี้ถูก Link ไว้กับตัวใหม่ (this) 
        // ถ้าเราทำลายตัวใหม่ ปุ่มจะกดไม่ติด
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Setup ปุ่ม Close
        if (closeLevelsPanelButton != null)
        {
            closeLevelsPanelButton.onClick.RemoveAllListeners();
            closeLevelsPanelButton.onClick.AddListener(OnCloseLevelsPanelClicked);
        }

        // Logic การแสดงผลเริ่มต้น
        if (SceneManager.GetActiveScene().name == mainMenuSceneName)
        {
            // ถ้าอยู่ในหน้า Menu ให้สร้างปุ่มเลือกด่านรอไว้เลย
            CreateLevelButtons();
            if (levelsPanel != null) levelsPanel.SetActive(false);
        }
        else
        {
            // ถ้าข้าม Scene ไปหน้าเกม ให้ซ่อน Panel ไว้
            if (levelsPanel != null) levelsPanel.SetActive(false);
        }
    }

    // -------------------- UI Control --------------------

    public void ShowLevelsPanel()
    {
        if (levelsPanel != null)
        {
            levelsPanel.SetActive(true);
            // รีเฟรชปุ่มทุกครั้งที่เปิด (เผื่อดาวมีการเปลี่ยนแปลง)
            CreateLevelButtons();
        }
    }

    public void HideLevelsPanel()
    {
        if (levelsPanel != null) levelsPanel.SetActive(false);
    }

    public void OnCloseLevelsPanelClicked()
    {
        HideLevelsPanel();
    }

    public void OnQuitPressed()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // -------------------- Level Button Logic --------------------

    private void CreateLevelButtons()
    {
        if (levelButtonPrefab == null || levelsContainer == null) return;

        // ล้างปุ่มเก่า
        foreach (Transform c in levelsContainer)
            Destroy(c.gameObject);

        // สร้างปุ่มใหม่
        for (int i = 0; i < levelEntries.Count; i++)
        {
            LevelEntry entry = levelEntries[i];
            GameObject obj = Instantiate(levelButtonPrefab, levelsContainer);

            LevelButtonUI ui = obj.GetComponent<LevelButtonUI>();
            if (ui != null)
            {
                int levelIndex = i + 1;
                bool unlocked = (LevelProgressManager.Instance != null) &&
                                LevelProgressManager.Instance.IsLevelUnlocked(levelIndex);
                int stars = (LevelProgressManager.Instance != null) ?
                            LevelProgressManager.Instance.GetLevelStars(levelIndex) : 0;

                ui.Setup(levelIndex,
                    string.IsNullOrEmpty(entry.displayName) ? $"Level {levelIndex}" : entry.displayName,
                    unlocked,
                    stars);
            }
        }
    }

    public void OnLevelButtonPressed(int levelIndex)
    {
        if (levelIndex < 1 || levelIndex > levelEntries.Count) return;

        string sceneName = levelEntries[levelIndex - 1].SceneName;
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}