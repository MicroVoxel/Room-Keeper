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
    [Tooltip("เวลาพื้นฐานของเกม")]
    public float baseGameDuration = 300f;

    public float totalGameDuration { get; private set; }

    [Header("Room Generation Settings")]
    public int roomsToCreatePerEvent = 2;
    public float roomCreationInterval = 10f;
    [SerializeField] private float roomCreationTimer;

    [Header("Game Progress")]
    [SerializeField] private int totalRoomsToWin = 10;
    [SerializeField] private Slider mainProgressBar;

    private int roomsCompleted = 0;
    private float currentGameTime;
    private bool hasRevived = false;

    [Header("Star System (HUD)")]
    [SerializeField] private int star1Threshold = 3;
    [SerializeField] private int star2Threshold = 6;

    [SerializeField] private GameObject star1Fill;
    [SerializeField] private GameObject star2Fill;
    [SerializeField] private GameObject star3Fill;

    [Header("End Game UI")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button reviveButton;

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
            Debug.LogError("DungeonGenerator not found!");
            return;
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        if (reviveButton != null)
        {
            reviveButton.onClick.AddListener(OnWatchAdToReviveClicked);
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
        // คำนวณเวลา Bonus จาก VIP ที่ซื้อ
        float bonusTime = 0f;

        // ใช้ Key จาก IAPManager เพื่อความถูกต้อง
        if (PlayerPrefs.GetInt(IAPManager.KEY_VIP1, 0) == 1) bonusTime += 10f;
        if (PlayerPrefs.GetInt(IAPManager.KEY_VIP2, 0) == 1) bonusTime += 20f;
        if (PlayerPrefs.GetInt(IAPManager.KEY_VIP3, 0) == 1) bonusTime += 30f;

        totalGameDuration = baseGameDuration + bonusTime;
        hasRevived = false;

        // ... Logic เดิม ...
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

        if (isVictory)
        {
            CloseAllTasks();
            if (playerController != null) playerController.SetMovement(false);

            int starsEarned = CalculateStars();
            if (LevelProgressManager.Instance != null)
                LevelProgressManager.Instance.SaveLevelResult(currentLevelID, starsEarned);

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
                SetupVictoryPanel(starsEarned);
            }
            DestroyAllGeneratedRooms();
        }
        else
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                if (reviveButton != null)
                    reviveButton.gameObject.SetActive(!hasRevived);
            }
            if (playerController != null) playerController.SetMovement(false);
        }
    }

    // --- REVIVE LOGIC ---
    public void OnWatchAdToReviveClicked()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.ShowRewardedVideoAds(() => {
                ReviveGameSuccess();
            });
        }
        else
        {
            Debug.LogError("AdsManager Instance is null!");
        }
    }

    private void ReviveGameSuccess()
    {
        Debug.Log("Revive Success! Adding 30 seconds.");
        hasRevived = true;
        currentGameTime = 30f;
        isGameActive = true;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (playerController != null) playerController.SetMovement(true);

        StartCoroutine(GameLoopCoroutine());
    }

    private void CloseAllTasks() { Debug.Log("Closing tasks..."); }

    private void DestroyAllGeneratedRooms()
    {
        if (dungeonGenerator == null || dungeonGenerator.GeneratedRooms == null) return;
        List<RoomData> roomsToDestroy = dungeonGenerator.GeneratedRooms.ToList();
        foreach (RoomData room in roomsToDestroy)
        {
            if (room != null && room.roomType != RoomData.RoomType.Spawn)
                DestroyRoom(room);
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
            float timeRemaining = Mathf.Max(0, currentGameTime);
            TimeSpan t = TimeSpan.FromSeconds(timeRemaining);
            victoryTimeText.text = string.Format("{0:0}:{1:00}", t.Minutes, t.Seconds);
        }

        if (victoryStar1 != null) victoryStar1.SetActive(starsEarned >= 1);
        if (victoryStar2 != null) victoryStar2.SetActive(starsEarned >= 2);
        if (victoryStar3 != null) victoryStar3.SetActive(starsEarned >= 3);
    }

    // --- ROOM LOGIC (Condensed for brevity as it was unchanged) ---
    private void SubscribeToRoomEvents(RoomData room) { room.OnRoomCompletion += HandleRoomCompletion; }
    private void UnsubscribeFromRoomEvents(RoomData room) { room.OnRoomCompletion -= HandleRoomCompletion; }

    private void HandleRoomCompletion(RoomData completedRoom)
    {
        UnsubscribeFromRoomEvents(completedRoom);
        if (isGameActive)
        {
            roomsCompleted++;
            UpdateMainProgressBar();
            UpdateStarDisplay(CalculateStars());
            if (roomsCompleted >= totalRoomsToWin) { EndGame(true); return; }
        }
        DestroyRoom(completedRoom);
    }

    public IEnumerator CreateRoomsCoroutine(int count)
    {
        if (spawnRoom == null || dungeonGenerator == null) yield break;
        List<Connector> allConnectors = spawnRoom.connectors
            .Select(t => t.GetComponent<Connector>()).Where(c => c != null).ToList();

        for (int i = 0; i < count; i++)
        {
            if (roomsCompleted >= totalRoomsToWin) break;
            bool roomCreated = false;
            foreach (Connector connector in allConnectors)
            {
                if (connector.IsOccupied()) continue;
                connector.SetOccupied(true);
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (TryPlaceRoomChain(spawnRoom, connector.transform))
                    {
                        roomCreated = true; break;
                    }
                }
                if (roomCreated) break; else connector.SetOccupied(false);
            }
            if (!roomCreated) break;
            yield return null;
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
                else { dungeonGenerator.RemoveRoom(hallwayData); return false; }
            }
            else { dungeonGenerator.RemoveRoom(hallwayData); return false; }
        }
        return false;
    }

    public void DestroyRoom(RoomData roomToDestroy)
    {
        if (roomToDestroy != null && roomToDestroy.roomType != RoomData.RoomType.Spawn)
        {
            if (IsPlayerInRoom(roomToDestroy)) TeleportPlayerToSpawn();
            dungeonGenerator.RemoveRoom(roomToDestroy);
        }
    }

    private bool IsPlayerInRoom(RoomData room)
    {
        if (playerController == null || room.roomBoundsCollider == null) return false;
        return room.roomBoundsCollider.bounds.Contains(playerController.transform.position);
    }

    private bool TeleportPlayerToSpawn()
    {
        if (spawnRoom == null || playerController == null) return false;
        Vector3 spawnPos = spawnRoom.GetPlayerSpawnPosition();
        CharacterController cc = playerController.GetComponent<CharacterController>();
        bool ccWasEnabled = (cc != null && cc.enabled);
        if (cc != null) cc.enabled = false;
        playerController.transform.position = spawnPos;
        Physics.SyncTransforms();
        if (cc != null && ccWasEnabled) cc.enabled = true;
        return true;
    }

    private void UpdateGameTimeDisplay(float time)
    {
        if (globalTimeDisplay != null)
        {
            TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0, time));
            globalTimeDisplay.text = string.Format("{0:0}:{1:00}", (int)t.TotalMinutes, t.Seconds);
        }
    }

    private void UpdateMainProgressBar()
    {
        if (mainProgressBar != null) mainProgressBar.value = (float)roomsCompleted / totalRoomsToWin;
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