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

    [Header("Spawn Point Settings")]
    [Tooltip("ลาก Empty GameObject ที่เป็นจุดเกิดของผู้เล่นมาใส่ตรงนี้ (เฉพาะห้อง Spawn)")]
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
    private List<TaskBase> _taskSelectionList;
    private const int MAX_CONNECTOR_RETRIES = 5;

    public List<TaskBase> AssignedTasks => assignedTasks;

    #endregion

    #region LIFECYCLE

    private void Awake()
    {
        assignedTasks = new List<TaskBase>();
        allTasksInRoom = new List<TaskBase>();
        _taskSelectionList = new List<TaskBase>();
    }

    private void OnValidate()
    {
        maxTasksToAssign = Mathf.Max(1, maxTasksToAssign);
    }

    private void OnDestroy()
    {
        assignedTasks?.Clear();
        allTasksInRoom?.Clear();
        _taskSelectionList?.Clear();
    }

    private void OnDrawGizmos()
    {
        // วาด Gizmos เพื่อให้เห็นจุด Spawn ใน Scene View
        if (playerSpawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerSpawnPoint.position, 0.5f);
            Gizmos.DrawLine(playerSpawnPoint.position, playerSpawnPoint.position + Vector3.up * 2);
        }
    }

    #endregion

    #region TASK LOGIC

    public void SpawnAndInitAllTasks()
    {
        assignedTasks.Clear();
        allTasksInRoom.Clear();
        _taskSelectionList.Clear();

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

            // เริ่มต้น: สั่งให้ Zone นี้ Inactive ไปก่อน (ห้ามเดินชน, ห้ามโชว์ Indicator)
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

        _taskSelectionList.Clear();
        _taskSelectionList.AddRange(allTasksInRoom);

        int numTasksToActivate = Mathf.Min(maxTasksToAssign, _taskSelectionList.Count);

        for (int i = 0; i < numTasksToActivate; i++)
        {
            if (_taskSelectionList.Count == 0) break;

            int randomIndex = UnityEngine.Random.Range(0, _taskSelectionList.Count);
            TaskBase taskToActivate = _taskSelectionList[randomIndex];

            if (taskToActivate != null)
            {
                TasksZone correspondingZone = FindZoneForTask(taskToActivate);

                if (correspondingZone != null)
                {
                    // UPDATED: เปลี่ยนมาใช้ SetTaskActive(true) ซึ่งจะเปิดทั้ง Indicator และอนุญาตให้เดินชนได้
                    correspondingZone.SetTaskActive(true);

                    assignedTasks.Add(taskToActivate);
                }
                else
                {
                    Debug.LogWarning($"RoomData ({gameObject.name}) สุ่ม Task '{taskToActivate.name}' แต่หา Zone (Trigger) ที่คู่กันไม่เจอ!");
                }

                _taskSelectionList.RemoveAt(randomIndex);
            }
        }

        if (assignedTasks.Count == 0 && allTasksInRoom.Count > 0)
        {
            Debug.LogWarning($"Room {gameObject.name} has tasks, but failed to activate any (maxTasksToAssign might be 0?)");
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

    #region CONNECTION LOGIC (UNCHANGED)

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

    #region HELPER METHODS

    /// <summary>
    /// ดึงตำแหน่ง Spawn Point ถ้ามี (ถ้าไม่มีจะคืนค่าตำแหน่งของตัวห้องเอง)
    /// </summary>
    public Vector3 GetPlayerSpawnPosition()
    {
        if (playerSpawnPoint != null)
        {
            return playerSpawnPoint.position;
        }

        // Fallback กรณีลืมใส่ SpawnPoint
        return transform.position;
    }

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