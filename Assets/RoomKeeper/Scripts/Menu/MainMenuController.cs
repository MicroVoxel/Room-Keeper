using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement; // เพิ่มเข้ามาเพื่อใช้ SceneManager

/// <summary>
/// (NEW) คลาสสำหรับเก็บข้อมูลของด่านแต่ละด่าน
/// การใส่ [System.Serializable] ทำให้มันแสดงผลใน Inspector ได้
/// </summary>
[System.Serializable]
public class LevelEntry
{
    [Tooltip("ชื่อที่จะแสดงบนปุ่ม (เช่น 'Level 1', 'ด่านป่าไม้')")]
    public string displayName;

    [Tooltip("ลาก Scene Asset (.unity file) จากหน้าต่าง Project มาใส่ที่นี่")]
    [SerializeField]
    private Object sceneAsset; // เราใช้ Object เพราะ SceneAsset อยู่ใน UnityEditor และใช้ใน Build ไม่ได้

    // Property นี้จะดึงชื่อของ Scene Asset ออกมาโดยอัตโนมัติ
    public string SceneName
    {
        get
        {
            if (sceneAsset == null)
            {
                Debug.LogError("Scene Asset is null for display name: " + displayName);
                return null;
            }
            return sceneAsset.name; // .name ของ Asset คือชื่อไฟล์ซีน ซึ่งตรงกับชื่อที่ SceneManager ใช้
        }
    }
}


public class MainMenuController : MonoBehaviour
{
    #region Inspector
    [Header("Panels & Prefabs")]
    [SerializeField] private GameObject levelsPanel;          // Panel แสดงรายชื่อด่าน
    [SerializeField] private Transform levelsContainer;        // Container ที่วางปุ่มด่าน (Grid Layout Group)
    [SerializeField] private GameObject levelButtonPrefab;      // Prefab ของปุ่มแต่ละด่าน

    [Header("Config")]
    // [SerializeField] private List<string> levelSceneNames; // --- (REMOVED) ---

    [Tooltip("กำหนดด่านต่างๆ โดยการลาก Scene Asset มาใส่")]
    [SerializeField] private List<LevelEntry> levelEntries; // --- (NEW) ---

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private SettingsController settingsController;
    #endregion

    #region Unity Events
    private void Start()
    {
        // ... (โค้ดส่วนนี้เหมือนเดิม) ...
        HideAllPanels();
        CreateLevelButtons();

        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonPressed);

        if (settingButton != null && settingsController != null)
            settingButton.onClick.AddListener(settingsController.ShowSettingsPanel);
    }
    #endregion

    #region UI Controls
    private void HideAllPanels()
    {
        // ... (โค้ดส่วนนี้เหมือนเดิม) ...
        if (levelsPanel != null)
            levelsPanel.SetActive(false);

        if (settingsController != null)
            settingsController.HideSettingsPanel();
    }

    private void OnStartButtonPressed()
    {
        // ... (โค้ดส่วนนี้เหมือนเดิม) ...
        HideAllPanels();

        if (levelsPanel != null)
            levelsPanel.SetActive(true);
    }

    /// <summary>
    /// --- (MODIFIED) ---
    /// ถูกเรียกเมื่อปุ่มด่านถูกกด (ตอนนี้รับ Index ของ List)
    /// </summary>
    private void OnLevelButtonClicked(int levelListIndex)
    {
        // ตรวจสอบว่า Index อยู่ในขอบเขตของ List หรือไม่
        if (levelListIndex < 0 || levelListIndex >= levelEntries.Count)
        {
            Debug.LogError($"Invalid level index: {levelListIndex}. List count is {levelEntries.Count}");
            return;
        }

        // --- (MODIFIED) ---
        // ดึงชื่อ Scene จาก List โดยใช้ Index และ Property 'SceneName'
        string sceneToLoad = levelEntries[levelListIndex].SceneName;

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogError($"Scene name is null or empty for level index {levelListIndex}. Did you forget to drag the Scene Asset in the Inspector?");
            return;
        }

        Debug.Log("Level button clicked. Loading scene: " + sceneToLoad);

        // โหลดซีนตามชื่อที่ระบุ
        SceneManager.LoadScene(sceneToLoad);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// --- (MODIFIED) ---
    /// สร้างปุ่มตามรายการ levelEntries
    /// </summary>
    private void CreateLevelButtons()
    {
        if (levelButtonPrefab == null || levelsContainer == null) return;
        if (levelEntries == null) return; // (NEW) เพิ่มการตรวจสอบ List

        // ลบของเก่าก่อนสร้างใหม่
        for (int i = levelsContainer.childCount - 1; i >= 0; i--)
            Destroy(levelsContainer.GetChild(i).gameObject);

        // --- (MODIFIED) ---
        // วนลูปตามจำนวน Scene ใน List
        for (int i = 0; i < levelEntries.Count; i++)
        {
            GameObject btnObj = Instantiate(levelButtonPrefab, levelsContainer);

            LevelEntry entry = levelEntries[i]; // (NEW) ดึงข้อมูลด่านปัจจุบัน
            int displayLevelNumber = i + 1;
            btnObj.name = "LevelButton_" + displayLevelNumber;

            // --- (MODIFIED) ---
            // ตั้งชื่อปุ่ม (ใช้ displayName ถ้ามี, ถ้าไม่มีก็ใช้ "Level [เลข]")
            string buttonText = entry.displayName;
            if (string.IsNullOrEmpty(buttonText))
            {
                buttonText = $"Level {displayLevelNumber}";
            }

            TMP_Text label = btnObj.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = buttonText; // (MODIFIED)

            // ผูก event
            Button button = btnObj.GetComponent<Button>();

            // --- (MODIFIED) ---
            // ดักจับ Index (i) ของ List
            int index = i;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                // ส่ง 'index' (0, 1, 2...) ไปยังเมธอด
                button.onClick.AddListener(() => OnLevelButtonClicked(index));
            }
        }
    }
    #endregion
}