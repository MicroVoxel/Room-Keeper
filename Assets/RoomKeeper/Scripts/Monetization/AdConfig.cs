using UnityEngine;

public static class AdConfig
{
    // --- App Keys ---
    private const string ANDROID_APP_KEY = "245292935";
    private const string IOS_APP_KEY = "245950835"; // <--- ตรวจสอบ

    // --- Banner IDs ---
    private const string ANDROID_BANNER_ID = "pqcyn9r4u354xafh";
    private const string IOS_BANNER_ID = "ds2606ub132bvhya"; // <--- ตรวจสอบ

    // --- Interstitial IDs ---
    private const string ANDROID_INTERSTITIAL_ID = "nfa2f38z1c2i3mco";
    private const string IOS_INTERSTITIAL_ID = "b8kjge4s07a0qqjg"; // <--- ตรวจสอบ

    // --- Rewarded Video IDs ---
    private const string ANDROID_REWARDED_ID = "8x4me4bekc1v1xk5";
    private const string IOS_REWARDED_ID = "nkstisygwsd0h6tb"; // <--- ตรวจสอบ

    public static string AppKey
    {
        get
        {
#if UNITY_ANDROID
            return ANDROID_APP_KEY;
#elif UNITY_IOS
            return IOS_APP_KEY;
#else
            return "unexpected_platform";
#endif
        }
    }

    public static string BannerAdUnitId
    {
        get
        {
#if UNITY_ANDROID
            return ANDROID_BANNER_ID;
#elif UNITY_IOS
            return IOS_BANNER_ID;
#else
            return "unexpected_platform";
#endif
        }
    }

    public static string InterstitialAdUnitId
    {
        get
        {
#if UNITY_ANDROID
            return ANDROID_INTERSTITIAL_ID;
#elif UNITY_IOS
            return IOS_INTERSTITIAL_ID;
#else
            return "unexpected_platform";
#endif
        }
    }

    public static string RewardedVideoAdUnitId
    {
        get
        {
#if UNITY_ANDROID
            return ANDROID_REWARDED_ID;
#elif UNITY_IOS
            return IOS_REWARDED_ID;
#else
            return "unexpected_platform";
#endif
        }
    }
}