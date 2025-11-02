using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; 
using System;

/// <summary>
/// จัดการ Game Loop หลัก, รับ Event จาก RoomData, และสั่งการทำลาย/สร้างห้อง
/// ระบบทำงานโดยอิสระ ไม่ขึ้นอยู่กับการเคลื่อนที่ของผู้เล่น (Player)
/// </summary>
public class GameCoreManager : MonoBehaviour
{
    public static GameCoreManager Instance { get; private set; }

    [Header("Game Time Settings")]
    [Tooltip("เวลารวมทั้งหมดของเกม (วินาที)")]
    public float totalGameDuration = 300f; // 5 นาที

    [Header("Room Generation Settings")]
    [Tooltip("จำนวนห้องที่ต้องการสร้างใหม่ เมื่อห้องเก่าถูกทำลายหรือเสร็จสิ้น")]
    public int roomsToCreatePerEvent = 2; // สร้าง 2 ห้องพร้อมกัน/ต่อครั้ง

    [Tooltip("ช่วงเวลา (วินาที) ในการสร้างห้องใหม่โดยอัตโนมัติ")]
    public float roomCreationInterval = 10f; 
    [SerializeField] private float roomCreationTimer; 

    [Header("Global UI Reference")]
    [Tooltip("ลาก TextMeshProUGUI Component ที่จะแสดงเวลารวมของเกมมาใส่")]
    [SerializeField] private TextMeshProUGUI globalTimeDisplay;

    [Header("Core Systems")]
    [SerializeField] private DungeonGenerator dungeonGenerator;

    private bool isGameActive = false;
    private RoomData spawnRoom;

    private List<RoomData> activeRooms = new List<RoomData>();

    [Header("Player Reference")]
    [SerializeField] private PlayerController playerController;

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

    private void Start()
    {
        if (dungeonGenerator == null)
        {
            Debug.LogError("DungeonGenerator not found! Game cannot start.");
            return;
        }

        StartGameInitialization();

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }
    }

    // -------------------------------------------------------------------
    // ⭐ 1. เริ่มเกมและสร้าง Spawn Room
    // -------------------------------------------------------------------

    private void StartGameInitialization()
    {
        spawnRoom = dungeonGenerator.GenerateSpawn();

        if (spawnRoom != null)
        {
            roomCreationTimer = roomCreationInterval;

            // เริ่มลูปเกมหลัก (ตรวจจับเวลาเกมรวมเท่านั้น)
            StartCoroutine(GameLoopCoroutine());
        }
        else
        {
            Debug.LogError("Failed to initialize Spawn Room.");
        }
    }

    // -------------------------------------------------------------------
    // ⭐ 2. ลูปเกมหลัก (Game Loop - ตรวจจับเวลาเกมรวมเท่านั้น)
    // -------------------------------------------------------------------

    private IEnumerator GameLoopCoroutine()
    {
        isGameActive = true;
        float gameTimeRemaining = totalGameDuration;

        while (gameTimeRemaining > 0 && isGameActive)
        {
            UpdateGameTimeDisplay(gameTimeRemaining);

            roomCreationTimer += Time.deltaTime;

            if (roomCreationTimer >= roomCreationInterval)
            {
                TryCreateMultipleRooms(roomsToCreatePerEvent);
                roomCreationTimer = 0f; // รีเซ็ตตัวนับเวลา
            }

            gameTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        // 4. จบเกมเมื่อหมดเวลา
        UpdateGameTimeDisplay(0f); // อัปเดตเป็น 0 ก่อนจบ
        EndGame(gameTimeRemaining <= 0);
    }

    /// <summary>
    /// ⭐ NEW: เมธอดอัปเดตค่าเวลาเกมรวมที่แสดงบน Canvas
    /// </summary>
    private void UpdateGameTimeDisplay(float time)
    {
        if (globalTimeDisplay == null) return;

        // แปลงเวลาให้เป็นรูปแบบ M:SS
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0, time));
        string timeText = string.Format("{0:0}:{1:00}", (int)t.TotalMinutes, t.Seconds);

        globalTimeDisplay.text = timeText;
    }

    // -------------------------------------------------------------------
    // ⭐ 3. Event Handlers (รับการแจ้งเตือนจาก RoomData)
    // -------------------------------------------------------------------

    private void SubscribeToRoomEvents(RoomData room)
    {
        // เชื่อมต่อ Event เมื่อ Task เสร็จ
        room.OnRoomCompletion += HandleRoomCompletion;
        // เชื่อมต่อ Event เมื่อหมดเวลา
        room.OnRoomTimeout += HandleRoomTimeout;
    }

    private void UnsubscribeFromRoomEvents(RoomData room)
    {
        room.OnRoomCompletion -= HandleRoomCompletion;
        room.OnRoomTimeout -= HandleRoomTimeout;
    }

    private void HandleRoomCompletion(RoomData completedRoom)
    {
        Debug.Log($"CoreManager received completion from {completedRoom.name}.");

        // 1. Unsubscribe เพื่อล้าง Event
        UnsubscribeFromRoomEvents(completedRoom);

        // 2. ล้าง Task สถานะของห้องนี้ 
        completedRoom.ClearAssignedTasks();
       
    }

    private void HandleRoomTimeout(RoomData timedOutRoom)
    {
        //Debug.Log($"CoreManager received timeout from {timedOutRoom.name}. Destroying room.");

        // 1. Unsubscribe เพื่อล้าง Event ก่อนทำลายห้อง
        UnsubscribeFromRoomEvents(timedOutRoom);

        // 2. สั่งทำลายห้อง (และเอาออกจาก DungeonGenerator)
        DestroyRoom(timedOutRoom);
       
    }

    // -------------------------------------------------------------------
    // ⭐ 4. ตรรกะการจัดการห้อง (ทำงานอัตโนมัติ)
    // -------------------------------------------------------------------

    /// <summary>
    /// ⭐ NEW: พยายามสร้าง Hallway-Room chain ใหม่ ตามจำนวนที่กำหนด
    /// </summary>
    public void TryCreateMultipleRooms(int count)
    {
        int successfulCreations = 0;

        // ตั้งค่าความพยายามสูงสุดเพื่อป้องกัน Infinite Loop หาก Connector มีปัญหาการชนซ้ำๆ
        const int MAX_ATTEMPTS_PER_ROOM = 3;

        // วนลูปตามจำนวนห้องที่ต้องการสร้าง
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < MAX_ATTEMPTS_PER_ROOM; attempt++)
            {
                if (TryCreateSingleRoomChain())
                {
                    successfulCreations++;
                    break; // สร้างสำเร็จ, ข้ามไปสร้างห้องถัดไป (i++)
                }
            }
        }

        if (successfulCreations > 0)
        {
            Debug.Log($"Successfully created {successfulCreations} new room chains.");
        }
        else
        {
            Debug.LogWarning($"Failed to create any new rooms after {count * MAX_ATTEMPTS_PER_ROOM} attempts. Spawn connectors might be full or facing persistent intersections.");
        }
    }

    /// <summary>
    /// ⭐ RENAME: พยายามสร้าง Hallway-Room chain ใหม่ 1 ชุด
    /// </summary>
    private bool TryCreateSingleRoomChain() // เปลี่ยนชื่อและเปลี่ยน return type เป็น bool
    {
        if (spawnRoom == null) return false;

        // ตรวจสอบว่า Spawn Room มี Connector ว่างหรือไม่
        if (spawnRoom.HasAvailableConnector(out Transform startConnector))
        {
            GameObject hallwayPrefab = dungeonGenerator.SelectRandomHallwayPrefab();
            GameObject roomPrefab = dungeonGenerator.SelectNextRoomPrefab();

            if (hallwayPrefab == null || roomPrefab == null)
            {
                // Fail 1: Prefab หายไป
                spawnRoom.UnuseConnector(startConnector);
                return false;
            }

            // 1. วาง Hallway
            if (dungeonGenerator.TryPlaceRoom(spawnRoom, startConnector, hallwayPrefab))
            {
                RoomData hallwayData = dungeonGenerator.GeneratedRooms.Last();
                hallwayData.parentConnector = startConnector;

                // 2. วาง Room ต่อจาก Hallway
                if (hallwayData.HasAvailableConnector(out Transform hallwayConnector))
                {
                    if (dungeonGenerator.TryPlaceRoom(hallwayData, hallwayConnector, roomPrefab))
                    {
                        RoomData newRoom = dungeonGenerator.GeneratedRooms.Last();
                        newRoom.parentConnector = hallwayConnector;
                        //Debug.Log($"New Hallway-Room chain generated: {newRoom.name}");

                        // 3. กำหนด Task (AssignRandomTask จะเรียก StartRoomTimer() เอง)
                        newRoom.AssignRandomTask();

                        // 4. Subscribe Event
                        SubscribeToRoomEvents(newRoom);

                        return true; // ⭐ SUCCESS
                    }
                    // ROLLBACK 1: วาง Room ต่อ Hallway ล้มเหลว (เช่น ชน)
                    else
                    {
                        dungeonGenerator.RemoveRoom(hallwayData);
                        spawnRoom.UnuseConnector(startConnector);
                        return false; // ล้มเหลวในรอบนี้
                    }
                }
                // ROLLBACK 2: Hallway ไม่มี Connector ว่างเหลืออยู่
                else
                {
                    dungeonGenerator.RemoveRoom(hallwayData);
                    spawnRoom.UnuseConnector(startConnector);
                    return false; // ล้มเหลวในรอบนี้
                }
            }
            // ROLLBACK 3: วาง Hallway ล้มเหลว (เช่น ชนตั้งแต่แรก)
            else
            {
                spawnRoom.UnuseConnector(startConnector);
                return false; // ล้มเหลวในรอบนี้
            }
        }
        // Connector หมดแล้ว (ไม่ถือว่าเป็นความล้มเหลว)
        // การสร้างห้องทั้งหมดจะหยุดลงโดยธรรมชาติเมื่อ Spawn Room ไม่มี Connector
        // และ Log Warning จะถูกแสดงใน TryCreateMultipleRooms
        return false;
    }

    /// <summary>
    /// สั่งทำลายห้องที่หมดเวลา/ออกจาก List พร้อมจัดการ Hallway และ Connector
    /// </summary>
    public void DestroyRoom(RoomData roomToDestroy)
    {
        if (roomToDestroy != null && roomToDestroy.roomType != RoomData.RoomType.Spawn)
        {
            // 1. จัดการผู้เล่นวาร์ป (โค้ดเดิม)
            if (IsPlayerInRoom(roomToDestroy))
            {
                Debug.LogWarning($"Player detected in room {roomToDestroy.name}. Teleporting player to Spawn Room.");
                TeleportPlayerToSpawn();
            }

            // ⭐ SIMPLIFIED: สั่ง DungeonGenerator ให้จัดการทำลาย GameObject, Hallway, และล้าง Connector
            // (Hallway จะถูกทำลายใน DungeonGenerator.RemoveRoom)
            dungeonGenerator.RemoveRoom(roomToDestroy);
        }
    }

    /// <summary>
    /// ตรวจสอบว่าผู้เล่นอยู่ใน Collider ของห้องที่กำลังจะถูกทำลายหรือไม่
    /// </summary>
    private bool IsPlayerInRoom(RoomData room)
    {
        // ⭐ ใช้ roomBoundsCollider แทน room.collider2D
        if (playerController == null || playerController.transform == null || room.roomBoundsCollider == null) return false;

        //Debug.Log($"Checking Room: {room.name}");
        //Debug.Log($"Room Bounds Center: {room.roomBoundsCollider.bounds.center}");
        //Debug.Log($"Room Bounds Size: {room.roomBoundsCollider.bounds.size}");
        //Debug.Log($"Player Position: {playerController.transform.position}");

        // ⭐ ใช้ roomBoundsCollider.bounds
        bool isInBounds = room.roomBoundsCollider.bounds.Contains(playerController.transform.position);

        //Debug.Log($"Is Player in Room: {isInBounds}");

        return isInBounds;
    }

    /// <summary>
    /// วาร์ปผู้เล่นไปยังตำแหน่งเริ่มต้นของ Spawn Room
    /// </summary>
    private void TeleportPlayerToSpawn()
    {
        if (spawnRoom == null || playerController == null)
        {
            Debug.LogError("Cannot teleport player: Spawn Room or Player Controller is missing!");
            return;
        }

        // ⭐ สมมติว่า Spawn Room มีตำแหน่งเริ่มต้นสำหรับผู้เล่น (เช่น taskPoints[0] หรือจุดศูนย์กลาง)
        Vector3 spawnPosition = spawnRoom.transform.position;
        spawnPosition = spawnRoom.transform.position;

        // ย้ายผู้เล่นไปยังตำแหน่งวาร์ป (การใช้ Rigidbody.position หรือ Transform.position ขึ้นอยู่กับ PlayerController)
        playerController.transform.position = spawnPosition;
        //Debug.Log("Player Teleport");
    }

    private void EndGame(bool timeUp)
    {
        isGameActive = false;
        StopAllCoroutines();

        // ... (โค้ดจบเกม) ...
    }
}