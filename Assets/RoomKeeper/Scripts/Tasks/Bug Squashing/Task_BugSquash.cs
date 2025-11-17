using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 1. "สมอง" ของมินิเกมตบแมลง (สืบทอดจาก TaskBase)
/// แปะสคริปต์นี้ไว้ที่ GameObject หลักของ Panel มินิเกม
/// </summary>
public class Task_BugSquash : TaskBase
{
    [Header("Bug Squash Setup")]
    [Tooltip("Prefab ของแมลง (ต้องมีสคริปต์ BugItem)")]
    public GameObject bugPrefab;

    [Tooltip("พื้นที่ (RectTransform) ที่จะให้แมลงสุ่มเกิด (เช่น Panel พื้นหลัง)")]
    public RectTransform spawnArea;

    [Tooltip("จำนวนแมลงที่ต้องตบให้ครบ")]
    public int bugsToSquash = 10;

    [Tooltip("แมลงจะโผล่มานานแค่ไหน (วินาที) ก่อนจะหายไปเอง")]
    public float bugLifeTime = 2.0f;

    [Tooltip("หน่วงเวลาก่อนแมลงตัวต่อไปเกิด (สุ่มระหว่าง X และ Y)")]
    public Vector2 spawnDelayRange = new Vector2(0.5f, 1.5f);

    [Header("UI")]
    public TMP_Text counterText; // (Optional) ตัวนับ "0/10"

    [Header("Progress")]
    private int squashedCount = 0;
    private List<BugItem> activeBugs = new List<BugItem>();
    private Coroutine spawnCoroutine;

    // --- TaskBase Overrides ---

    public override void Open()
    {
        base.Open();
        if (IsCompleted) return;

        ResetTask();
        // เริ่ม Coroutine สำหรับการ Spawn
        spawnCoroutine = StartCoroutine(SpawnBugRoutine());
    }

    public override void Close()
    {
        base.Close();

        // หยุดการ Spawn
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        // ถ้าปิดโดยที่ยังไม่เสร็จ ให้เคลียร์แมลงที่ค้างอยู่
        if (!IsCompleted)
        {
            ClearAllBugs();
        }
    }

    // --- Game Logic ---

    void ResetTask()
    {
        squashedCount = 0;
        UpdateCounter();
        ClearAllBugs(); // เคลียร์แมลงที่อาจจะค้างอยู่ (ถ้ามี)
    }

    void ClearAllBugs()
    {
        // ทำลาย BugItem ทุกตัวที่ยัง Active
        foreach (var bug in activeBugs)
        {
            if (bug != null)
            {
                Destroy(bug.gameObject);
            }
        }
        activeBugs.Clear();
    }

    /// <summary>
    /// Coroutine หลักในการสุ่มเกิดแมลง
    /// </summary>
    IEnumerator SpawnBugRoutine()
    {
        // วนลูปไปเรื่อยๆ ตราบใดที่ภารกิจยังเปิดอยู่และยังไม่สำเร็จ
        while (IsOpen && !IsCompleted)
        {
            // รอเวลาสุ่ม
            float delay = Random.Range(spawnDelayRange.x, spawnDelayRange.y);
            yield return new WaitForSeconds(delay);

            // ถ้าเกมถูกปิดระหว่างรอ ให้หยุด
            if (!IsOpen) yield break;

            // สุ่มตำแหน่งภายในขอบเขตของ spawnArea
            Vector2 randomPos = GetRandomPositionInSpawnArea();

            // สร้าง Prefab แมลง
            GameObject bugGO = Instantiate(bugPrefab, spawnArea);
            bugGO.transform.localPosition = randomPos; // ใช้ localPosition

            BugItem bugItem = bugGO.GetComponent<BugItem>();
            if (bugItem)
            {
                // "ฉีด" ค่าเริ่มต้นให้แมลงรู้ว่า "สมอง" คือใคร และมีชีวิตนานแค่ไหน
                bugItem.Initialize(this, bugLifeTime);
                activeBugs.Add(bugItem);
            }
            else
            {
                Debug.LogError($"[Task_BugSquash] Prefab '{bugPrefab.name}' ไม่มีสคริปต์ 'BugItem'!", this);
                Destroy(bugGO);
            }
        }
    }

    /// <summary>
    /// ถูกเรียกโดย BugItem เมื่อมันถูกตบ
    /// </summary>
    public void OnBugSquashed(BugItem bug)
    {
        if (IsCompleted) return;

        // ตรวจสอบว่าแมลงตัวนี้ยังอยู่ในลิสต์ (ยังไม่หมดเวลา หรือถูกตบไปแล้ว)
        if (!activeBugs.Contains(bug)) return;

        squashedCount++;
        UpdateCounter();

        activeBugs.Remove(bug); // เอาออกจากลิสต์ (BugItem จะทำลายตัวเอง)

        // ตรวจสอบว่าครบจำนวนหรือยัง
        if (squashedCount >= bugsToSquash)
        {
            // หยุดการ Spawn
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            // สำเร็จภารกิจ
            CompleteTask();
        }
    }

    /// <summary>
    /// ถูกเรียกโดย BugItem เมื่อมันหมดเวลา (ไม่ถูกตบ)
    /// </summary>
    public void OnBugTimedOut(BugItem bug)
    {
        if (activeBugs.Contains(bug))
        {
            activeBugs.Remove(bug); // แค่เอาออกจากลิสต์ (BugItem จะทำลายตัวเอง)
        }
    }

    // --- UI & Helpers ---

    void UpdateCounter()
    {
        if (counterText)
            counterText.text = $"{squashedCount}/{bugsToSquash}";
    }

    Vector2 GetRandomPositionInSpawnArea()
    {
        if (spawnArea == null) return Vector2.zero;

        Rect rect = spawnArea.rect;
        float x = Random.Range(rect.xMin, rect.xMax);
        float y = Random.Range(rect.yMin, rect.yMax);

        return new Vector2(x, y);
    }
}