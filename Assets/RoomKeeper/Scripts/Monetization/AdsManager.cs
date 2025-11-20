using UnityEngine;
using Unity.Services.LevelPlay;
using System;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    private LevelPlayBannerAd bannerAd;
    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayRewardedAd rewardedVideoAd;

    private bool isAdsEnabled = false;
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
        if (isAdsEnabled) return;

        // Best Practice: เช็คเน็ตก่อน Init เพื่อลด Error Log รกหน้าจอ
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("No Internet Connection. Ads initialization skipped.");
            return;
        }

        LevelPlay.ValidateIntegration();

        // Subscribe Events
        LevelPlay.OnInitSuccess += SdkInitCompletedEvent;
        LevelPlay.OnInitFailed += SdkInitFailedEvent;

        LevelPlay.Init(AdConfig.AppKey);
    }

    private void OnDestroy()
    {
        // Unsubscribe เสมอเพื่อป้องกัน Memory Leak
        LevelPlay.OnInitSuccess -= SdkInitCompletedEvent;
        LevelPlay.OnInitFailed -= SdkInitFailedEvent;

        DisposeBanner();

        if (rewardedVideoAd != null)
        {
            rewardedVideoAd.OnAdRewarded -= OnAdRewarded;
            rewardedVideoAd.OnAdDisplayFailed -= OnRewardedAdDisplayFailed;
        }
    }

    private void SdkInitFailedEvent(LevelPlayInitError error)
    {
        Debug.LogError($"Ads Init Failed: {error}");
        isAdsEnabled = false;
    }

    private void SdkInitCompletedEvent(LevelPlayConfiguration configuration)
    {
        Debug.Log("Ads Init Completed");
        isAdsEnabled = true;
        InitializeAds();
    }

    void InitializeAds()
    {
        if (!isAdsEnabled) return;

        // 1. Setup Banner (แยก Method เพื่อให้เรียกสร้างใหม่ได้)
        CreateBannerAd();

        // 2. Setup Interstitial
        if (interstitialAd == null)
        {
            interstitialAd = new LevelPlayInterstitialAd(AdConfig.InterstitialAdUnitId);
            interstitialAd.OnAdLoadFailed += (error) => { Debug.Log("Interstitial Load Failed: " + error); };
            interstitialAd.OnAdLoaded += (info) => { Debug.Log("Interstitial Loaded"); };
        }

        // 3. Setup Rewarded Video
        if (rewardedVideoAd == null)
        {
            rewardedVideoAd = new LevelPlayRewardedAd(AdConfig.RewardedVideoAdUnitId);
            rewardedVideoAd.OnAdRewarded += OnAdRewarded;
            rewardedVideoAd.OnAdDisplayFailed += OnRewardedAdDisplayFailed;
        }

        // เริ่มโหลดโฆษณา
        LoadBannerAds();
        LoadInterstitialAds();
        LoadRewardedVideoAds();
    }

    #region Banner Logic

    // แยก Method สร้าง Banner ออกมา เพื่อแก้บั๊ก "ซ่อนแล้วเรียกกลับมาไม่ได้"
    private void CreateBannerAd()
    {
        if (bannerAd != null) return;

        var configBuilder = new LevelPlayBannerAd.Config.Builder();
        configBuilder.SetSize(LevelPlayAdSize.BANNER)
                     .SetPosition(LevelPlayBannerPosition.BottomCenter);

        var bannerConfig = configBuilder.Build();
        bannerAd = new LevelPlayBannerAd(AdConfig.BannerAdUnitId, bannerConfig);

        bannerAd.OnAdLoaded += OnBannerLoaded;
        bannerAd.OnAdLoadFailed += OnBannerLoadFailed;
    }

    private void DisposeBanner()
    {
        if (bannerAd != null)
        {
            bannerAd.OnAdLoaded -= OnBannerLoaded;
            bannerAd.OnAdLoadFailed -= OnBannerLoadFailed;
            bannerAd.DestroyAd();
            bannerAd = null;
        }
    }

    public void LoadBannerAds()
    {
        if (!isAdsEnabled) return;

        // *** แก้ไข: ใช้ Key จาก IAPManager เพื่อความถูกต้อง ***
        // ถ้า IAPManager ยังไม่ Compile ให้ใช้ string "RemoveAds_Owned" แทนได้
        if (PlayerPrefs.GetInt(IAPManager.KEY_REMOVE_ADS, 0) == 1)
        {
            Debug.Log("User has NoAds VIP. Skipping Banner Load.");
            HideBannerAds(); // ซ่อนของเก่าถ้ามีค้างอยู่
            return;
        }

        // *** จุดสำคัญ: ถ้า Banner ถูก Destroy ไปแล้ว ต้องสร้างใหม่ ***
        if (bannerAd == null)
        {
            CreateBannerAd();
        }

        bannerAd.LoadAd();
        bannerAd.ShowAd();
    }

    public void HideBannerAds()
    {
        if (bannerAd != null)
        {
            bannerAd.HideAd();
            DisposeBanner(); // ทำลายทิ้งเพื่อคืน Memory
        }
    }

    private void OnBannerLoaded(LevelPlayAdInfo info) { Debug.Log("Banner Loaded"); }
    private void OnBannerLoadFailed(LevelPlayAdError error) { Debug.Log("Banner Load Failed: " + error); }

    #endregion

    #region Interstitial Logic

    public void LoadInterstitialAds()
    {
        if (!isAdsEnabled) return;
        if (interstitialAd != null) interstitialAd.LoadAd();
    }

    public void ShowInterstitialAds()
    {
        if (!isAdsEnabled) return;

        // เช็ค VIP No Ads
        if (PlayerPrefs.GetInt(IAPManager.KEY_REMOVE_ADS, 0) == 1) return;

        if (interstitialAd != null && interstitialAd.IsAdReady())
        {
            interstitialAd.ShowAd();
        }
        else
        {
            Debug.Log("Interstitial Ad is not ready, reloading...");
            LoadInterstitialAds();
        }
    }

    #endregion

    #region Rewarded Video Logic

    public void LoadRewardedVideoAds()
    {
        if (!isAdsEnabled) return;
        // ตรวจสอบว่ากำลังโหลดอยู่หรือไม่ เพื่อลด Traffic
        if (rewardedVideoAd != null && !rewardedVideoAd.IsAdReady())
        {
            rewardedVideoAd.LoadAd();
        }
    }

    public void ShowRewardedVideoAds(Action onSuccess)
    {
        if (!isAdsEnabled)
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
            Debug.Log("Rewarded Ad is not ready. Reloading...");
            LoadRewardedVideoAds();
        }
    }

    private void OnRewardedAdDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        Debug.Log("Rewarded Ad Display Failed: " + error.ToString());
        onRewardCompleteCallback = null;
        LoadRewardedVideoAds();
    }

    private void OnAdRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
    {
        Debug.Log("User Earned Reward: " + reward.Name);

        onRewardCompleteCallback?.Invoke();
        onRewardCompleteCallback = null;

        LoadRewardedVideoAds();
    }

    #endregion
}