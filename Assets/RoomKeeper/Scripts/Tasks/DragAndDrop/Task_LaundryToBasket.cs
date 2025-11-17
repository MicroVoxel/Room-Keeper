using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Task_LaundryToBasket : TaskBase
{
    [Header("Setup")]
    [Tooltip("ชิ้นผ้าที่จะลากลงตะกร้า (กำหนดใน Inspector)")]
    public TrashDrag[] clothItems;   // ใช้สคริปต์ลากเดียวกับตัวอย่าง หรือดูเวอร์ชันข้างล่าง
    public TMP_Text counterText;         // แสดงจำนวนที่เก็บแล้ว / ทั้งหมด

    [Header("Progress")]
    public int itemsToCollect = 5;
    private int collected = 0;
    private Vector2[] initialPositions;

    override protected void Start()
    {
        base.Start();
        // เก็บตำแหน่งเริ่มต้นของผ้าทั้งหมด
        if (clothItems.Length > 0)
        {
            initialPositions = new Vector2[clothItems.Length];
            for (int i = 0; i < clothItems.Length; i++)
            {
                if (clothItems[i] != null && clothItems[i].TryGetComponent<RectTransform>(out var rt))
                {
                    initialPositions[i] = rt.anchoredPosition;
                }
            }
        }
    }

    public override void Open()
    {
        base.Open();
        ResetTask();
    }
    public override void Close()
    {
        base.Close();

    }

    void ResetTask()
    {
        collected = 0;

        for (int i = 0; i < clothItems.Length; i++)
        {
            var c = clothItems[i];
            if (!c) continue;

            c.gameObject.SetActive(true);
            var rt = c.GetComponent<RectTransform>();

            if (rt != null && initialPositions != null && i < initialPositions.Length)
            {
                // ใช้ตำแหน่งเริ่มต้นที่เก็บไว้ (เอา Random ออกเพื่อให้ตำแหน่งคงที่)
                rt.anchoredPosition = initialPositions[i];
            }

            c.EnableReturnToStart(true); // ให้เด้งกลับถ้ายังไม่ลงตะกร้า

            c.ResetRaycastBlock();
        }
        UpdateCounter();
    }

    public void OnClothDroppedIntoBasket(GameObject clothGO)
    {
        // ถูกเรียกจากตะกร้าเมื่อ drop สำเร็จ
        collected++;
        UpdateCounter();

        // ซ่อน/ทำลายชิ้นผ้าที่เก็บแล้ว
        clothGO.SetActive(false);

        if (collected >= itemsToCollect)
        {
            CompleteTask();
        }
    }

    void UpdateCounter()
    {
        if (counterText)
            counterText.text = $"{collected}/{itemsToCollect}";
    }
}
