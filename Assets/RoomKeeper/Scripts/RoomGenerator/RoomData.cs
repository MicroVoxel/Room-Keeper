using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Linq;

public class RoomData : MonoBehaviour
{
    // ... (Enums, Fields, Properties, Lifecycle, Timer... ทั้งหมดเหมือนเดิม) ...
    #region ENUMS
    public enum RoomType { Spawn, Room, Hallway }
    #endregion

    #region FIELDS & PROPERTIES
    public event Action<RoomData> OnRoomCompletion;
    public event Action<RoomData> OnRoomTimeout;
    [Header("Room Setup")]
    public RoomType roomType;
    public Collider2D collider2DForComposit;
    public Collider2D roomBoundsCollider;
    [SerializeField] private LayerMask roomLayerMask;
    [Header("Connection Points")]
    public List<Transform> connectors;
    [Header("Connection Info")]
    public Transform parentConnector;
    [Header("Task Spawn Points")]
    public List<Transform> taskPoints;
    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI timeDisplay;
    public TextMeshProUGUI TimeDisplay => timeDisplay;
    [Header("Game Logic & Tasks")]
    public float roomTimeLimit = 60f;
    [SerializeField] private List<GameObject> possibleTaskPrefabs;
    [SerializeField] private int maxTasksToAssign = 1;
    private List<TaskBase> assignedTasks;
    private Coroutine roomTimerCoroutine;
    public List<TaskBase> AssignedTasks => assignedTasks;
    private const int MAX_CONNECTOR_RETRIES = 5;
    #endregion

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
    private IEnumerator RoomTimerCoroutine()
    {
        float timer = roomTimeLimit;
        while (timer > 0)
        {
            UpdateTimeDisplay(timer);
            timer -= Time.deltaTime;
            yield return null;
        }
        UpdateTimeDisplay(0f);
        OnRoomTimeout?.Invoke(this);
    }
    private void UpdateTimeDisplay(float time)
    {
        if (timeDisplay == null) return;
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0, time));
        string timeText = string.Format("{0:0}:{1:00}", (int)t.TotalMinutes, t.Seconds);
        timeDisplay.text = timeText;
    }
    #endregion

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
                // 1. Spawn 'TasksZone' (ตัวชน) และตั้ง 'transform' (RoomData) เป็น Parent
                GameObject taskObject = Instantiate(taskPrefab, spawnPoint.position, Quaternion.identity, transform);

                // --- ⭐ CHANGED ---
                // 2. ค้นหา Component 'TasksZone' (เพื่อเช็คว่า Prefab ถูกต้อง)
                TasksZone taskZone = taskObject.GetComponentInChildren<TasksZone>();

                if (taskZone == null)
                {
                    Debug.LogError($"Task Prefab '{taskPrefab.name}' instantiated but failed to find TasksZone component. Destroying instance.");
                    Destroy(taskObject);
                    continue;
                }
                // 3. (REMOVED) ลบการเรียก 'taskZone.InitializeAndRegister(this);'
                //    'TasksZone.Awake()' จะทำงานและ 'RegisterSpawnedTask' กลับมาเอง
                // --- END CHANGED ---

                availableTaskPrefabs.RemoveAt(randomTaskIndex);
                availableTaskPoints.RemoveAt(randomPointIndex);
            }
        }

        // (โค้ดส่วนนี้ยังคงเดิม และสำคัญมาก)
        // เรายังต้องรอ 1 เฟรม ให้ Awake() ของ TasksZone ทำงานเสร็จ
        StartCoroutine(StartTimerAfterInitialization());
    }
    private IEnumerator StartTimerAfterInitialization()
    {
        yield return null;
        if (assignedTasks.Count > 0)
        {
            StartRoomTimer();
        }
        else if (roomType == RoomType.Room && (possibleTaskPrefabs.Count > 0 || taskPoints.Count > 0))
        {
            Debug.LogWarning($"Failed to assign or register any task to {gameObject.name}.");
        }
    }
    public void RegisterSpawnedTask(TaskBase task)
    {
        if (task != null)
        {
            assignedTasks.Add(task);
        }
    }

    public void CheckForCompletion()
    {
        if (AreAllTasksCompleted())
        {
            StopRoomTimer();
            UpdateTimeDisplay(0f);
            OnRoomCompletion?.Invoke(this);
        }
    }

    public bool AreAllTasksCompleted()
    {
        if (roomType != RoomType.Room)
        {
            return true;
        }
        if (possibleTaskPrefabs.Count == 0) return true;
        if (assignedTasks.Count == 0 && possibleTaskPrefabs.Count > 0) return false;
        if (assignedTasks.Count == 0) return false;
        foreach (TaskBase task in assignedTasks)
        {
            if (!task.IsCompleted)
            {
                return false;
            }
        }
        return true;
    }

    public void ClearAssignedTasks()
    {
        assignedTasks.Clear();
    }
    #endregion

    // ... (ส่วน Connection Logic... ไม่มีการเปลี่ยนแปลง) ...
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
    public bool HasAnyAvailableConnector()
    {
        foreach (Transform connectorT in connectors)
        {
            if (connectorT.TryGetComponent<Connector>(out Connector connComponent))
            {
                if (!connComponent.IsOccupied())
                {
                    return true;
                }
            }
        }
        return false;
    }
    #endregion
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