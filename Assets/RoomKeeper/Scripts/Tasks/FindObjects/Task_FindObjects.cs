using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 1. "สมอง" ของมินิเกมหาของ (สืบทอดจาก TaskBase)
/// แปะสคริปต์นี้ไว้ที่ GameObject หลักของ Panel ภารกิจ
/// </summary>
public class Task_FindObjects : TaskBase
{
    [Header("Find Objects Setup")]
    [Tooltip("ไอเทมทั้งหมดที่ต้องหา (จะค้นหาอัตโนมัติถ้าเว้นว่างไว้)")]
    public List<FindableObject> itemsToFind;

    [Header("UI")]
    [Tooltip("UI ที่แสดงจำนวนที่หาเจอ / ทั้งหมด")]
    public TMP_Text counterText;

    [Tooltip("(Optional) เป้าหมายที่ไอเทมจะลอยไปหา (เช่น RectTransform ของ Counter)")]
    public RectTransform collectTarget; // <-- ++ เพิ่มเข้ามา ++

    [Header("Progress")]
    private int foundCount = 0;
    private int totalItems = 0;

    /// <summary>
    /// ใช้ Awake() เพื่อค้นหาและลงทะเบียนไอเทมทั้งหมด
    /// </summary>
    private void Awake()
    {
        // 1. ถ้าไม่ได้ลากไอเทมมาใส่ใน Inspector ให้ค้นหาในลูกๆ
        if (itemsToFind == null || itemsToFind.Count == 0)
        {
            itemsToFind = new List<FindableObject>();
            // true ใน GetComponentsInChildren หมายถึง "ค้นหาในลูกที่ถูกปิด (inactive) ด้วย"
            GetComponentsInChildren<FindableObject>(true, itemsToFind);
        }

        totalItems = itemsToFind.Count;

        // 2. "ฉีด" (Inject) ตัวสมอง (this) นี้เข้าไปในไอเทมแต่ละชิ้น
        // เพื่อให้ไอเทมรู้ว่าต้องรายงานกลับไปหาใคร
        foreach (var item in itemsToFind)
        {
            if (item != null)
            {
                // ++ อัปเดตเมธอด Initialize ++
                item.Initialize(this, collectTarget);
            }
        }
    }

    override protected void Start()
    {
        base.Start();
        // Logic ถูกย้ายไป Awake() และ Open()
    }

    public override void Open()
    {
        base.Open();
        if (IsCompleted) return;

        ResetTask();
    }

    // --- Game Logic ---

    void ResetTask()
    {
        foundCount = 0;
        UpdateCounter();

        // สั่งให้ไอเทมทุกชิ้น "รีเซ็ต" (กลับไปซ่อนเหมือนเดิม)
        foreach (var item in itemsToFind)
        {
            if (item != null)
            {
                item.ResetItem();
            }
        }
    }

    /// <summary>
    /// Event Handler: ถูกเรียกโดย 'FindableObject' เมื่อมันถูกคลิก
    /// </summary>
    public void OnItemFound(FindableObject foundItem)
    {
        if (IsCompleted) return;

        foundCount++;
        UpdateCounter();

        // (Optional) เล่นเสียง "ติ๊ง!"

        // ตรวจสอบว่าครบจำนวนหรือยัง
        if (foundCount >= totalItems)
        {
            CompleteTask();
        }
    }

    void UpdateCounter()
    {
        if (counterText)
            counterText.text = $"{foundCount}/{totalItems}";
    }
}