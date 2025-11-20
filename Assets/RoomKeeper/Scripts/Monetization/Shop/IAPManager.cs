#pragma warning disable CS0618

using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using Unity.Services.Core;
using Unity.Services.Core.Environments;

public class IAPManager : MonoBehaviour, IDetailedStoreListener
{
    public static IAPManager Instance { get; private set; }

    private static IStoreController storeController;
    private static IExtensionProvider storeExtensionProvider;

    // --- Keys for PlayerPrefs ---
    public const string KEY_VIP1 = "VIP1_Owned";
    public const string KEY_VIP2 = "VIP2_Owned";
    public const string KEY_VIP3 = "VIP3_Owned";
    public const string KEY_REMOVE_ADS = "RemoveAds_Owned";

    // --- Product IDs ---
    public const string ID_VIP1 = "vip1";
    public const string ID_VIP2 = "vip2";
    public const string ID_VIP3 = "vip3";
    public const string ID_REMOVE_ADS = "vip4";

    public bool IsInitialized => storeController != null && storeExtensionProvider != null;

    // Events
    public event Action<string> OnPurchaseSuccessEvent;
    public event Action OnRestoreFinishedEvent;

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        await InitUnityServices();
    }

    private async System.Threading.Tasks.Task InitUnityServices()
    {
        try
        {
            var options = new InitializationOptions().SetEnvironmentName("production");
            await UnityServices.InitializeAsync(options);

            InitializePurchasing();
        }
        catch (Exception e)
        {
            Debug.LogError($"Unity Services Init Failed: {e.Message}");
        }
    }

    private void InitializePurchasing()
    {
        if (IsInitialized) return;

        // --- ใช้ StandardPurchasingModule + FakeStore UI เปิดใช้งานใน Editor ---
        var module = StandardPurchasingModule.Instance();
#if UNITY_EDITOR
        module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser; // เปิด UI Buy/Cancel
#endif
        var builder = ConfigurationBuilder.Instance(module);

        // Add products
        builder.AddProduct(ID_VIP1, ProductType.NonConsumable);
        builder.AddProduct(ID_VIP2, ProductType.NonConsumable);
        builder.AddProduct(ID_VIP3, ProductType.NonConsumable);
        builder.AddProduct(ID_REMOVE_ADS, ProductType.NonConsumable);

        UnityPurchasing.Initialize(this, builder);
    }

    #region Public APIs

    public void BuyProduct(string productId)
    {
        if (IsInitialized)
        {
            Product product = storeController.products.WithID(productId);

            if (product != null && product.availableToPurchase)
            {
                Debug.Log($"Buying: {product.definition.id}");
                storeController.InitiatePurchase(product);
            }
            else
            {
                Debug.LogError("BuyProduct FAIL: Product not found or unavailable.");
            }
        }
        else
        {
            Debug.LogError("BuyProduct FAIL: IAP not initialized.");
        }
    }

    public void RestorePurchases()
    {
        if (!IsInitialized)
        {
            Debug.LogError("RestorePurchases FAIL: Not initialized.");
            return;
        }

#if UNITY_IOS || UNITY_STANDALONE_OSX
        Debug.Log("Restoring purchases...");
        var apple = storeExtensionProvider.GetExtension<IAppleExtensions>();
        apple.RestoreTransactions((result, error) =>
        {
            Debug.Log($"Restore Result: {result}. {error}");
            CheckAllEntitlements();
            OnRestoreFinishedEvent?.Invoke();
        });
#else
        Debug.Log("RestorePurchases: Auto-restore on Android/Windows.");
#endif
    }

    public string GetProductPrice(string productId)
    {
        if (!IsInitialized) return "-";
        Product product = storeController.products.WithID(productId);
        return product != null ? product.metadata.localizedPriceString : "-";
    }

    private void CheckAllEntitlements()
    {
        if (storeController == null) return;
        foreach (var product in storeController.products.all)
        {
            if (product.hasReceipt)
            {
                ProcessSuccessfulPurchase(product.definition.id);
            }
        }
    }

    #endregion

    #region IDetailedStoreListener Implementation

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        Debug.Log("IAP Initialized Successfully.");
        storeController = controller;
        storeExtensionProvider = extensions;

        // --- Register Deferred Listener ทั้ง iOS และ Editor ---
        var apple = extensions.GetExtension<IAppleExtensions>();
        if (apple != null)
            apple.RegisterPurchaseDeferredListener(OnPurchaseDeferred);

        CheckAllEntitlements();
        OnRestoreFinishedEvent?.Invoke();
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogError($"IAP Init Failed: {error}");
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogError($"IAP Init Failed: {error}. Message: {message}");
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string productId = args.purchasedProduct.definition.id;
        Debug.Log($"ProcessPurchase Success: {productId}");

        ProcessSuccessfulPurchase(productId);

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogError($"Purchase Failed: {product.definition.id}, Reason: {failureReason}");
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.LogError($"Purchase Failed: {product.definition.id}, Reason: {failureDescription.reason}, Message: {failureDescription.message}");
    }

    #endregion

    private void OnPurchaseDeferred(Product product)
    {
        Debug.Log($"Purchase Deferred (Ask to Buy): {product.definition.id}");
    }

    private void ProcessSuccessfulPurchase(string productId)
    {
        switch (productId)
        {
            case ID_VIP1: PlayerPrefs.SetInt(KEY_VIP1, 1); break;
            case ID_VIP2: PlayerPrefs.SetInt(KEY_VIP2, 1); break;
            case ID_VIP3: PlayerPrefs.SetInt(KEY_VIP3, 1); break;
            case ID_REMOVE_ADS:
                PlayerPrefs.SetInt(KEY_REMOVE_ADS, 1);
                if (AdsManager.Instance != null) AdsManager.Instance.HideBannerAds();
                break;
        }
        PlayerPrefs.Save();
        OnPurchaseSuccessEvent?.Invoke(productId);
    }
}
