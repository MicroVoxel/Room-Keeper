using UnityEngine;
using Unity.Services.LevelPlay;
using System;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    public bool IsAdShowing { get; private set; }

    private LevelPlayBannerAd bannerAd;
    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayRewardedAd rewardedVideoAd;

    private bool isAdsInitialized = false;
    private Action onRewardCompleteCallback;

    private bool _isRewardClaimedInSession = false;

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
        if (Application.internetReachability == NetworkReachability.NotReachable) return;

        LevelPlay.OnInitSuccess += SdkInitCompletedEvent;
        LevelPlay.OnInitFailed += SdkInitFailedEvent;
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

    private void SdkInitFailedEvent(LevelPlayInitError error) => isAdsInitialized = false;

    private void SdkInitCompletedEvent(LevelPlayConfiguration configuration)
    {
        Debug.Log("[AdsManager] Init Completed");
        isAdsInitialized = true;
        InitializeAdUnits();
    }

    void InitializeAdUnits()
    {
        if (!isAdsInitialized) return;

        // Banner
        CreateBannerAd();

        // Interstitial
        interstitialAd = new LevelPlayInterstitialAd(AdConfig.InterstitialAdUnitId);
        interstitialAd.OnAdDisplayed += OnAdDisplayed;
        interstitialAd.OnAdClosed += OnAdClosed;

        // Rewarded
        rewardedVideoAd = new LevelPlayRewardedAd(AdConfig.RewardedVideoAdUnitId);
        rewardedVideoAd.OnAdRewarded += OnAdRewarded;
        rewardedVideoAd.OnAdDisplayFailed += OnAdDisplayFailed;
        rewardedVideoAd.OnAdDisplayed += OnAdDisplayed;
        rewardedVideoAd.OnAdClosed += OnAdClosed;

        LoadBannerAds();
        LoadInterstitialAds();
        LoadRewardedVideoAds();
    }

    // --- Pause / Resume Logic ---

    private void OnAdDisplayed(LevelPlayAdInfo info)
    {
        IsAdShowing = true;

        // 1. ซ่อน Banner ทันทีที่โฆษณาเต็มจอเด้งขึ้นมา
        HideBannerAds();

        // 2. สั่ง UI Manager ให้ปิด Panel ทั้งหมด
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.SwitchUIState(UIState.Ads);
        }

        // 3. บังคับเวลาเดิน เพื่อให้ Test Ad ทำงานได้
        Time.timeScale = 1f;
        AudioListener.pause = true;

        Debug.Log("Ad Displayed -> UI Hidden, Banner Hidden");
    }

    private void OnAdClosed(LevelPlayAdInfo info)
    {
        IsAdShowing = false;
        AudioListener.pause = false;

        // เรียก Banner กลับมาแสดงใหม่เมื่อดูจบ (ฟังก์ชัน LoadBannerAds จะเช็ค IAP ให้เอง ถ้าซื้อแล้วจะไม่โชว์)
        LoadBannerAds();

        // เช็ค Reward
        if (_isRewardClaimedInSession && onRewardCompleteCallback != null)
        {
            onRewardCompleteCallback.Invoke();
        }

        // แจ้ง UI Manager
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ResumeFromAds(_isRewardClaimedInSession);
        }

        onRewardCompleteCallback = null;
        LoadRewardedVideoAds();

        Debug.Log($"Ad Closed -> Banner Reloaded Check, Reward Claimed: {_isRewardClaimedInSession}");
    }

    // ----------------------------

    private void OnAdRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
    {
        Debug.Log($"Reward Earned: {reward.Name}");
        _isRewardClaimedInSession = true;
    }

    private void OnAdDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        Debug.LogWarning($"Rewarded Video Display Failed: {error.ErrorMessage}");

        IsAdShowing = false;
        AudioListener.pause = false;

        LoadBannerAds();

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ResumeFromAds(false);
        }

        onRewardCompleteCallback = null;
        LoadRewardedVideoAds();
    }

    private void CreateBannerAd()
    {
        DisposeBanner();
        var config = new LevelPlayBannerAd.Config.Builder()
            .SetSize(LevelPlayAdSize.BANNER)
            .SetPosition(LevelPlayBannerPosition.BottomCenter)
            .Build();
        bannerAd = new LevelPlayBannerAd(AdConfig.BannerAdUnitId, config);
        bannerAd.OnAdLoadFailed += (error) => Debug.LogWarning($"Banner Load Failed: {error}");
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

        // [IAP Check] ตรวจสอบสถานะ Remove Ads
        // ถ้าผู้เล่นซื้อแพ็กเกจ VIP4 (Remove Ads) แล้ว ให้ซ่อน Banner และออกจากฟังก์ชันทันที
        if (IAPManager.Instance != null && IAPManager.Instance.IsOwned(IAPManager.ID_REMOVE_ADS))
        {
            HideBannerAds(); // make sure it's hidden
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

    public void ShowInterstitialAds()
    {
        if (!isAdsInitialized) return;

        // [IAP Check] ตรวจสอบสถานะ Remove Ads
        // ถ้าซื้อ Remove Ads แล้ว จะไม่โชว์ Interstitial (โฆษณาคั่น)
        if (IAPManager.Instance != null && IAPManager.Instance.IsOwned(IAPManager.ID_REMOVE_ADS))
        {
            Debug.Log("[AdsManager] User has Remove Ads. Skipping Interstitial.");
            return;
        }

        if (interstitialAd != null && interstitialAd.IsAdReady())
            interstitialAd.ShowAd();
        else
            LoadInterstitialAds();
    }

    public void LoadInterstitialAds()
    {
        if (interstitialAd != null) interstitialAd.LoadAd();
    }

    // Rewarded Video ปกติจะไม่เช็ค Remove Ads เพราะเป็นการสมัครใจดูแลกของรางวัล
    public void ShowRewardedVideoAds(Action onSuccess)
    {
        _isRewardClaimedInSession = false;

        if (!isAdsInitialized)
        {
            Debug.LogWarning("Ads SDK not initialized yet.");
            return;
        }

        if (rewardedVideoAd != null && rewardedVideoAd.IsAdReady())
        {
            onRewardCompleteCallback = onSuccess;
            rewardedVideoAd.ShowAd();
        }
        else
        {
            Debug.Log("Rewarded Ad not ready, reloading...");
            LoadRewardedVideoAds();
        }
    }

    public void LoadRewardedVideoAds()
    {
        if (rewardedVideoAd != null && !rewardedVideoAd.IsAdReady())
            rewardedVideoAd.LoadAd();
    }
}