using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections; // <-- ++ เพิ่มเข้ามา ++

/// <summary>
/// 2. "ไอเทมที่หาได้" (Reusable Component)
/// แปะสคริปต์นี้ไว้ที่ GameObject ของ "ไอเทม" ที่ซ่อนอยู่
/// (เช่น Image รูปถุงเท้า)
/// </summary>
[RequireComponent(typeof(Image))] // ต้องมี Image เพื่อรับคลิก
public class FindableObject : MonoBehaviour
{
    private Task_FindObjects manager; // อ้างอิงถึง "สมอง"
    private bool isFound = false;

    [Header("Visuals")]
    [Tooltip("(Optional) เอฟเฟกต์เมื่อถูกพบ (เช่น กรอบเรืองแสง)")]
    public GameObject foundEffect;

    private Image itemImage;
    private Button itemButton; // เราจะใช้ Button เพื่อให้คลิกง่าย

    // ++ เพิ่มเข้ามาสำหรับ Tween ++
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private RectTransform tweenTarget;
    private Coroutine tweenCoroutine;
    private Vector3 originalScale;

    private void Awake()
    {
        // เราจะใช้ Button component ในการรับคลิก
        // ถ้าไม่มี ให้เพิ่มเข้าไป
        itemButton = GetComponent<Button>();
        if (itemButton == null)
        {
            itemButton = gameObject.AddComponent<Button>();
        }

        // ตั้งค่าให้ปุ่มโปร่งใส (เราใช้ Image ของเราเอง)
        itemButton.targetGraphic = null;
        itemButton.transition = Selectable.Transition.None;

        // ผูก Event OnClick
        itemButton.onClick.AddListener(OnItemClicked);

        itemImage = GetComponent<Image>();

        // ++ เพิ่มเข้ามา ++
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;

        // เราใช้ CanvasGroup เพื่อ Fade Out ทั้งหมด (รวมถึง Found Effect)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        // -- จบส่วนที่เพิ่ม --

        // ปิดเอฟเฟกต์ไว้ก่อน
        if (foundEffect) foundEffect.SetActive(false);
    }

    /// <summary>
    /// รับ "สมอง" มาจาก Task_FindObjects
    /// </summary>
    public void Initialize(Task_FindObjects manager, RectTransform collectTarget) // <-- อัปเดต Signature
    {
        this.manager = manager;
        this.tweenTarget = collectTarget; // <-- ++ เพิ่มเข้ามา ++
    }

    /// <summary>
    /// รีเซ็ตไอเทมให้กลับไปซ่อน
    /// </summary>
    public void ResetItem()
    {
        isFound = false;

        // ++ เพิ่มเข้ามา ++
        // หยุดแอนิเมชันเก่า (ถ้ามี) และคืนค่า
        if (tweenCoroutine != null) StopCoroutine(tweenCoroutine);
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        rectTransform.localScale = originalScale;
        // -- จบส่วนที่เพิ่ม --

        // เปิดปุ่มให้คลิกได้
        itemButton.interactable = true;

        // (Optional) ทำให้สีจางลง (ถ้าอยากให้มันหายาก)
        // var color = itemImage.color;
        // color.a = 1f;
        // itemImage.color = color;

        if (foundEffect) foundEffect.SetActive(false);
    }

    /// <summary>
    /// ถูกเรียกเมื่อ Button ถูกคลิก
    /// </summary>
    private void OnItemClicked()
    {
        if (isFound || manager == null) return;

        isFound = true;

        // ปิดปุ่ม ไม่ให้คลิกซ้ำ
        itemButton.interactable = false;

        // (Optional) ทำให้สีจางลงเมื่อถูกพบ
        // var color = itemImage.color;
        // color.a = 0.5f;
        // itemImage.color = color;

        // เปิดเอฟเฟกต์
        if (foundEffect) foundEffect.SetActive(true);

        // แจ้ง "สมอง" ว่าถูกพบแล้ว
        manager.OnItemFound(this);

        // ++ (FIX) ++
        // ตรวจสอบว่า GameObject ของเรายัง Active อยู่หรือไม่
        // (เพราะถ้าเราเป็นไอเทมชิ้นสุดท้าย Manager อาจจะสั่งปิด Panel ไปแล้ว)
        if (!gameObject.activeInHierarchy)
        {
            return; // ไม่ต้องทำ Tween
        }
        // ++ (END FIX) ++

        // ++ เพิ่มเข้ามา ++
        // เริ่มแอนิเมชัน Tween
        if (tweenCoroutine != null) StopCoroutine(tweenCoroutine);
        tweenCoroutine = StartCoroutine(TweenToTarget());
    }

    /// <summary>
    /// ++ (NEW) Coroutine สำหรับแอนิเมชันลอยไปหาเป้าหมาย ++
    /// </summary>
    private IEnumerator TweenToTarget()
    {
        float duration = 0.5f; // ระยะเวลาแอนิเมชัน (วินาที)

        // ถ้าไม่ได้ตั้งเป้าหมายไว้ ให้ลอยไปกลางจอ (ของ Canvas)
        Vector3 targetPosition;
        if (tweenTarget != null)
        {
            targetPosition = tweenTarget.position;
        }
        else
        {
            // หา Canvas ที่เราอยู่
            Canvas canvas = GetComponentInParent<Canvas>();
            targetPosition = canvas.transform.TransformPoint(Vector3.zero); // กลางจอ
        }

        Vector3 startPosition = rectTransform.position;
        Vector3 startScale = rectTransform.localScale;
        Vector3 targetScale = Vector3.one * 0.1f; // ย่อให้เล็ก

        // ทำให้ลอยอยู่เหนือ UI อื่นๆ
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();

        float timer = 0f;
        while (timer < duration)
        {
            // คำนวณ % ความคืบหน้า (t)
            float t = timer / duration;
            // (Optional) ใช้ SmoothStep เพื่อให้การเคลื่อนที่ดูลื่นไหล (Ease In/Out)
            t = t * t * (3f - 2f * t);

            // เคลื่อนที่ (Lerp)
            rectTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
            rectTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t); // ค่อยๆ จางหาย

            timer += Time.deltaTime;
            yield return null;
        }

        // เมื่อแอนิเมชันจบ
        gameObject.SetActive(false); // ซ่อนตัวเอง
        tweenCoroutine = null;
    }

    // เราไม่จำเป็นต้องใช้ OnPointerClick อีกต่อไป เพราะเราใช้ Button
    // แต่ถ้าคุณไม่ชอบ Button และอยากใช้ IPointerClickHandler ก็ทำได้:
    // public void OnPointerClick(PointerEventData eventData)
    // {
    //     OnItemClicked(); // แค่เรียก OnItemClicked()
    // }
}