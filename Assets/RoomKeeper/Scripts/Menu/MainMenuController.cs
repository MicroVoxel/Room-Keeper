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
    [SerializeField] private GameObject settingsPanel; // [NEW] เพิ่มช่องใส่ Settings Panel
    [SerializeField] private Transform levelsContainer;

    [Header("Buttons (Optional)")]
    [Tooltip("ลากปุ่ม Close ใน LevelPanel มาใส่ที่นี่")]
    [SerializeField] private Button closeLevelsPanelButton;

    [Tooltip("ลากปุ่ม Setting (รูปฟันเฟือง) ในหน้าเมนูมาใส่ที่นี่")]
    [SerializeField] private Button openSettingsButton; // [NEW]

    [Tooltip("ลากปุ่ม Close ใน SettingsPanel มาใส่ที่นี่")]
    [SerializeField] private Button closeSettingsButton; // [NEW]

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
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SetupButtons();

        // Logic การแสดงผลเริ่มต้น
        if (SceneManager.GetActiveScene().name == mainMenuSceneName)
        {
            CreateLevelButtons();
            HideAllPanels(); // ซ่อนทุก Panel ตอนเริ่ม
        }
        else
        {
            HideAllPanels();
        }
    }

    private void SetupButtons()
    {
        // 1. Setup Level Panel Buttons
        if (closeLevelsPanelButton != null)
        {
            closeLevelsPanelButton.onClick.RemoveAllListeners();
            closeLevelsPanelButton.onClick.AddListener(HideLevelsPanel);
        }

        // 2. Setup Settings Panel Buttons [NEW]
        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.RemoveAllListeners();
            openSettingsButton.onClick.AddListener(ShowSettingsPanel);
        }

        if (closeSettingsButton != null)
        {
            // เทคนิคสำคัญ: RemoveAllListeners จะช่วยลบคำสั่งเก่าที่ติดมาจาก Script อื่น (เช่น GameUIManager)
            // ทำให้เราใช้ Prefab ตัวเดิมได้โดยไม่ Error
            closeSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.AddListener(HideSettingsPanel);
        }
    }

    private void HideAllPanels()
    {
        if (levelsPanel != null) levelsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // -------------------- UI Control : Levels --------------------

    public void ShowLevelsPanel()
    {
        if (levelsPanel != null)
        {
            HideSettingsPanel(); // ปิด Setting ก่อนเปิด Level (กันซ้อน)
            levelsPanel.SetActive(true);
            CreateLevelButtons();
        }
    }

    public void HideLevelsPanel()
    {
        if (levelsPanel != null) levelsPanel.SetActive(false);
    }

    // -------------------- UI Control : Settings [NEW] --------------------

    public void ShowSettingsPanel()
    {
        if (settingsPanel != null)
        {
            HideLevelsPanel(); // ปิด Level ก่อนเปิด Setting (กันซ้อน)
            settingsPanel.SetActive(true);
        }
    }

    public void HideSettingsPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // -------------------- General --------------------

    public void OnCloseLevelsPanelClicked() => HideLevelsPanel();

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

        foreach (Transform c in levelsContainer)
            Destroy(c.gameObject);

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
                    string.IsNullOrEmpty(entry.displayName) ? $"{levelIndex}" : entry.displayName,
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