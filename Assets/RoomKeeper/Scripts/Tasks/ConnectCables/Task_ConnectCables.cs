using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 1. "สมอง" ของมินิเกมต่อสายไฟ (สืบทอดจาก TaskBase)
/// แปะสคริปต์นี้ไว้ที่ GameObject หลักของ Panel ภารกิจ
/// </summary>
public class Task_ConnectCables : TaskBase
{
    [Header("Cable Setup")]
    [Tooltip("หัวปลั๊กทั้งหมดที่จะใช้ในมินิเกมนี้ (ต้องมี 'TrashDrag' และ 'CablePlugItem')")]
    public List<TrashDrag> plugItems; // ใช้ TrashDrag ที่เรามีอยู่แล้ว

    [Tooltip("จำนวนสายไฟที่ต้องเสียบให้ถูกต้องเพื่อจบภารกิจ")]
    public int itemsToConnect = 4;

    [Header("UI")]
    public TMP_Text counterText; // (Optional) ตัวนับ "0/4"

    [Header("Progress")]
    private int connectedCount = 0;
    private Dictionary<TrashDrag, Vector2> initialPositions;

    /// <summary>
    /// --- ⭐ FIXED ---
    /// ย้าย Logic มาไว้ที่ Awake() เพื่อให้ทำงานทันทีที่ TasksZone สั่ง Instantiate
    /// </summary>
    private void Awake()
    {
        initialPositions = new Dictionary<TrashDrag, Vector2>();

        if (plugItems == null)
        {
            Debug.LogError($"[Task_ConnectCables] 'Plug Items' List is not assigned!", this);
            plugItems = new List<TrashDrag>();
        }

        // เก็บตำแหน่งเริ่มต้นของหัวปลั๊กทั้งหมด
        foreach (var item in plugItems)
        {
            if (item != null && item.TryGetComponent<RectTransform>(out var rt))
            {
                initialPositions[item] = rt.anchoredPosition;
            }
        }

        // ตั้งค่าจำนวนที่ต้องเสียบ ให้เท่ากับจำนวนปลั๊กที่มี
        itemsToConnect = plugItems.Count;
    }

    override protected void Start()
    {
        base.Start();
        // Logic ทั้งหมดถูกย้ายไป Awake() แล้ว
    }

    public override void Open()
    {
        base.Open();
        if (!IsCompleted)
        {
            ResetTask();
        }
    }

    void ResetTask()
    {
        connectedCount = 0;
        if (plugItems == null) return;

        foreach (var item in plugItems)
        {
            if (item == null) continue;

            // 1. เปิดหัวปลั๊กที่อาจถูกซ่อนไว้
            item.gameObject.SetActive(true);

            // 2. คืนตำแหน่งเดิม
            if (initialPositions.TryGetValue(item, out Vector2 startPos) && item.TryGetComponent<RectTransform>(out var rt))
            {
                rt.anchoredPosition = startPos;
            }

            // 3. บอกให้ TrashDrag เด้งกลับ (ถ้ายังไม่เสียบ)
            item.EnableReturnToStart(true);
            item.ResetRaycastBlock();

            // 4. (สำคัญ) เปิดการลากเผื่อไว้ (ถ้าเราปิดไปตอนเสียบถูก)
            var dragComp = item.GetComponent<TrashDrag>();
            if (dragComp) dragComp.enabled = true;
        }
        UpdateCounter();
    }

    /// <summary>
    /// เมธอดนี้จะถูกเรียกโดย 'CableSocketDropZone' เมื่อมีการเสียบปลั๊ก "ถูก"
    /// </summary>
    public void OnPlugConnected(GameObject plugGO)
    {
        if (IsCompleted) return;

        connectedCount++;
        UpdateCounter();

        // (Optional) เราอาจจะ "ปิด" การลากไปเลย เมื่อเสียบถูกแล้ว
        var dragComp = plugGO.GetComponent<TrashDrag>();
        if (dragComp)
        {
            dragComp.enabled = false; // ปิดการลาก
        }

        if (connectedCount >= itemsToConnect)
        {
            // ทำภารกิจสำเร็จ!
            CompleteTask();
        }
    }

    void UpdateCounter()
    {
        if (counterText)
            counterText.text = $"{connectedCount}/{itemsToConnect}";
    }
}