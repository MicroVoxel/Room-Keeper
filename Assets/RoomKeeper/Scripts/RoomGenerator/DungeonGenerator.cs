using UnityEngine;
using System.Collections.Generic;

public class DungeonGenerator : MonoBehaviour
{
    public static DungeonGenerator Instance { get; private set; }

    [Header("Generation Settings")]
    public int maxRooms = 10;

    [Header("Required Prefabs")]
    [SerializeField] private GameObject spawnRoomPrefab;
    [SerializeField] private List<GameObject> roomsPrefab;
    [SerializeField] private List<GameObject> specialRoomsPrefab;
    [SerializeField] private List<GameObject> alterSpawns;
    [SerializeField] private List<GameObject> hallwaysPrefabs;
    [SerializeField] private GameObject door;
    [SerializeField] private LayerMask roomsLayermask;

    private List<RoomData> generatedRooms;
    private bool isGenerated = false;

    public List<RoomData> GetGeneratedRooms() => generatedRooms;
    public bool IsGenerated() => isGenerated;

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
        generatedRooms = new List<RoomData>();
        // StartGeneration() ถูกย้ายไปเรียกใน GameCoreManager.StartGameInitialization()
    }

    /// <summary>
    /// ถูกเรียกโดย GameCoreManager เพื่อเริ่มต้นการสร้าง Dungeon ครั้งแรก
    /// </summary>
    public void StartGeneration()
    {
        // 1. สร้าง Spawn Room (ถูกจัดการใน Generate() ครั้งแรก)
        Generate();
        // 2. สร้างห้องเสริม/ทางเข้าอื่น
        GenerateAlternateEntrances();
        // 3. ปิด Connector ที่เหลือ
        FillEmpty();
        isGenerated = true;
    }

    /// <summary>
    /// เมธอดหลักสำหรับสร้างห้อง
    /// </summary>
    public void Generate()
    {
        // 1. วาง Spawn Room เป็นอันดับแรกเสมอ
        if (generatedRooms.Count < 1)
        {
            GameObject generatedRoom = Instantiate(spawnRoomPrefab, transform.position, Quaternion.identity);
            generatedRoom.transform.SetParent(gameObject.transform);
            if (generatedRoom.TryGetComponent<RoomData>(out RoomData roomData))
            {
                generatedRooms.Add(roomData);
            }
        }

        // 2. สร้างห้องที่เหลือ โดยเชื่อมต่อด้วย Hallway เสมอ (สำหรับการสร้างล่วงหน้า)
        int maxRoomsToGenerate = maxRooms - alterSpawns.Count;

        for (int i = generatedRooms.Count; i < maxRoomsToGenerate; i++)
        {
            RoomData startRoom = null;
            Transform startConnector = null;

            int totalRetries = 100;
            int retryIndex = 0;

            // หา Connector ที่ว่างจากห้องที่มีอยู่
            while (startRoom == null && retryIndex < totalRetries)
            {
                int randomLinkRoomIndex = Random.Range(0, generatedRooms.Count);
                RoomData roomToTest = generatedRooms[randomLinkRoomIndex];

                if (roomToTest.HasAvailableConnector(out startConnector))
                {
                    startRoom = roomToTest;
                    break;
                }
                retryIndex++;
            }

            if (startRoom == null)
            {
                Debug.LogWarning("Could not find an available connector after " + totalRetries + " retries. Stopping generation.");
                break;
            }

            // ⭐ เรียกใช้เมธอดสาธารณะสำหรับการสร้าง Hallway -> Room Chain
            if (!TryPlaceNewRoom(startRoom, startConnector, isHallwayChain: true))
            {
                // ถ้าการวางล้มเหลว (ชน/หา Connector ไม่เจอ) ให้ลองใหม่ในรอบถัดไป
                i--;
            }
        }
    }

    /// <summary>
    /// ⭐ เมธอดสาธารณะสำหรับสร้าง Room ใหม่ (Hallway-Room Chain หรือ Room เดี่ยว)
    /// </summary>
    /// <param name="startRoom">ห้องเดิมที่ต้องการต่อ</param>
    /// <param name="startConnector">Connector ที่ถูกจองไว้แล้วบน startRoom</param>
    /// <param name="isHallwayChain">True: วาง Hallway -> Room; False: วาง Room เดี่ยวๆ (สำหรับ Alternate Entrances)</param>
    /// <returns>True ถ้าวางสำเร็จ, False ถ้าวางล้มเหลว</returns>
    public bool TryPlaceNewRoom(RoomData startRoom, Transform startConnector, bool isHallwayChain = true)
    {
        if (!isHallwayChain)
        {
            // วาง Room เดี่ยว (เช่นสำหรับ Alternate Entrances)
            return PlaceSingleRoom(startRoom, startConnector, SelectNextRoomPrefab());
        }

        // วาง Hallway -> Room Chain
        if (hallwaysPrefabs.Count == 0)
        {
            Debug.LogError("Hallways Prefabs are required for HallwayChain logic but none are assigned!");
            startRoom.UnuseConnector(startConnector);
            return false;
        }

        GameObject hallwayPrefab = hallwaysPrefabs[Random.Range(0, hallwaysPrefabs.Count)];
        GameObject roomPrefab = SelectNextRoomPrefab();

        if (roomPrefab == null)
        {
            startRoom.UnuseConnector(startConnector);
            return false;
        }

        // 1. วาง Hallway
        GameObject door1 = Instantiate(door, transform.position, Quaternion.identity);
        GameObject generatedHallway = Instantiate(hallwayPrefab, transform.position, Quaternion.identity, gameObject.transform);

        if (generatedHallway.TryGetComponent<RoomData>(out RoomData hallwayData))
        {
            if (hallwayData.HasAvailableConnector(out Transform hallwayConnector1))
            {
                generatedRooms.Add(hallwayData);
                GenerateDoor(door1, startConnector);
                AlignRooms(startRoom.transform, generatedHallway.transform, startConnector, hallwayConnector1);

                if (HandleIntersection2D(hallwayData))
                {
                    // ล้มเหลว: ทำลาย Hallway, ปลดล็อค Connector ทั้งหมด
                    hallwayData.UnuseConnector(hallwayConnector1);
                    startRoom.UnuseConnector(startConnector);
                    Destroy(generatedHallway);
                    Destroy(door1);
                    return false;
                }

                // 2. วาง Room/SpecialRoom ต่อจาก Hallway
                GameObject door2 = Instantiate(door, transform.position, Quaternion.identity);

                if (hallwayData.HasAvailableConnector(out Transform hallwayConnector2))
                {
                    if (PlaceSingleRoom(hallwayData, hallwayConnector2, roomPrefab, door2))
                    {
                        return true; // วาง Hallway-Room สำเร็จ
                    }
                }

                // หาก Room ล้มเหลว แต่ Hallway ถูกวางไปแล้ว (ต้องทำลาย Hallway ด้วย)
                hallwayData.UnuseConnector(hallwayConnector1);
                startRoom.UnuseConnector(startConnector);
                Destroy(generatedHallway);
                Destroy(door1);
                Destroy(door2);
            }
        }

        // หาก Hallway ล้มเหลวตั้งแต่แรก (หา Connector ใน Hallway ไม่เจอ)
        startRoom.UnuseConnector(startConnector);
        Destroy(generatedHallway);
        Destroy(door1);
        return false;
    }

    /// <summary>
    /// วางห้อง Room/Special Room เดี่ยวๆ ต่อจากห้องที่มีอยู่
    /// </summary>
    private bool PlaceSingleRoom(RoomData startRoom, Transform startConnector, GameObject roomPrefab, GameObject doorToAlign = null)
    {
        GameObject generatedRoom = Instantiate(roomPrefab, transform.position, Quaternion.identity, gameObject.transform);

        bool doorIsNew = doorToAlign == null;
        if (doorIsNew) doorToAlign = Instantiate(door, transform.position, Quaternion.identity);

        if (generatedRoom.TryGetComponent<RoomData>(out RoomData roomData))
        {
            if (roomData.HasAvailableConnector(out Transform roomConnector))
            {
                generatedRooms.Add(roomData);
                GenerateDoor(doorToAlign, startConnector);
                AlignRooms(startRoom.transform, generatedRoom.transform, startConnector, roomConnector);

                if (HandleIntersection2D(roomData))
                {
                    // ล้มเหลว
                    roomData.UnuseConnector(roomConnector);
                    startRoom.UnuseConnector(startConnector);
                    Destroy(generatedRoom);
                    if (doorIsNew) Destroy(doorToAlign);
                    return false;
                }

                return true; // สำเร็จ
            }
        }

        // ล้มเหลว
        startRoom.UnuseConnector(startConnector);
        Destroy(generatedRoom);
        if (doorIsNew) Destroy(doorToAlign);
        return false;
    }

    /// <summary>
    /// ⭐ เมธอดสาธารณะสำหรับลบ Room (รวมถึง Hallway) และทำลาย Door
    /// </summary>
    public void RemoveRoom(RoomData roomToRemove)
    {
        if (roomToRemove == null || !generatedRooms.Contains(roomToRemove)) return;

        // ⭐ 1. แจ้ง TaskManager ให้ลบ Task ที่เกี่ยวข้อง
        TaskManager taskManager = FindFirstObjectByType<TaskManager>();
        if (taskManager != null)
        {
            taskManager.RemoveTask(roomToRemove);
        }

        // 2. ทำลายประตู/กำแพงทั้งหมดที่ติดอยู่กับ Connector ของห้องนี้
        foreach (Transform connector in roomToRemove.connectors)
        {
            if (connector.childCount > 0)
            {
                Destroy(connector.GetChild(0).gameObject);
            }
            roomToRemove.UnuseConnector(connector);
        }

        // 3. ลบออกจากรายการและทำลาย GameObject
        generatedRooms.Remove(roomToRemove);
        Destroy(roomToRemove.gameObject);

        Debug.Log($"Room {roomToRemove.name} removed.");
    }

    private GameObject SelectNextRoomPrefab()
    {
        if (roomsPrefab.Count == 0) return null;

        if (specialRoomsPrefab.Count > 0 && Random.Range(0f, 1f) > 0.9f)
        {
            return specialRoomsPrefab[Random.Range(0, specialRoomsPrefab.Count)];
        }

        return roomsPrefab[Random.Range(0, roomsPrefab.Count)];
    }

    private void GenerateDoor(GameObject doorToAlign, Transform room1Connector)
    {
        doorToAlign.transform.SetParent(room1Connector);
        doorToAlign.transform.position = room1Connector.transform.position;
        doorToAlign.transform.rotation = room1Connector.transform.rotation;
    }

    private void GenerateAlternateEntrances()
    {
        if (alterSpawns.Count < 1) return;

        for (int i = 0; i < alterSpawns.Count; i++)
        {
            RoomData randomGeneratedRoom = null;
            Transform room1Connector = null;
            int totalRetries = 100;
            int retryIndex = 0;

            while (randomGeneratedRoom == null && retryIndex < totalRetries)
            {
                int randomLinkRoomIndex = Random.Range(0, generatedRooms.Count);
                RoomData roomToTest = generatedRooms[randomLinkRoomIndex];
                if (roomToTest.HasAvailableConnector(out room1Connector))
                {
                    randomGeneratedRoom = roomToTest;
                    break;
                }
                retryIndex++;
            }
            if (randomGeneratedRoom == null) continue;

            // วางห้อง alterSpawn เดี่ยวๆ โดยไม่ใช้ Hallway
            GameObject alterPrefab = alterSpawns[Random.Range(0, alterSpawns.Count)];
            PlaceSingleRoom(randomGeneratedRoom, room1Connector, alterPrefab);
        }
    }

    private void FillEmpty()
    {
        generatedRooms.ForEach(room => room.FillEmptyDoors());
    }

    private bool HandleIntersection2D(RoomData roomData)
    {
        if (roomData.collider2D == null) return false;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            roomData.collider2D.bounds.center,
            roomData.collider2D.bounds.size * 0.9f,
            roomData.collider2D.transform.eulerAngles.z,
            roomsLayermask);

        foreach (Collider2D hit in hits)
        {
            if (hit.transform.root != roomData.transform.root)
            {
                return true;
            }
        }
        return false;
    }

    private void AlignRooms(Transform room1, Transform room2, Transform room1Connector, Transform room2Connector)
    {
        // ตรรกะการจัดตำแหน่งสำหรับ 2D (สมมติว่า Connector ชี้ออกตามแกน X)
        Vector3 desiredDirection = -room1Connector.right;
        float angleDifference = Vector2.SignedAngle(room2Connector.right, desiredDirection);

        room2.Rotate(0, 0, angleDifference, Space.World);

        Vector3 offset = room1Connector.position - room2Connector.position;
        room2.position += offset;

        Physics2D.SyncTransforms();
    }

}