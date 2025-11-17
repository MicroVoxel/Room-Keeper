using Crystal;
using UnityEngine;

/// <summary>
/// (เวอร์ชัน Spawn ของตัวเอง - Autonomous)
/// --- ⭐ UPDATED ---
/// 'Awake()' จะทำงานอัตโนมัติเมื่อถูก Spawn
/// 1. ค้นหา 'RoomData' (Parent) ของตัวเอง
/// 2. Spawn Panel UI
/// 3. "รายงานตัว" (Register) TaskBase กลับไปให้ RoomData (Parent)
/// </summary>
public class TasksZone : MonoBehaviour
{
    [Header("Task Prefab")]
    [Tooltip("ลาก *Prefab* ของ UI Task Panel (ที่มี TaskBase) มาใส่ที่นี่")]
    public GameObject taskPanelPrefab;

    private TaskBase spawnedTaskInstance;

    /// <summary>
      	/// --- ⭐ NEW LOGIC ---
      	/// 'Awake' จะถูกเรียกอัตโนมัติโดย Unity เมื่อ 'RoomData' สั่ง Instantiate
      	/// </summary>
    private void Awake()
    {
        // 1. ค้นหา "ผู้สร้าง" (RoomData) โดยอัตโนมัติ
        //    (วิธีนี้จะทำงานได้ ถ้า RoomData.AssignRandomTask() สั่ง Instantiate
        //     โดยใช้ 'transform' (ของ RoomData) เป็น Parent)
        RoomData creator = GetComponentInParent<RoomData>();

        if (taskPanelPrefab == null)
        {
            Debug.LogError($"[TasksZone] ที่ {gameObject.name}: ยังไม่ได้กำหนด 'Task Panel Prefab'", this);
            this.enabled = false;
            return;
        }

        // 2. ค้นหา Canvas (SafeArea)
        SafeArea mainCanvas = FindFirstObjectByType<SafeArea>();
        if (mainCanvas == null)
        {
            Debug.LogError($"[TasksZone] ที่ {gameObject.name}: หา <SafeArea> (จาก Crystal) ไม่เจอในซีน!", this);
            this.enabled = false;
            return;
        }

        // 3. Spawn Panel
        GameObject spawnedPanelObject = Instantiate(taskPanelPrefab, mainCanvas.transform);

        // 4. ดึง TaskBase จาก Panel ที่ Spawn
        spawnedTaskInstance = spawnedPanelObject.GetComponent<TaskBase>();

        if (spawnedTaskInstance == null)
        {
            Debug.LogError($"[TasksZone] ที่ {gameObject.name}: Prefab '{taskPanelPrefab.name}' ไม่มี Component 'TaskBase'!", this);
            Destroy(spawnedPanelObject);
            this.enabled = false;
            return;
        }

        // 5. "รายงานตัว" (Register) กลับไป
        if (creator != null)
        {
            // 5.1 "ฉีด" (Inject) RoomData (ผู้สร้าง) เข้าไปใน TaskBase
            spawnedTaskInstance.SetOwner(creator);

            // 5.2 "รายงานตัว" (Register) TaskBase กลับไปให้ RoomData
            creator.RegisterSpawnedTask(spawnedTaskInstance);
        }
        else
        {
            Debug.LogError($"[TasksZone] ({gameObject.name}) ไม่พบ RoomData ที่เป็น Parent! Task จะไม่ถูกตรวจสอบ", this);
        }

        // 6. ซ่อน Panel ที่ Spawn ไว้
        spawnedPanelObject.SetActive(false);
    }

    // --- (REMOVED) ---
    // ลบ 'InitializeAndRegister(RoomData creator)' ทิ้ง

    // --- ตรรกะการชน (Collision) - ไม่เปลี่ยนแปลง ---
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
}