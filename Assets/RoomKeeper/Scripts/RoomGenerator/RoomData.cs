using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RoomData : MonoBehaviour
{
    #region ENUMS

    public enum RoomType { Spawn, Room, Hallway }

    #endregion

    #region FIELDS & EVENTS

    public event Action<RoomData> OnRoomCompletion;

    [Header("Room Setup")]
    public RoomType roomType;
    public bool allowRotation = true;
    public Collider2D collider2DForComposit;
    public Collider2D roomBoundsCollider;
    [SerializeField] private LayerMask roomLayerMask;

    [Header("Spawn Settings")]
    [Tooltip("จุดที่ผู้เล่นจะเกิดหรือถูกส่งกลับมาเมื่อเริ่มเกมหรือ Revive (ลาก Transform มาใส่ที่นี่)")]
    public Transform playerSpawnPoint;

    [Header("Connection Points")]
    public List<Transform> connectors;

    [Header("Connection Info")]
    public Transform parentConnector;

    [Header("Game Logic & Tasks")]
    [SerializeField] private int maxTasksToAssign = 1;

    [Header("Task Setup")]
    [SerializeField] private List<TasksZone> manuallyAssignedZones;

    private List<TaskBase> assignedTasks;
    private List<TaskBase> allTasksInRoom;

    private const int MAX_CONNECTOR_RETRIES = 5;

    public List<TaskBase> AssignedTasks => assignedTasks;

    #endregion

    #region LIFECYCLE

    private void Awake()
    {
        assignedTasks = new List<TaskBase>();
        allTasksInRoom = new List<TaskBase>();
    }

    private void OnValidate()
    {
        maxTasksToAssign = Mathf.Max(1, maxTasksToAssign);
    }

    private void OnDestroy()
    {
        assignedTasks?.Clear();
        allTasksInRoom?.Clear();
    }

    #endregion

    #region TASK LOGIC

    public void SpawnAndInitAllTasks()
    {
        assignedTasks.Clear();
        allTasksInRoom.Clear();

        if (manuallyAssignedZones == null)
        {
            manuallyAssignedZones = new List<TasksZone>();
        }

        foreach (TasksZone zone in manuallyAssignedZones)
        {
            if (zone == null)
            {
                Debug.LogWarning($"RoomData ({gameObject.name}) มีช่องว่าง (Null) ใน List 'manuallyAssignedZones'", this);
                continue;
            }

            zone.InitializeAndSpawnTask(this);
            zone.SetTaskActive(false);

            TaskBase spawnedTask = zone.GetTaskInstance();
            if (spawnedTask != null)
            {
                allTasksInRoom.Add(spawnedTask);
            }
        }
    }

    public void ActivateRandomTasks()
    {
        if (roomType != RoomType.Room) return;

        assignedTasks.Clear();

        if (allTasksInRoom == null || allTasksInRoom.Count == 0)
        {
            return;
        }

        // --- DECK SYSTEM INTEGRATION ---

        List<TaskBase> tasksToActivate = new List<TaskBase>();

        if (GameCoreManager.Instance != null)
        {
            // เรียกใช้ Logic จาก GameCoreManager โดยตรง
            tasksToActivate = GameCoreManager.Instance.GetPrioritizedTasks(allTasksInRoom, maxTasksToAssign);
        }
        else
        {
            // Fallback กรณี Test Scene ไม่มี GameCoreManager
            Debug.LogWarning("GameCoreManager not found! Using local random fallback.");
            List<TaskBase> tempPool = new List<TaskBase>(allTasksInRoom);
            int count = Mathf.Min(maxTasksToAssign, tempPool.Count);

            for (int i = 0; i < count; i++)
            {
                int rnd = UnityEngine.Random.Range(0, tempPool.Count);
                tasksToActivate.Add(tempPool[rnd]);
                tempPool.RemoveAt(rnd);
            }
        }

        // --- END SELECTION ---

        foreach (TaskBase taskToActivate in tasksToActivate)
        {
            if (taskToActivate != null)
            {
                TasksZone correspondingZone = FindZoneForTask(taskToActivate);

                if (correspondingZone != null)
                {
                    correspondingZone.SetTaskActive(true);
                    assignedTasks.Add(taskToActivate);
                }
                else
                {
                    Debug.LogWarning($"RoomData ({gameObject.name}) เลือก Task '{taskToActivate.name}' แต่หา Zone ไม่เจอ!");
                }
            }
        }

        if (assignedTasks.Count == 0 && allTasksInRoom.Count > 0)
        {
            Debug.LogWarning($"Room {gameObject.name} has tasks, but failed to activate any.");
        }
    }

    public void CheckForCompletion()
    {
        if (AreAllTasksCompleted())
        {
            OnRoomCompletion?.Invoke(this);
        }
    }

    public bool AreAllTasksCompleted()
    {
        if (roomType != RoomType.Room) return true;
        if (allTasksInRoom.Count == 0) return true;
        if (assignedTasks.Count == 0) return false;

        foreach (TaskBase task in assignedTasks)
        {
            if (!task.IsCompleted) return false;
        }

        return true;
    }

    public void ClearAssignedTasks()
    {
        assignedTasks.Clear();
    }

    #endregion

    #region CONNECTION LOGIC

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
                if (!connComponent.IsOccupied()) return true;
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

    private TasksZone FindZoneForTask(TaskBase task)
    {
        foreach (TasksZone zone in manuallyAssignedZones)
        {
            if (zone.GetTaskInstance() == task)
            {
                return zone;
            }
        }
        return null;
    }

    #endregion
}