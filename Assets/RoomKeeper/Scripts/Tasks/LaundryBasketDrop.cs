using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class LaundryBasketDrop : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Task_LaundryToBasket task;   // <- serialize ไว้ด้วย
    public Image highlightFrame;                  // optional

    void Awake()
    {
        // auto-find ถ้าไม่ได้ลากใน Inspector
        if (task == null) task = GetComponentInParent<Task_LaundryToBasket>();
        // เปิด raycast เป้าหมายให้แน่ใจ
        var img = GetComponent<Image>();
        if (img) img.raycastTarget = true;
    }

    public void OnDrop(PointerEventData e)
    {
        var dragged = e.pointerDrag;
        if (!dragged) return;

        var dragComp = dragged.GetComponent<TrashDrag>();
        if (dragComp == null) return;

        if (task == null)
        {
            Debug.LogError("[LaundryBasketDrop] 'task' is null. Make sure Basket is under the Task panel or assign it in Inspector.");
            return;
        }

        // กันเด้งกลับหลังวาง
        dragComp.EnableReturnToStart(false);

        // นับชิ้น + ซ่อนผ้า
        task.OnClothDroppedIntoBasket(dragged);
    }

    public void OnPointerEnter(PointerEventData _)
    {
        if (highlightFrame) highlightFrame.enabled = true;
        transform.localScale = Vector3.one * 1.05f;
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (highlightFrame) highlightFrame.enabled = false;
        transform.localScale = Vector3.one;
    }
}
