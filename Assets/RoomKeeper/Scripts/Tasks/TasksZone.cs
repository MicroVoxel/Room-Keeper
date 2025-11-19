using Crystal;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TasksZone : MonoBehaviour
{
    #region 1. Fields
    [Header("Task Prefab")]
    [Tooltip("ลาก *Prefab* ของ UI Task Panel (ที่มี TaskBase) มาใส่ที่นี่")]
    public GameObject taskPanelPrefab;

    [Header("Visuals")]
    [Tooltip("GameObject ที่จะแสดงเพื่อบอกว่ามี Task (เช่น เครื่องหมาย ! หรือลูกศร)")]
    public GameObject taskIndicator;

    [Header("Debug Info")]
    [SerializeField, ReadOnlyInspector]
    private TaskBase spawnedTaskInstance;

    // เพิ่มตัวแปรนี้เพื่อคุมว่า Zone นี้ทำงานได้หรือยัง
    [SerializeField, ReadOnlyInspector]
    private bool isTaskActive = false;

    private RoomData owningRoom;
    #endregion

    #region 2. Initialization / Setup
    public void InitializeAndSpawnTask(RoomData owner)
    {
        this.owningRoom = owner;

        // เริ่มต้น: สั่งปิดการทำงานของ Task นี้ไปก่อน (ทั้ง Logic และ Visual)
        SetTaskActive(false);

        if (taskPanelPrefab == null)
        {
            Debug.LogError($"[TasksZone] ที่ {gameObject.name}: ยังไม่ได้กำหนด 'Task Panel Prefab'", this);
            this.enabled = false;
            return;
        }

        SafeArea mainCanvas = Object.FindFirstObjectByType<SafeArea>();
        if (mainCanvas == null)
        {
            Debug.LogError($"[TasksZone] ที่ {gameObject.name}: หา <SafeArea> ไม่เจอ!", this);
            this.enabled = false;
            return;
        }

        GameObject spawnedPanelObject = Instantiate(taskPanelPrefab, mainCanvas.transform);

        if (!spawnedPanelObject.TryGetComponent<TaskBase>(out spawnedTaskInstance))
        {
            Debug.LogError($"[TasksZone] Prefab ไม่มี Component 'TaskBase'!", this);
            Destroy(spawnedPanelObject);
            return;
        }

        spawnedTaskInstance.SetOwner(owner);
        spawnedPanelObject.SetActive(false);

        spawnedTaskInstance.OnTaskCompleted += HandleTaskCompleted;
    }

    private void HandleTaskCompleted()
    {
        // เมื่อทำเสร็จ ให้ปิด Indicator และปิดสถานะ Active เพื่อไม่ให้ทำซ้ำหรือโชว์อีก
        SetTaskActive(false);
    }

    /// <summary>
    /// ฟังก์ชันใหม่: คุมทั้ง Logic (ให้เดินชนได้ไหม) และ Visual (โชว์ Indicator ไหม) พร้อมกัน
    /// </summary>
    public void SetTaskActive(bool active)
    {
        isTaskActive = active;

        if (taskIndicator != null)
        {
            taskIndicator.SetActive(active);
        }
    }

    public TaskBase GetTaskInstance()
    {
        return spawnedTaskInstance;
    }
    #endregion

    #region 3. Collision Logic
    void OnCollisionEnter2D(Collision2D collision)
    {
        // 1. เช็ค Tag และ Component ผู้เล่น
        if (!collision.gameObject.CompareTag("Player")) return;
        if (!collision.gameObject.TryGetComponent<PlayerController>(out var _)) return;

        // 2. --- จุดสำคัญที่แก้บั๊ก ---
        // ถ้า Task นี้ไม่ได้ถูกสั่ง Active (จาก RoomData) ให้ Return ออกไปเลย ไม่ต้องเปิด UI
        if (!isTaskActive) return;

        if (spawnedTaskInstance == null || !this.enabled) return;

        if (spawnedTaskInstance.IsCompleted) return;

        spawnedTaskInstance.Open();
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // การปิด Task Panel อนุญาตให้ทำได้เสมอ เพื่อความปลอดภัย UI ไม่ค้าง
        if (spawnedTaskInstance != null && this.enabled)
        {
            spawnedTaskInstance.Close();
        }
    }
    #endregion

    #region 4. Cleanup
    private void OnDestroy()
    {
        if (spawnedTaskInstance != null)
        {
            spawnedTaskInstance.OnTaskCompleted -= HandleTaskCompleted;
            if (spawnedTaskInstance.gameObject != null)
            {
                Destroy(spawnedTaskInstance.gameObject);
            }
        }
    }
    #endregion
}

// Optional Helper
public class ReadOnlyInspectorAttribute : PropertyAttribute { }