using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Collections;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// (Optimized Version)
/// เก็บข้อมูลพื้นฐาน, สถานะ Task, และตรรกะการเชื่อมต่อสำหรับห้อง
/// จัดการเวลาและ Task ของตัวเอง และแจ้ง CoreManager เมื่อเสร็จสิ้น/หมดเวลา
/// </summary>
public class RoomData : MonoBehaviour
{
    #region ENUMS

    public enum RoomType
    {
        Spawn,
        Room,
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

    // --- Optimization Fields ---
    [Tooltip("ความถี่ในการตรวจสอบ Task Completion (ทุกๆ กี่เฟรม)")]
    private const int CHECK_COMPLETION_FREQUENCY = 15; // ตรวจสอบทุก 15 เฟรม (ประมาณ 4 ครั้งต่อวินาทีที่ 60fps)
    private int frameCheckCounter = 0;
    // -----------------------------

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
        UpdateTimeDisplay(0f);
    }

    public void StartRoomTimer()
    {
        if (roomType != RoomType.Room || assignedTasks.Count == 0) return;

        StopRoomTimer();
        UpdateTimeDisplay(roomTimeLimit);
        frameCheckCounter = 0; // รีเซ็ตตัวนับเฟรม
        roomTimerCoroutine = StartCoroutine(RoomTimerCoroutine());
    }

    public void StopRoomTimer()
    {
        if (roomTimerCoroutine != null)
        {
            StopCoroutine(roomTimerCoroutine);
            roomTimerCoroutine = null;
        }
    }

    /// <summary>
    /// (Optimized) ตัวจับเวลาห้อง (Room Timer)
    /// </summary>
    private IEnumerator RoomTimerCoroutine()
    {
        float timer = roomTimeLimit;

        while (timer > 0)
        {
            // 1. อัปเดต UI และเวลาก่อน
            UpdateTimeDisplay(timer);
            timer -= Time.deltaTime;

            // --- Optimization Start ---
            // 2. ตรวจสอบ Task Completion เป็นระยะๆ ไม่ใช่ทุกเฟรม
            frameCheckCounter++;
            if (frameCheckCounter >= CHECK_COMPLETION_FREQUENCY)
            {
                frameCheckCounter = 0; // รีเซ็ตตัวนับ

                // 3. (Optimized) เรียกใช้ AreAllTasksCompleted (เวอร์ชันไม่ใช้ LINQ)
                if (AreAllTasksCompleted())
                {
                    UpdateTimeDisplay(0f);
                    OnRoomCompletion?.Invoke(this);
                    yield break; // จบ Coroutine
                }
            }
            // --- Optimization End ---

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
        if (timeDisplay == null) return;
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0, time));
        string timeText = string.Format("{0:0}:{1:00}", (int)t.TotalMinutes, t.Seconds);
        timeDisplay.text = timeText;
    }

    #endregion

    // -------------------------------------------------------------------

    #region PUBLIC TASK LOGIC 

    public void AssignRandomTask()
    {
        if (roomType != RoomType.Room) return;

        foreach (TaskBase oldTask in assignedTasks)
        {
            if (oldTask != null) Destroy(oldTask.gameObject);
        }
        assignedTasks.Clear();

        if (possibleTaskPrefabs == null || possibleTaskPrefabs.Count == 0 || taskPoints.Count == 0)
        {
            if (taskPoints.Count == 0 && possibleTaskPrefabs.Count > 0)
            {
                Debug.LogWarning($"Room {gameObject.name} has tasks defined but no Task Points to spawn them.");
            }
            return;
        }

        int numTasksToSpawn = Mathf.Min(maxTasksToAssign, taskPoints.Count);
        List<GameObject> availableTaskPrefabs = new List<GameObject>(possibleTaskPrefabs);
        List<Transform> availableTaskPoints = new List<Transform>(taskPoints);

        for (int i = 0; i < numTasksToSpawn; i++)
        {
            if (availableTaskPrefabs.Count == 0 || availableTaskPoints.Count == 0) break;

            int randomTaskIndex = UnityEngine.Random.Range(0, availableTaskPrefabs.Count);
            GameObject taskPrefab = availableTaskPrefabs[randomTaskIndex];

            int randomPointIndex = UnityEngine.Random.Range(0, availableTaskPoints.Count);
            Transform spawnPoint = availableTaskPoints[randomPointIndex];

            if (taskPrefab != null && spawnPoint != null)
            {
                GameObject taskObject = Instantiate(taskPrefab, spawnPoint.position, Quaternion.identity, transform);
                TasksZone taskZone = taskObject.GetComponentInChildren<TasksZone>();
                TaskBase newTask = null;

                if (taskZone != null)
                {
                    newTask = taskZone.task;
                }

                if (newTask != null)
                {
                    assignedTasks.Add(newTask);
                }
                else
                {
                    Debug.LogError($"Task Prefab '{taskPrefab.name}' instantiated but failed to find TasksZone or TaskBase component assigned within TasksZone. Destroying instance.");
                    Destroy(taskObject);
                    continue;
                }

                availableTaskPrefabs.RemoveAt(randomTaskIndex);
                availableTaskPoints.RemoveAt(randomPointIndex);
            }
        }

        if (assignedTasks.Count > 0)
        {
            StartRoomTimer();
        }
        else if (roomType == RoomType.Room && (possibleTaskPrefabs.Count > 0 || taskPoints.Count > 0))
        {
            Debug.LogWarning($"Failed to assign any task to {gameObject.name}. Check if task prefabs have TaskBase component.");
        }
    }

    /// <summary>
    /// (Optimized) ตรวจสอบว่า Task ทั้งหมดในห้องนี้เสร็จสิ้นแล้วหรือไม่ (โดยไม่ใช้ LINQ)
    /// </summary>
    public bool AreAllTasksCompleted()
    {
        if (roomType != RoomType.Room)
        {
            return true;
        }
        if (possibleTaskPrefabs.Count == 0) return true;
        if (assignedTasks.Count == 0) return false;

        // --- Optimization: Replaced LINQ .All() with a fast foreach loop ---
        foreach (TaskBase task in assignedTasks)
        {
            if (!task.IsCompleted)
            {
                return false; // เจอ Task ที่ยังไม่เสร็จ, ออกจาก Loop ทันที
            }
        }
        return true; // วนครบทุก Task และทุกอันเสร็จหมด
        // -----------------------------------------------------------------
    }

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

    /// <summary>
    /// (NEW OPTIMIZATION) ตรวจสอบว่ามี Connector ที่ว่างเหลืออยู่หรือไม่ (แบบ Read-only "Peek")
    /// </summary>
    public bool HasAnyAvailableConnector()
    {
        foreach (Transform connectorT in connectors)
        {
            if (connectorT.TryGetComponent<Connector>(out Connector connComponent))
            {
                if (!connComponent.IsOccupied())
                {
                    return true; // เจออย่างน้อย 1 อันที่ว่าง
                }
            }
        }
        return false; // ไม่มีที่ว่างเลย
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