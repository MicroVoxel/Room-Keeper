using Crystal;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TasksZone : MonoBehaviour
{
    #region 1. Fields
    [Header("Task Prefab")]
    [Tooltip("ลาก *Prefab* ของ UI Task Panel (ที่มี TaskBase) มาใส่ที่นี่")]
    public GameObject taskPanelPrefab;

    private TaskBase spawnedTaskInstance;
    private RoomData owningRoom;
    #endregion

    #region 2. Initialization / Setup
    /// <summary>
    /// 'RoomData.Awake()' จะเรียกฟังก์ชันนี้
    /// เพื่อสั่งให้ Zone นี้ Spawn UI (TaskBase) ที่มันรับผิดชอบ
    /// </summary>
    public void InitializeAndSpawnTask(RoomData owner)
    {
        this.owningRoom = owner;

        if (taskPanelPrefab == null)
        {
            Debug.LogError($"[TasksZone] ที่ {gameObject.name}: ยังไม่ได้กำหนด 'Task Panel Prefab'", this);
            this.enabled = false;
            return;
        }

        SafeArea mainCanvas = FindFirstObjectByType<SafeArea>();
        if (mainCanvas == null)
        {
            Debug.LogError($"[TasksZone] ที่ {gameObject.name}: หา <SafeArea> (จาก Crystal) ไม่เจอในซีน!", this);
            this.enabled = false;
            return;
        }

        GameObject spawnedPanelObject = Instantiate(taskPanelPrefab, mainCanvas.transform);
        spawnedTaskInstance = spawnedPanelObject.GetComponent<TaskBase>();

        if (spawnedTaskInstance == null)
        {
            Debug.LogError($"[TasksZone] ที่ {gameObject.name}: Prefab '{taskPanelPrefab.name}' ไม่มี Component 'TaskBase'!", this);
            Destroy(spawnedPanelObject);
            this.enabled = false;
            return;
        }

        spawnedTaskInstance.SetOwner(owner);
        spawnedPanelObject.SetActive(false);
    }

    /// <summary>
    /// ให้ RoomData เรียกใช้ เพื่อดึง TaskBase (UI) ที่ Zone นี้ Spawn
    /// </summary>
    public TaskBase GetTaskInstance()
    {
        if (spawnedTaskInstance == null && this.enabled)
        {
            Debug.LogWarning($"TasksZone {gameObject.name} ถูกถามหา TaskBase instance แต่ยังไม่ได้ Spawn", this);
        }
        return spawnedTaskInstance;
    }
    #endregion

    #region 3. Collision Logic
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.GetComponent<PlayerController>()) return;

        if (spawnedTaskInstance == null || !this.enabled) return;

        if (spawnedTaskInstance.IsCompleted) return;
        spawnedTaskInstance.Open();
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.gameObject.GetComponent<PlayerController>()) return;
        if (spawnedTaskInstance != null && this.enabled)
        {
            spawnedTaskInstance.Close();
        }
    }
    #endregion

    #region 4. Cleanup
    /// <summary>
    /// ลบ UI Task Panel ที่ถูก Spawn ออกจาก Canvas เมื่อ Zone นี้ถูกลบ
    /// </summary>
    private void OnDestroy()
    {
        // ตรวจสอบว่า Task UI instance ยังอยู่หรือไม่ ก่อนที่จะสั่ง Destroy
        if (spawnedTaskInstance != null)
        {
            // Destroy GameObject ที่ TaskBase เกาะอยู่ (ซึ่งก็คือ Task Panel UI)
            Destroy(spawnedTaskInstance.gameObject);
        }
    }
    #endregion
}