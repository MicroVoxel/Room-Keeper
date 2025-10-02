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

    public override void Open()
    {
        base.Open();
        ResetTask();
    }

    void ResetTask()
    {
        collected = 0;
        // เปิดผ้าทั้งหมด และกระจายตำแหน่งแบบสุ่มนิด ๆ
        foreach (var c in clothItems)
        {
            if (!c) continue;
            c.gameObject.SetActive(true);
            var rt = c.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition += new Vector2(Random.Range(-20f, 20f), Random.Range(-20f, 20f));
            }
            c.EnableReturnToStart(true); // ให้เด้งกลับถ้ายังไม่ลงตะกร้า
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
