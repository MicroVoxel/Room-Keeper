using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    #region Inspector
    [Header("Panels & Prefabs")]
    [SerializeField] private GameObject levelsPanel;            // Panel แสดงรายชื่อด่าน
    [SerializeField] private Transform levelsContainer;         // Container ที่วางปุ่มด่าน (Grid Layout Group)
    [SerializeField] private GameObject levelButtonPrefab;      // Prefab ของปุ่มแต่ละด่าน

    [Header("Config")]
    [SerializeField, Min(1)] private int totalLevelButtons; // จำนวนปุ่มที่ต้องการสร้าง
    
    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private SettingsController settingsController;
    #endregion
    
    #region Unity Events
    private void Start()
    {
        // ปิด panels ทั้งหมดตอนเริ่ม
        HideAllPanels();

        // สร้างปุ่มด่านแบบ placeholder
        CreateLevelButtons();

        // ผูก event ปุ่ม
        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonPressed);
        
        if (settingButton != null && settingsController != null)
            settingButton.onClick.AddListener(settingsController.ShowSettingsPanel);
    }
    #endregion

    #region UI Controls
    private void HideAllPanels()
    {
        // ปิด panel เริ่มต้น (ถ้าตั้งไม่ปิดใน Inspector)
        if (levelsPanel != null)
            levelsPanel.SetActive(false);
        
        if (settingsController != null) 
            settingsController.HideSettingsPanel();
    }
    
    private void OnStartButtonPressed()
    {
        HideAllPanels();
        
        if (levelsPanel != null) 
            levelsPanel.SetActive(true);
    }
    
    private void OnLevelButtonClicked(int levelIndex)
    {
        Debug.Log("Level button clicked: " + levelIndex);

        // TODO: ถ้าจะโหลดซีน ปลดคอมเมนต์โค้ดด้านล่างและเพิ่ม SceneManager.LoadScene(...)
        // UnityEngine.SceneManagement.SceneManager.LoadScene("Level" + levelIndex);

        // ตอนนี้แค่ปิด panel กลับไปหน้าเมนูหลัก
        if (levelsPanel != null)
            levelsPanel.SetActive(false);
    }
    #endregion

    #region Helpers
    private void CreateLevelButtons()
    {
        if (levelButtonPrefab == null || levelsContainer == null) return;

        // ลบของเก่าก่อนสร้างใหม่
        for (int i = levelsContainer.childCount - 1; i >= 0; i--)
            Destroy(levelsContainer.GetChild(i).gameObject);

        for (int i = 1; i <= totalLevelButtons; i++)
        {
            GameObject btnObj = Instantiate(levelButtonPrefab, levelsContainer);
            btnObj.name = "LevelButton_" + i;

            // ตั้งชื่อปุ่ม
            TMP_Text label = btnObj.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = $"Level {i}";

            // ผูก event
            Button button = btnObj.GetComponent<Button>();
            int index = i;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnLevelButtonClicked(index));
            }
        }
    }
    #endregion
}
