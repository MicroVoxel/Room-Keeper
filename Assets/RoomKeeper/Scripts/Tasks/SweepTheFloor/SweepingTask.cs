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

    // --- Performance Optimization ---
    private Texture2D dirtyTexture;
    private Texture2D originalSourceTexture;

    // We will operate on this C# array, which is much faster than GetPixel/SetPixel
    private Color[] pixelData;
    private int textureWidth;
    private int textureHeight;
    // --- End Performance Optimization ---

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
        // pixelData (Color[]) is managed by GC, no need to manually destroy
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
        if (dirtyTexture == null || pixelData == null) return; // Check pixelData too

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
        // Clear the C# array
        pixelData = null;
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

        // --- Create a copy (same as before) ---
        RenderTexture rt = RenderTexture.GetTemporary(
            originalSourceTexture.width,
            originalSourceTexture.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear
        );
        Graphics.Blit(originalSourceTexture, rt);

        dirtyTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        textureWidth = rt.width; // Store width
        textureHeight = rt.height; // Store height

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        dirtyTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        dirtyTexture.Apply(); // Apply the ReadPixels
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        // --- PERFORMANCE STEP 1: Get all pixels into C# array ONCE ---
        pixelData = dirtyTexture.GetPixels();
        // -----------------------------------------------------------

        totalInitialAlpha = 0f;
        currentCleanedAlpha = 0f;

        // Calculate total alpha from the C# array (faster)
        foreach (Color color in pixelData)
        {
            totalInitialAlpha += color.a;
        }
        totalDirtyPixels = pixelData.Length;

        // Update the UI Sprite
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
        int brushRadiusInt = Mathf.RoundToInt(brushRadius);
        float localCleanedAlpha = 0f; // Alpha ที่ถูกกำจัดในการลากครั้งนี้
        bool pixelChanged = false; // Flag to check if we need to Apply()

        // Loop ตรวจสอบพิกเซลในรัศมีแปรง
        for (int x = px - brushRadiusInt; x <= px + brushRadiusInt; x++)
        {
            for (int y = py - brushRadiusInt; y <= py + brushRadiusInt; y++)
            {
                // ตรวจสอบให้อยู่ในขอบเขตของ Texture
                if (x >= 0 && x < textureWidth && y >= 0 && y < textureHeight)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(px, py));

                    if (dist <= brushRadius)
                    {
                        // --- PERFORMANCE STEP 2: Operate on the C# array ---
                        // Convert 2D (x,y) coord to 1D array index
                        int index = y * textureWidth + x;

                        Color currentColor = pixelData[index];

                        if (currentColor.a > 0.001f) // ตรวจสอบว่ายังมี Alpha เหลืออยู่
                        {
                            float originalAlpha = currentColor.a; // Alpha ก่อนลด

                            // คำนวณตัวคูณการลด (Brush Falloff)
                            float alphaReductionFactor = Mathf.Clamp01(1f - dist / brushRadius);

                            // คำนวณปริมาณที่ควรลด (ใช้ Fixed Reduction)
                            float reductionAmount = alphaReductionFactor * sweepStrength;

                            // ลด Alpha และป้องกันไม่ให้ติดลบ
                            currentColor.a = Mathf.Max(0f, originalAlpha - reductionAmount);

                            // คำนวณปริมาณ Alpha ที่ถูกลบออกไปจริง
                            float actualReduction = originalAlpha - currentColor.a;

                            if (actualReduction > 0)
                            {
                                localCleanedAlpha += actualReduction;
                                pixelData[index] = currentColor; // Write back to C# array
                                pixelChanged = true;
                            }
                        }
                    }
                }
            }
        }

        if (pixelChanged) // Only Apply if changes were made
        {
            // --- PERFORMANCE STEP 3: Upload the entire array in one go ---
            dirtyTexture.SetPixels(pixelData);

            // --- PERFORMANCE STEP 4: Apply without mipmaps ---
            dirtyTexture.Apply(false);

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