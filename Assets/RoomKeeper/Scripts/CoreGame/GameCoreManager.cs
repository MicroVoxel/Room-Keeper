using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System;
using UnityEngine.UI;

public class GameCoreManager : MonoBehaviour
{
    #region Singleton

    public static GameCoreManager Instance { get; private set; }

    #endregion

    #region 1. Fields & Properties

    [Header("Level Settings")]
    public int currentLevelID = 1;

    [Header("Game Time Settings")]
    public float totalGameDuration = 300f;

    [Header("Room Generation Settings")]
    public int roomsToCreatePerEvent = 2;

    public float roomCreationInterval = 10f;
    [SerializeField] private float roomCreationTimer;

    [Header("Game Progress")]
    [SerializeField] private int totalRoomsToWin = 10;

    [SerializeField] private Slider mainProgressBar;

    private int roomsCompleted = 0;
    private float currentGameTime;

    [Header("Star System (HUD)")]
    [SerializeField] private int star1Threshold = 3;
    [SerializeField] private int star2Threshold = 6;

    [SerializeField] private GameObject star1Fill;
    [SerializeField] private GameObject star2Fill;
    [SerializeField] private GameObject star3Fill;

    [Header("End Game UI")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Victory Panel Elements")]
    [SerializeField] private GameObject victoryStar1;
    [SerializeField] private GameObject victoryStar2;
    [SerializeField] private GameObject victoryStar3;
    [SerializeField] private TextMeshProUGUI victoryTimeText;

    [Header("Global UI Reference")]
    [SerializeField] private TextMeshProUGUI globalTimeDisplay;

    [Header("Core Systems")]
    [SerializeField] private DungeonGenerator dungeonGenerator;

    [Header("Player Reference")]
    [SerializeField] private PlayerController playerController;

    private bool isGameActive = false;
    private RoomData spawnRoom;

    #endregion

    #region 2. Unity Lifecycle & Core Initialization

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

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        StartGameInitialization();
    }

    private void OnDestroy()
    {
        // ทำลาย Room ทั้งหมดเมื่อ GameCoreManager ถูกลบออกจากซีน
        DestroyAllGeneratedRooms();
    }

    #endregion

    #region 3. Game Loop & State Management

    private void StartGameInitialization()
    {
        spawnRoom = dungeonGenerator.GenerateSpawn();

        if (spawnRoom != null)
        {
            // NEW: ต้องเรียก SpawnAndInitAllTasks() สำหรับ Spawn Room ที่วางสำเร็จ
            spawnRoom.SpawnAndInitAllTasks();

            roomCreationTimer = roomCreationInterval;
            currentGameTime = totalGameDuration;

            roomsCompleted = 0;
            UpdateMainProgressBar();

            UpdateStarDisplay(true);

            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);

            StartCoroutine(GameLoopCoroutine());
        }
        else
        {
            Debug.LogError("Failed to initialize Spawn Room.");
        }
    }

    private IEnumerator GameLoopCoroutine()
    {
        isGameActive = true;

        while (currentGameTime > 0 && isGameActive)
        {
            UpdateGameTimeDisplay(currentGameTime);

            // ตรวจสอบเงื่อนไข Win ทุกเฟรม
            if (roomsCompleted >= totalRoomsToWin)
            {
                EndGame(true);
                yield break;
            }

            roomCreationTimer += Time.deltaTime;

            if (roomCreationTimer >= roomCreationInterval)
            {
                StartCoroutine(CreateRoomsCoroutine(roomsToCreatePerEvent));
                roomCreationTimer = 0f;
            }

            currentGameTime -= Time.deltaTime;
            yield return null;
        }

        // จบเกมเมื่อเวลาหมด (ถ้ายัง Active อยู่)
        if (isGameActive)
        {
            currentGameTime = 0f;
            UpdateGameTimeDisplay(0f);

            // ใช้ roomsCompleted ตรวจสอบการชนะตามเกณฑ์ดาวขั้นต่ำ
            if (roomsCompleted >= star1Threshold)
            {
                EndGame(true); // ชนะ (ตามเกณฑ์ดาวขั้นต่ำ)
            }
            else
            {
                EndGame(false); // แพ้
            }
        }
    }

    private void EndGame(bool isVictory)
    {
        if (!isGameActive) return;

        isGameActive = false;
        StopAllCoroutines();

        // 1. ปิด Task UI ทั้งหมด
        if (dungeonGenerator != null && dungeonGenerator.GeneratedRooms != null)
        {
            foreach (RoomData room in dungeonGenerator.GeneratedRooms)
            {
                if (room == null || room.AssignedTasks == null) continue;

                foreach (TaskBase task in room.AssignedTasks)
                {
                    if (task != null && task.IsOpen)
                    {
                        task.Close();
                    }
                }
            }
        }

        // 2. ล็อกการเคลื่อนที่ของผู้เล่น
        if (playerController != null)
        {
            playerController.SetMovement(false);
        }

        // 3. แสดงผลลัพธ์
        if (isVictory)
        {
            Debug.Log("VICTORY!");

            int starsEarned = CalculateStars();

            if (LevelProgressManager.Instance != null)
            {
                LevelProgressManager.Instance.SaveLevelResult(currentLevelID, starsEarned);
            }
            else
            {
                Debug.LogWarning("LevelProgressManager not found! Progress will not be saved.");
            }

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
                SetupVictoryPanel();
            }
        }
        else
        {
            Debug.Log("GAME OVER: Failed to reach minimum stars.");
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
        }

        // 4. ทำลาย Room ทั้งหมด
        DestroyAllGeneratedRooms();
    }

    private void DestroyAllGeneratedRooms()
    {
        if (dungeonGenerator == null || dungeonGenerator.GeneratedRooms == null) return;

        // ใช้ ToList() เพื่อป้องกันการแก้ไข list ขณะวนลูป
        List<RoomData> roomsToDestroy = dungeonGenerator.GeneratedRooms.ToList();

        // ทำลาย Room ทั้งหมด ยกเว้น Spawn Room (ถ้าต้องการเก็บไว้)
        foreach (RoomData room in roomsToDestroy)
        {
            if (room != null && room.roomType != RoomData.RoomType.Spawn)
            {
                DestroyRoom(room);
            }
        }
    }

    private int CalculateStars()
    {
        if (roomsCompleted >= totalRoomsToWin) return 3;
        if (roomsCompleted >= star2Threshold) return 2;
        if (roomsCompleted >= star1Threshold) return 1;
        return 0;
    }

    private void SetupVictoryPanel()
    {
        if (victoryTimeText != null)
        {
            // เวลาที่ใช้ไปคือ totalGameDuration - currentGameTime
            float timeElapsed = totalGameDuration - Mathf.Max(0, currentGameTime);
            TimeSpan t = TimeSpan.FromSeconds(timeElapsed);
            victoryTimeText.text = string.Format("{0:0}:{1:00}", (int)t.TotalMinutes, t.Seconds);
        }

        bool getStar1 = roomsCompleted >= star1Threshold;
        bool getStar2 = roomsCompleted >= star2Threshold;
        bool getStar3 = roomsCompleted >= totalRoomsToWin;

        if (victoryStar1 != null) victoryStar1.SetActive(getStar1);
        if (victoryStar2 != null) victoryStar2.SetActive(getStar2);
        if (victoryStar3 != null) victoryStar3.SetActive(getStar3);
    }

    #endregion

    #region 4. Room Event Handling

    private void SubscribeToRoomEvents(RoomData room)
    {
        room.OnRoomCompletion += HandleRoomCompletion;
    }

    private void UnsubscribeFromRoomEvents(RoomData room)
    {
        room.OnRoomCompletion -= HandleRoomCompletion;
    }

    private void HandleRoomCompletion(RoomData completedRoom)
    {
        Debug.Log($"CoreManager received completion from {completedRoom.name}. Destroying room.");

        UnsubscribeFromRoomEvents(completedRoom);

        if (isGameActive)
        {
            roomsCompleted++;
            UpdateMainProgressBar();
            UpdateStarDisplay();

            // ตรวจสอบเงื่อนไข Win ทันทีหลังทำ Task เสร็จ
            if (roomsCompleted >= totalRoomsToWin)
            {
                EndGame(true);
                // ไม่ต้องทำลายห้องที่นี่ เพราะ EndGame จะเรียก DestroyAllGeneratedRooms()
                return;
            }
        }

        DestroyRoom(completedRoom);
    }

    #endregion

    #region 5. Room Management (Creation & Destruction)

    public IEnumerator CreateRoomsCoroutine(int count)
    {
        if (spawnRoom == null || dungeonGenerator == null) yield break;

        int successfulCreations = 0;
        const int MAX_ATTEMPTS_PER_CONNECTOR = 3;
        int roomsToAttempt = count;

        // สร้าง List ชั่วคราวของ connectors ที่พร้อมใช้งานใน spawnRoom
        List<Transform> availableConnectors = spawnRoom.connectors
            .Where(c => c.TryGetComponent<Connector>(out Connector conn) && !conn.IsOccupied())
            .ToList();

        // สุ่มลำดับการเลือก Connector เพื่อลดการเลือก Connector เดิมซ้ำ
        System.Random rng = new System.Random();
        availableConnectors = availableConnectors.OrderBy(c => rng.Next()).ToList();


        for (int i = 0; i < roomsToAttempt; i++)
        {
            if (roomsCompleted >= totalRoomsToWin) break;
            if (availableConnectors.Count == 0) break;

            bool roomCreatedInThisSlot = false;

            Transform startConnector = availableConnectors[0];
            availableConnectors.RemoveAt(0);

            Connector connectorComponent = startConnector.GetComponent<Connector>();
            if (connectorComponent == null) continue; // Should not happen

            connectorComponent.SetOccupied(true); // ตั้งค่าให้ถูกจองไว้ก่อน

            for (int attempt = 0; attempt < MAX_ATTEMPTS_PER_CONNECTOR; attempt++)
            {
                if (TryPlaceRoomChain(spawnRoom, startConnector))
                {
                    successfulCreations++;
                    roomCreatedInThisSlot = true;
                    break;
                }
            }

            if (!roomCreatedInThisSlot)
            {
                // ถ้าสร้างไม่สำเร็จ ให้คืนค่า Connector
                connectorComponent.SetOccupied(false);
            }

            yield return null; // FIX: Force yield after processing one room slot to distribute the load across frames.
        }

        if (successfulCreations == 0 && count > 0)
        {
            Debug.LogWarning($"Failed to create any new rooms after attempting {count} creations.");
        }
    }

    private bool TryPlaceRoomChain(RoomData startRoom, Transform startConnector)
    {

        GameObject hallwayPrefab = dungeonGenerator.SelectRandomHallwayPrefab();
        GameObject roomPrefab = dungeonGenerator.SelectNextRoomPrefab();

        if (hallwayPrefab == null || roomPrefab == null)
        {
            // UnuseConnector ถูกเรียกภายนอก (ใน CreateRoomsCoroutine) ถ้าสร้างไม่สำเร็จ
            return false;
        }

        if (dungeonGenerator.TryPlaceRoom(startRoom, startConnector, hallwayPrefab))
        {
            RoomData hallwayData = dungeonGenerator.GeneratedRooms.Last();
            hallwayData.parentConnector = startConnector;

            if (hallwayData.HasAvailableConnector(out Transform hallwayConnector))
            {
                if (dungeonGenerator.TryPlaceRoom(hallwayData, hallwayConnector, roomPrefab))
                {
                    RoomData newRoom = dungeonGenerator.GeneratedRooms.Last();
                    newRoom.parentConnector = hallwayConnector;

                    // NEW: เรียก Spawn Task UI หลังจากยืนยันว่าวาง Hallway และ Room สำเร็จ
                    hallwayData.SpawnAndInitAllTasks();
                    newRoom.SpawnAndInitAllTasks();

                    newRoom.ActivateRandomTasks();
                    SubscribeToRoomEvents(newRoom);

                    return true;
                }
                else
                {
                    // ล้มเหลวในการวาง Room: ลบ Hallway และคืน Connector ทั้งสอง
                    dungeonGenerator.RemoveRoom(hallwayData);
                    // startRoom.UnuseConnector(startConnector); // ถูกจัดการโดย TryPlaceRoom
                    return false;
                }
            }
            else
            {
                // Hallway ไม่มี Connector ว่าง: ลบ Hallway และคืน Connector
                dungeonGenerator.RemoveRoom(hallwayData);
                // startRoom.UnuseConnector(startConnector); // ถูกจัดการโดย TryPlaceRoom
                return false;
            }
        }
        else
        {
            // ล้มเหลวในการวาง Hallway: คืน Connector
            // startRoom.UnuseConnector(startConnector); // ถูกจัดการโดย TryPlaceRoom
            return false;
        }
    }

    public void DestroyRoom(RoomData roomToDestroy)
    {
        if (roomToDestroy != null && roomToDestroy.roomType != RoomData.RoomType.Spawn)
        {
            if (IsPlayerInRoom(roomToDestroy))
            {
                Debug.LogWarning($"Player detected in room {roomToDestroy.name}. Teleporting player to Spawn Room.");
                TeleportPlayerToSpawn();
            }
            // DungeonGenerator.RemoveRoom จะจัดการการทำลาย Hallway ที่เกี่ยวข้องด้วย
            dungeonGenerator.RemoveRoom(roomToDestroy);
        }
    }

    #endregion

    #region 6. Player Helpers

    private bool IsPlayerInRoom(RoomData room)
    {
        if (playerController == null || playerController.transform == null || room.roomBoundsCollider == null) return false;
        // ใช้ 0.5f เพื่อให้แน่ใจว่าการตรวจสอบจะเกิดขึ้นเมื่อ Player เข้ามาในขอบเขตอย่างชัดเจน
        Vector3 playerPos = playerController.transform.position;
        playerPos.z = room.roomBoundsCollider.bounds.center.z;
        return room.roomBoundsCollider.bounds.Contains(playerPos);
    }

    private void TeleportPlayerToSpawn()
    {
        if (spawnRoom == null || playerController == null)
        {
            Debug.LogError("Cannot teleport player: Spawn Room or Player Controller is missing!");
            return;
        }
        // กำหนดตำแหน่งใหม่ให้เป็นตรงกลางของ Spawn Room Collider Bounds
        Vector3 spawnPos = spawnRoom.roomBoundsCollider != null ? spawnRoom.roomBoundsCollider.bounds.center : spawnRoom.transform.position;
        spawnPos.z = playerController.transform.position.z; // รักษาระดับ Z ของผู้เล่น
        playerController.transform.position = spawnPos;
    }

    #endregion

    #region 7. UI Callbacks

    private void UpdateGameTimeDisplay(float time)
    {
        if (globalTimeDisplay == null) return;

        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0, time));
        globalTimeDisplay.text = string.Format("{0:0}:{1:00}", (int)t.TotalMinutes, t.Seconds);
    }

    private void UpdateMainProgressBar()
    {
        if (mainProgressBar == null) return;
        float progressValue = (float)roomsCompleted / totalRoomsToWin;
        mainProgressBar.value = progressValue;
    }

    private void UpdateStarDisplay(bool forceReset = false)
    {
        if (star1Fill != null) star1Fill.SetActive(!forceReset && roomsCompleted >= star1Threshold);
        if (star2Fill != null) star2Fill.SetActive(!forceReset && roomsCompleted >= star2Threshold);
        if (star3Fill != null) star3Fill.SetActive(!forceReset && roomsCompleted >= totalRoomsToWin);
    }

    // เมธอดสำหรับปุ่ม Restart (ถ้ามี)
    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    #endregion
}