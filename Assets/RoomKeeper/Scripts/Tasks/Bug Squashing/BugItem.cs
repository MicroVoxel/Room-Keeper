using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// 2. "ตัวแมลง" (แปะไว้ที่ Prefab ของแมลง)
/// ทำหน้าที่รับคลิก และแจ้งกลับไปยัง "สมอง" (Task_BugSquash)
/// </summary>
[RequireComponent(typeof(Image))] // ต้องมี Image เพื่อรับ Raycast
public class BugItem : MonoBehaviour, IPointerClickHandler
{
    private Task_BugSquash manager; // อ้างอิงถึง "สมอง"
    private bool isSquashed = false; // ป้องกันการคลิกซ้ำ

    /// <summary>
    /// รับค่าเริ่มต้นจาก "สมอง"
    /// </summary>
    public void Initialize(Task_BugSquash manager, float lifeTime)
    {
        this.manager = manager;
        // เริ่มนับเวลาถอยหลังเพื่อหายตัวเอง
        StartCoroutine(LifetimeRoutine(lifeTime));
    }

    /// <summary>
    /// เมื่อถูกคลิก (ตบ)
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isSquashed || manager == null) return;

        isSquashed = true;

        // หยุด Coroutine การนับเวลา (เพราะถูกตบแล้ว)
        StopAllCoroutines();

        // แจ้ง "สมอง" ว่าฉันถูกตบแล้ว
        manager.OnBugSquashed(this);

        // (Optional) เล่นเสียง/Animation ตบแมลง
        // เช่น: GetComponent<Animator>().SetTrigger("Squashed");

        // ทำลายตัวเอง
        Destroy(gameObject);
    }

    /// <summary>
    /// Coroutine นับเวลาถอยหลัง
    /// </summary>
    IEnumerator LifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        // ถ้ามาถึงตรงนี้ แสดงว่าเวลาหมด และ "ยังไม่ถูกตบ"
        if (!isSquashed && manager != null)
        {
            // แจ้ง "สมอง" ว่าฉันหมดเวลาแล้ว (หนีไปได้)
            manager.OnBugTimedOut(this);
        }

        // ทำลายตัวเอง (ไม่ว่าจะหนีได้หรือถูกตบไปแล้วแต่เผลอมาถึง)
        // (เพิ่ม isSquashed check อีกชั้น กันกรณีที่ถูกตบเฟรมเดียวกับที่หมดเวลา)
        if (!isSquashed)
        {
            Destroy(gameObject);
        }
    }
}