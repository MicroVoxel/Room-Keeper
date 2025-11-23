using UnityEngine;
using TMPro;
using System.Collections.Generic;

// 1. "สมอง" ของมินิเกมแยกผ้า (สืบทอดจาก TaskBase)
// เวอร์ชั่น: เพิ่มเสียง SFX และใช้ AudioManager เพื่อ Performance สูงสุด
public class Task_SortLaundry : TaskBase
{
    [Header("Laundry Setup")]
    [Tooltip("ชิ้นผ้าทั้งหมดที่จะใช้ในมินิเกมนี้ (ต้องมี component 'TrashDrag' และ 'LaundryItemType')")]
    public List<TrashDrag> clothItems; // ใช้ TrashDrag ที่มีอยู่แล้ว

    [Tooltip("จำนวนผ้าที่ต้องคัดแยกให้ถูกต้องเพื่อจบภารกิจ")]
    public int itemsToCollect = 6;

    [Header("Audio")]
    [Tooltip("เสียงเมื่อแยกผ้าลงตะกร้าถูกต้อง")]
    public AudioClip sortSound;
    [Tooltip("ความดังของเสียง (0-1)")]
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("UI")]
    public TMP_Text counterText;

    [Header("Progress")]
    private int collected = 0;
    private Dictionary<TrashDrag, Vector2> initialPositions; // ใช้ Dictionary เพื่อความแม่นยำ

    // Awake() จะทำงาน *ทันที* ที่ TasksZone สั่ง Instantiate
    private void Awake()
    {
        // 1. สร้าง Dictionary ทันที (ป้องกัน NullReference)
        initialPositions = new Dictionary<TrashDrag, Vector2>();

        // 2. ตรวจสอบว่าลาก clothItems มาใส่ใน Inspector แล้วหรือยัง
        if (clothItems == null)
        {
            Debug.LogError($"[Task_SortLaundry] 'Cloth Items' List is not assigned in the Inspector!", this);
            clothItems = new List<TrashDrag>(); // สร้าง List เปล่าไว้ กัน NRE
        }

        // 3. เก็บตำแหน่งเริ่มต้นของผ้าทั้งหมด
        foreach (var item in clothItems)
        {
            if (item != null && item.TryGetComponent<RectTransform>(out var rt))
            {
                initialPositions[item] = rt.anchoredPosition;
            }
        }

        // 4. ตั้งค่าจำนวนที่ต้องเก็บให้เท่ากับจำนวนผ้าที่มีเลย
        itemsToCollect = clothItems.Count;
    }

    override protected void Start()
    {
        base.Start();
        // Logic ทั้งหมดถูกย้ายไป Awake() แล้วเพื่อความชัวร์เรื่อง Execution Order
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
        collected = 0;

        if (clothItems == null) return;

        foreach (var item in clothItems)
        {
            if (item == null) continue;

            // 1. เปิดผ้าที่อาจถูกซ่อนไว้
            item.gameObject.SetActive(true);

            // 2. คืนตำแหน่งเดิม
            if (initialPositions.TryGetValue(item, out Vector2 startPos) && item.TryGetComponent<RectTransform>(out var rt))
            {
                rt.anchoredPosition = startPos;
            }

            // 3. บอกให้ TrashDrag เด้งกลับ (ถ้ายังไม่ลงตะกร้า)
            item.EnableReturnToStart(true);
            item.ResetRaycastBlock();
        }
        UpdateCounter();
    }

    /// <summary>
    /// เมธอดนี้จะถูกเรียกโดย 'LaundrySortingBasket' เมื่อมีการวางผ้าลงตะกร้า "ถูก"
    /// </summary>
    public void OnItemSorted(GameObject sortedCloth)
    {
        if (IsCompleted) return;

        // --- [NEW] เล่นเสียง SFX ---
        PlaySortSound();

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

    private void PlaySortSound()
    {
        // ใช้ AudioManager เพื่อ Performance และควบคุมผ่าน SFX Slider ได้
        if (AudioManager.Instance != null && sortSound != null)
        {
            // Pitch Variance 0.1f ช่วยให้เสียงดู Dynamic เวลาแยกผ้ารัวๆ
            AudioManager.Instance.PlaySFX(sortSound, sfxVolume, 0.1f);
        }
    }

    void UpdateCounter()
    {
        if (counterText)
            counterText.text = $"{collected}/{itemsToCollect}";
    }
}