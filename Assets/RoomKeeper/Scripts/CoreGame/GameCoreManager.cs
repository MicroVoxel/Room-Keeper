using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System;
using UnityEngine.UI;

/// <summary>
/// (Updated Logic)
/// จัดการ Game Loop หลัก, รับ Event จาก RoomData, และสั่งการทำลาย/สร้างห้อง
/// (เพิ่มระบบ Main Progress Bar และ Star System)
/// </summary>
public class GameCoreManager : MonoBehaviour
{
    #region Singleton
    public static GameCoreManager Instance { get; private set; }
    #endregion

    #region Fields & Properties

    [Header("Game Time Settings")]
    [Tooltip("เวลารวมทั้งหมดของเกม (วินาที)")]
    public float totalGameDuration = 300f; // 5 นาที

    [Header("Room Generation Settings")]
    [Tooltip("จำนวนห้องที่ต้องการสร้างใหม่ เมื่อห้องเก่าถูกทำลายหรือเสร็จสิ้น")]
    public int roomsToCreatePerEvent = 2; // สร้าง 2 ห้องพร้อมกัน/ต่อครั้ง

    [Tooltip("ช่วงเวลา (วินาที) ในการสร้างห้องใหม่โดยอัตโนมัติ")]
    public float roomCreationInterval = 10f;
    [SerializeField] private float roomCreationTimer;


    [Header("Game Progress")]
    [Tooltip("จำนวนห้องทั้งหมดที่ต้องทำภารกิจให้เสร็จสิ้นเพื่อชนะเกม (ดาวดวงที่ 3)")]
    [SerializeField] private int totalRoomsToWin = 10;

    [Tooltip("ลาก Slider Component ที่จะแสดง Progress ของเกมมาใส่")]
    [SerializeField] private Slider mainProgressBar;

    private int roomsCompleted = 0; // ตัวแปรนับห้องที่เสร็จแล้ว

    // -------------------------------------------------------------------
    // ⭐ 1. (NEW) เพิ่มตัวแปรสำหรับระบบดาว
    // -------------------------------------------------------------------
    [Header("Star System")]
    [Tooltip("จำนวนห้องที่ต้องเสร็จเพื่อให้ได้ดาวดวงที่ 1")]
    [SerializeField] private int star1Threshold = 3;
    [Tooltip("จำนวนห้องที่ต้องเสร็จเพื่อให้ได้ดาวดวงที่ 2")]
    [SerializeField] private int star2Threshold = 6;
    // (ดาวดวงที่ 3 คือ totalRoomsToWin)

    [Tooltip("ลาก GameObject ของ 'ดาวดวงที่ 1 (แบบเต็ม/ได้รับแล้ว)' มาใส่")]
    [SerializeField] private GameObject star1Fill;
    [Tooltip("ลาก GameObject ของ 'ดาวดวงที่ 2 (แบบเต็ม/ได้รับแล้ว)' มาใส่")]
    [SerializeField] private GameObject star2Fill;
    [Tooltip("ลาก GameObject ของ 'ดาวดวงที่ 3 (แบบเต็ม/ได้รับแล้ว)' มาใส่")]
    [SerializeField] private GameObject star3Fill;
    // -------------------------------------------------------------------


    [Header("Global UI Reference")]
    [Tooltip("ลาก TextMeshProUGUI Component ที่จะแสดงเวลารวมของเกมมาใส่")]
    [SerializeField] private TextMeshProUGUI globalTimeDisplay;

    [Header("Core Systems")]
    [SerializeField] private DungeonGenerator dungeonGenerator;

    [Header("Player Reference")]
    [SerializeField] private PlayerController playerController;

    // Private State
    private bool isGameActive = false;
    private RoomData spawnRoom;
    private List<RoomData> activeRooms = new List<RoomData>();

    #endregion

    #region Unity Lifecycle

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

    #endregion

    #region Game Loop & State

    /// <summary>
    /// ⭐ 1. เริ่มเกมและสร้าง Spawn Room
    /// </summary>
    private void StartGameInitialization()
    {
        spawnRoom = dungeonGenerator.GenerateSpawn();

        if (spawnRoom != null)
        {
            roomCreationTimer = roomCreationInterval;

            // รีเซ็ตค่า Progress เมื่อเริ่มเกม
            roomsCompleted = 0;
            UpdateMainProgressBar(); // อัปเดต UI Bar ให้เป็น 0

            // ⭐ 2. (NEW) รีเซ็ตดาวทั้งหมดตอนเริ่มเกม
            UpdateStarDisplay(true); // ส่ง true เพื่อบังคับรีเซ็ต (ซ่อนทั้งหมด)
            // ------------------------------------

            // เริ่มลูปเกมหลัก (ตรวจจับเวลาเกมรวมเท่านั้น)
            StartCoroutine(GameLoopCoroutine());
        }
        else
        {
            Debug.LogError("Failed to initialize Spawn Room.");
        }
    }

    /// <summary>
    /// ⭐ 2. ลูปเกมหลัก (Game Loop - ตรวจจับเวลาเกมรวมเท่านั้น)
    /// </summary>
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

        // 4. จบเกมเมื่อหมดเวลา (หรือเมื่อ isGameActive เป็น false จากการชนะ)
        if (isGameActive) // ถ้ายัง Active อยู่ แปลว่าจบเพราะหมดเวลา
        {
            UpdateGameTimeDisplay(0f); // อัปเดตเป็น 0 ก่อนจบ
            EndGame(true); // true = จบเพราะหมดเวลา (แพ้)
        }
    }

    /// <summary>
    /// ⭐ 6. อัปเดต EndGame ให้แยกแยะระหว่าง ชนะ (Progress เต็ม) กับ แพ้ (เวลาหมด)
    /// </summary>
    private void EndGame(bool timeUp)
    {
        if (!isGameActive) return; // ป้องกันการเรียกซ้ำ

        isGameActive = false;
        StopAllCoroutines();

        if (timeUp)
        {
            // แพ้ (เวลาหมด)
            Debug.Log("GAME OVER: Time's Up!");
            // ... (ใส่โค้ดแสดงหน้าจอ แพ้ ที่นี่) ...
        }
        else
        {
            // ชนะ (Progress Bar เต็ม)
            Debug.Log("VICTORY: Main Progress Bar is Full!");
            // ... (ใส่โค้ดแสดงหน้าจอ ชนะ ที่นี่) ...
        }

        // ... (โค้ดจบเกมอื่นๆ ที่ต้องทำทั้งตอนแพ้และชนะ) ...
    }

    #endregion

    #region Room Event Handling

    /// <summary>
    /// ⭐ 3. Event Handlers (รับการแจ้งเตือนจาก RoomData)
    /// </summary>
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

    /// <summary>
    /// --- (MODIFIED) ---
    /// ⭐ 5. อัปเดต HandleRoomCompletion เพื่อเพิ่ม Progress
    /// </summary>
    private void HandleRoomCompletion(RoomData completedRoom)
    {
        // Debug.Log($"CoreManager received completion from {completedRoom.name}.");
        Debug.Log($"CoreManager received completion from {completedRoom.name}. Destroying room.");

        // 1. Unsubscribe เพื่อล้าง Event
        UnsubscribeFromRoomEvents(completedRoom);

        // 2. --- (NEW) เพิ่ม Progress และตรวจสอบเงื่อนไขชนะ ---
        if (isGameActive) // ตรวจสอบว่าเกมยังไม่จบ (เผื่อ Event เข้ามาซ้อนกัน)
        {
            roomsCompleted++;
            UpdateMainProgressBar();

            // ⭐ 3. (NEW) อัปเดตการแสดงผลดาว
            UpdateStarDisplay();
            // ---------------------------

            if (roomsCompleted >= totalRoomsToWin)
            {
                // ชนะเกม! (ดาวดวงที่ 3 จะถูกเปิดใช้งานโดย UpdateStarDisplay() พอดี)
                EndGame(false); // false = จบเพราะทำภารกิจสำเร็จ (ชนะ)
            }
        }
        // --------------------------------------------------

        // 3. --- (NEW) --- สั่งทำลายห้อง
        DestroyRoom(completedRoom);
    }

    private void HandleRoomTimeout(RoomData timedOutRoom)
    {
        //Debug.Log($"CoreManager received timeout from {timedOutRoom.name}. Destroying room.");

        // (เมื่อห้องหมดเวลา เราจะไม่เพิ่ม Progress)

        // 1. Unsubscribe เพื่อล้าง Event ก่อนทำลายห้อง
        UnsubscribeFromRoomEvents(timedOutRoom);

        // 2. สั่งทำลายห้อง (และเอาออกจาก DungeonGenerator)
        DestroyRoom(timedOutRoom);
    }

    #endregion

    #region Room Management (Creation & Destruction)

    /// <summary>
    /// (Optimized) พยายามสร้าง Hallway-Room chain ใหม่ ตามจำนวนที่กำหนด
    /// </summary>
    public void TryCreateMultipleRooms(int count)
    {
        // --- OPTIMIZATION: ตรวจสอบก่อนว่า Spawn Room มีที่ว่างเหลือหรือไม่ ---
        if (spawnRoom == null || !spawnRoom.HasAnyAvailableConnector())
        {
            // ไม่ต้องพยายามสร้างถ้า Spawn Room เต็มแล้ว
            return;
        }
        // --- END OPTIMIZATION ---

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

            // --- OPTIMIZATION 2: ถ้าพยายามสร้างห้องแรกล้มเหลว 3 ครั้ง และ Connector เต็มแล้ว, ให้ออกจากลูปเลย
            if (successfulCreations == 0 && !spawnRoom.HasAnyAvailableConnector())
            {
                //Debug.Log("Spawn room became full during creation attempts. Stopping loop.");
                break; // ออกจาก Loop (for i)
            }
            // --- END OPTIMIZATION 2 ---
        }

        if (successfulCreations > 0)
        {
            //Debug.Log($"Successfully created {successfulCreations} new room chains.");
        }
        else
        {
            // LogWarning นี้จะแสดงผลก็ต่อเมื่อ "มีที่ว่าง" แต่ "สร้างแล้วชน" ซ้ำๆ
            Debug.LogWarning($"Failed to create any new rooms after {count * MAX_ATTEMPTS_PER_ROOM} attempts. Spawn connectors might be full or facing persistent intersections.");
        }
    }

    /// <summary>
    /// พยายามสร้าง Hallway-Room chain ใหม่ 1 ชุด
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
            dungeonGenerator.RemoveRoom(roomToDestroy);
        }
    }

    #endregion

    #region Player Helpers

    /// <summary>
    /// ตรวจสอบว่าผู้เล่นอยู่ใน Collider ของห้องที่กำลังจะถูกทำลายหรือไม่
    /// </summary>
    private bool IsPlayerInRoom(RoomData room)
    {
        if (playerController == null || playerController.transform == null || room.roomBoundsCollider == null) return false;

        // ⭐ ใช้ roomBoundsCollider.bounds
        bool isInBounds = room.roomBoundsCollider.bounds.Contains(playerController.transform.position);

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

        Vector3 spawnPosition = spawnRoom.transform.position;
        spawnPosition = spawnRoom.transform.position;

        playerController.transform.position = spawnPosition;
        //Debug.Log("Player Teleport");
    }

    #endregion

    #region UI Callbacks

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

    /// <summary>
    /// ⭐ 4. (NEW) เมธอดสำหรับอัปเดต UI Progress Bar
    /// </summary>
    private void UpdateMainProgressBar()
    {
        if (mainProgressBar == null) return;

        // คำนวณค่า Progress (0.0 ถึง 1.0)
        // ต้องแปลงเป็น float เพื่อให้หารเลขทศนิยมได้
        float progressValue = (float)roomsCompleted / totalRoomsToWin;

        mainProgressBar.value = progressValue;
    }

    // -------------------------------------------------------------------
    // ⭐ 4. (NEW) เมธอดสำหรับอัปเดตการแสดงผลดาว
    // -------------------------------------------------------------------
    /// <summary>
    /// อัปเดตการแสดงผลดาวตามจำนวนห้องที่เสร็จสิ้น
    /// </summary>
    /// <param name="forceReset">ถ้าเป็น true จะซ่อนดาวทั้งหมด (ใช้ตอนเริ่มเกม)</param>
    private void UpdateStarDisplay(bool forceReset = false)
    {
        if (star1Fill != null)
        {
            // ถ้าไม่ forceReset ให้เช็คว่าถึง Threshold หรือยัง
            star1Fill.SetActive(!forceReset && roomsCompleted >= star1Threshold);
        }
        if (star2Fill != null)
        {
            star2Fill.SetActive(!forceReset && roomsCompleted >= star2Threshold);
        }
        if (star3Fill != null)
        {
            // ดาวดวงที่ 3 คือเงื่อนไขชนะ
            star3Fill.SetActive(!forceReset && roomsCompleted >= totalRoomsToWin);
        }
    }
    // -------------------------------------------------------------------

    #endregion
}