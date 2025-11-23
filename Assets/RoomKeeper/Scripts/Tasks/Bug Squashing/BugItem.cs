using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // จำเป็นสำหรับ Image
using System.Collections;

/// <summary>
/// 2. "ตัวแมลง" (BugItem)
/// เวอร์ชั่น: เปลี่ยนร่างเป็นรอยเลือด (Splat) เมื่อถูกตบ
/// </summary>
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(CanvasGroup))] // เพิ่ม CanvasGroup เพื่อใช้ Fade Out
public class BugItem : MonoBehaviour, IPointerClickHandler
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 300f;
    [SerializeField] private float waitTimeMin = 0.5f;
    [SerializeField] private float waitTimeMax = 2.0f;

    [Header("Death Settings")]
    [SerializeField] private float fadeDuration = 0.5f; // เวลาที่ใช้จางหายหลังตาย
    [SerializeField] private float deadDisplayTime = 1.0f; // โชว์ศพกี่วินาทีก่อนเริ่มจาง

    private Task_BugSquash manager;
    private bool isSquashed = false;

    // Components
    private Image _image;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;

    // Logic Variables
    private RectTransform movementArea;
    private Coroutine moveCoroutine;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Initialize(Task_BugSquash manager, float lifeTime, RectTransform area)
    {
        this.manager = manager;
        this.movementArea = area;
        this.isSquashed = false;

        // Reset ค่าต่างๆ (เผื่อใช้ Pool ในอนาคต)
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        _image.raycastTarget = true;
        _rectTransform.localRotation = Quaternion.identity;

        // เริ่มนับเวลาถอยหลัง
        StartCoroutine(LifetimeRoutine(lifeTime));

        // เริ่มบิน
        StartMovement();
    }

    // --- Movement Logic ---
    private void StartMovement()
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(RoamRoutine());
    }

    private IEnumerator RoamRoutine()
    {
        while (!isSquashed)
        {
            if (movementArea == null) yield break;

            Vector2 targetPos = GetRandomPosition();
            Vector2 startPos = _rectTransform.anchoredPosition;

            float distance = Vector2.Distance(startPos, targetPos);
            float duration = distance / moveSpeed;
            float elapsed = 0f;

            // หันหน้าไปทางที่บิน (Optional: ถ้าต้องการให้หัวหันไปทางที่บิน)
            // Vector3 dir = targetPos - startPos;
            // float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // _rectTransform.rotation = Quaternion.AngleAxis(angle - 90, Vector3.forward);

            while (elapsed < duration)
            {
                if (isSquashed) yield break;
                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _rectTransform.anchoredPosition = targetPos;

            yield return new WaitForSeconds(Random.Range(waitTimeMin, waitTimeMax));
        }
    }

    private Vector2 GetRandomPosition()
    {
        Rect rect = movementArea.rect;
        float x = Random.Range(rect.xMin, rect.xMax);
        float y = Random.Range(rect.yMin, rect.yMax);
        return new Vector2(x, y);
    }

    // --- Input Logic ---
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isSquashed || manager == null) return;

        // แจ้ง Manager ว่าโดนตบแล้วนะ
        // Manager จะเป็นคนสั่งกลับมาว่าให้เปลี่ยนรูปเป็นอะไร ผ่านฟังก์ชัน Squash()
        manager.OnBugSquashed(this);
    }

    /// <summary>
    /// เปลี่ยนร่างเป็นรอยเลือด และเริ่มกระบวนการตาย
    /// </summary>
    /// <param name="splatSprite">รูปศพ/รอยเลือด ที่จะเปลี่ยนไปใช้</param>
    public void Squash(Sprite splatSprite)
    {
        isSquashed = true;

        // 1. หยุดทุกอย่าง
        StopAllCoroutines();

        // 2. ปิดการรับ Raycast ทันที (กันกดซ้ำ)
        _image.raycastTarget = false;
        _canvasGroup.blocksRaycasts = false;

        // 3. เปลี่ยนรูปเป็นรอยเลือด
        if (splatSprite != null)
        {
            _image.sprite = splatSprite;
            _image.SetNativeSize(); // ปรับขนาดให้ตรงกับรูปเลือด
        }

        // 4. ปรับการหมุน/ขนาดสุ่ม เพื่อความสมจริง
        transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        transform.localScale = Vector3.one * Random.Range(0.9f, 1.1f);

        // 5. เริ่ม Fade Out และทำลายตัวเอง
        StartCoroutine(FadeOutAndDestroy());
    }

    IEnumerator FadeOutAndDestroy()
    {
        // โชว์ศพสักพัก
        yield return new WaitForSeconds(deadDisplayTime);

        // ค่อยๆ จางหาย
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        Destroy(gameObject);
    }

    IEnumerator LifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (!isSquashed && manager != null)
        {
            manager.OnBugTimedOut(this);
        }

        if (!isSquashed)
        {
            Destroy(gameObject);
        }
    }
}