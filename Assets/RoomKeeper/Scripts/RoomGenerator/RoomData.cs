using System.Collections.Generic;
using UnityEngine;

public class RoomData : MonoBehaviour
{
    public enum RoomType
    {
        Spawn,
        Room,
        Hallway
    }

    [Header("Room Settings")]
    [SerializeField] LayerMask roomLayerMask;
    [SerializeField] public RoomType roomType;
    [SerializeField] GameObject wall;

    [Header("Connection Points")]
    public List<Transform> connectors;

    // ⭐ แนะนำให้ใช้ชื่อที่ชัดเจนขึ้นและทำให้เป็น public/private ตามความจำเป็น
    [Tooltip("Collider2D หลักของห้อง ใช้สำหรับตรวจสอบการซ้อนทับ")]
    public Collider2D collider2D; // เปลี่ยนชื่อเป็น collider2D เพื่อความชัดเจน

    private const int MAX_RETRIES = 100;

    /// <summary>
    /// สุ่มหา Connector ที่ยังไม่ถูกใช้งาน และตั้งค่าให้ถูกใช้งานทันที
    /// </summary>
    /// <param name="connector">Connector Transform ที่ว่าง</param>
    /// <returns>True หากพบ Connector ที่ว่าง</returns>
    public bool HasAvailableConnector(out Transform connector)
    {
        connector = null;

        // ⭐ ตรรกะสำหรับห้องที่มี Connector เดียว
        if (connectors.Count == 1)
        {
            Transform connect = connectors[0];
            if (connect.TryGetComponent<Connector>(out Connector connComponent))
            {
                if (!connComponent.IsOccupied())
                {
                    connector = connect;
                    connComponent.SetOccupied(true);
                    return true;
                }
            }
            return false;
        }

        // ⭐ ตรรกะสำหรับห้องที่มี Connector หลายอัน (ใช้การสุ่มพร้อม Retry)
        for (int i = 0; i < MAX_RETRIES; i++)
        {
            int randomConnectIndex = Random.Range(0, connectors.Count);
            Transform connect = connectors[randomConnectIndex];

            if (connect.TryGetComponent<Connector>(out Connector connComponent))
            {
                if (!connComponent.IsOccupied())
                {
                    connector = connect;
                    connComponent.SetOccupied(true); // ตั้งค่าให้ถูกใช้งานทันที
                    return true;
                }
            }
            // ถ้าไม่เจอ Connector หรือ Connector นั้นถูกใช้งานแล้ว ให้ลองใหม่
        }

        // หากพยายามครบ 100 ครั้งแล้วยังหาไม่เจอ
        return false;
    }

    /// <summary>
    /// ตั้งค่า Connector ให้กลับไปเป็นสถานะว่าง
    /// </summary>
    /// <param name="connector">Connector Transform ที่ต้องการยกเลิกการใช้งาน</param>
    public void UnuseConnector(Transform connector)
    {
        if (connector.TryGetComponent<Connector>(out Connector connect))
        {
            connect.SetOccupied(false);
        }
    }

    /// <summary>
    /// วางกำแพง (Wall) ปิด Connector ที่ยังไม่ได้ถูกใช้งานทั้งหมด
    /// </summary>
    public void FillEmptyDoors()
    {
        // ⭐ เพิ่มการตรวจสอบ: หากไม่มี Prefab กำแพง (wall) ให้หยุดการทำงาน
        if (wall == null)
        {
            // สามารถเพิ่ม Debug.LogWarning ได้หากต้องการแจ้งเตือนใน Editor
            // Debug.LogWarning($"RoomData on {gameObject.name} is missing the Wall prefab to fill empty doors.", gameObject);
            return;
        }

        foreach (Transform connect in connectors)
        {
            if (connect.TryGetComponent(out Connector _connector))
            {
                if (!_connector.IsOccupied())
                {
                    // ทำการ Instantiate เฉพาะเมื่อ 'wall' ไม่ใช่ null
                    GameObject _wall = Instantiate(wall, connect.position, connect.rotation, gameObject.transform);
                }
            }
        }
    }

    // ⭐ ใช้ OnValidate เพื่อช่วยแจ้งเตือนใน Editor หาก Collider2D หายไป
    private void OnValidate()
    {
        // ตรวจสอบว่ามีการอ้างอิงถึง Collider2D หรือไม่
        if (collider2D == null)
        {
            // พยายามหา Collider2D บน Game Object นี้อัตโนมัติ
            collider2D = GetComponent<Collider2D>();
        }
    }


    //private void OnDrawGizmos()
    //{
    //    if (collider2D != null)
    //    {
    //        Gizmos.color = Color.red;
    //        // การใช้ DrawWireCube กับ bounds.center และ bounds.size ใช้งานได้ดีสำหรับ 2D
    //        // แม้ว่าการหมุนอาจดูผิดพลาดถ้าห้องถูกหมุน (ควรใช้ Gizmos.matrix)
    //        Gizmos.DrawWireCube(collider2D.bounds.center, collider2D.bounds.size);
    //    }
    //}
}