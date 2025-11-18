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
        // ย้ายตรรกะการ Spawn Task ออกไป เพื่อรอการยืนยันการวางห้อง
        assignedTasks = new List<TaskBase>();
        allTasksInRoom = new List<TaskBase>();
        _taskSelectionList = new List<TaskBase>();

        // ส่วนของการ Spawn Task จะถูกเรียกใช้ในเมธอด SpawnAndInitAllTasks() แทน
    }

    private void OnValidate()
    {
        maxTasksToAssign = Mathf.Max(1, maxTasksToAssign);
    }

    /// <summary>
    /// ทำความสะอาดรายการ List ต่างๆ เมื่อ Room ถูกทำลาย เพื่อจัดการหน่วยความจำ
    /// (Task UI จะถูกลบโดย TasksZone.OnDestroy() ที่ถูกเรียกตามมาโดยอัตโนมัติ)
    /// </summary>
    private void OnDestroy()
    {
        // เคลียร์ List เพื่อคืนหน่วยความจำและตัดการอ้างอิง
        assignedTasks?.Clear();
        allTasksInRoom?.Clear();
        _taskSelectionList?.Clear();
    }

    #endregion

    #region TASK LOGIC

    /// <summary>
    /// ต้องถูกเรียกหลังจากวางห้องสำเร็จแล้วเท่านั้น เพื่อทำการ Spawn UI Tasks 
    /// ที่รับผิดชอบลง Canvas และรวบรวม TaskBase Instances
    /// </summary>
    public void SpawnAndInitAllTasks()
    {
        // ตั้งค่า List ขึ้นมาใหม่ (เผื่อ Room ถูก reuse หรือเรียกซ้ำ)
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

            // 1. Initialize and Spawn Task UI (จุดที่เคยอยู่ใน Awake)
            zone.InitializeAndSpawnTask(this);

            // 2. Collect TaskBase instance
            TaskBase spawnedTask = zone.GetTaskInstance();

            if (spawnedTask != null)
            {
                allTasksInRoom.Add(spawnedTask);
            }

            zone.gameObject.SetActive(false);
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
                    correspondingZone.gameObject.SetActive(true);
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
        if (roomType != RoomType.Room)
        {
            return true;
        }

        if (allTasksInRoom.Count == 0)
        {
            return true;
        }

        if (assignedTasks.Count == 0)
        {
            return false;
        }

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