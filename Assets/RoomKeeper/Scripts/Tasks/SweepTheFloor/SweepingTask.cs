using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class SweepingTask : TaskBase, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    #region UI & Settings

    [Header("Sweeping Minigame Settings")]
    [Tooltip("จำนวนสิ่งสกปรกที่ต้องถูกกวาดออกไป (เป็นเปอร์เซ็นต์: 0.0 ถึง 1.0)")]
    public float requiredSweepAmount = 0.9f;

    [Range(0.01f, 1.0f)]
    [Tooltip("ค่าความแรงในการลด Alpha ต่อการลาก/เฟรม (Fixed Reduction)")]
    public float sweepStrength = 0.1f;

    [SerializeField] private float currentSweepProgress = 0f;

    [Header("UI References")]
    public Image dirtyOverlay; // รูปภาพ UI Overlay ที่เป็นสิ่งสกปรก (ต้องเปิด Raycast Target)
    public Slider progressBar; // แถบความคืบหน้า (Progress Bar) สำหรับ UI

    [Header("Brush Settings")]
    public float brushRadius = 50f; // รัศมีของแปรงในหน่วยพิกเซลของ Texture

    // ตัวแปรภายใน
    private Texture2D dirtyTexture;
    private Texture2D originalSourceTexture;
    private int totalDirtyPixels;
    private float totalInitialAlpha = 0f;    // ปริมาณ Alpha Channel เริ่มต้นรวมทั้งหมด
    private float currentCleanedAlpha = 0f;  // ปริมาณ Alpha ที่ถูกกำจัดไปแล้ว (ใช้สำหรับ Progress)

    #endregion

    // ==================================================================================

    #region Unity Life Cycle & TaskBase Overrides

    public override void Awake()
    {
        base.Awake();
        if (dirtyOverlay != null && dirtyOverlay.mainTexture is Texture2D texture)
        {
            originalSourceTexture = texture;
        }
        else
        {
            Debug.LogError("Please ensure 'Dirty Overlay' has a Texture2D assigned in the Source Image slot and Read/Write is enabled.");
        }
    }

    public override void Open()
    {
        if (IsCompleted) return;
        base.Open();

        if (!IsCompleted)
        {
            // เริ่มต้นหรือรีเซ็ต Texture และ Progress
            InitializeDirtyTexture();
            currentSweepProgress = 0f;
            if (progressBar) progressBar.value = 0f;
        }
    }

    public override void Close()
    {
        base.Close();

        // ถ้าภารกิจถูกปิดก่อนที่จะทำเสร็จสมบูรณ์ ให้รีเซ็ตสถานะทั้งหมด
        if (!IsCompleted)
        {
            ResetTask();
        }
    }

    private void OnDestroy()
    {
        // ทำลาย Texture2D ที่สร้างขึ้นเมื่อ GameObject ถูกทำลาย
        if (dirtyTexture != null)
        {
            UnityEngine.Object.Destroy(dirtyTexture);
        }
    }

    #endregion

    // ==================================================================================

    #region UI Drag Handlers (Input)

    public void OnBeginDrag(PointerEventData eventData)
    {
        // เริ่มต้นการลาก
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsOpen || IsCompleted) return;
        if (dirtyTexture == null) return;

        // แปลงตำแหน่งหน้าจอเป็น Local Point ภายใน RectTransform ของ dirtyOverlay
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dirtyOverlay.rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        // แปลง Local Point เป็นพิกเซล (0 ถึง width/height)
        Vector2 textureCoord = new Vector2(
            (localPoint.x - dirtyOverlay.rectTransform.rect.x) * (dirtyTexture.width / dirtyOverlay.rectTransform.rect.width),
            (localPoint.y - dirtyOverlay.rectTransform.rect.y) * (dirtyTexture.height / dirtyOverlay.rectTransform.rect.height)
        );

        SweepAt(Mathf.RoundToInt(textureCoord.x), Mathf.RoundToInt(textureCoord.y));
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // สิ้นสุดการลาก
    }

    #endregion

    // ==================================================================================

    #region Reset & Initialization Logic

    private void ResetTask()
    {
        currentCleanedAlpha = 0f;
        currentSweepProgress = 0f;
        if (progressBar) progressBar.value = 0f;

        if (dirtyTexture != null)
        {
            UnityEngine.Object.Destroy(dirtyTexture);
            dirtyTexture = null;
        }
    }

    private void InitializeDirtyTexture()
    {
        if (originalSourceTexture == null)
        {
            Debug.LogError("Original Source Texture is missing or invalid. Check Awake() error message.");
            return;
        }

        if (dirtyTexture != null)
        {
            UnityEngine.Object.Destroy(dirtyTexture);
            dirtyTexture = null;
        }

        RenderTexture rt = RenderTexture.GetTemporary(
            originalSourceTexture.width, // ใช้ต้นฉบับ
            originalSourceTexture.height, // ใช้ต้นฉบับ
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear
        );
        Graphics.Blit(originalSourceTexture, rt);

        dirtyTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        dirtyTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        dirtyTexture.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        totalInitialAlpha = 0f;
        currentCleanedAlpha = 0f;
        Color[] allPixels = dirtyTexture.GetPixels();

        foreach (Color color in allPixels)
        {
            totalInitialAlpha += color.a;
        }
        totalDirtyPixels = allPixels.Length;

        dirtyOverlay.sprite = Sprite.Create(
            dirtyTexture,
            new Rect(0, 0, dirtyTexture.width, dirtyTexture.height),
            Vector2.one * 0.5f,
            100f
        );
    }

    #endregion

    // ==================================================================================

    #region Sweeping and Progress Logic

    private void SweepAt(int px, int py)
    {
        int width = dirtyTexture.width;
        int height = dirtyTexture.height;
        int brushRadiusInt = Mathf.RoundToInt(brushRadius);
        float localCleanedAlpha = 0f; // Alpha ที่ถูกกำจัดในการลากครั้งนี้

        // Loop ตรวจสอบพิกเซลในรัศมีแปรง
        for (int x = px - brushRadiusInt; x <= px + brushRadiusInt; x++)
        {
            for (int y = py - brushRadiusInt; y <= py + brushRadiusInt; y++)
            {
                // ตรวจสอบให้อยู่ในขอบเขตของ Texture
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(px, py));

                    if (dist <= brushRadius)
                    {
                        if (dirtyTexture.GetPixel(x, y).a > 0.001f) // ตรวจสอบว่ายังมี Alpha เหลืออยู่
                        {
                            Color newColor = dirtyTexture.GetPixel(x, y);
                            float originalAlpha = newColor.a; // Alpha ก่อนลด

                            // คำนวณตัวคูณการลด (Brush Falloff)
                            float alphaReductionFactor = Mathf.Clamp01(1f - dist / brushRadius);

                            // คำนวณปริมาณที่ควรลด (ใช้ Fixed Reduction)
                            float reductionAmount = alphaReductionFactor * sweepStrength;

                            // ลด Alpha และป้องกันไม่ให้ติดลบ
                            newColor.a = Mathf.Max(0f, originalAlpha - reductionAmount);

                            // คำนวณปริมาณ Alpha ที่ถูกลบออกไปจริง
                            float actualReduction = originalAlpha - newColor.a;
                            localCleanedAlpha += actualReduction;

                            dirtyTexture.SetPixel(x, y, newColor);
                        }
                    }
                }
            }
        }

        if (localCleanedAlpha > 0f)
        {
            dirtyTexture.Apply(); // อัปเดต Texture
            currentCleanedAlpha += localCleanedAlpha; // อัปเดตปริมาณที่ถูกกำจัดแล้วทั้งหมด
            CalculateProgress();
        }
    }

    private void CalculateProgress()
    {
        if (totalInitialAlpha <= 0f)
        {
            currentSweepProgress = 1f; // ป้องกันการหารด้วยศูนย์
            return;
        }

        // คำนวณความคืบหน้าจากปริมาณ Alpha ที่ถูกกำจัด / Alpha เริ่มต้นทั้งหมด
        currentSweepProgress = currentCleanedAlpha / totalInitialAlpha;

        // จำกัดค่าสูงสุดไม่ให้เกิน 1.0
        currentSweepProgress = Mathf.Clamp01(currentSweepProgress);

        if (progressBar) progressBar.value = currentSweepProgress;

        if (currentSweepProgress >= requiredSweepAmount)
        {
            CompleteTask();
        }
    }

    #endregion
}