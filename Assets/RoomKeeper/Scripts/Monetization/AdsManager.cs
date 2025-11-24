using UnityEngine;
using Unity.Services.LevelPlay;
using System;
using System.Collections;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    [Header("Debug Settings")]
    public bool enableDebugLogs = true;

    // สถานะสำหรับ GameCoreManager เอาไปใช้ทำ Soft Pause
    public bool IsAdShowing { get; private set; }

    private LevelPlayBannerAd bannerAd;
    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayRewardedAd rewardedVideoAd;

    private bool isAdsInitialized = false;

    private Action<double> onRewardCompleteCallback;

    private bool _rewardEventTriggered = false;
    private double _earnedRewardAmount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // ขั้นตอนที่ 1: ตรวจสอบ Integration (จะแจ้งเตือนใน Console ถ้าขาด Adapter ตัวไหน)
        LevelPlay.ValidateIntegration();

        LevelPlay.OnInitSuccess += SdkInitCompletedEvent;
        LevelPlay.OnInitFailed += SdkInitFailedEvent;

        DebugLog("Initializing LevelPlay SDK...");
        // ขั้นตอนที่ 2: เริ่มต้น SDK ด้วย App Key จาก Config
        LevelPlay.Init(AdConfig.AppKey);
    }

    private void OnDestroy()
    {
        LevelPlay.OnInitSuccess -= SdkInitCompletedEvent;
        LevelPlay.OnInitFailed -= SdkInitFailedEvent;
        DisposeBanner();

        if (rewardedVideoAd != null)
        {
            rewardedVideoAd.OnAdRewarded -= OnAdRewarded;
            rewardedVideoAd.OnAdDisplayFailed -= OnAdDisplayFailed;
            rewardedVideoAd.OnAdDisplayed -= OnAdDisplayed;
            rewardedVideoAd.OnAdClosed -= OnAdClosed;
        }

        if (interstitialAd != null)
        {
            interstitialAd.OnAdDisplayed -= OnAdDisplayed;
            interstitialAd.OnAdClosed -= OnAdClosed;
        }
    }

    private void SdkInitFailedEvent(LevelPlayInitError error)
    {
        DebugLog($"Init Failed: {error.ErrorMessage}");
        isAdsInitialized = false;
    }

    private void SdkInitCompletedEvent(LevelPlayConfiguration configuration)
    {
        DebugLog("Init Completed");
        isAdsInitialized = true;

        // ขั้นตอนที่ 3: เมื่อ SDK พร้อม ให้สร้างและโหลด Ad Units ทันที
        InitializeAdUnits();
    }

    void InitializeAdUnits()
    {
        if (!isAdsInitialized) return;

        CreateBannerAd();

        // Setup Interstitial
        interstitialAd = new LevelPlayInterstitialAd(AdConfig.InterstitialAdUnitId);
        interstitialAd.OnAdDisplayed += OnAdDisplayed;
        interstitialAd.OnAdClosed += OnAdClosed;

        // Setup Rewarded Video
        rewardedVideoAd = new LevelPlayRewardedAd(AdConfig.RewardedVideoAdUnitId);
        rewardedVideoAd.OnAdRewarded += OnAdRewarded;
        rewardedVideoAd.OnAdDisplayFailed += OnAdDisplayFailed;
        rewardedVideoAd.OnAdDisplayed += OnAdDisplayed;
        rewardedVideoAd.OnAdClosed += OnAdClosed;

        // Load Ads ล่วงหน้า (Preload) เพื่อ Performance ที่ดี
        LoadBannerAds();
        LoadInterstitialAds();
        LoadRewardedVideoAds();
    }

    // --- Core Logic ---

    private void OnAdDisplayed(LevelPlayAdInfo info)
    {
        IsAdShowing = true;
        _rewardEventTriggered = false;

        HideBannerAds(); // ซ่อน Banner ไม่ให้บัง
        GameUIManager.Instance?.SwitchUIState(UIState.Ads);

        Time.timeScale = 1f; // ปล่อยเวลาให้เดิน เพื่อให้ SDK ทำงานได้
        AudioListener.pause = true; // แต่ปิดเสียงเกม

        DebugLog($"Ad Displayed ({info.AdUnitId})");
    }

    private void OnAdRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
    {
        _earnedRewardAmount = reward.Amount;
        _rewardEventTriggered = true;
        DebugLog($"<color=green>OnAdRewarded Triggered. Amount: {_earnedRewardAmount}</color>");
    }

    private void OnAdClosed(LevelPlayAdInfo info)
    {
        DebugLog("Ad Closed. Waiting for potential late reward...");
        // เรียก Coroutine เพื่อรอเช็คผลรางวัล
        StartCoroutine(WaitAndProcessRewardRoutine());
    }

    private IEnumerator WaitAndProcessRewardRoutine()
    {
        // รอ 0.5 วินาที (Realtime) เผื่อ Reward มาช้ากว่า Event Close
        yield return new WaitForSecondsRealtime(0.1f);

        bool wasSuccessful = _rewardEventTriggered;

        // จบการแสดงผล
        IsAdShowing = false;
        AudioListener.pause = false;

        if (wasSuccessful)
        {
            DebugLog($"Processing Reward -> <color=green>SUCCESS ({_earnedRewardAmount})</color>");
            onRewardCompleteCallback?.Invoke(_earnedRewardAmount);
        }
        else
        {
            DebugLog("Processing Reward -> <color=red>FAILED / SKIPPED</color>");
        }

        // Cleanup
        onRewardCompleteCallback = null;
        _rewardEventTriggered = false;
        _earnedRewardAmount = 0;

        // โหลดโฆษณาตัวถัดไปมารอ
        LoadBannerAds();
        LoadRewardedVideoAds();

        // แจ้ง UI ให้ทำงานต่อ
        GameUIManager.Instance?.ResumeFromAds(wasSuccessful);
    }

    private void OnAdDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        DebugLog($"Ad Display Failed: {error.ErrorMessage}");
        IsAdShowing = false;
        AudioListener.pause = false;

        onRewardCompleteCallback = null;
        _rewardEventTriggered = false;

        LoadBannerAds();
        GameUIManager.Instance?.ResumeFromAds(false);
        LoadRewardedVideoAds();
    }

    // --- Banner Logic ---

    private void CreateBannerAd()
    {
        DisposeBanner();
        // สร้าง Config Banner (วางด้านล่าง)
        var config = new LevelPlayBannerAd.Config.Builder()
            .SetSize(LevelPlayAdSize.BANNER)
            .SetPosition(LevelPlayBannerPosition.BottomCenter)
            .Build();

        bannerAd = new LevelPlayBannerAd(AdConfig.BannerAdUnitId, config);
        bannerAd.OnAdLoadFailed += (error) => DebugLog($"Banner Load Failed: {error.ErrorMessage}");
    }

    private void DisposeBanner()
    {
        if (bannerAd != null)
        {
            bannerAd.DestroyAd();
            bannerAd = null;
        }
    }

    public void LoadBannerAds()
    {
        if (!isAdsInitialized) return;
        // เช็ค IAP No Ads
        if (IAPManager.Instance != null && IAPManager.Instance.IsOwned(IAPManager.ID_REMOVE_ADS))
        {
            HideBannerAds();
            return;
        }
        if (bannerAd == null) CreateBannerAd();
        bannerAd.LoadAd();
        bannerAd.ShowAd();
    }

    public void HideBannerAds()
    {
        if (bannerAd != null)
        {
            bannerAd.HideAd();
            DisposeBanner();
        }
    }

    // --- Interstitial Logic ---

    public void ShowInterstitialAds()
    {
        if (!isAdsInitialized) return;
        if (IAPManager.Instance != null && IAPManager.Instance.IsOwned(IAPManager.ID_REMOVE_ADS)) return;

        if (interstitialAd != null && interstitialAd.IsAdReady())
            interstitialAd.ShowAd();
        else
            LoadInterstitialAds();
    }

    public void LoadInterstitialAds()
    {
        interstitialAd?.LoadAd();
    }

    // --- Rewarded Video Logic ---

    public void ShowRewardedVideoAds(Action<double> onSuccess)
    {
        _rewardEventTriggered = false;
        _earnedRewardAmount = 0;

        if (!isAdsInitialized)
        {
            DebugLog("SDK Not Initialized");
            return;
        }

        onRewardCompleteCallback = onSuccess;

        if (rewardedVideoAd != null && rewardedVideoAd.IsAdReady())
        {
            DebugLog("Showing Rewarded Video...");
            rewardedVideoAd.ShowAd();
        }
        else
        {
            DebugLog("Rewarded Not Ready. Reloading...");
            onRewardCompleteCallback = null;
            // ลองโหลดใหม่เผื่อรอบหน้าจะพร้อม
            LoadRewardedVideoAds();
        }
    }

    public void LoadRewardedVideoAds()
    {
        if (rewardedVideoAd != null && !rewardedVideoAd.IsAdReady())
            rewardedVideoAd.LoadAd();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"<color=#00FF00>[AdsManager]</color> {message}");
    }
}