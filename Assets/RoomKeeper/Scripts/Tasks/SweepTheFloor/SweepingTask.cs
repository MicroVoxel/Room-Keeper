using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class SweepingTask : TaskBase, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    #region UI & Settings

    [Header("Sweeping Minigame Settings")]
    public float requiredSweepAmount = 0.9f;
    [Range(0.01f, 1.0f)]
    public float sweepStrength = 0.1f;

    [SerializeField] private float currentSweepProgress = 0f;

    [Header("UI References")]
    public Image dirtyOverlay;
    public Slider progressBar;

    [Header("Brush Settings")]
    public float brushRadius = 50f;

    [Header("Audio")]
    [Tooltip("เสียงเมื่อถูพื้น (จะเล่นแบบ Loop ขณะถู)")]
    public AudioClip sweepSound;
    [Tooltip("เสียงเมื่อทำความสะอาดเสร็จ")]
    public AudioClip completeSound;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // --- Performance Optimization ---
    private Texture2D dirtyTexture;
    private Texture2D originalSourceTexture;
    private Color[] pixelData;
    private int textureWidth;
    private int textureHeight;

    private int totalDirtyPixels;
    private float totalInitialAlpha = 0f;
    private float currentCleanedAlpha = 0f;

    // --- [NEW] Audio Control ---
    private AudioSource continuousAudioSource; // ตัวแหล่งกำเนิดเสียงส่วนตัว
    private float lastDirtTime = 0f; // เวลาล่าสุดที่ถูโดนสิ่งสกปรก

    #endregion

    // ==================================================================================

    private void Awake()
    {
        if (dirtyOverlay != null && dirtyOverlay.mainTexture is Texture2D texture)
        {
            originalSourceTexture = texture;
        }
        else
        {
            Debug.LogError("Please ensure 'Dirty Overlay' has a Texture2D assigned.", this);
        }

        // [NEW] สร้าง AudioSource ส่วนตัวไว้สำหรับเสียงถู (Loop)
        continuousAudioSource = gameObject.AddComponent<AudioSource>();
        continuousAudioSource.loop = true; // ตั้งให้เล่นวนซ้ำ
        continuousAudioSource.playOnAwake = false;
    }

    override protected void Start()
    {
        base.Start();

        // [NEW] ตั้งค่า AudioSource ให้ตรงกับ AudioManager (เพื่อให้ Slider ปรับเสียงได้)
        if (AudioManager.Instance != null)
        {
            continuousAudioSource.outputAudioMixerGroup = AudioManager.Instance.SFXGroup;
        }
    }

    public override void Open()
    {
        if (IsCompleted) return;
        base.Open();

        if (!IsCompleted)
        {
            InitializeDirtyTexture();
            currentSweepProgress = 0f;
            if (progressBar) progressBar.value = 0f;
        }
    }

    public override void Close()
    {
        base.Close();
        if (!IsCompleted) ResetTask();

        // [NEW] หยุดเสียงทันทีเมื่อปิด Task
        if (continuousAudioSource != null) continuousAudioSource.Stop();
    }

    private void OnDestroy()
    {
        if (dirtyTexture != null) Destroy(dirtyTexture);
    }

    // ==================================================================================

    private void Update()
    {
        // [NEW] Logic สำหรับหยุดเสียงเมื่อ "ไม่ได้ถูโดนสิ่งสกปรก" สักพักหนึ่ง
        // (เช่น ลากเม้าส์ค้างไว้แต่ลากไปตรงที่สะอาดแล้ว หรือลากเม้าส์นิ่งๆ)
        if (continuousAudioSource.isPlaying)
        {
            // ถ้าเวลาผ่านไปเกิน 0.15 วิ โดยไม่ได้ถูโดนอะไรเลย ให้หยุดเสียง
            if (Time.time - lastDirtTime > 0.15f)
            {
                continuousAudioSource.Stop();
            }
        }
    }

    // ==================================================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        // เริ่มต้นการลาก
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsOpen || IsCompleted) return;
        if (dirtyTexture == null || pixelData == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dirtyOverlay.rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        Vector2 textureCoord = new Vector2(
            (localPoint.x - dirtyOverlay.rectTransform.rect.x) * (dirtyTexture.width / dirtyOverlay.rectTransform.rect.width),
            (localPoint.y - dirtyOverlay.rectTransform.rect.y) * (dirtyTexture.height / dirtyOverlay.rectTransform.rect.height)
        );

        SweepAt(Mathf.RoundToInt(textureCoord.x), Mathf.RoundToInt(textureCoord.y));
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // [NEW] หยุดเสียงทันทีเมื่อปล่อยมือ
        if (continuousAudioSource != null && continuousAudioSource.isPlaying)
        {
            continuousAudioSource.Stop();
        }
    }

    // ==================================================================================

    private void ResetTask()
    {
        currentCleanedAlpha = 0f;
        currentSweepProgress = 0f;
        if (progressBar) progressBar.value = 0f;

        if (dirtyTexture != null)
        {
            Destroy(dirtyTexture);
            dirtyTexture = null;
        }
        pixelData = null;
    }

    private void InitializeDirtyTexture()
    {
        if (originalSourceTexture == null) return;

        if (dirtyTexture != null)
        {
            Destroy(dirtyTexture);
            dirtyTexture = null;
        }

        RenderTexture rt = RenderTexture.GetTemporary(
            originalSourceTexture.width,
            originalSourceTexture.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear
        );
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

        pixelData = dirtyTexture.GetPixels();

        totalInitialAlpha = 0f;
        currentCleanedAlpha = 0f;

        foreach (Color color in pixelData)
        {
            totalInitialAlpha += color.a;
        }
        totalDirtyPixels = pixelData.Length;

        dirtyOverlay.sprite = Sprite.Create(
            dirtyTexture,
            new Rect(0, 0, dirtyTexture.width, dirtyTexture.height),
            Vector2.one * 0.5f,
            100f
        );
    }

    // ==================================================================================

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
            // --- [NEW] Logic การเล่นเสียงแบบต่อเนื่อง ---
            lastDirtTime = Time.time; // บันทึกเวลาว่า "พึ่งจะถูโดนสิ่งสกปรกนะ"

            if (continuousAudioSource != null)
            {
                // ถ้าเสียงยังไม่เล่น ให้เล่นเลย
                if (!continuousAudioSource.isPlaying && sweepSound != null)
                {
                    continuousAudioSource.clip = sweepSound;
                    continuousAudioSource.volume = sfxVolume;
                    continuousAudioSource.Play();
                }
            }

            dirtyTexture.SetPixels(pixelData);
            dirtyTexture.Apply(false);

            currentCleanedAlpha += localCleanedAlpha;
            CalculateProgress();
        }
    }

    private void CalculateProgress()
    {
        if (totalInitialAlpha <= 0f)
        {
            currentSweepProgress = 1f;
            return;
        }

        currentSweepProgress = currentCleanedAlpha / totalInitialAlpha;
        currentSweepProgress = Mathf.Clamp01(currentSweepProgress);

        if (progressBar) progressBar.value = currentSweepProgress;

        if (currentSweepProgress >= requiredSweepAmount)
        {
            PlayCompleteSound();
            CompleteTask();

            // [NEW] จบเกมแล้วหยุดเสียงถูทันที
            if (continuousAudioSource != null) continuousAudioSource.Stop();
        }
    }

    private void PlayCompleteSound()
    {
        if (AudioManager.Instance != null && completeSound != null)
        {
            AudioManager.Instance.PlaySFX(completeSound, sfxVolume, 0f);
        }
    }
}