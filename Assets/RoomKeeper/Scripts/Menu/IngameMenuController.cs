using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class IngameMenuController : MonoBehaviour
{
    [Header("Optional: MainMenuController Reference")]
    [Tooltip("ถ้าไม่ใส่ จะพยายามหาจาก Singleton (MainMenuController.Instance)")]
    [SerializeField] private MainMenuController mainMenuControllerOverride;

    // Property อัจฉริยะ: หาจาก Override ก่อน -> ถ้าไม่มีหาจาก Singleton -> ถ้าไม่มีคืนค่า null
    private MainMenuController MainMenuRef
    {
        get
        {
            if (mainMenuControllerOverride != null) return mainMenuControllerOverride;
            return MainMenuController.Instance;
        }
    }

    /// <summary>
    /// กลับหน้า Main Menu
    /// </summary>
    public void OnHomeClicked()
    {
        string sceneToLoad = "MainMenu"; // ชื่อ Default กรณีหา Controller ไม่เจอ

        if (MainMenuRef != null && !string.IsNullOrEmpty(MainMenuRef.MainMenuSceneName))
        {
            sceneToLoad = MainMenuRef.MainMenuSceneName;
        }
        else
        {
            Debug.LogWarning("MainMenuController not found. Defaulting to loading 'MainMenu' scene.");
        }

        LoadScene(sceneToLoad);
    }

    /// <summary>
    /// เล่นด่านเดิมซ้ำ (Replay)
    /// </summary>
    public void OnReplayClicked()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        LoadScene(currentScene);
    }

    /// <summary>
    /// โหลดด่านถัดไป
    /// </summary>
    public void OnNextLevelClicked()
    {
        // 1. พยายามหาจาก Level List ใน MainMenuController
        if (MainMenuRef != null && MainMenuRef.LevelEntries != null)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            int currentIndex = MainMenuRef.LevelEntries.FindIndex(e => e.SceneName == currentScene);

            if (currentIndex >= 0)
            {
                int nextIndex = currentIndex + 1;

                // เช็คว่ามีด่านถัดไปหรือไม่
                if (nextIndex < MainMenuRef.LevelEntries.Count)
                {
                    int nextLevelID = nextIndex + 1; // Level IDs start at 1

                    // เช็คว่าปลดล็อคหรือยัง
                    if (LevelProgressManager.Instance != null &&
                        !LevelProgressManager.Instance.IsLevelUnlocked(nextLevelID))
                    {
                        Debug.LogWarning($"Next level '{MainMenuRef.LevelEntries[nextIndex].SceneName}' is locked!");
                        return;
                    }

                    LoadScene(MainMenuRef.LevelEntries[nextIndex].SceneName);
                    return;
                }
                else
                {
                    Debug.Log("No next level in List! Returning to Main Menu.");
                    OnHomeClicked();
                    return;
                }
            }
        }

        // 2. Fallback System: ถ้าไม่มี MainMenuController ให้ลองโหลด Scene ถัดไปตาม Build Settings Index
        Debug.LogWarning("MainMenuController not found or Level not in list. Trying BuildIndex + 1.");
        int nextBuildIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextBuildIndex < SceneManager.sceneCountInBuildSettings)
        {
            LoadScene(SceneManager.GetSceneByBuildIndex(nextBuildIndex).name); // Note: Getting name by index is tricky without loading, usually safe to just load by index
            // เพื่อความง่ายใช้ LoadSceneAsync โดยตรงกับ Index ใน function LoadScene ไม่ได้ ต้องแก้ LoadScene ให้รองรับ int หรือส่งชื่อ dummy
            // ในที่นี้ขออนุญาตโหลดโดยใช้ Index ผ่าน Coroutine เดิมโดยแปลง logic นิดหน่อย
            Time.timeScale = 1f;
            SceneManager.LoadScene(nextBuildIndex);
        }
        else
        {
            Debug.Log("No next Build Index. Returning Home.");
            OnHomeClicked();
        }
    }

    // ---------------- Private ----------------
    private void LoadScene(string sceneName)
    {
        Time.timeScale = 1f; // คืนค่า TimeScale ก่อนโหลด
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) yield break;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone) yield return null;
    }
}