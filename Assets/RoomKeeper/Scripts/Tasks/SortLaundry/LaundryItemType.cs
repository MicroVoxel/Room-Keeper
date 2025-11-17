using UnityEngine;

// 2. นี่คือ "ป้าย" บอกประเภทผ้า
//    ให้แปะสคริปต์นี้ไว้ที่ GameObject ของ "ชิ้นผ้า" (ที่เดียวกับ 'TrashDrag')
public class LaundryItemType : MonoBehaviour
{
    // สร้าง enum เพื่อให้เลือกใน Inspector ได้ง่าย และป้องกันการพิมพ์ผิด
    public enum LaundryType
    {
        White, // ผ้าขาว
        Color  // ผ้าสี
    }

    [Tooltip("ผ้าชิ้นนี้เป็นประเภทไหน? (ผ้าขาว หรือ ผ้าสี)")]
    public LaundryType type;
}