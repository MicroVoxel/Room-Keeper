using UnityEngine;
using System.Collections.Generic;
using System.Linq; // เพิ่ม using เพื่อใช้ LINQ (ToList(), Where(), FirstOrDefault())

/// <summary>
/// จัดการการ Instantiate, การจัดตำแหน่ง, การตรวจสอบการชน, และการทำลายของ Room Prefabs
/// โค้ดที่เรียกใช้เมธอดเหล่านี้ (Generation Logic) ควรอยู่ในสคริปต์อื่น
/// </summary>
public class DungeonGenerator : MonoBehaviour
{
    #region INSTANCE & CORE SETUP

    public static DungeonGenerator Instance { get; private set; }

    [Header("Generation Settings")]
    [Tooltip("จำนวนห้องสูงสุดที่ต้องการให้ Dungeon Planner สร้าง")]
    public int maxRooms = 10;

    [Header("Required Prefabs")]
    // เรายังคงต้องการ Prefabs เหล่านี้เพื่อใช้ในการ Instantiate
    [SerializeField] private GameObject spawnRoomPrefab;
    [SerializeField] private List<GameObject> roomsPrefab;
    [SerializeField] private List<GameObject> specialRoomsPrefab;
    [SerializeField] private List<GameObject> alterSpawns;
    [SerializeField] private List<GameObject> hallwaysPrefabs;
    [SerializeField] private LayerMask roomsLayermask;

    [SerializeField] private List<RoomData> generatedRooms = new List<RoomData>();
    private bool isGenerated = false;

    // Properties สาธารณะ
    public List<RoomData> GeneratedRooms => generatedRooms;
    public bool IsGenerated => isGenerated;

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

    #endregion

    // -------------------------------------------------------------------

    #region PUBLIC ROOM UTILITIES (EXECUTION LAYER)

    /// <summary>
    /// ⭐ สร้าง Spawn Room โดยใช้ spawnRoomPrefab
    /// </summary>
    public RoomData GenerateSpawn()
    {
        if (spawnRoomPrefab == null)
        {
            Debug.LogError("Spawn Room Prefab is not assigned!");
            return null;
        }

        // ใช้เมธอด GenerateRoomInternal ที่เป็น Generic Instantiation
        RoomData roomData = GenerateRoomInternal(spawnRoomPrefab);
        if (roomData != null)
        {
            //Debug.Log("Spawn Room generated.");
            isGenerated = true; // ตั้งค่า isGenerated ที่นี่ เนื่องจากเป็นจุดเริ่มต้น
        }
        return roomData;
    }

    /// <summary>
    /// ⭐ สร้างห้องทั่วไปจาก Prefab ที่กำหนด และเพิ่มเข้า List
    /// </summary>
    /// <param name="roomPrefab">Prefab ของห้องที่ต้องการสร้าง (Room, Hallway, Special)</param>
    /// <returns>RoomData ที่ถูกสร้างขึ้น, หรือ null หากล้มเหลว</returns>
    public RoomData GenerateRoom(GameObject roomPrefab)
    {
        return GenerateRoomInternal(roomPrefab);
    }

    /// <summary>
    /// ⭐ เมธอดหลักสำหรับพยายามวางห้องใหม่โดยต่อเข้ากับ Connector ของห้องเดิม
    /// </summary>
    public bool TryPlaceRoom(RoomData startRoom, Transform startConnector, GameObject newRoomPrefab, GameObject doorPrefab = null)
    {
        // 1. Instantiation และ Check
        RoomData roomData = GenerateRoomInternal(newRoomPrefab);
        if (roomData == null)
        {
            startRoom.UnuseConnector(startConnector);
            return false;
        }

        // 2. Connector Alignment
        if (!roomData.HasAvailableConnector(out Transform roomConnector))
        {
            // ถ้าห้องใหม่ไม่มี Connector ว่างให้ต่อ (ไม่ควรเกิดขึ้นกับ Prefab ที่ดี)
            RemoveRoom(roomData);
            startRoom.UnuseConnector(startConnector);
            return false;
        }

        // 3. Placement

        AlignRooms(startRoom.transform, roomData.transform, startConnector, roomConnector);

        // 4. Intersection Check
        if (HandleIntersection2D(roomData))
        {
            // ล้มเหลว: ห้องชน
            roomData.UnuseConnector(roomConnector);
            startRoom.UnuseConnector(startConnector);
            RemoveRoom(roomData); // ใช้ RemoveRoom เพื่อความสมบูรณ์
            return false;
        }

        // 5. Success
        return true;
    }

    /// <summary>
    /// ⭐ เมธอดสาธารณะสำหรับลบ Room (รวมถึง Hallway) และทำลาย Door/Task
    /// </summary>
    public void RemoveRoom(RoomData roomToRemove)
    {
        if (roomToRemove == null || !generatedRooms.Contains(roomToRemove)) return;

        // 1. Unuse Connector ที่เชื่อมมายัง Room นี้ (Connector ของ Parent Room)
        if (roomToRemove.parentConnector != null)
        {
            // 1.1 Unuse Connector ใน Parent Room
            if (roomToRemove.parentConnector.TryGetComponent<Connector>(out Connector parentConnectorComponent))
            {
                parentConnectorComponent.SetOccupied(false);
                //Debug.Log($"Connector freed: {roomToRemove.parentConnector.name} on Parent Room.");
            }
        }

        // 2. ทำลาย Hallway ที่เป็น Parent (ถ้าห้องที่ถูกลบคือ Room)
        if (roomToRemove.roomType == RoomData.RoomType.Room)
        {
            // ⭐ NEW: ดึง Hallway Data มาจาก Parent Connector
            RoomData hallwayData = null;

            // Parent Connector (Connector ของ Hallway) จะเป็นตัวเชื่อม
            // หา RoomData จาก GameObject ที่เป็นเจ้าของ Connector (Hallway)
            if (roomToRemove.parentConnector != null && roomToRemove.parentConnector.parent != null)
            {
                // Hallway คือ parent ของ Connector นี้ (ในทาง hierarchy)
                roomToRemove.parentConnector.parent.TryGetComponent<RoomData>(out hallwayData);
            }

            if (hallwayData != null && hallwayData.roomType == RoomData.RoomType.Hallway)
            {
                //Debug.Log($"Destroying linked Hallway: {hallwayData.name}");

                // ⚠️ WARNING: Hallway นี้จะต้อง Unuse Connector ของ Spawn Room ด้วย!
                // ถ้า HallwayData มี parentConnector (ซึ่งคือ Connector ของ Spawn Room)
                // เราต้องสั่ง Unuse ตัวนั้นด้วย
                if (hallwayData.parentConnector != null)
                {
                    if (hallwayData.parentConnector.TryGetComponent<Connector>(out Connector spawnConnectorComponent))
                    {
                        spawnConnectorComponent.SetOccupied(false);
                        //Debug.Log($"Hallway freed Spawn Connector: {hallwayData.parentConnector.name}");
                    }
                }

                // ลบ Hallway ออกจาก List และทำลาย GameObject
                generatedRooms.Remove(hallwayData);
                Destroy(hallwayData.gameObject);
            }
        }

        // 3. ลบ Room ปัจจุบันออกจากรายการและทำลาย GameObject
        generatedRooms.Remove(roomToRemove);
        Destroy(roomToRemove.gameObject);
    }

    /// <summary>
    /// วางกำแพง (Wall) ปิด Connector ที่ยังไม่ได้ถูกใช้งานทั้งหมดในทุกห้อง
    /// </summary>
    public void FillEmpty()
    {

    }

    #endregion

    // -------------------------------------------------------------------

    #region PRIVATE INSTANTIATION & UTILITIES

    /// <summary>
    /// Internal Instantiation Logic สำหรับทุกประเภทห้อง
    /// </summary>
    private RoomData GenerateRoomInternal(GameObject roomPrefab)
    {
        if (roomPrefab == null) return null;

        GameObject generatedRoom = Instantiate(roomPrefab, transform.position, Quaternion.identity, gameObject.transform);

        if (generatedRoom.TryGetComponent<RoomData>(out RoomData roomData))
        {
            generatedRooms.Add(roomData);
            return roomData;
        }

        // ถ้าไม่มี RoomData component ให้ทำลายทิ้ง
        Destroy(generatedRoom);
        return null;
    }

    private bool HandleIntersection2D(RoomData roomData)
    {
        if (roomData.collider2DForComposit == null) return false;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            roomData.collider2DForComposit.bounds.center,
            roomData.collider2DForComposit.bounds.size * 0.9f,
            roomData.collider2DForComposit.transform.eulerAngles.z,
            roomsLayermask);

        // ใช้ LINQ เพื่อให้สั้นลง
        return hits.Any(hit => hit.transform.root != roomData.transform.root);
    }

    private void AlignRooms(Transform room1, Transform room2, Transform room1Connector, Transform room2Connector)
    {
        Vector3 desiredDirection = -room1Connector.right;
        float angleDifference = Vector2.SignedAngle(room2Connector.right, desiredDirection);

        room2.Rotate(0, 0, angleDifference, Space.World);

        Vector3 offset = room1Connector.position - room2Connector.position;
        room2.position += offset;

        Physics2D.SyncTransforms();
    }

    #endregion

    // -------------------------------------------------------------------

    #region PREFAB SELECTION UTILITIES

    /// <summary>
    /// สุ่มเลือก Prefab ห้องถัดไป (Room หรือ Special Room)
    /// </summary>
    public GameObject SelectNextRoomPrefab()
    {
        if (roomsPrefab.Count == 0) return null;

        // สุ่มโอกาส 10% ที่จะได้ Special Room
        if (specialRoomsPrefab.Count > 0 && UnityEngine.Random.Range(0f, 1f) > 0.9f)
        {
            return specialRoomsPrefab[UnityEngine.Random.Range(0, specialRoomsPrefab.Count)];
        }

        return roomsPrefab[UnityEngine.Random.Range(0, roomsPrefab.Count)];
    }

    /// <summary>
    /// ส่งคืน Prefab Hallway แบบสุ่ม
    /// </summary>
    public GameObject SelectRandomHallwayPrefab()
    {
        if (hallwaysPrefabs.Count == 0)
        {
            Debug.LogError("Hallways Prefabs are required but none are assigned!");
            return null;
        }
        return hallwaysPrefabs[UnityEngine.Random.Range(0, hallwaysPrefabs.Count)];
    }

    /// <summary>
    /// ส่งคืน Prefab Alternate Spawn แบบสุ่ม
    /// </summary>
    public GameObject SelectRandomAlterSpawnPrefab()
    {
        if (alterSpawns.Count == 0) return null;
        return alterSpawns[UnityEngine.Random.Range(0, alterSpawns.Count)];
    }

    #endregion
}