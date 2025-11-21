using UnityEngine;
using UnityEngine.Purchasing; // ต้องมีเพื่อเรียก CodelessIAPStoreListener

public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    // --- Product IDs ---
    public const string ID_VIP1 = "vip1";
    public const string ID_VIP2 = "vip2";
    public const string ID_VIP3 = "vip3";
    public const string ID_REMOVE_ADS = "vip4";

    // --- Save Keys (สำหรับ Cache หรือใช้เช็ค Offline) ---
    public const string KEY_VIP1 = "VIP1_Owned";
    public const string KEY_VIP2 = "VIP2_Owned";
    public const string KEY_VIP3 = "VIP3_Owned";
    public const string KEY_REMOVE_ADS = "VIP4_Owned";

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

    // สั่งซื้อผ่าน Codeless Listener โดยตรง
    public void BuyProduct(string productId)
    {
        if (CodelessIAPStoreListener.Instance != null)
        {
            // ฟังก์ชันนี้ของ Codeless จะจัดการ Process การซื้อให้ทั้งหมด
            CodelessIAPStoreListener.Instance.InitiatePurchase(productId);
        }
        else
        {
            Debug.LogError("[IAPManager] CodelessIAPStoreListener not ready. Check IAP Catalog.");
        }
    }

    // เช็คความเป็นเจ้าของผ่าน StoreController
    public bool IsOwned(string productId)
    {
        // 1. เช็คจาก StoreController (Real-time และแม่นยำที่สุด)
        // หมายเหตุ: เราเข้าถึง StoreController ผ่าน Instance ของ Codeless
        var controller = CodelessIAPStoreListener.Instance.StoreController;

        if (controller != null)
        {
            var product = controller.products.WithID(productId);

            // ถ้ามี Receipt แสดงว่าซื้อแล้ว (Non-Consumable / Subscription)
            if (product != null && product.hasReceipt)
            {
                // อัปเดต Local Save เผื่อไว้
                SyncLocalSave(productId, true);
                return true;
            }
        }

        // 2. Fallback มาเช็ค Local Save (กรณี Offline หรือ IAP ยัง Init ไม่เสร็จ)
        switch (productId)
        {
            case ID_VIP1: return PlayerPrefs.GetInt(KEY_VIP1, 0) == 1;
            case ID_VIP2: return PlayerPrefs.GetInt(KEY_VIP2, 0) == 1;
            case ID_VIP3: return PlayerPrefs.GetInt(KEY_VIP3, 0) == 1;
            case ID_REMOVE_ADS: return PlayerPrefs.GetInt(KEY_REMOVE_ADS, 0) == 1;
            default: return false;
        }
    }

    public void RestorePurchases()
    {
        var controller = CodelessIAPStoreListener.Instance.StoreController;
        if (controller == null) return;

#if UNITY_IOS || UNITY_IPHONE
        var apple = controller.extensions.GetExtension<IAppleExtensions>();
        apple?.RestoreTransactions((result, error) => {
            Debug.Log($"[IAPManager] Restore Result: {result}");
        });
#endif
    }

    // Helper: บันทึกสถานะลงเครื่องเพื่อให้เช็คได้ตอน Offline
    private void SyncLocalSave(string productId, bool isOwned)
    {
        int value = isOwned ? 1 : 0;
        switch (productId)
        {
            case ID_VIP1: PlayerPrefs.SetInt(KEY_VIP1, value); break;
            case ID_VIP2: PlayerPrefs.SetInt(KEY_VIP2, value); break;
            case ID_VIP3: PlayerPrefs.SetInt(KEY_VIP3, value); break;
            case ID_REMOVE_ADS:
                PlayerPrefs.SetInt(KEY_REMOVE_ADS, value);
                // ถ้าซื้อแล้ว สั่งปิด Ads ทันที
                if (isOwned && AdsManager.Instance != null)
                    AdsManager.Instance.HideBannerAds();
                break;
        }
        PlayerPrefs.Save();
    }
}