using UnityEngine;
using TMPro;
using System.Collections.Generic;

// 1. นี่คือ "สมอง" ของมินิเกม สคริปต์นี้ต้องอยู่บน GameObject หลักของ Panel ภารกิจ
//    และต้องถูกอ้างอิงโดย TasksZone (เหมือนกับ Task_LaundryToBasket)
public class Task_SortLaundry : TaskBase
{
    [Header("Laundry Setup")]
    [Tooltip("ชิ้นผ้าทั้งหมดที่จะใช้ในมินิเกมนี้ (ต้องมี component 'TrashDrag' และ 'LaundryItemType')")]
    public List<TrashDrag> clothItems; // เราจะใช้ TrashDrag ที่คุณมีอยู่แล้ว

    [Tooltip("จำนวนผ้าที่ต้องคัดแยกให้ถูกต้องเพื่อจบภารกิจ")]
    public int itemsToCollect = 6;

    [Header("UI")]
    public TMP_Text counterText; // (Optional) ตัวนับ "0/6"

    [Header("Progress")]
    private int collected = 0;
    private Dictionary<TrashDrag, Vector2> initialPositions; // ใช้ Dictionary เพื่อความแม่นยำ

    // --- ⭐ FIXED ---
    // ย้าย Logic จาก Start() มาไว้ที่ Awake()
    // Awake() จะทำงาน *ทันที* ที่ TasksZone สั่ง Instantiate
    // แม้ว่า GameObject จะถูก SetActive(false) ในบรรทัดต่อมาก็ตาม
    private void Awake()
    {
        // 1. สร้าง Dictionary ทันที (ป้องกัน NullReference)
        initialPositions = new Dictionary<TrashDrag, Vector2>();

        // 2. (Added Check) ตรวจสอบว่าลาก clothItems มาใส่ใน Inspector แล้วหรือยัง
        if (clothItems == null)
        {
            Debug.LogError($"[Task_SortLaundry] 'Cloth Items' List is not assigned in the Inspector!", this);
            clothItems = new List<TrashDrag>(); // สร้าง List เปล่าไว้ กัน NRE บรรทัดล่าง
        }

        // 3. เก็บตำแหน่งเริ่มต้นของผ้าทั้งหมด
        foreach (var item in clothItems)
        {
            if (item != null && item.TryGetComponent<RectTransform>(out var rt))
            {
                initialPositions[item] = rt.anchoredPosition;
            }
        }

        // 4. เราตั้งค่าจำนวนที่ต้องเก็บให้เท่ากับจำนวนผ้าที่มีเลย
        itemsToCollect = clothItems.Count;
    }

    override protected void Start()
    {
        base.Start();
        // Logic ทั้งหมดถูกย้ายไป Awake() แล้ว
        // เพื่อป้องกัน NullReferenceException เมื่อ TasksZone
        // สั่ง SetActive(false) ก่อนที่ Start() จะทำงาน
    }

    public override void Open()
    {
        base.Open();
        if (!IsCompleted)
        {
            ResetTask(); // <-- ตอนนี้ initialPositions จะไม่ null แล้ว
        }
    }

    void ResetTask()
    {
        collected = 0;

        // (Added Check) ตรวจสอบเผื่อ clothItems เป็น null (ถ้าลืมลากใส่ Inspector)
        if (clothItems == null) return;

        foreach (var item in clothItems)
        {
            if (item == null) continue;

            // 1. เปิดผ้าที่อาจถูกซ่อนไว้
            item.gameObject.SetActive(true);

            // 2. คืนตำแหน่งเดิม
            // (ตอนนี้ initialPositions จะไม่ null แล้ว)
            if (initialPositions.TryGetValue(item, out Vector2 startPos) && item.TryGetComponent<RectTransform>(out var rt))
            {
                rt.anchoredPosition = startPos;
            }

            // 3. บอกให้ TrashDrag เด้งกลับ (ถ้ายังไม่ลงตะกร้า)
            item.EnableReturnToStart(true);
            item.ResetRaycastBlock(); // (จากโค้ด TrashDrag ของคุณ)
        }
        UpdateCounter();
    }

    /// <summary>
    /// เมธอดนี้จะถูกเรียกโดย 'LaundrySortingBasket' เมื่อมีการวางผ้าลงตะกร้า "ถูก"
    /// </summary>
    public void OnItemSorted(GameObject sortedCloth)
    {
        if (IsCompleted) return;

        collected++;
        UpdateCounter();

        // ซ่อนชิ้นผ้าที่เก็บแล้ว
        sortedCloth.SetActive(false);

        if (collected >= itemsToCollect)
        {
            // ทำภารกิจสำเร็จ!
            CompleteTask();
        }
    }

    void UpdateCounter()
    {
        if (counterText)
            counterText.text = $"{collected}/{itemsToCollect}";
    }
}