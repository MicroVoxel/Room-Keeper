using UnityEngine;
using UnityEngine.EventSystems;

public class TrashDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    RectTransform rt;
    RectTransform dragSpace;   // พาเรนต์ที่อยากลากภายใน (เช่น Panel ของมินิเกม)
    Canvas canvas;
    Vector2 startPos;
    Vector2 pointerOffset;
    bool returnToStart = true;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        // ลากใน local space ของพาเรนต์โดยตรง จะตรงมือกว่า
        dragSpace = rt.parent as RectTransform;
    }

    public void EnableReturnToStart(bool enable) => returnToStart = enable;

    public void OnBeginDrag(PointerEventData e)
    {
        startPos = rt.anchoredPosition;

        // หา local-point ณ จุดกด แล้วคำนวณ offset ระหว่างจุดกดกับตำแหน่งวัตถุ
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragSpace, e.position, e.pressEventCamera, out var localPoint);
        pointerOffset = rt.anchoredPosition - localPoint;

        // ให้ DropZone รับอีเวนต์: ระหว่างลากปิด raycast ของตัวเองไว้
        var cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragSpace, e.position, e.pressEventCamera, out var localPoint))
            return;

        rt.anchoredPosition = localPoint + pointerOffset;
    }

    public void OnEndDrag(PointerEventData e)
    {
        // ถ้าถูกปิดทิ้งใน OnDrop จะไม่เด้ง
        if (!gameObject.activeInHierarchy) return;

        var cg = GetComponent<CanvasGroup>();
        if (cg) cg.blocksRaycasts = true;

        if (returnToStart)
            rt.anchoredPosition = startPos;
    }
}
