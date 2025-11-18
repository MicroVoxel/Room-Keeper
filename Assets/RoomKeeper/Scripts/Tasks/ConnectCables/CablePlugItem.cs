using UnityEngine;

/// <summary>
/// 2. นี่คือ "ป้าย" บอกประเภทของหัวปลั๊ก
/// แปะสคริปต์นี้ไว้ที่ GameObject ของ "หัวปลั๊ก" (ที่เดียวกับ 'TrashDrag')
/// </summary>
public class CablePlugItem : MonoBehaviour
{
    // สร้าง enum เพื่อให้เลือกใน Inspector ได้ง่าย
    public enum PlugType
    {
        Red,
        White,
        Yellow,
        Black
    }

    [Tooltip("หัวปลั๊กนี้เป็นประเภทไหน?")]
    public PlugType type;
}