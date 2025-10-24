using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels & Prefabs")]
    [SerializeField] private GameObject levelsPanel;
    [SerializeField] private Transform levelsContainer;
    [SerializeField] private GameObject levelButtonPrefab;

    [Header("Config")]
    [SerializeField, Min(1)] private int totalLevelButtons = 9;
    
    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    private void Start()
    {
        if (levelsPanel != null)
            levelsPanel.SetActive(false);

        CreateLevelButtons();

        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonPressed);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    public void OnStartButtonPressed()
    {
        if (levelsPanel == null) return;
        levelsPanel.SetActive(true);
        // ยังไม่โหลดซีน แค่โชว์ panel ให้ผู้เล่นเลือกด่าน
    }

    private void CreateLevelButtons()
    {
        if (levelButtonPrefab == null || levelsContainer == null)
        {
            return;
        }

        for (int i = levelsContainer.childCount - 1; i >= 0; i--)
            Destroy(levelsContainer.GetChild(i).gameObject);

        for (int i = 1; i <= totalLevelButtons; i++)
        {
            GameObject btnObj = Instantiate(levelButtonPrefab, levelsContainer);
            btnObj.name = "LevelButton_" + i;

            TMP_Text label = btnObj.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = "Level " + i;

            Button button = btnObj.GetComponent<Button>();
            int index = i;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnLevelButtonClicked(index));
            }
        }
    }

    private void OnLevelButtonClicked(int levelIndex)
    {
        Debug.Log("Level button clicked: " + levelIndex);

        // TODO: ถ้าจะโหลดซีน ปลดคอมเมนต์โค้ดด้านล่างและเพิ่ม SceneManager.LoadScene(...)
        // UnityEngine.SceneManagement.SceneManager.LoadScene("Level" + levelIndex);

        if (levelsPanel != null)
            levelsPanel.SetActive(false);
    }

    private void QuitGame()
    {
        Debug.Log("Quit Game Clicked!");
        Application.Quit();
    }
}
