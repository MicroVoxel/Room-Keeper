using UnityEngine;

public class TextRotationCompensator : MonoBehaviour
{
    // ค่า Local Rotation ที่ชดเชย 180 องศา
    private readonly Quaternion compensatedRotation = Quaternion.Euler(0f, 0f, 180f);

    // ค่า Local Rotation ปกติ
    private readonly Quaternion defaultRotation = Quaternion.identity;

    void LateUpdate()
    {
        // 1. ดึง Vector 'ขวา' (Right) ของ Root GameObject (Room)
        // Vector Right แสดงทิศทางแนวนอนของ Room
        Vector3 roomRight = transform.root.right;

        // 2. ตรวจสอบว่า Room อยู่ในสถานะ 'กลับหัว' หรือไม่
        // ถ้า Room ถูกหมุน 180 องศา (หรือประมาณ -180) Vector Right จะชี้ลง/ซ้าย (ค่า y จะเป็นลบมาก)
        // ถ้า Room หมุน 0 องศา หรือ 90/270 องศา ค่า y จะเป็น 0 หรือบวก

        // ตรวจสอบว่า Room Root ชี้ไปในทิศทาง "กลับหัว" (เทียบกับแกน y เดิมของโลก)
        // ถ้า Room ถูกหมุน 180 องศา, Vector up (แกน y) ของมันจะชี้ลง (y < 0)

        // เราจะดูที่แกน Y ของ Room Root เมื่อเทียบกับแกน Y ของโลก
        if (transform.parent.up.y < 0)
        {
            // Room ถูกหมุน 180 องศา (Room Up ชี้ลง)
            // บังคับให้ Text หมุนกลับ 180 องศา (Local Compensation)
            transform.localRotation = compensatedRotation;
        }
        else
        {
            // Room อยู่ในแนวตั้งปกติ (Room Up ชี้ขึ้น หรือหมุน 90/270 องศา)
            // บังคับให้ Local Rotation เป็นค่าเริ่มต้น (0 องศา)
            transform.localRotation = defaultRotation;
        }
    }
}
