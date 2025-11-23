using UnityEngine;
using UnityEngine.UI; // สำหรับ Button
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 1. "สมอง" ของมินิเกมล้างจาน (สืบทอดจาก TaskBase)
/// เวอร์ชัน: เพิ่มระบบเสียงขัดจานแบบต่อเนื่อง (Continuous Scrubbing Sound) แก้ปัญหาเสียงค้าง
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

    [Header("Audio - Events")]
    [Tooltip("เสียงเมื่อหยิบจานมาล้าง (เช่น เสียงน้ำกระเพื่อม)")]
    public AudioClip selectDishSound;
    [Tooltip("เสียงเมื่อจานสะอาด (เช่น เสียงวิ้ง!)")]
    public AudioClip dishCleanedSound;

    [Header("Audio - Scrubbing (Loop)")]
    [Tooltip("เสียงขณะขัดจาน (จะเล่นวนลูปเมื่อขยับเมาส์)")]
    public AudioClip scrubSound;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Progress")]
    private int dishesCleaned = 0;
    private int totalDishes = 0;
    private ScrubbableItem activeDish = null; // จานที่กำลังล้างอยู่

    // --- Audio Control Variables ---
    private AudioSource continuousAudioSource; // สำหรับเสียงขัด (Loop)
    private Vector3 lastMousePosition;
    private float lastScrubTime = 0f;

    /// <summary>
    /// ใช้ Awake() เพื่อค้นหาและลงทะเบียนจานทั้งหมด
    /// </summary>
    private void Awake()
    {
        // 1. ค้นหาจานทั้งหมด
        if (dishesToWash == null || dishesToWash.Count == 0)
        {
            dishesToWash = new List<ScrubbableItem>();
            GetComponentsInChildren<ScrubbableItem>(true, dishesToWash);
        }

        totalDishes = dishesToWash.Count;

        // 2. ลงทะเบียน Event และเพิ่มปุ่ม
        foreach (var dish in dishesToWash)
        {
            if (dish != null)
            {
                // Subscribe Event
                dish.OnCleaned += HandleDishCleaned;

                // [SAFETY] เช็คก่อนว่ามี Button หรือยัง กันการ Add ซ้ำ
                Button button = dish.GetComponent<Button>();
                if (button == null)
                {
                    button = dish.gameObject.AddComponent<Button>();
                }

                // Remove Listener เดิมก่อนเสมอเพื่อความชัวร์ แล้วค่อย Add ใหม่
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectDishToWash(dish));
            }
        }

        // [NEW] สร้าง AudioSource ส่วนตัวไว้สำหรับเสียงขัด (Loop)
        continuousAudioSource = gameObject.AddComponent<AudioSource>();
        continuousAudioSource.loop = true;
        continuousAudioSource.playOnAwake = false;
    }

    override protected void Start()
    {
        base.Start();

        // [NEW] เชื่อมต่อ Mixer Group เพื่อให้ Slider ควบคุมเสียงได้
        if (AudioManager.Instance != null)
        {
            continuousAudioSource.outputAudioMixerGroup = AudioManager.Instance.SFXGroup;
        }
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
        // หยุดเสียงทันทีเมื่อปิดหน้าต่าง
        StopScrubSound();

        if (!IsCompleted)
        {
            ResetTask();
        }
    }

    // --- Game Logic ---

    private void Update()
    {
        // [NEW] Logic ตรวจจับการขัดจาน (Scrubbing Audio Logic)
        // เราจะเช็คว่า:
        // 1. มีจานกำลังล้างอยู่ (activeDish != null)
        // 2. ผู้เล่นกดเมาส์ค้าง (Input.GetMouseButton(0))
        // 3. เมาส์มีการขยับ (Movement Check)

        bool isScrubbing = false;

        if (activeDish != null && Input.GetMouseButton(0))
        {
            Vector3 currentMousePos = Input.mousePosition;
            // เช็คระยะห่างจากเฟรมที่แล้ว (ถ้าขยับเกิน 2 pixel ถือว่าขยับ)
            float dist = (currentMousePos - lastMousePosition).magnitude;

            if (dist > 2f)
            {
                isScrubbing = true;
            }
            lastMousePosition = currentMousePos;
        }
        else
        {
            // อัปเดตตำแหน่งเมาส์ตลอดเวลา กันค่ากระโดดตอนคลิกครั้งแรก
            lastMousePosition = Input.mousePosition;
        }

        // การควบคุมเสียง
        if (isScrubbing)
        {
            lastScrubTime = Time.time; // บันทึกเวลาล่าสุดที่ขัด

            if (!continuousAudioSource.isPlaying && scrubSound != null)
            {
                continuousAudioSource.clip = scrubSound;
                continuousAudioSource.volume = sfxVolume;
                continuousAudioSource.Play();
            }
        }

        // Timeout Logic: ถ้าไม่ได้ขัดเกิน 0.15 วินาที ให้หยุดเสียง
        // (วิธีนี้จะแก้ปัญหาเสียงค้างเวลาลากเมาส์แล้วหยุดมือแต่ไม่ปล่อยปุ่ม)
        if (Time.time - lastScrubTime > 0.15f && continuousAudioSource.isPlaying)
        {
            continuousAudioSource.Stop();
        }
    }

    void ResetTask()
    {
        dishesCleaned = 0;
        UpdateCounter();
        activeDish = null;
        StopScrubSound();

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

    public void SelectDishToWash(ScrubbableItem dish)
    {
        if (activeDish != null) return;
        if (dish.IsClean) return;

        // เล่นเสียงหยิบจาน (One Shot)
        PlayOneShotSound(selectDishSound, 1.0f);

        activeDish = dish;

        dish.transform.SetParent(activeStation, false);
        dish.transform.localPosition = Vector3.zero;
        dish.transform.localScale = new Vector3(2f, 2f, 2f);

        dish.enabled = true;

        var button = dish.GetComponent<Button>();
        if (button) button.interactable = false;
    }

    private void HandleDishCleaned(ScrubbableItem cleanedDish)
    {
        if (IsCompleted) return;

        dishesCleaned++;
        UpdateCounter();

        // เล่นเสียงจานสะอาด (One Shot)
        PlayOneShotSound(dishCleanedSound, 1.1f);

        // หยุดเสียงขัดทันที
        StopScrubSound();

        cleanedDish.transform.SetParent(cleanPileContainer, false);
        cleanedDish.transform.localScale = Vector3.one;

        activeDish = null;

        if (dishesCleaned >= totalDishes)
        {
            CompleteTask();
        }
    }

    // --- Helper Methods ---

    private void PlayOneShotSound(AudioClip clip, float pitch = 1f)
    {
        if (AudioManager.Instance != null && clip != null)
        {
            AudioManager.Instance.PlaySFX(clip, sfxVolume, 0.1f);
        }
    }

    private void StopScrubSound()
    {
        if (continuousAudioSource != null)
        {
            continuousAudioSource.Stop();
        }
    }

    void UpdateCounter()
    {
        if (counterText)
            counterText.text = $"{dishesCleaned}/{totalDishes}";
    }
}