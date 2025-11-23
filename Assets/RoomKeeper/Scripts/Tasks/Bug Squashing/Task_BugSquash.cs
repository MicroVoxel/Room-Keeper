using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 1. "สมอง" (Task_BugSquash)
/// เวอร์ชั่น: ปรับปรุงให้ใช้ AudioManager เพื่อรองรับ SFX Slider และ Pooling
/// </summary>
public class Task_BugSquash : TaskBase
{
    [Header("Bug Squash Setup")]
    public GameObject bugPrefab; // Prefab ยุง (ต้องมี Script BugItem)
    public RectTransform spawnArea;
    public int bugsToSquash = 10;
    public float bugLifeTime = 2.0f;
    public Vector2 spawnDelayRange = new Vector2(0.5f, 1.5f);

    [Header("Finish Settings")]
    public float finishDelay = 1.5f;

    [Header("Audio & Visuals")]
    public AudioClip[] squashClips;

    [Tooltip("รูปภาพรอยเลือด/ซากยุง (ใส่ได้หลายรูปแบบ จะสุ่มหยิบไปใช้)")]
    public Sprite[] splatSprites;

    // [OPTIMIZATION] ลบ AudioSource ของตัวเองออก เพื่อไปใช้ระบบกลาง
    // private AudioSource _audioSource; 

    [Header("UI")]
    public TMP_Text counterText;

    // State
    private int squashedCount = 0;
    private List<BugItem> activeBugs = new List<BugItem>();
    private Coroutine spawnCoroutine;

    // ลบ Awake ทิ้งได้เลย เพราะไม่ได้ GetComponent แล้ว
    // private void Awake() { ... }

    public override void Open()
    {
        base.Open();
        if (IsCompleted) return;
        ResetTask();
        spawnCoroutine = StartCoroutine(SpawnBugRoutine());
    }

    public override void Close()
    {
        base.Close();
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        StopAllCoroutines();
        if (!IsCompleted) ClearAllBugs();
    }

    void ResetTask()
    {
        squashedCount = 0;
        UpdateCounter();
        ClearAllBugs();
    }

    void ClearAllBugs()
    {
        foreach (var bug in activeBugs)
        {
            if (bug != null) Destroy(bug.gameObject);
        }
        activeBugs.Clear();
    }

    IEnumerator SpawnBugRoutine()
    {
        while (IsOpen && !IsCompleted)
        {
            float delay = Random.Range(spawnDelayRange.x, spawnDelayRange.y);
            yield return new WaitForSeconds(delay);

            if (!IsOpen || squashedCount >= bugsToSquash) yield break;

            // สร้างแมลง
            GameObject bugGO = Instantiate(bugPrefab, spawnArea);
            bugGO.transform.localScale = Vector3.one;

            // สุ่มตำแหน่งเกิด
            BugItem bugItem = bugGO.GetComponent<BugItem>();
            if (bugItem)
            {
                Rect rect = spawnArea.rect;
                Vector2 startPos = new Vector2(Random.Range(rect.xMin, rect.xMax), Random.Range(rect.yMin, rect.yMax));
                bugGO.GetComponent<RectTransform>().anchoredPosition = startPos;

                // Initialize
                bugItem.Initialize(this, bugLifeTime, spawnArea);
                activeBugs.Add(bugItem);
            }
        }
    }

    public void OnBugSquashed(BugItem bug)
    {
        if (IsCompleted || !activeBugs.Contains(bug)) return;

        squashedCount++;
        UpdateCounter();
        activeBugs.Remove(bug);

        PlayRandomSquashSound();

        // สุ่มรูปเลือด แล้วสั่งให้ยุงเปลี่ยนร่าง
        Sprite selectedSplat = null;
        if (splatSprites != null && splatSprites.Length > 0)
        {
            selectedSplat = splatSprites[Random.Range(0, splatSprites.Length)];
        }

        // สั่งยุงให้ตายและเปลี่ยนรูป
        bug.Squash(selectedSplat);

        // เช็คเงื่อนไขชนะ
        if (squashedCount >= bugsToSquash)
        {
            if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
            StartCoroutine(WaitAndCompleteRoutine());
        }
    }

    IEnumerator WaitAndCompleteRoutine()
    {
        yield return new WaitForSeconds(finishDelay);
        CompleteTask();
    }

    private void PlayRandomSquashSound()
    {
        // [KEY CHANGE] ใช้ AudioManager แทน AudioSource ในตัว
        // เพื่อให้มั่นใจว่าเสียงจะออกผ่าน SFX Mixer Group และถูกคุมด้วย Slider ได้
        if (AudioManager.Instance != null && squashClips != null && squashClips.Length > 0)
        {
            AudioClip clip = squashClips[Random.Range(0, squashClips.Length)];

            // เรียกใช้ PlaySFX (Clip, Volume, PitchVariance)
            // 0.1f คือค่า Variance ที่จะสุ่ม Pitch ระหว่าง 0.9 - 1.1 โดยอัตโนมัติใน AudioManager
            AudioManager.Instance.PlaySFX(clip, 1f, 0.1f);
        }
    }

    public void OnBugTimedOut(BugItem bug)
    {
        if (activeBugs.Contains(bug)) activeBugs.Remove(bug);
    }

    void UpdateCounter()
    {
        if (counterText) counterText.text = $"{squashedCount}/{bugsToSquash}";
    }
}