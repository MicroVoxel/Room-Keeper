using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Collections;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// เก็บข้อมูลพื้นฐาน, สถานะ Task, และตรรกะการเชื่อมต่อสำหรับห้อง
/// จัดการเวลาและ Task ของตัวเอง และแจ้ง CoreManager เมื่อเสร็จสิ้น/หมดเวลา
/// </summary>
public class RoomData : MonoBehaviour
{
    #region ENUMS

    public enum RoomType
    {
        /// <summary>ห้องที่ผู้เล่นเริ่มต้น</summary>
        Spawn,
        /// <summary>ห้องทั่วไป</summary>
        Room,
        /// <summary>ทางเดินเชื่อมระหว่างห้อง</summary>
        Hallway
    }

    #endregion

    // -------------------------------------------------------------------

    #region FIELDS & PROPERTIES

    // Events เพื่อแจ้ง GameCoreManager
    public event Action<RoomData> OnRoomCompletion;
    public event Action<RoomData> OnRoomTimeout;

    [Header("Room Setup")]
    public RoomType roomType;
    public Collider2D collider2DForComposit;

    [Tooltip("Collider ที่ใช้สำหรับวัดขอบเขตของห้อง (ควรเป็น BoxCollider2D ที่ Is Trigger)")]
    public Collider2D roomBoundsCollider;

    [SerializeField] private LayerMask roomLayerMask;

    [Header("Connection Points")]
    public List<Transform> connectors;

    [Header("Connection Info")]
    [Tooltip("Connector ใน Parent Room ที่ Room นี้ใช้เชื่อมต่อ")]
    public Transform parentConnector;

    [Header("Task Spawn Points")]
    public List<Transform> taskPoints;

    [Header("UI Reference")]
    [Tooltip("ลาก TextMeshProUGUI Component ที่จะแสดงเวลาที่เหลือมาใส่ใน Field นี้")]
    [SerializeField] private TextMeshProUGUI timeDisplay;

    public TextMeshProUGUI TimeDisplay => timeDisplay;

    [Header("Game Logic & Tasks")]
    public float roomTimeLimit = 60f;

    [Tooltip("Prefabs ของ TaskBase ที่สามารถสุ่มเกิดในห้องนี้")]
    [SerializeField] private List<GameObject> possibleTaskPrefabs;

    [Tooltip("จำนวน Task สูงสุดที่จะสุ่มสร้างในห้องนี้ (จำกัดโดย Task Points)")]
    [SerializeField] private int maxTasksToAssign = 1;

    private List<TaskBase> assignedTasks;
    private Coroutine roomTimerCoroutine;

    public List<TaskBase> AssignedTasks => assignedTasks;

    private const int MAX_CONNECTOR_RETRIES = 5;

    #endregion

    // -------------------------------------------------------------------

    #region LIFECYCLE & TIMER LOGIC

    private void Awake()
    {
        assignedTasks = new List<TaskBase>();
    }

    private void OnValidate()
    {
        maxTasksToAssign = Mathf.Max(1, maxTasksToAssign);
    }

    private void OnDestroy()
    {
        StopRoomTimer();
        // หาก Timer ยังทำงานอยู่และถูกทำลาย ให้ตั้งค่า UI เป็นค่าว่าง
        UpdateTimeDisplay(0f);
    }

    /// <summary>
    /// เริ่มจับเวลาห้อง
    /// </summary>
    public void StartRoomTimer()
    {
        if (roomType != RoomType.Room || assignedTasks.Count == 0) return;

        StopRoomTimer();

        UpdateTimeDisplay(roomTimeLimit);

        roomTimerCoroutine = StartCoroutine(RoomTimerCoroutine());
        //Debug.Log($"Timer started for {gameObject.name}. Time Limit: {roomTimeLimit}s");
    }

    /// <summary>
    /// หยุดจับเวลาห้อง
    /// </summary>
    public void StopRoomTimer()
    {
        if (roomTimerCoroutine != null)
        {
            StopCoroutine(roomTimerCoroutine);
            roomTimerCoroutine = null;
        }
    }

    /// <summary>
    /// ตัวจับเวลาห้อง (Room Timer)
    /// </summary>
    private IEnumerator RoomTimerCoroutine()
    {
        float timer = roomTimeLimit;

        while (timer > 0)
        {
            // อัปเดตทุกเฟรมเพื่อความแม่นยำและการแสดงผลที่ลื่นไหล
            UpdateTimeDisplay(timer);

            if (AreAllTasksCompleted())
            {
                UpdateTimeDisplay(0f);
                OnRoomCompletion?.Invoke(this);
                yield break;
            }

            timer -= Time.deltaTime;
            yield return null;
        }

        // เมื่อหมดเวลา
        UpdateTimeDisplay(0f);
        OnRoomTimeout?.Invoke(this);
    }

    /// <summary>
    /// เมธอดอัปเดตค่าเวลาที่แสดงบน Canvas
    /// </summary>
    private void UpdateTimeDisplay(float time)
    {
        // ⭐ ใช้ timeDisplay ที่ถูกลากมาใส่ใน Inspector
        if (timeDisplay == null) return;

        // แปลงเวลาให้เป็นรูปแบบ M:SS
        // ใช้ Mathf.Max(0, time) เพื่อป้องกันการแสดงค่าติดลบหากมีการประมวลผลล่าช้า
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0, time));
        string timeText = string.Format("{0:0}:{1:00}", (int)t.TotalMinutes, t.Seconds);

        timeDisplay.text = timeText;
    }

    #endregion

    // -------------------------------------------------------------------

    #region PUBLIC TASK LOGIC 

    /// <summary>
    /// สุ่มและสร้าง Task หลายอย่างจาก List ที่เป็นไปได้ ตามจำนวนที่กำหนด
    /// </summary>
    public void AssignRandomTask()
    {
        if (roomType != RoomType.Room) return;

        // ล้าง Task เก่า (ถ้ามี)
        foreach (TaskBase oldTask in assignedTasks)
        {
            if (oldTask != null) Destroy(oldTask.gameObject);
        }
        assignedTasks.Clear();

        // ตรวจสอบความถูกต้อง
        if (possibleTaskPrefabs == null || possibleTaskPrefabs.Count == 0 || taskPoints.Count == 0)
        {
            if (taskPoints.Count == 0 && possibleTaskPrefabs.Count > 0)
            {
                Debug.LogWarning($"Room {gameObject.name} has tasks defined but no Task Points to spawn them.");
            }
            return;
        }

        // ตรรกะการสุ่มหลาย Task

        int numTasksToSpawn = Mathf.Min(maxTasksToAssign, taskPoints.Count);

        List<GameObject> availableTaskPrefabs = new List<GameObject>(possibleTaskPrefabs);
        List<Transform> availableTaskPoints = new List<Transform>(taskPoints);

        for (int i = 0; i < numTasksToSpawn; i++)
        {
            if (availableTaskPrefabs.Count == 0 || availableTaskPoints.Count == 0) break;

            // 2. สุ่ม Task
            int randomTaskIndex = UnityEngine.Random.Range(0, availableTaskPrefabs.Count);
            GameObject taskPrefab = availableTaskPrefabs[randomTaskIndex];

            // 3. สุ่ม TaskPoint
            int randomPointIndex = UnityEngine.Random.Range(0, availableTaskPoints.Count);
            Transform spawnPoint = availableTaskPoints[randomPointIndex];

            if (taskPrefab != null && spawnPoint != null)
            {
                // 4. สร้าง Task
                GameObject taskObject = Instantiate(taskPrefab, spawnPoint.position, Quaternion.identity, transform);

                // ⭐ NEW: 4a. หา TasksZone Component จาก GameObject ที่สร้างขึ้น
                TasksZone taskZone = taskObject.GetComponentInChildren<TasksZone>();

                TaskBase newTask = null;

                if (taskZone != null)
                {
                    // 4b. ดึง TaskBase จาก Field 'task' ภายใน TasksZone
                    newTask = taskZone.task;
                }

                // 4c. ตรวจสอบและเพิ่ม TaskBase เข้า AssignedTasks
                if (newTask != null)
                {
                    assignedTasks.Add(newTask);
                    //Debug.Log($"Assigned task {newTask.name} via TasksZone in {gameObject.name}. Total tasks: {assignedTasks.Count}");
                }
                else
                {
                    // หากหา TasksZone หรือ TaskBase ภายใน TasksZone ไม่เจอ
                    Debug.LogError($"Task Prefab '{taskPrefab.name}' instantiated but failed to find TasksZone or TaskBase component assigned within TasksZone. Destroying instance.");
                    Destroy(taskObject);
                    continue; // ข้ามไปสร้าง Task ตัวถัดไป
                }

                // 5. นำ Task และ TaskPoint ที่ใช้แล้วออกจากการสุ่ม
                availableTaskPrefabs.RemoveAt(randomTaskIndex);
                availableTaskPoints.RemoveAt(randomPointIndex);
            }
        }

        // ⭐ ตรวจสอบและเริ่ม Timer
        if (assignedTasks.Count > 0)
        {
            //Debug.Log($"Attempting to start timer for {gameObject.name} (Type: {roomType}) with {assignedTasks.Count} tasks.");
            StartRoomTimer();
        }
        else if (roomType == RoomType.Room && (possibleTaskPrefabs.Count > 0 || taskPoints.Count > 0))
        {
            Debug.LogWarning($"Failed to assign any task to {gameObject.name}. Check if task prefabs have TaskBase component.");
        }
    }

    /// <summary>
    /// ตรวจสอบว่า Task ทั้งหมดในห้องนี้เสร็จสิ้นแล้วหรือไม่
    /// </summary>
    public bool AreAllTasksCompleted()
    {
        if (roomType != RoomType.Room)
        {
            return true;
        }
        if (possibleTaskPrefabs.Count == 0) return true;
        if (assignedTasks.Count == 0) return false;

        return assignedTasks.All(task => task.IsCompleted);
    }

    /// <summary>
    /// ล้าง Task ที่ Assign ไว้ออกจาก List 
    /// </summary>
    public void ClearAssignedTasks()
    {
        assignedTasks.Clear();
    }

    #endregion

    // -------------------------------------------------------------------

    #region PUBLIC CONNECTION LOGIC

    public bool HasAvailableConnector(out Transform connector)
    {
        connector = null;
        if (connectors.Count == 1)
        {
            return TryConnect(connectors[0], out connector);
        }

        for (int i = 0; i < MAX_CONNECTOR_RETRIES; i++)
        {
            int randomConnectIndex = UnityEngine.Random.Range(0, connectors.Count);
            Transform potentialConnector = connectors[randomConnectIndex];

            if (TryConnect(potentialConnector, out connector))
            {
                return true;
            }
        }

        return false;
    }

    public void UnuseConnector(Transform connector)
    {
        if (connector.TryGetComponent<Connector>(out Connector connectComponent))
        {
            connectComponent.SetOccupied(false);
        }
    }

    #endregion

    // -------------------------------------------------------------------

    #region PRIVATE HELPER METHODS

    private bool TryConnect(Transform potentialConnector, out Transform connector)
    {
        connector = null;
        if (potentialConnector.TryGetComponent<Connector>(out Connector connComponent))
        {
            if (!connComponent.IsOccupied())
            {
                connector = potentialConnector;
                connComponent.SetOccupied(true);
                return true;
            }
        }
        return false;
    }

    #endregion
}