using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 3. นี่คือ "ตะกร้า" ที่ฉลาดขึ้น
//    ใช้สคริปต์นี้แทน 'LaundryBasketDrop'
//    แปะไว้ที่ UI Image ของตะกร้า (ต้องเปิด Raycast Target)
[RequireComponent(typeof(Image))]
public class LaundrySortingBasket : MonoBehaviour, IDropHandler
{
    [Tooltip("ตะกร้าใบนี้รับผ้าประเภทไหน? (ตั้งค่าใน Inspector)")]
    public LaundryItemType.LaundryType acceptedType; // อ้างอิง enum จากไฟล์ LaundryItemType

    [Tooltip("ลาก 'Task_SortLaundry' (ตัวจัดการหลัก) มาใส่ที่นี่")]
    [SerializeField] private Task_SortLaundry taskManager;

    // (Optional) โค้ดสำหรับ Highlight เหมือนใน LaundryBasketDrop เดิม
    public Image highlightFrame;
    private Vector3 originalScale;

    void Awake()
    {
        // Auto-find ถ้าไม่ได้ลาก
        if (taskManager == null)
            taskManager = GetComponentInParent<Task_SortLaundry>();

        originalScale = transform.localScale;
    }

    public void OnDrop(PointerEventData e)
    {
        if (taskManager == null || taskManager.IsCompleted) return;

        var draggedObject = e.pointerDrag;
        if (draggedObject == null) return;

        // 1. ตรวจสอบว่ามีสคริปต์ลาก (TrashDrag) หรือไม่
        var dragComp = draggedObject.GetComponent<TrashDrag>();
        if (dragComp == null) return;

        // 2. ตรวจสอบว่ามี "ป้าย" (LaundryItemType) หรือไม่
        var itemTypeComp = draggedObject.GetComponent<LaundryItemType>();
        if (itemTypeComp == null) return; // ไม่ใช่ผ้า, ปล่อยให้เด้งกลับ

        // 3. --- นี่คือตรรกะสำคัญ ---
        // ตรวจสอบว่า "ประเภทของผ้า" ตรงกับ "ประเภทที่ตะต้ารับ" หรือไม่
        if (itemTypeComp.type == this.acceptedType)
        {
            // 3.1. วางถูก! (เช่น ผ้าสี ลง ตะกร้าสี)

            // สั่ง 'TrashDrag' ว่าไม่ต้องเด้งกลับ
            dragComp.EnableReturnToStart(false);

            // แจ้ง "สมอง" (taskManager) ให้นับคะแนน
            taskManager.OnItemSorted(draggedObject);
        }
        else
        {
            // 3.2. วางผิด! (เช่น ผ้าขาว ลง ตะกร้าสี)
            // เราไม่ต้องทำอะไรเลย 'TrashDrag' (ที่ returnToStart ยังเป็น true)
            // จะจัดการเด้งผ้ากลับไปที่เดิมเอง
        }

        // (Optional) ปิด Highlight เมื่อวาง
        OnPointerExit(e);
    }

    // (Optional) เอฟเฟกต์ Highlight
    public void OnPointerEnter(PointerEventData _)
    {
        if (highlightFrame) highlightFrame.enabled = true;
        transform.localScale = originalScale * 1.05f;
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (highlightFrame) highlightFrame.enabled = false;
        transform.localScale = originalScale;
    }
}