using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; // ใช้สำหรับการโหลด Scene ใหม่เมื่อเกมจบ

public class GameCoreManager : MonoBehaviour
{
    public static GameCoreManager Instance { get; private set; }

    [Header("Game Time Settings")]
    [Tooltip("เวลารวมทั้งหมดของเกม (วินาที)")]
    public float totalGameDuration = 300f; // 5 นาที = 300 วินาที

    [Tooltip("เวลาที่ผู้เล่นใช้ได้ในแต่ละห้อง (วินาที)")]
    public float roomTimeLimit = 60f; // 1 นาทีต่อห้อง

    [Header("References")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject spawnPoint; // GameObject ที่ระบุจุดเกิด (Spawn Room)

    // อ้างอิงถึง Script อื่นๆ
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField]private TaskManager taskManager; // สมมติว่ามี TaskManager สำหรับสุ่ม Task

    private float currentRoomTimer;
    private bool isGameActive = false;
    private RoomData currentRoomData;
    private GameObject playerInstance;

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
    }

    // ⭐ 1. เริ่มเกมและสร้าง Spawn Room/Player
    private void StartGameInitialization()
    {
        // สร้าง Spawn Room (DungeonGenerator.Generate() จะจัดการเอง)
        dungeonGenerator.Generate();

        // หาจุดเกิด (สมมติว่า RoomData แรกที่สร้างคือ Spawn Room)
        if (dungeonGenerator.GetGeneratedRooms().Count > 0)
        {
            currentRoomData = dungeonGenerator.GetGeneratedRooms()[0];
            spawnPoint = currentRoomData.transform.Find("SpawnPoint")?.gameObject;
            if (spawnPoint == null)
            {
                spawnPoint = currentRoomData.gameObject; // ใช้ Room เป็นจุดเกิดสำรอง
            }

            // สร้างผู้เล่น
            playerInstance = playerPrefab;

            // เริ่มลูปเกมหลัก
            StartCoroutine(GameLoopCoroutine());
        }
        else
        {
            Debug.LogError("Failed to initialize Spawn Room.");
        }
    }

    // ⭐ 2. ลูปเกมหลัก (Game Loop)
    private IEnumerator GameLoopCoroutine()
    {
        isGameActive = true;

        // ตัวจับเวลาเกมโดยรวม
        float gameTimeRemaining = totalGameDuration;

        while (gameTimeRemaining > 0 && isGameActive)
        {
            // Update UI/HUD (แสดงเวลาที่เหลือ)
            // UIManager.UpdateGameTimer(gameTimeRemaining); 

            // ตรวจสอบสถานะ Task ในห้องปัจจุบัน
            if (taskManager != null && taskManager.IsRoomTaskCompleted(currentRoomData))
            {
                // ถ้า Task เสร็จ ให้สร้างห้องใหม่ทันที
                TransitionToNewRoom();
            }

            // ลดเวลาเกมรวม
            gameTimeRemaining -= Time.deltaTime;

            yield return null;
        }

        // 4. จบเกมเมื่อหมดเวลา
        EndGame(gameTimeRemaining <= 0);
    }

    // ⭐ 3.1 & 3.2 จัดการการเปลี่ยนห้อง (Room Transition)
    public void TransitionToNewRoom()
    {
        // 1. ตรวจสอบว่ามีห้องใหม่ที่จะเชื่อมต่อหรือไม่
        RoomData nextRoom = null;
        Transform startConnector = null;

        // พยายามหา Connector ที่ว่างบนห้องปัจจุบัน
        if (currentRoomData.HasAvailableConnector(out startConnector))
        {
            // ⭐ เรียก DungeonGenerator สร้าง Hallway-Room Chain
            if (dungeonGenerator.TryPlaceNewRoom(currentRoomData, startConnector, isHallwayChain: true))
            {
                // สมมติว่า Hallway-Room Chain ถูกเพิ่มเข้าไปใน generatedRooms ล่าสุด
                // Hallway จะอยู่ก่อน Room
                nextRoom = dungeonGenerator.GetGeneratedRooms()[dungeonGenerator.GetGeneratedRooms().Count - 1];
            }
        }

        if (nextRoom != null)
        {
            // Transition สำเร็จ: เข้าห้องใหม่
            EnterRoom(nextRoom);
        }
        else
        {
            // Transition ล้มเหลว: ห้องเต็ม/ชน ให้ลองหาห้องที่ว่างอื่นๆ
            Debug.LogWarning("Failed to generate a new room. Checking for existing rooms with empty connectors.");

            // (คุณอาจเพิ่มโค้ดที่นี่เพื่อสุ่มหาห้องที่วางอยู่แล้วที่มี Connector ว่างแล้วเข้าห้องนั้น)

            // หากหาห้องใหม่หรือห้องที่มีอยู่ไม่ได้ ให้ผู้เล่นทำ Task เดิมต่อไป
        }
    }

    // เมธอดสำหรับเข้าสู่ห้อง
    private void EnterRoom(RoomData newRoom)
    {
        // ย้ายผู้เล่นไปที่จุดเกิดใหม่ (จุดเข้าห้อง)
        // Note: คุณอาจต้องระบุตำแหน่งเกิดที่ประตูของห้องใหม่
        playerInstance.transform.position = newRoom.transform.position;
        currentRoomData = newRoom;
        currentRoomTimer = roomTimeLimit;

        // ⭐ กำหนด Task ใหม่
        if (taskManager != null)
        {
            taskManager.AssignNewRoomTask(newRoom);
        }

        // ⭐ เริ่มจับเวลาห้อง
        StartCoroutine(RoomTimerCoroutine(newRoom));
    }

    // ⭐ 3.1 & 3.2 ตัวจับเวลาห้อง (Room Timer)
    private IEnumerator RoomTimerCoroutine(RoomData room)
    {
        currentRoomTimer = roomTimeLimit;

        while (currentRoomTimer > 0 && currentRoomData == room && isGameActive)
        {
            // UIManager.UpdateRoomTimer(currentRoomTimer);

            // ตรวจสอบว่า Task เสร็จก่อนหมดเวลาหรือไม่
            if (taskManager != null && taskManager.IsRoomTaskCompleted(room))
            {
                // ถ้า Task เสร็จ ลูปนี้จะหยุด และ GameLoopCoroutine จะเรียก TransitionToNewRoom()
                yield break;
            }

            currentRoomTimer -= Time.deltaTime;
            yield return null;
        }

        // 3.2 เมื่อหมดเวลาห้อง (และ Task ยังไม่เสร็จ)
        if (currentRoomData == room)
        {
            Debug.Log($"Time's up in {room.name}! Returning to spawn.");
            ReturnToSpawn();
        }
    }

    // ⭐ 3.2 ดีดผู้เล่นกลับ Spawn
    public void ReturnToSpawn()
    {
        // 1. ทำลายห้องปัจจุบัน (ยกเว้น Spawn Room)
        if (currentRoomData.roomType != RoomData.RoomType.Spawn)
        {
            dungeonGenerator.RemoveRoom(currentRoomData);
        }

        // 2. ย้ายผู้เล่นกลับ Spawn
        RoomData spawnRoom = dungeonGenerator.GetGeneratedRooms().Find(r => r.roomType == RoomData.RoomType.Spawn);
        if (spawnRoom != null)
        {
            currentRoomData = spawnRoom;
            playerInstance.transform.position = spawnPoint.transform.position;

            // ⭐ เริ่มจับเวลาใหม่สำหรับห้อง Spawn (หรือเข้าสู่ลูปปกติ)
            EnterRoom(spawnRoom);
        }
    }

    // ⭐ 4. จบเกม
    private void EndGame(bool timeUp)
    {
        isGameActive = false;
        StopAllCoroutines();

        if (timeUp)
        {
            Debug.Log("GAME OVER! Time has run out.");
        }
        else
        {
            Debug.Log("GAME OVER! Something went wrong.");
        }

        //SceneManager.LoadScene(SceneManager.GetActiveScene().name); // รีสตาร์ทเกม
    }

    // เมธอดสาธารณะสำหรับ TaskManager เรียกเมื่อ Task เสร็จสมบูรณ์
    public void NotifyTaskCompleted()
    {
        // TransitionToNewRoom จะถูกเรียกโดย GameLoopCoroutine โดยการเช็คสถานะ
        // เราสามารถหยุด RoomTimerCoroutine ได้ที่นี่
    }
}