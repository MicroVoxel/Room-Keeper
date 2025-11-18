using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// IngameMenuController - ควบคุมปุ่ม UI สำหรับการเปลี่ยนฉาก (Home, Replay, Next Level)
/// รองรับการลาก Scene Asset ใส่ใน Inspector ได้โดยตรง
/// </summary>
public class IngameMenuController : MonoBehaviour
{
    [Header("Scene Configuration")]

#if UNITY_EDITOR
    [Tooltip("ลากไฟล์ Scene จาก Project มาใส่ที่นี่")]
    [SerializeField] private SceneAsset mainMenuSceneAsset;
#endif

    [Tooltip("ชื่อ Scene ของหน้า Main Menu (จะอัปเดตอัตโนมัติจากช่องข้างบน)")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // ฟังก์ชันนี้จะทำงานใน Editor เท่านั้น เพื่อดึงชื่อ Scene จากไฟล์ที่เราลากมาใส่
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (mainMenuSceneAsset != null)
        {
            mainMenuSceneName = mainMenuSceneAsset.name;
        }
    }
#endif

    /// <summary>
    /// กลับสู่หน้า Home / Main Menu
    /// ผูกกับปุ่ม Home
    /// </summary>
    public void OnHomeClicked()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("Main Menu Scene Name is empty! Please assign a scene in the Inspector.");
            return;
        }

        Debug.Log($"Loading Main Menu: {mainMenuSceneName}...");
        StartCoroutine(LoadSceneAsync(mainMenuSceneName));
    }

    /// <summary>
    /// เล่นด่านเดิมซ้ำ (Replay)
    /// ผูกกับปุ่ม Replay
    /// </summary>
    public void OnReplayClicked()
    {
        // ดึงชื่อ Scene ปัจจุบันมาโหลดใหม่
        string currentSceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"Replaying Level: {currentSceneName}");
        StartCoroutine(LoadSceneAsync(currentSceneName));
    }

    /// <summary>
    /// ไปยังด่านถัดไป (Next Level)
    /// ผูกกับปุ่ม Next Level
    /// </summary>
    public void OnNextLevelClicked()
    {
        // คำนวณ Index ของ Scene ถัดไปตามลำดับใน Build Settings
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

        // ตรวจสอบว่ามี Scene ถัดไปจริงหรือไม่
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log($"Loading Next Level (Index: {nextSceneIndex})...");
            StartCoroutine(LoadSceneIndexAsync(nextSceneIndex));
        }
        else
        {
            Debug.LogWarning("No more levels in Build Settings! Returning to Main Menu.");
            OnHomeClicked(); // ถ้าไม่มีด่านต่อ ให้กลับหน้าเมนู
        }
    }

    // --- Private Helpers ---

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        // คืนค่า TimeScale เป็น 1 เสมอก่อนเปลี่ยนฉาก (เผื่อเกม Pause อยู่)
        Time.timeScale = 1f;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        // รอจนกว่าจะโหลดเสร็จ
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    private IEnumerator LoadSceneIndexAsync(int sceneIndex)
    {
        Time.timeScale = 1f;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
}