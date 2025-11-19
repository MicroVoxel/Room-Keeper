using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 2. "จานสกปรก" (Reusable Component)
/// แปะสคริปต์นี้ไว้ที่ GameObject ของ "คราบ" (Image ที่อยู่บนจาน)
/// </summary>
[RequireComponent(typeof(Image))]
public class ScrubbableItem : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Header("Scrub Settings")]
    [Tooltip("Image ของคราบสกปรก (ต้องเปิด Raycast Target)")]
    public Image dirtyOverlay;

    [Tooltip("เปอร์เซ็นต์ที่ต้องสะอาด (0.0 - 1.0)")]
    public float requiredSweepAmount = 0.95f;

    [Range(0.01f, 1.0f)]
    public float sweepStrength = 0.1f;

    public float brushRadius = 30f;

    // Event ที่จะแจ้งกลับไปหา "สมอง" (Task_WashDishes)
    public Action<ScrubbableItem> OnCleaned;
    public bool IsClean { get; private set; } = false;

    // --- Texture Management ---
    private Texture2D dirtyTexture;
    private Texture2D originalSourceTexture;
    private Color[] pixelData;
    private int textureWidth;
    private int textureHeight;
    private float totalInitialAlpha = 0f;
    private float currentCleanedAlpha = 0f;

    private RectTransform rt;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (dirtyOverlay == null) dirtyOverlay = GetComponent<Image>();

        if (dirtyOverlay.mainTexture is Texture2D texture)
        {
            // (สำคัญ) เราต้องใช้ Texture ต้นฉบับ
            originalSourceTexture = texture;
            // สร้าง Texture ที่จะใช้เล่นจริง (Copy)
            InitializeDirtyTexture();
        }
        else
        {
            Debug.LogError($"[ScrubbableItem] '{gameObject.name}' ไม่มี Texture2D หรือ Texture ไม่ได้ตั้งค่า Read/Write Enabled!", this);
        }
    }

    /// <summary>
    /// รีเซ็ตให้กลับมาสกปรกเหมือนเดิม
    /// </summary>
    public void ResetScrub()
    {
        IsClean = false;
        // สร้าง Texture ขึ้นมาใหม่จากต้นฉบับ
        InitializeDirtyTexture();

        // (สำคัญ) เปิดให้ลากได้อีกครั้ง
        this.enabled = true;
    }

    /// <summary>
    /// (นำโค้ดมาจาก SweepingTask)
    /// สร้าง Texture ที่แก้ไขได้ จาก Texture ต้นฉบับ
    /// </summary>
    private void InitializeDirtyTexture()
    {
        if (originalSourceTexture == null) return;
        if (dirtyTexture != null) Destroy(dirtyTexture);

        // --- Create a copy ---
        RenderTexture rt = RenderTexture.GetTemporary(originalSourceTexture.width, originalSourceTexture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(originalSourceTexture, rt);
        dirtyTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        textureWidth = rt.width;
        textureHeight = rt.height;
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        dirtyTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        dirtyTexture.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        // --- Get C# array (Fast) ---
        pixelData = dirtyTexture.GetPixels();

        // --- Calculate total alpha ---
        totalInitialAlpha = 0f;
        currentCleanedAlpha = 0f;
        foreach (Color color in pixelData)
        {
            totalInitialAlpha += color.a;
        }

        // --- Apply to UI ---
        dirtyOverlay.sprite = Sprite.Create(dirtyTexture, new Rect(0, 0, textureWidth, textureHeight), Vector2.one * 0.5f, 100f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // เราจำเป็นต้อง implement IBeginDragHandler เพื่อให้ OnDrag ทำงาน
        // แม้ว่าเราจะลากบนตัวมันเองก็ตาม
    }

    /// <summary>
    /// เมื่อผู้เล่น "ถู" บนคราบสกปรก
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (IsClean || dirtyTexture == null || pixelData == null) return;

        // แปลง Screen Point เป็น Local Point
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

        // แปลง Local Point (UI) เป็น Texture Coordinate (Pixel)
        Vector2 textureCoord = new Vector2(
            (localPoint.x - rt.rect.x) * (textureWidth / rt.rect.width),
            (localPoint.y - rt.rect.y) * (textureHeight / rt.rect.height)
        );

        SweepAt(Mathf.RoundToInt(textureCoord.x), Mathf.RoundToInt(textureCoord.y));
    }

    /// <summary>
    /// (นำโค้ดมาจาก SweepingTask)
    /// ลบ Alpha ที่ตำแหน่งที่กำหนด
    /// </summary>
    private void SweepAt(int px, int py)
    {
        int brushRadiusInt = Mathf.RoundToInt(brushRadius);
        float localCleanedAlpha = 0f;
        bool pixelChanged = false;

        for (int x = px - brushRadiusInt; x <= px + brushRadiusInt; x++)
        {
            for (int y = py - brushRadiusInt; y <= py + brushRadiusInt; y++)
            {
                if (x >= 0 && x < textureWidth && y >= 0 && y < textureHeight)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(px, py));
                    if (dist <= brushRadius)
                    {
                        int index = y * textureWidth + x;
                        Color currentColor = pixelData[index];

                        if (currentColor.a > 0.001f)
                        {
                            float originalAlpha = currentColor.a;
                            float alphaReductionFactor = Mathf.Clamp01(1f - dist / brushRadius);
                            float reductionAmount = alphaReductionFactor * sweepStrength;
                            currentColor.a = Mathf.Max(0f, originalAlpha - reductionAmount);
                            float actualReduction = originalAlpha - currentColor.a;

                            if (actualReduction > 0)
                            {
                                localCleanedAlpha += actualReduction;
                                pixelData[index] = currentColor;
                                pixelChanged = true;
                            }
                        }
                    }
                }
            }
        }

        if (pixelChanged)
        {
            dirtyTexture.SetPixels(pixelData);
            dirtyTexture.Apply(false);
            currentCleanedAlpha += localCleanedAlpha;
            CalculateProgress();
        }
    }

    /// <summary>
    /// (นำโค้ดมาจาก SweepingTask)
    /// คำนวณความคืบหน้า และตรวจสอบว่าสะอาดหรือยัง
    /// </summary>
    private void CalculateProgress()
    {
        if (IsClean) return;
        if (totalInitialAlpha <= 0f) return;

        float currentSweepProgress = currentCleanedAlpha / totalInitialAlpha;

        if (currentSweepProgress >= requiredSweepAmount)
        {
            IsClean = true;
            // (สำคัญ) แจ้ง "สมอง" ว่าฉันสะอาดแล้ว!
            OnCleaned?.Invoke(this);

            // (สำคัญ) ปิดการทำงานของสคริปต์นี้ไปเลย จะได้ไม่เปลือง
            this.enabled = false;
        }
    }
}