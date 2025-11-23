using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 1. "สมอง" ภารกิจเก็บผ้าลงตะกร้า (Task_LaundryToBasket)
/// เวอร์ชั่น: เพิ่มเสียง SFX และปรับปรุง Performance (Initialization ใน Awake)
/// </summary>
public class Task_LaundryToBasket : TaskBase
{
    [Header("Setup")]
    [Tooltip("ชิ้นผ้าที่จะลากลงตะกร้า (กำหนดใน Inspector)")]
    public TrashDrag[] clothItems;   // ใช้ TrashDrag เหมือนเดิม
    public TMP_Text counterText;     // แสดงจำนวนที่เก็บแล้ว / ทั้งหมด

    [Header("Audio")]
    [Tooltip("เสียงเมื่อผ้าลงตะกร้า")]
    public AudioClip dropSound;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Progress")]
    public int itemsToCollect = 5;
    private int collected = 0;
    private Vector2[] initialPositions;

    private void Awake()
    {
        // [OPTIMIZATION] ย้ายการจำตำแหน่งมาไว้ที่ Awake
        // เพื่อให้มั่นใจว่าเก็บค่าเริ่มต้นจริงๆ ก่อนที่ object จะถูก deactivate หรือย้าย
        if (clothItems != null && clothItems.Length > 0)
        {
            initialPositions = new Vector2[clothItems.Length];
            for (int i = 0; i < clothItems.Length; i++)
            {
                if (clothItems[i] != null && clothItems[i].TryGetComponent<RectTransform>(out var rt))
                {
                    initialPositions[i] = rt.anchoredPosition;
                }
            }
            // ปรับจำนวนเป้าหมายให้ตรงกับจำนวนของที่มีจริง (กันลืมตั้งค่า)
            itemsToCollect = clothItems.Length;
        }
    }

    override protected void Start()
    {
        base.Start();
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

        if (clothItems == null) return;

        for (int i = 0; i < clothItems.Length; i++)
        {
            var c = clothItems[i];
            if (!c) continue;

            c.gameObject.SetActive(true);
            var rt = c.GetComponent<RectTransform>();

            if (rt != null && initialPositions != null && i < initialPositions.Length)
            {
                // ใช้ตำแหน่งเริ่มต้นที่เก็บไว้ใน Awake
                rt.anchoredPosition = initialPositions[i];
            }

            c.EnableReturnToStart(true); // ให้เด้งกลับถ้ายังไม่ลงตะกร้า
            c.ResetRaycastBlock();
        }
        UpdateCounter();
    }

    public void OnClothDroppedIntoBasket(GameObject clothGO)
    {
        // [AUDIO] เล่นเสียงเมื่อเก็บผ้าสำเร็จ
        PlayDropSound();

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

    private void PlayDropSound()
    {
        // ใช้ AudioManager เพื่อ Performance ที่ดีที่สุด (Pooling)
        if (AudioManager.Instance != null && dropSound != null)
        {
            // Pitch Variance 0.1f ช่วยให้เสียงดูไม่ซ้ำซากเวลาโยนรัวๆ
            AudioManager.Instance.PlaySFX(dropSound, sfxVolume, 0.1f);
        }
    }

    void UpdateCounter()
    {
        if (counterText)
            counterText.text = $"{collected}/{itemsToCollect}";
    }
}