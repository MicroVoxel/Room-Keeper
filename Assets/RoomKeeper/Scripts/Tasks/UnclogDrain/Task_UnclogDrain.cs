using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// "สมอง" ของมินิเกมปั๊มส้วม/อ่าง (สืบทอดจาก TaskBase)
/// แปะสคริปต์นี้ไว้ที่ GameObject หลักของ Panel ภารกิจ
/// </summary>
public class Task_UnclogDrain : TaskBase
{
    [Header("Unclog Setup")]
    [Tooltip("ปุ่มสำหรับกดปั๊ม (ลาก Button มาใส่)")]
    public Button plungerButton;

    [Tooltip("รูปภาพของที่ปั๊ม (Optional, สำหรับ Animation)")]
    public RectTransform plungerImage;

    [Tooltip("Slider ที่แสดงความคืบหน้า")]
    public Slider progressBar;

    [Tooltip("ค่าความคืบหน้าที่เพิ่มขึ้นต่อการปั๊ม 1 ครั้ง")]
    public float progressPerPlunge = 5f;

    [Tooltip("ค่าความคืบหน้าที่ลดลงต่อวินาที (ถ้าไม่ปั๊ม)")]
    public float progressDecayPerSecond = 10f;

    [Tooltip("ค่าความคืบหน้าที่ต้องทำให้ถึง 100%")]
    public float requiredProgress = 100f;

    [Header("Shake Effect")]
    [Tooltip("ความแรงของการสั่น (pixels)")]
    public float shakeIntensity = 5f;
    [Tooltip("ระยะเวลาการสั่น (seconds)")]
    public float shakeDuration = 0.15f;

    [Header("Progress")]
    private float currentProgress = 0f;
    private Coroutine plungerAnimationCoroutine;
    private Coroutine progressShakeCoroutine; // <- เพิ่มตัวแปรสำหรับ Coroutine สั่น
    private Vector2 plungerOriginalPos;
    private Vector2 progressBarOriginalPos; // <- เพิ่มตัวแปรเก็บตำแหน่งดั้งเดิม

    // --- TaskBase Overrides & Setup ---

    /// <summary>
    /// ใช้ Awake() เพื่อลงทะเบียน Listener ของปุ่ม
    /// </summary>
    private void Awake()
    {
        if (plungerButton == null)
        {
            Debug.LogError("[Task_UnclogDrain] 'Plunger Button' is not assigned!", this);
            return;
        }

        // เราจะผูกโค้ด OnPlunge() เข้ากับ Event 'onClick' ของปุ่ม
        plungerButton.onClick.AddListener(OnPlunge);

        if (plungerImage != null)
        {
            plungerOriginalPos = plungerImage.anchoredPosition;
        }

        // ++ เพิ่มเข้ามา ++
        // เก็บตำแหน่งดั้งเดิมของ Progress Bar
        if (progressBar != null)
        {
            progressBarOriginalPos = progressBar.GetComponent<RectTransform>().anchoredPosition;
        }
    }

    override protected void Start()
    {
        base.Start();
        // ไม่ต้องทำอะไรเป็นพิเศษใน Start()
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
        // ไม่ต้องทำอะไรเป็นพิเศษเมื่อปิด
    }

    // --- Game Logic ---

    void ResetTask()
    {
        currentProgress = 0f;
        UpdateUI();

        if (plungerButton != null)
        {
            plungerButton.interactable = true; // เปิดให้ปุ่มกดได้
        }
    }

    /// <summary>
    /// เมธอดนี้จะถูกเรียกทุกครั้งที่ 'plungerButton' ถูกคลิก
    /// </summary>
    public void OnPlunge()
    {
        if (IsCompleted || !IsOpen) return;

        // 1. เพิ่มความคืบหน้า
        currentProgress += progressPerPlunge;
        currentProgress = Mathf.Clamp(currentProgress, 0, requiredProgress);

        // (Optional) เล่น Animation ปั๊ม
        if (plungerImage != null)
        {
            if (plungerAnimationCoroutine != null) StopCoroutine(plungerAnimationCoroutine);
            plungerAnimationCoroutine = StartCoroutine(PlungerAnimation());
        }

        // ++ เพิ่มเข้ามา ++
        // สั่งให้ Progress Bar สั่น
        if (progressBar != null)
        {
            if (progressShakeCoroutine != null) StopCoroutine(progressShakeCoroutine);
            progressShakeCoroutine = StartCoroutine(ShakeProgressBar());
        }

        // 3. อัปเดต UI และตรวจสอบว่าชนะหรือยัง
        UpdateUI();
        CheckForCompletion();
    }

    /// <summary>
    /// เราใช้ Update() เพื่อ "ลด" ค่าความคืบหน้าตลอดเวลา
    /// นี่คือกลไกหลักที่บีบให้ผู้เล่นต้อง "คลิกรัวๆ"
    /// </summary>
    private void Update()
    {
        if (!IsOpen || IsCompleted) return;

        // ถ้าผู้เล่นไม่ทำอะไร ความคืบหน้าจะค่อยๆ ลดลง
        if (currentProgress > 0)
        {
            currentProgress -= progressDecayPerSecond * Time.deltaTime;
            currentProgress = Mathf.Max(0, currentProgress); // ไม่ให้ติดลบ
            UpdateUI();
        }
    }

    void UpdateUI()
    {
        if (progressBar)
        {
            progressBar.value = currentProgress / requiredProgress;
        }
    }

    void CheckForCompletion()
    {
        if (currentProgress >= requiredProgress)
        {
            // สำเร็จ!
            if (plungerButton != null)
            {
                plungerButton.interactable = false; // ปิดปุ่ม (กันคลิกต่อ)
            }

            // เรียกฟังก์ชันหลักของ TaskBase
            CompleteTask();
        }
    }

    /// <summary>
    /// (Optional) Coroutine สำหรับทำ Animation กดปั๊มแบบง่ายๆ
    /// </summary>
    IEnumerator PlungerAnimation()
    {
        if (plungerImage == null) yield break;

        float animDuration = 0.05f; // กดลงเร็ว
        float returnDuration = 0.1f; // คืนตัวช้ากว่า
        float punchDistance = 20f; // กดลงไปลึกแค่ไหน (pixels)

        Vector2 punchPos = plungerOriginalPos - new Vector2(0, punchDistance);

        // กดลง
        float timer = 0f;
        while (timer < animDuration)
        {
            plungerImage.anchoredPosition = Vector2.Lerp(plungerOriginalPos, punchPos, timer / animDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        plungerImage.anchoredPosition = punchPos;

        // คืนตัว
        timer = 0f;
        while (timer < returnDuration)
        {
            plungerImage.anchoredPosition = Vector2.Lerp(punchPos, plungerOriginalPos, timer / returnDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        plungerImage.anchoredPosition = plungerOriginalPos;
        plungerAnimationCoroutine = null;
    }

    /// <summary>
    /// (NEW) Coroutine สำหรับสั่น ProgressBar
    /// </summary>
    IEnumerator ShakeProgressBar()
    {
        if (progressBar == null) yield break;

        RectTransform barRect = progressBar.GetComponent<RectTransform>();
        float timer = 0f;

        while (timer < shakeDuration)
        {
            // สุ่มตำแหน่งสั่นรอบๆ ตำแหน่งดั้งเดิม
            Vector2 shakeOffset = Random.insideUnitCircle * shakeIntensity;
            barRect.anchoredPosition = progressBarOriginalPos + shakeOffset;

            timer += Time.deltaTime;
            yield return null;
        }

        // คืนตำแหน่งเดิมเมื่อสั่นเสร็จ
        barRect.anchoredPosition = progressBarOriginalPos;
        progressShakeCoroutine = null;
    }
}