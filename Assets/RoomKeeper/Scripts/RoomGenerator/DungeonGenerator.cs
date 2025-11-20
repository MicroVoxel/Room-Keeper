using UnityEngine;
using System.Collections.Generic;

public class DungeonGenerator : MonoBehaviour
{
    #region INSTANCE & CORE SETUP

    public static DungeonGenerator Instance { get; private set; }

    //[Header("Generation Settings")]
    //public int maxRooms = 10;

    [Header("Required Prefabs")]
    [SerializeField] private GameObject spawnRoomPrefab;
    [SerializeField] private List<GameObject> roomsPrefab;
    [SerializeField] private List<GameObject> specialRoomsPrefab;
    [SerializeField] private List<GameObject> alterSpawns;
    [SerializeField] private List<GameObject> hallwaysPrefabs;
    [SerializeField] private LayerMask roomsLayermask;

    [SerializeField] private List<RoomData> generatedRooms = new List<RoomData>();
    private bool isGenerated = false;

    private Collider2D[] collisionCheckResults = new Collider2D[20];
    private ContactFilter2D roomCollisionFilter;

    // --- DECK SYSTEM VARIABLES ---
    private List<GameObject> _availableRoomDeck = new List<GameObject>();

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

        roomCollisionFilter = new ContactFilter2D();
        roomCollisionFilter.SetLayerMask(roomsLayermask);
        roomCollisionFilter.useTriggers = true;

        // เริ่มต้น: เตรียม Deck ให้พร้อมใช้งาน
        RefillRoomDeck();
    }

    #endregion

    #region PUBLIC ROOM UTILITIES (EXECUTION LAYER)

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

        if (!AlignRooms(startRoom.transform, roomData.transform, startConnector, roomConnector, roomData.allowRotation))
        {
            startRoom.UnuseConnector(startConnector);
            RemoveRoom(roomData);
            return false;
        }

        if (HandleIntersection2D(roomData))
        {
            roomData.UnuseConnector(roomConnector);
            startRoom.UnuseConnector(startConnector);
            RemoveRoom(roomData);
            return false;
        }

        return true;
    }

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

    #region PRIVATE INSTANTIATION & UTILITIES

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

    private bool HandleIntersection2D(RoomData roomData)
    {
        if (roomData.roomBoundsCollider == null) return false;

        Physics2D.SyncTransforms();

        int hitCount = 0;

        if (roomData.roomBoundsCollider is BoxCollider2D boxCol)
        {
            Vector2 localScaledSize = new Vector2(
                boxCol.size.x * roomData.transform.lossyScale.x,
                boxCol.size.y * roomData.transform.lossyScale.y
            );

            hitCount = Physics2D.OverlapBox(
                boxCol.bounds.center,
                localScaledSize * 0.99f,
                roomData.transform.eulerAngles.z,
                roomCollisionFilter,
                collisionCheckResults
            );
        }
        else
        {
            hitCount = roomData.roomBoundsCollider.Overlap(roomCollisionFilter, collisionCheckResults);
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = collisionCheckResults[i];

            if (hit.gameObject == roomData.gameObject) continue;
            if (hit.transform.IsChildOf(roomData.transform)) continue;

            return true;
        }

        return false;
    }

    private bool AlignRooms(Transform room1, Transform room2, Transform room1Connector, Transform room2Connector, bool allowRotation)
    {
        if (allowRotation)
        {
            Vector3 desiredDirection = -room1Connector.right;
            float angleDifference = Vector2.SignedAngle(room2Connector.right, desiredDirection);

            room2.Rotate(0, 0, angleDifference, Space.World);
        }
        else
        {
            float dot = Vector2.Dot(room1Connector.right, room2Connector.right);
            if (!Mathf.Approximately(dot, -1f))
            {
                return false;
            }
        }

        Vector3 offset = room1Connector.position - room2Connector.position;
        room2.position += offset;

        Physics2D.SyncTransforms();
        return true;
    }

    #endregion

    #region PREFAB SELECTION UTILITIES (DECK SYSTEM IMPLEMENTED)

    /// <summary>
    /// รีเซ็ต Deck ให้เต็ม โดยก๊อปปี้จาก Master List (roomsPrefab)
    /// </summary>
    private void RefillRoomDeck()
    {
        _availableRoomDeck.Clear();
        if (roomsPrefab != null)
        {
            _availableRoomDeck.AddRange(roomsPrefab);
        }
    }

    public GameObject SelectNextRoomPrefab()
    {
        if (roomsPrefab == null || roomsPrefab.Count == 0) return null;

        // 1. โอกาสเกิดห้องพิเศษ (แยกต่างหาก ไม่เกี่ยวกับ Deck)
        if (specialRoomsPrefab.Count > 0 && UnityEngine.Random.Range(0f, 1f) > 0.9f)
        {
            return specialRoomsPrefab[UnityEngine.Random.Range(0, specialRoomsPrefab.Count)];
        }

        // 2. ตรวจสอบว่า Deck หมดหรือยัง ถ้าหมดให้เติมใหม่
        if (_availableRoomDeck.Count == 0)
        {
            RefillRoomDeck();
        }

        // 3. สุ่มเลือกจาก Deck ที่มีอยู่
        int randomIndex = UnityEngine.Random.Range(0, _availableRoomDeck.Count);
        GameObject selectedPrefab = _availableRoomDeck[randomIndex];

        // 4. ลบออกจาก Deck เพื่อไม่ให้ซ้ำในรอบนี้
        _availableRoomDeck.RemoveAt(randomIndex);

        return selectedPrefab;
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