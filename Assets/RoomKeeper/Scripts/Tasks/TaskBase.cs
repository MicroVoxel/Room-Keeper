using UnityEngine;

public abstract class TaskBase : MonoBehaviour
{
    public GameObject panel;
    public bool IsOpen => panel && panel.activeSelf;
    public bool IsCompleted { get; private set; }

    public virtual void Open()
    {
        if (IsCompleted) return;          // ถ้าจบแล้ว ไม่ต้องเปิดอีก
        if (panel) panel.SetActive(true);
    }

    public virtual void Close()
    {
        if (panel) panel.SetActive(false);
    }

    protected void CompleteTask()
    {
        if (IsCompleted) return;
        IsCompleted = true;
        //LevelManager.I.AddProgress(1);
        Close();

        // ซ่อนสัญลักษณ์ภารกิจถ้ามี
        var marker = GetComponentInChildren<SpriteRenderer>();
        if (marker) marker.enabled = false;
        // หรือถ้าเป็น UI: ปิดปุ่มไอคอนภารกิจ
    }
}
