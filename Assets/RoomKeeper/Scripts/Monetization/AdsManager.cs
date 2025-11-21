using UnityEngine;
using Unity.Services.LevelPlay;
using System;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    private LevelPlayBannerAd bannerAd;
    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayRewardedAd rewardedVideoAd;

    private bool isAdsInitialized = false;
    private Action onRewardCompleteCallback;

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

        // Rewarded
        rewardedVideoAd = new LevelPlayRewardedAd(AdConfig.RewardedVideoAdUnitId);
        rewardedVideoAd.OnAdRewarded += (info, reward) => {
            Debug.Log($"Reward: {reward.Name}");
            onRewardCompleteCallback?.Invoke();
            LoadRewardedVideoAds();
        };
        rewardedVideoAd.OnAdDisplayFailed += (info, error) => LoadRewardedVideoAds();

        // Load All
        LoadBannerAds();
        LoadInterstitialAds();
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

    // --- Public Methods ---

    public void LoadBannerAds()
    {
        if (!isAdsInitialized) return;

        // เช็คสถานะ Remove Ads ผ่าน IAPManager (ซึ่งเช็คจาก StoreController อีกที)
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

    public void ShowInterstitialAds()
    {
        if (!isAdsInitialized) return;

        if (IAPManager.Instance != null && IAPManager.Instance.IsOwned(IAPManager.ID_REMOVE_ADS))
        {
            Debug.Log("Premium User - No Interstitial");
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

    public void ShowRewardedVideoAds(Action onSuccess)
    {
        if (!isAdsInitialized) return;

        if (rewardedVideoAd != null && rewardedVideoAd.IsAdReady())
        {
            onRewardCompleteCallback = onSuccess;
            rewardedVideoAd.ShowAd();
        }
        else
        {
            LoadRewardedVideoAds();
        }
    }

    public void LoadRewardedVideoAds()
    {
        if (rewardedVideoAd != null && !rewardedVideoAd.IsAdReady())
            rewardedVideoAd.LoadAd();
    }
}