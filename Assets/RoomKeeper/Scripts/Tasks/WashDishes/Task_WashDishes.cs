using UnityEngine;
using UnityEngine.UI; // <-- เพิ่มเข้ามาสำหรับ Button
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 1. "สมอง" ของมินิเกมล้างจาน (สืบทอดจาก TaskBase)
/// (เวอร์ชันอัปเดต: เป็นตัวจัดการ Station ซ้าย-กลาง-ขวา)
/// </summary>
public class Task_WashDishes : TaskBase
{
    [Header("Dish Washing Setup")]
    [Tooltip("จานสกปรกทั้งหมดในภารกิจนี้ (จะค้นหาอัตโนมัติถ้าเว้นว่างไว้)")]
    public List<ScrubbableItem> dishesToWash;

    [Header("Station Layout")]
    [Tooltip("จุดวางจานสกปรก (ซ้าย)")]
    public RectTransform dirtyPileContainer;

    [Tooltip("จุดล้างจาน (กลาง)")]
    public RectTransform activeStation;

    [Tooltip("จุดวางจานสะอาด (ขวา)")]
    public RectTransform cleanPileContainer;


    [Header("UI")]
    public TMP_Text counterText; // (Optional) ตัวนับ "0/3"

    [Header("Progress")]
    private int dishesCleaned = 0;
    private int totalDishes = 0;
    private ScrubbableItem activeDish = null; // จานที่กำลังล้างอยู่

    /// <summary>
    /// ใช้ Awake() เพื่อค้นหาและลงทะเบียนจานทั้งหมด
    /// </summary>
    private void Awake()
    {
        // 1. ค้นหาจานทั้งหมด (เหมือนเดิม)
        if (dishesToWash == null || dishesToWash.Count == 0)
        {
            dishesToWash = new List<ScrubbableItem>();
            GetComponentsInChildren<ScrubbableItem>(true, dishesToWash);
        }

        totalDishes = dishesToWash.Count;

        // 2. ลงทะเบียน Event "เมื่อจานสะอาด" (เหมือนเดิม)
        // และ "เพิ่มปุ่ม" ให้จานแต่ละใบ เพื่อให้คลิกย้ายมาตรงกลางได้
        foreach (var dish in dishesToWash)
        {
            if (dish != null)
            {
                dish.OnCleaned += HandleDishCleaned;

                // เพิ่มปุ่มให้จานโดยอัตโนมัติ
                var button = dish.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => SelectDishToWash(dish));
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

    public override void Close()
    {
        base.Close();

        // (Optional) ถ้าปิดก่อนเสร็จ ก็รีเซ็ตจานทั้งหมด
        if (!IsCompleted)
        {
            ResetTask();
        }
    }

    // --- Game Logic ---

    void ResetTask()
    {
        dishesCleaned = 0;
        UpdateCounter();
        activeDish = null; // ไม่มีจานที่กำลังล้าง

        // สั่งให้จานทุกใบ "สกปรก" และ "ย้ายไปกองซ้าย"
        foreach (var dish in dishesToWash)
        {
            if (dish != null)
            {
                dish.ResetScrub(); // ทำให้สกปรก
                dish.transform.SetParent(dirtyPileContainer, false); // ย้ายไปกองซ้าย

                // ปิดการขัดถู (เพราะอยู่กองซ้าย)
                dish.enabled = false;

                // เปิดปุ่มให้คลิกได้
                var button = dish.GetComponent<Button>();
                if (button) button.interactable = true;
            }
        }
    }

    /// <summary>
    /// ถูกเรียกเมื่อผู้เล่น "คลิก" จานจากกองซ้าย
    /// </summary>
    public void SelectDishToWash(ScrubbableItem dish)
    {
        // ถ้ามีจานอยู่ตรงกลางแล้ว (ยังล้างไม่เสร็จ) ให้คลิกไม่ได้
        if (activeDish != null) return;

        // ถ้าจานใบนี้สะอาดแล้ว ก็ไม่ต้องทำอะไร
        if (dish.IsClean) return;

        activeDish = dish;

        // ย้ายจานมา "ตรงกลาง" (Active Station)
        dish.transform.SetParent(activeStation, false);
        dish.transform.localPosition = Vector3.zero; // จัดให้อยู่กลาง Station
        dish.transform.localScale = new Vector3(1.5f,1.5f,1.5f);

        // "เปิด" การขัดถู
        dish.enabled = true;

        // "ปิด" ปุ่ม (เพราะย้ายมาแล้ว)
        var button = dish.GetComponent<Button>();
        if (button) button.interactable = false;
    }


    /// <summary>
    /// Event Handler: ถูกเรียกโดย 'ScrubbableItem' เมื่อมันสะอาดแล้ว
    /// </summary>
    private void HandleDishCleaned(ScrubbableItem cleanedDish)
    {
        if (IsCompleted) return;

        dishesCleaned++;
        UpdateCounter();

        // (Optional) เล่นเสียง "สะอาด!"

        // ย้ายจานที่สะอาดแล้วไป "กองขวา"
        cleanedDish.transform.SetParent(cleanPileContainer, false);
        cleanedDish.transform.localScale = Vector3.one; // รีเซ็ตสเกล

        // เคลียร์ช่องตรงกลางให้ว่าง
        activeDish = null;

        // ตรวจสอบว่าครบจำนวนหรือยัง
        if (dishesCleaned >= totalDishes)
        {
            CompleteTask();
        }
    }

    void UpdateCounter()
    {
        if (counterText)
            counterText.text = $"{dishesCleaned}/{totalDishes}";
    }
}