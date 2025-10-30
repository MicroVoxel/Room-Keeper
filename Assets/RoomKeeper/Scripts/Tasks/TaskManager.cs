using System.Collections.Generic;
using UnityEngine;

// หมายเหตุ: เราไม่ต้องใช้คลาส RoomTask อีกต่อไป เพราะ TaskBase ทำหน้าที่นี้แทน
// แต่เราจะใช้ GameObject Prefab ของ TaskBase ที่กำหนดไว้ใน TaskManager

public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance { get; private set; }

    [Header("Task Settings")]
    [Tooltip("รายชื่อ Prefabs ของ TaskBase ที่เป็นไปได้ทั้งหมด (ต้องมี TaskBase Component ติดอยู่)")]
    public List<GameObject> availableTaskPrefabs;

    // Dictionary เพื่อเก็บ Task ที่กำลังใช้งาน (Key: RoomData, Value: TaskBase Component ที่ถูก Instantiate แล้ว)
    private Dictionary<RoomData, TaskBase> activeRoomTasks = new Dictionary<RoomData, TaskBase>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    /// <summary>
    /// กำหนด Task ใหม่ให้กับห้องที่เพิ่งสร้าง/เข้าสู่ โดยการ Instantiate Task Prefab เข้าไปในห้อง
    /// </summary>
    /// <param name="room">RoomData ของห้องที่จะรับ Task</param>
    public void AssignNewRoomTask(RoomData room)
    {
        if (availableTaskPrefabs.Count == 0)
        {
            Debug.LogWarning("No available task prefabs defined in TaskManager.");
            return;
        }

        // 1. ตรวจสอบและลบ Task เก่าของห้องออกก่อน (ถ้ามี)
        if (activeRoomTasks.ContainsKey(room))
        {
            RemoveTask(room);
        }

        // 2. สุ่ม Task Prefab ใหม่
        int randomIndex = Random.Range(0, availableTaskPrefabs.Count);
        GameObject taskPrefab = availableTaskPrefabs[randomIndex];

        // 3. ⭐ Instantiate Task Component และแนบเข้ากับ Room GameObject
        GameObject taskObject = Instantiate(taskPrefab, room.transform); // ให้ Room เป็น Parent

        if (taskObject.TryGetComponent<TaskBase>(out TaskBase newTaskComponent))
        {
            activeRoomTasks.Add(room, newTaskComponent);

            // ⭐ ตรวจสอบและซ่อน Panel และ Marker ไว้ก่อน (หากไม่ได้กำหนดค่าเริ่มต้นไว้)
            newTaskComponent.Close();
            if (newTaskComponent.taskMarkerRenderer)
            {
                newTaskComponent.taskMarkerRenderer.enabled = true; // เปิด Marker ให้เห็น
            }

            Debug.Log($"Assigned new Task to {room.name}: {taskObject.name} (TaskBase)");

            // อัปเดต UI (ถ้ามี)
            // UIManager.UpdateCurrentTaskUI(newTaskComponent); 
        }
        else
        {
            Debug.LogError($"Task Prefab '{taskPrefab.name}' is missing TaskBase component!");
            Destroy(taskObject);
        }
    }

    /// <summary>
    /// ⭐ เมธอดที่ GameCoreManager ใช้ในการตรวจสอบสถานะ Task
    /// </summary>
    /// <param name="room">RoomData ของห้องปัจจุบัน</param>
    /// <returns>True ถ้า Task เสร็จแล้ว</returns>
    public bool IsRoomTaskCompleted(RoomData room)
    {
        if (activeRoomTasks.TryGetValue(room, out TaskBase currentTask))
        {
            // ใช้ IsCompleted property จาก TaskBase
            return currentTask.IsCompleted;
        }
        return true; // ถ้าไม่มี Task ถูกกำหนด ถือว่าห้องนั้นเสร็จสิ้นภารกิจแล้ว
    }

    /// <summary>
    /// เรียกใช้โดย Event/Script อื่น เมื่อเงื่อนไข Task สำเร็จ
    /// </summary>
    /// <param name="room">RoomData ของห้องที่ Task เสร็จ</param>
    public void CompleteTask(RoomData room)
    {
        if (activeRoomTasks.TryGetValue(room, out TaskBase taskToComplete))
        {
            // เรียก CompleteTask() protected method บน TaskBase instance
            // Note: ต้องแก้ไข TaskBase ให้ CompleteTask เป็น Public หรือสร้าง Wrapper ใน TaskBase

            // **สมมติว่าคุณจะเรียกผ่าน TaskBase เอง** (เช่น ผู้เล่นกดปุ่มใน Task panel) 
            // TaskManager จะตรวจสอบสถานะ IsCompleted ในลูปเท่านั้น
            Debug.LogWarning("CompleteTask() should ideally be called directly on the TaskBase instance or triggered by player interaction.");
            // หากคุณต้องการให้ TaskManager ทำการ Complete ให้ ต้องเปลี่ยน CompleteTask ใน TaskBase เป็น public
        }
    }

    /// <summary>
    /// ดึง TaskBase ของห้องปัจจุบัน
    /// </summary>
    public TaskBase GetRoomTask(RoomData room)
    {
        if (activeRoomTasks.TryGetValue(room, out TaskBase task))
        {
            return task;
        }
        return null;
    }

    /// <summary>
    /// ลบ Task ออกจากระบบเมื่อห้องถูกทำลาย
    /// </summary>
    public void RemoveTask(RoomData room)
    {
        if (activeRoomTasks.TryGetValue(room, out TaskBase taskToRemove))
        {
            activeRoomTasks.Remove(room);
            // เนื่องจาก Task ถูก Instantiate เป็น Child ของ Room และเราต้องการลบแค่ Task Object
            // แต่เนื่องจาก DungeonGenerator.RemoveRoom() ทำลาย RoomData ทั้งหมด (รวมถึง Children)
            // ในกรณีนี้ เราเพียงแค่ลบออกจาก Dictionary ก็พอ
            // แต่หากมีการลบ Task อิสระ (เช่น taskObject.Destroy()) ให้ทำลาย taskObject ด้วย

            // ในบริบทนี้: DungeonGenerator จะทำลาย Task Object เมื่อทำลาย Room
            Destroy(taskToRemove.gameObject);
            Debug.Log($"Task for {room.name} removed from TaskManager and destroyed.");
        }
    }
}