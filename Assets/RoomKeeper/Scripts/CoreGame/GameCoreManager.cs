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
        DestroyAllGeneratedRooms();
    }

    #endregion

    #region 3. Game Loop & State Management

    private void StartGameInitialization()
    {
        spawnRoom = dungeonGenerator.GenerateSpawn();

        if (spawnRoom != null)
        {
            spawnRoom.SpawnAndInitAllTasks();

            roomCreationTimer = roomCreationInterval;
            currentGameTime = totalGameDuration;

            roomsCompleted = 0;
            UpdateMainProgressBar();

            UpdateStarDisplay(0);

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

        if (isGameActive)
        {
            currentGameTime = 0f;
            UpdateGameTimeDisplay(0f);

            if (roomsCompleted >= star1Threshold)
            {
                EndGame(true);
            }
            else
            {
                EndGame(false);
            }
        }
    }

    private void EndGame(bool isVictory)
    {
        if (!isGameActive) return;
        isGameActive = false;
        StopAllCoroutines();

        CloseAllTasks();
        if (playerController != null) playerController.SetMovement(false);

        if (isVictory)
        {
            int starsEarned = CalculateStars();

            if (LevelProgressManager.Instance != null)
                LevelProgressManager.Instance.SaveLevelResult(currentLevelID, starsEarned);

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
                SetupVictoryPanel(starsEarned);
            }
        }
        else
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
        }

        DestroyAllGeneratedRooms();
    }

    private void CloseAllTasks()
    {
        Debug.Log("Closing all open tasks UI...");
    }

    private void DestroyAllGeneratedRooms()
    {
        if (dungeonGenerator == null || dungeonGenerator.GeneratedRooms == null) return;

        List<RoomData> roomsToDestroy = dungeonGenerator.GeneratedRooms.ToList();

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

    private void SetupVictoryPanel(int starsEarned)
    {
        if (victoryTimeText != null)
        {
            // FIX: เปลี่ยนจากแสดงเวลาที่ใช้ไป เป็น "เวลาที่เหลือ" (Time Remaining)
            // โดยใช้ currentGameTime โดยตรง
            float timeRemaining = Mathf.Max(0, currentGameTime);

            TimeSpan t = TimeSpan.FromSeconds(timeRemaining);
            // ใช้ t.Minutes และ t.Seconds เพื่อการแสดงผลที่ถูกต้อง
            victoryTimeText.text = string.Format("{0:0}:{1:00}", t.Minutes, t.Seconds);
        }

        if (victoryStar1 != null) victoryStar1.SetActive(starsEarned >= 1);
        if (victoryStar2 != null) victoryStar2.SetActive(starsEarned >= 2);
        if (victoryStar3 != null) victoryStar3.SetActive(starsEarned >= 3);
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
            UpdateStarDisplay(CalculateStars());

            if (roomsCompleted >= totalRoomsToWin)
            {
                EndGame(true);
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

        List<Transform> availableConnectors = spawnRoom.connectors
            .Where(c => c.TryGetComponent<Connector>(out Connector conn) && !conn.IsOccupied())
            .ToList();

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
            if (connectorComponent == null) continue;

            connectorComponent.SetOccupied(true);

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
                connectorComponent.SetOccupied(false);
            }

            yield return null;
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

        if (hallwayPrefab == null || roomPrefab == null) return false;

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

                    hallwayData.SpawnAndInitAllTasks();
                    newRoom.SpawnAndInitAllTasks();

                    newRoom.ActivateRandomTasks();
                    SubscribeToRoomEvents(newRoom);

                    return true;
                }
                else
                {
                    dungeonGenerator.RemoveRoom(hallwayData);
                    return false;
                }
            }
            else
            {
                dungeonGenerator.RemoveRoom(hallwayData);
                return false;
            }
        }
        else
        {
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
            dungeonGenerator.RemoveRoom(roomToDestroy);
        }
    }

    #endregion

    #region 6. Player Helpers

    private bool IsPlayerInRoom(RoomData room)
    {
        if (playerController == null || playerController.transform == null || room.roomBoundsCollider == null) return false;
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
        Vector3 spawnPos = spawnRoom.roomBoundsCollider != null ? spawnRoom.roomBoundsCollider.bounds.center : spawnRoom.transform.position;
        spawnPos.z = playerController.transform.position.z;
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

    private void UpdateStarDisplay(int starsEarned)
    {
        if (star1Fill != null) star1Fill.SetActive(starsEarned >= 1);
        if (star2Fill != null) star2Fill.SetActive(starsEarned >= 2);
        if (star3Fill != null) star3Fill.SetActive(starsEarned >= 3);
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    #endregion
}