using UnityEngine;
using System.Collections.Generic;
using System.Linq; // ยังคงต้องใช้ LINQ สำหรับส่วนอื่น แต่เราจะเลี่ยงใน Hot Path

/// <summary>
/// (Optimized Version)
/// จัดการการ Instantiate, การจัดตำแหน่ง, การตรวจสอบการชน, และการทำลายของ Room Prefabs
/// </summary>
public class DungeonGenerator : MonoBehaviour
{
    #region INSTANCE & CORE SETUP

    public static DungeonGenerator Instance { get; private set; }

    [Header("Generation Settings")]
    public int maxRooms = 10;

    [Header("Required Prefabs")]
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

    // ... (โค้ดส่วน GenerateSpawn, GenerateRoom, TryPlaceRoom ไม่มีการเปลี่ยนแปลง) ...

    public RoomData GenerateSpawn()
    {
        if (spawnRoomPrefab == null)
        {
            Debug.LogError("Spawn Room Prefab is not assigned!");
            return null;
        }

        RoomData roomData = GenerateRoomInternal(spawnRoomPrefab);
        if (roomData != null)
        {
            isGenerated = true;
        }
        return roomData;
    }

    public RoomData GenerateRoom(GameObject roomPrefab)
    {
        return GenerateRoomInternal(roomPrefab);
    }

    public bool TryPlaceRoom(RoomData startRoom, Transform startConnector, GameObject newRoomPrefab, GameObject doorPrefab = null)
    {
        RoomData roomData = GenerateRoomInternal(newRoomPrefab);
        if (roomData == null)
        {
            startRoom.UnuseConnector(startConnector);
            return false;
        }

        if (!roomData.HasAvailableConnector(out Transform roomConnector))
        {
            RemoveRoom(roomData);
            startRoom.UnuseConnector(startConnector);
            return false;
        }

        AlignRooms(startRoom.transform, roomData.transform, startConnector, roomConnector);

        // --- (Optimized) เรียกใช้ HandleIntersection2D (เวอร์ชันไม่ใช้ LINQ) ---
        if (HandleIntersection2D(roomData))
        {
            // ล้มเหลว: ห้องชน
            roomData.UnuseConnector(roomConnector);
            startRoom.UnuseConnector(startConnector);
            RemoveRoom(roomData);
            return false;
        }

        return true;
    }

    // ... (โค้ดส่วน RemoveRoom, FillEmpty ไม่มีการเปลี่ยนแปลง) ...
    public void RemoveRoom(RoomData roomToRemove)
    {
        if (roomToRemove == null || !generatedRooms.Contains(roomToRemove)) return;

        if (roomToRemove.parentConnector != null)
        {
            if (roomToRemove.parentConnector.TryGetComponent<Connector>(out Connector parentConnectorComponent))
            {
                parentConnectorComponent.SetOccupied(false);
            }
        }

        if (roomToRemove.roomType == RoomData.RoomType.Room)
        {
            RoomData hallwayData = null;
            if (roomToRemove.parentConnector != null && roomToRemove.parentConnector.parent != null)
            {
                roomToRemove.parentConnector.parent.TryGetComponent<RoomData>(out hallwayData);
            }

            if (hallwayData != null && hallwayData.roomType == RoomData.RoomType.Hallway)
            {
                if (hallwayData.parentConnector != null)
                {
                    if (hallwayData.parentConnector.TryGetComponent<Connector>(out Connector spawnConnectorComponent))
                    {
                        spawnConnectorComponent.SetOccupied(false);
                    }
                }
                generatedRooms.Remove(hallwayData);
                Destroy(hallwayData.gameObject);
            }
        }
        generatedRooms.Remove(roomToRemove);
        Destroy(roomToRemove.gameObject);
    }
    public void FillEmpty()
    {

    }

    #endregion

    // -------------------------------------------------------------------

    #region PRIVATE INSTANTIATION & UTILITIES

    // ... (โค้ดส่วน GenerateRoomInternal ไม่มีการเปลี่ยนแปลง) ...
    private RoomData GenerateRoomInternal(GameObject roomPrefab)
    {
        if (roomPrefab == null) return null;

        GameObject generatedRoom = Instantiate(roomPrefab, transform.position, Quaternion.identity, gameObject.transform);

        if (generatedRoom.TryGetComponent<RoomData>(out RoomData roomData))
        {
            generatedRooms.Add(roomData);
            return roomData;
        }

        Destroy(generatedRoom);
        return null;
    }

    /// <summary>
    /// (Optimized) ตรวจสอบการชนโดยไม่ใช้ LINQ
    /// </summary>
    private bool HandleIntersection2D(RoomData roomData)
    {
        if (roomData.collider2DForComposit == null) return false;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            roomData.collider2DForComposit.bounds.center,
            roomData.collider2DForComposit.bounds.size * 0.9f,
            roomData.collider2DForComposit.transform.eulerAngles.z,
            roomsLayermask);

        // --- Optimization: Replaced LINQ .Any() with a fast foreach loop ---
        // LINQ: return hits.Any(hit => hit.transform.root != roomData.transform.root);
        foreach (Collider2D hit in hits)
        {
            if (hit.transform.root != roomData.transform.root)
            {
                return true; // ชนกับห้องอื่นที่ไม่ใช่ตัวเอง
            }
        }
        return false; // ไม่ชน
        // -----------------------------------------------------------------
    }

    // ... (โค้ดส่วน AlignRooms ไม่มีการเปลี่ยนแปลง) ...
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

    // ... (โค้ดส่วน Prefab Selection ไม่มีการเปลี่ยนแปลง) ...
    public GameObject SelectNextRoomPrefab()
    {
        if (roomsPrefab.Count == 0) return null;
        if (specialRoomsPrefab.Count > 0 && UnityEngine.Random.Range(0f, 1f) > 0.9f)
        {
            return specialRoomsPrefab[UnityEngine.Random.Range(0, specialRoomsPrefab.Count)];
        }
        return roomsPrefab[UnityEngine.Random.Range(0, roomsPrefab.Count)];
    }

    public GameObject SelectRandomHallwayPrefab()
    {
        if (hallwaysPrefabs.Count == 0)
        {
            Debug.LogError("Hallways Prefabs are required but none are assigned!");
            return null;
        }
        return hallwaysPrefabs[UnityEngine.Random.Range(0, hallwaysPrefabs.Count)];
    }

    public GameObject SelectRandomAlterSpawnPrefab()
    {
        if (alterSpawns.Count == 0) return null;
        return alterSpawns[UnityEngine.Random.Range(0, alterSpawns.Count)];
    }

    #endregion
}