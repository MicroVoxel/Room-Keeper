using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 3. นี่คือ "เต้ารับ" (Drop Zone)
/// แปะไว้ที่ UI Image ของเต้ารับ (ต้องเปิด Raycast Target)
/// </summary>
[RequireComponent(typeof(Image))]
public class CableSocketDropZone : MonoBehaviour, IDropHandler
{
    [Tooltip("เต้ารับนี้... รับปลั๊กประเภทไหน?")]
    public CablePlugItem.PlugType acceptedType; // อ้างอิง enum จาก CablePlugItem

    [Tooltip("ลาก 'Task_ConnectCables' (ตัวจัดการหลัก) มาใส่ที่นี่")]
    [SerializeField] private Task_ConnectCables taskManager;

    [Tooltip("จุดกึ่งกลางของเต้ารับ (เพื่อให้ปลั๊ก snap สวยงาม)")]
    public RectTransform snapPoint;

    // (Optional) โค้ดสำหรับ Highlight
    public Image highlightFrame;
    private Vector3 originalScale;

    void Awake()
    {
        // Auto-find ถ้าไม่ได้ลาก
        if (taskManager == null)
            taskManager = GetComponentInParent<Task_ConnectCables>();

        if (snapPoint == null)
            snapPoint = GetComponent<RectTransform>(); // ถ้าไม่กำหนดจุด snap ก็ใช้ตัวเอง

        originalScale = transform.localScale;
    }

    public void OnDrop(PointerEventData e)
    {
        if (taskManager == null || taskManager.IsCompleted) return;

        var draggedObject = e.pointerDrag;
        if (draggedObject == null) return;

        // 1. ตรวจสอบว่ามีสคริปต์ลาก (TrashDrag)
        var dragComp = draggedObject.GetComponent<TrashDrag>();
        if (dragComp == null) return;

        // 2. ตรวจสอบว่ามี "ป้าย" (CablePlugItem)
        var itemTypeComp = draggedObject.GetComponent<CablePlugItem>();
        if (itemTypeComp == null) return; // ไม่ใช่ปลั๊ก, ปล่อยเด้งกลับ

        // 3. --- ตรรกะสำคัญ ---
        // ตรวจสอบว่า "ประเภทปลั๊ก" ตรงกับ "ประเภทเต้ารับ" หรือไม่
        if (itemTypeComp.type == this.acceptedType)
        {
            // 3.1. เสียบถูก!

            // สั่ง 'TrashDrag' ว่าไม่ต้องเด้งกลับ
            dragComp.EnableReturnToStart(false);

            // "Snap" หัวปลั๊กไปที่ตำแหน่งเต้ารับ
            // (เราใช้ position แทน anchoredPosition เพื่อให้แม่นยำระหว่าง RectTransform)
            draggedObject.transform.position = snapPoint.position;

            // แจ้ง "สมอง" (taskManager) ให้นับคะแนน
            taskManager.OnPlugConnected(draggedObject);
        }
        else
        {
            // 3.2. เสียบผิด!
            // 'TrashDrag' จะจัดการเด้งกลับไปที่เดิมเอง
        }

        // (Optional) ปิด Highlight เมื่อวาง
        if (highlightFrame) highlightFrame.enabled = false;
        transform.localScale = originalScale;
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