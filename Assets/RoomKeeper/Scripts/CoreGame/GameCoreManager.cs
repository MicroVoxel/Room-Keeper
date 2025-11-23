using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameCoreManager : MonoBehaviour
{
    public static GameCoreManager Instance { get; private set; }

    [Header("Level Settings")]
    public int currentLevelID = 1;
    public float totalGameDuration = 90f;

    [Header("VIP Bonuses")]
    [Tooltip("เวลาที่จะบวกเพิ่มให้ถ้ามี VIP 1")]
    public float vip1TimeBonus = 10f;
    [Tooltip("เวลาที่จะบวกเพิ่มให้ถ้ามี VIP 2")]
    public float vip2TimeBonus = 20f;
    [Tooltip("เวลาที่จะบวกเพิ่มให้ถ้ามี VIP 3")]
    public float vip3TimeBonus = 30f;

    [Header("Progress")]
    [SerializeField] private int totalRoomsToWin = 3;
    private int roomsCompleted = 0;
    private float currentGameTime;

    [Header("Star Thresholds")]
    [Tooltip("จำนวนห้องที่ต้องผ่านเพื่อให้ได้ 1 ดาว")]
    [SerializeField] private int star1Threshold = 1;
    [Tooltip("จำนวนห้องที่ต้องผ่านเพื่อให้ได้ 2 ดาว")]
    [SerializeField] private int star2Threshold = 2;

    [Header("Revive Settings")]
    [Tooltip("เวลาที่จะได้รับเพิ่มเมื่อกด Revive")]
    public float reviveTimeBonus = 30f;

    [Header("Systems")]
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField] private PlayerController playerController;

    [Header("Task Deck System")]
    public List<TaskBase> allTaskPrefabsMasterList;

    [Header("Room Generation Settings")]
    public int roomsToCreatePerEvent = 1;
    public float roomCreationInterval = 3f;
    private float roomCreationTimer;

    private RoomData spawnRoom;
    private bool isGameActive = false;
    private List<string> _taskPriorityDeck = new List<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void OnValidate()
    {
        // ป้องกันการตั้งค่าผิดพลาดใน Inspector
        if (star2Threshold >= totalRoomsToWin) star2Threshold = totalRoomsToWin - 1;
        if (star1Threshold >= star2Threshold) star1Threshold = star2Threshold - 1;
        if (star1Threshold < 1) star1Threshold = 1;
    }

    private void Start()
    {
        // ใช้ FindAnyObjectByType แทน FindObjectOfType ใน Unity 6 เพื่อ Performance ที่ดีกว่า
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (dungeonGenerator != null) StartGameInitialization();
    }

    private void StartGameInitialization()
    {
        RefillTaskDeck();
        spawnRoom = dungeonGenerator.GenerateSpawn();

        if (spawnRoom != null)
        {
            spawnRoom.SpawnAndInitAllTasks();

            // --- VIP Logic Calculation ---
            float finalGameTime = totalGameDuration;

            if (IAPManager.Instance != null)
            {
                if (IAPManager.Instance.IsOwned(IAPManager.ID_VIP1))
                {
                    finalGameTime += vip1TimeBonus;
                    Debug.Log($"[VIP] VIP1 Bonus Applied: +{vip1TimeBonus}s");
                }

                if (IAPManager.Instance.IsOwned(IAPManager.ID_VIP2))
                {
                    finalGameTime += vip2TimeBonus;
                    Debug.Log($"[VIP] VIP2 Bonus Applied: +{vip2TimeBonus}s");
                }

                if (IAPManager.Instance.IsOwned(IAPManager.ID_VIP3))
                {
                    finalGameTime += vip3TimeBonus;
                    Debug.Log($"[VIP] VIP3 Bonus Applied: +{vip3TimeBonus}s");
                }
            }
            // -----------------------------

            roomCreationTimer = roomCreationInterval;
            currentGameTime = finalGameTime;
            roomsCompleted = 0;
            isGameActive = true;

            RefreshUI();

            StartCoroutine(GameLoopCoroutine());
        }
    }

    private IEnumerator GameLoopCoroutine()
    {
        while (currentGameTime > 0 && isGameActive)
        {
            currentGameTime -= Time.deltaTime;
            roomCreationTimer += Time.deltaTime;

            RefreshUI();

            // เช็คเงื่อนไขชนะ (3 ดาวอัตโนมัติถ้าครบจำนวนห้องสูงสุด)
            if (roomsCompleted >= totalRoomsToWin)
            {
                EndGame(true);
                yield break;
            }

            if (roomCreationTimer >= roomCreationInterval)
            {
                StartCoroutine(CreateRoomsCoroutine(roomsToCreatePerEvent));
                roomCreationTimer = 0f;
            }

            yield return null;
        }

        // เวลาหมด
        if (isGameActive)
        {
            // ตรวจสอบว่าดาวถึงขั้นต่ำไหม (1 ดาวขึ้นไปถือว่าผ่าน)
            EndGame(roomsCompleted >= star1Threshold);
        }
    }

    private void RefreshUI()
    {
        if (GameUIManager.Instance == null) return;

        float progress = (float)roomsCompleted / totalRoomsToWin;
        GameUIManager.Instance.UpdateHUD(progress, currentGameTime, CalculateStars());
    }

    private void EndGame(bool isVictory)
    {
        if (!isGameActive) return;
        isGameActive = false;
        StopAllCoroutines();

        if (playerController != null) playerController.SetMovement(false);

        if (GameUIManager.Instance != null)
        {
            if (isVictory)
            {
                int stars = CalculateStars();
                if (LevelProgressManager.Instance != null)
                    LevelProgressManager.Instance.SaveLevelResult(currentLevelID, stars);

                GameUIManager.Instance.SetupVictoryScreen(stars, currentGameTime);
            }
            else
            {
                GameUIManager.Instance.ShowGameOver();
            }
        }

        if (isVictory)
        {
            DestroyAllGeneratedRooms();
        }
    }

    // --- Revive System ---

    public void OnClickReviveWithAd()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.ShowRewardedVideoAds(ReviveGame);
        }
        else
        {
            Debug.LogWarning("AdsManager not found! Reviving immediately (Dev Mode).");
            ReviveGame();
        }
    }

    public void ReviveGame()
    {
        Debug.Log("Resurrecting Player...");

        isGameActive = true;
        currentGameTime += reviveTimeBonus;

        if (playerController != null)
        {
            playerController.SetMovement(true);
        }

        TeleportPlayerToSpawn();

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.SwitchUIState(UIState.Gameplay);
        }

        RefreshUI();
        StartCoroutine(GameLoopCoroutine());
    }

    // --- Task Management System ---

    public void CloseAllOpenTasks()
    {
        // Unity 6.2 Best Practice: FindObjectsByType
        TaskBase[] activeTasks = FindObjectsByType<TaskBase>(FindObjectsSortMode.None);

        foreach (var task in activeTasks)
        {
            if (task.gameObject.activeInHierarchy)
            {
                Destroy(task.gameObject);
            }
        }

        Debug.Log($"Closed {activeTasks.Length} active tasks.");
    }

    // -----------------------------

    private int CalculateStars()
    {
        // Logic นี้ถูกต้องแล้ว:
        // ถ้าครบ 3 ห้อง (totalRoomsToWin) -> 3 ดาว
        // ถ้าไม่ครบ แต่มากกว่า star2Threshold -> 2 ดาว
        // ถ้ามากกว่า star1Threshold -> 1 ดาว
        // น้อยกว่านั้น -> 0 ดาว
        if (roomsCompleted >= totalRoomsToWin) return 3;
        if (roomsCompleted >= star2Threshold) return 2;
        if (roomsCompleted >= star1Threshold) return 1;
        return 0;
    }

    private void DestroyAllGeneratedRooms()
    {
        if (dungeonGenerator?.GeneratedRooms == null) return;
        foreach (var room in dungeonGenerator.GeneratedRooms.ToList())
        {
            if (room != null && room.roomType != RoomData.RoomType.Spawn)
                dungeonGenerator.RemoveRoom(room);
        }
    }

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
        UnsubscribeFromRoomEvents(completedRoom);

        if (isGameActive)
        {
            roomsCompleted++;
            RefreshUI();

            if (roomsCompleted >= totalRoomsToWin)
            {
                EndGame(true);
                return;
            }
        }

        DestroyRoom(completedRoom);
    }

    public IEnumerator CreateRoomsCoroutine(int count)
    {
        if (spawnRoom == null || dungeonGenerator == null) yield break;

        int successfulCreations = 0;
        int roomsToCreate = count;

        List<Connector> allConnectors = spawnRoom.connectors
            .Select(t => t.GetComponent<Connector>())
            .Where(c => c != null)
            .ToList();

        for (int i = 0; i < roomsToCreate; i++)
        {
            if (roomsCompleted >= totalRoomsToWin) break;

            int activeRoomsCount = dungeonGenerator.GeneratedRooms.Count(r => r != null && r.roomType == RoomData.RoomType.Room);
            if (roomsCompleted + activeRoomsCount >= totalRoomsToWin)
            {
                break;
            }

            bool roomCreatedInThisSlot = false;

            foreach (Connector connector in allConnectors)
            {
                if (connector.IsOccupied()) continue;

                connector.SetOccupied(true);

                const int MAX_ATTEMPTS_PER_CONNECTOR = 3;
                bool placedSuccess = false;

                for (int attempt = 0; attempt < MAX_ATTEMPTS_PER_CONNECTOR; attempt++)
                {
                    if (TryPlaceRoomChain(spawnRoom, connector.transform))
                    {
                        placedSuccess = true;
                        break;
                    }
                }

                if (placedSuccess)
                {
                    successfulCreations++;
                    roomCreatedInThisSlot = true;
                    break;
                }
                else
                {
                    connector.SetOccupied(false);
                }
            }

            if (!roomCreatedInThisSlot)
            {
                break;
            }

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
                TeleportPlayerToSpawn();
            }
            dungeonGenerator.RemoveRoom(roomToDestroy);
        }
    }

    private bool IsPlayerInRoom(RoomData room)
    {
        if (playerController == null || playerController.transform == null || room.roomBoundsCollider == null) return false;
        Vector3 playerPos = playerController.transform.position;
        return room.roomBoundsCollider.bounds.Contains(playerPos);
    }

    private void TeleportPlayerToSpawn()
    {
        if (spawnRoom == null || playerController == null) return;

        Vector3 targetPosition;

        if (spawnRoom.playerSpawnPoint != null)
        {
            targetPosition = spawnRoom.playerSpawnPoint.position;
        }
        else
        {
            Debug.LogWarning("Spawn Room does not have a 'Player Spawn Point' assigned. Using bounds center as fallback.");
            targetPosition = spawnRoom.roomBoundsCollider != null ? spawnRoom.roomBoundsCollider.bounds.center : spawnRoom.transform.position;
            targetPosition.y = playerController.transform.position.y;
        }

        playerController.transform.position = targetPosition;
    }

    public List<TaskBase> GetPrioritizedTasks(List<TaskBase> candidates, int count)
    {
        List<TaskBase> selectedTasks = new List<TaskBase>();
        List<TaskBase> tempCandidates = new List<TaskBase>(candidates);

        for (int i = 0; i < count; i++)
        {
            if (tempCandidates.Count == 0) break;

            TaskBase picked = PickOneTask(tempCandidates);

            if (picked != null)
            {
                selectedTasks.Add(picked);
                tempCandidates.Remove(picked);
            }
        }

        return selectedTasks;
    }

    private TaskBase PickOneTask(List<TaskBase> candidates)
    {
        if (_taskPriorityDeck.Count == 0)
        {
            RefillTaskDeck();
        }

        for (int i = 0; i < _taskPriorityDeck.Count; i++)
        {
            string taskNameInDeck = _taskPriorityDeck[i];
            TaskBase match = candidates.FirstOrDefault(t => CleanTaskName(t.name) == taskNameInDeck);

            if (match != null)
            {
                _taskPriorityDeck.RemoveAt(i);
                return match;
            }
        }

        if (candidates.Count > 0)
        {
            int rnd = Random.Range(0, candidates.Count);
            return candidates[rnd];
        }

        return null;
    }

    private void RefillTaskDeck()
    {
        _taskPriorityDeck.Clear();

        if (allTaskPrefabsMasterList == null || allTaskPrefabsMasterList.Count == 0) return;

        foreach (TaskBase taskPrefab in allTaskPrefabsMasterList)
        {
            if (taskPrefab != null)
            {
                _taskPriorityDeck.Add(taskPrefab.name);
            }
        }

        int n = _taskPriorityDeck.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            string value = _taskPriorityDeck[k];
            _taskPriorityDeck[k] = _taskPriorityDeck[n];
            _taskPriorityDeck[n] = value;
        }
    }

    private string CleanTaskName(string original)
    {
        return original.Replace("(Clone)", "").Trim();
    }
}