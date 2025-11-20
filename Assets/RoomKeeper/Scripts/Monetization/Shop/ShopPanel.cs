using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ShopPanel : MonoBehaviour
{
    [Header("Vip1 (+10s)")]
    [SerializeField] private Button vip1Button;
    [SerializeField] private TextMeshProUGUI vip1_price;
    [SerializeField] private GameObject vip1_ownedIndicator;

    [Header("Vip2 (+20s)")]
    [SerializeField] private Button vip2Button;
    [SerializeField] private TextMeshProUGUI vip2_price;
    [SerializeField] private GameObject vip2_ownedIndicator;

    [Header("Vip3 (+30s)")]
    [SerializeField] private Button vip3Button;
    [SerializeField] private TextMeshProUGUI vip3_price;
    [SerializeField] private GameObject vip3_ownedIndicator;

    [Header("Remove Ads")]
    [SerializeField] private Button removeAdsButton;
    [SerializeField] private TextMeshProUGUI removeAds_price;
    [SerializeField] private GameObject removeAds_ownedIndicator;

    [Header("Restore (iOS)")]
    [SerializeField] private Button restoreButton;

    private void Start()
    {
        // Setup Buttons
        vip1Button.onClick.AddListener(() => BuyItem(IAPManager.ID_VIP1));
        vip2Button.onClick.AddListener(() => BuyItem(IAPManager.ID_VIP2));
        vip3Button.onClick.AddListener(() => BuyItem(IAPManager.ID_VIP3));
        removeAdsButton.onClick.AddListener(() => BuyItem(IAPManager.ID_REMOVE_ADS));

        if (restoreButton != null)
        {
            restoreButton.onClick.AddListener(() =>
            {
                if (IAPManager.Instance != null) IAPManager.Instance.RestorePurchases();
            });
            restoreButton.gameObject.SetActive(Application.platform == RuntimePlatform.IPhonePlayer);
        }

        // Subscribe Events
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnPurchaseSuccessEvent += HandlePurchaseSuccess;
            IAPManager.Instance.OnRestoreFinishedEvent += RefreshUI;

            RefreshUI();
        }
    }

    private void OnDestroy()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnPurchaseSuccessEvent -= HandlePurchaseSuccess;
            IAPManager.Instance.OnRestoreFinishedEvent -= RefreshUI;
        }
    }

    private void BuyItem(string productId)
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.BuyProduct(productId);
        }
    }

    private void HandlePurchaseSuccess(string productId)
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (IAPManager.Instance == null || !IAPManager.Instance.IsInitialized) return;

        // Update Prices
        UpdatePriceText(vip1_price, IAPManager.ID_VIP1);
        UpdatePriceText(vip2_price, IAPManager.ID_VIP2);
        UpdatePriceText(vip3_price, IAPManager.ID_VIP3);
        UpdatePriceText(removeAds_price, IAPManager.ID_REMOVE_ADS);

        // Update Buttons
        UpdateProductState(IAPManager.ID_VIP1, vip1Button, vip1_ownedIndicator);
        UpdateProductState(IAPManager.ID_VIP2, vip2Button, vip2_ownedIndicator);
        UpdateProductState(IAPManager.ID_VIP3, vip3Button, vip3_ownedIndicator);
        UpdateProductState(IAPManager.ID_REMOVE_ADS, removeAdsButton, removeAds_ownedIndicator);
    }

    private void UpdatePriceText(TextMeshProUGUI text, string productId)
    {
        if (text != null)
        {
            text.text = IAPManager.Instance.GetProductPrice(productId);
        }
    }

    private void UpdateProductState(string productId, Button button, GameObject ownedIndicator)
    {
        // เช็คจาก PlayerPrefs เพื่อความรวดเร็วและแน่นอน
        bool isOwned = PlayerPrefs.GetInt(GetKeyForProduct(productId), 0) == 1;

        if (button != null) button.interactable = !isOwned;
        if (ownedIndicator != null) ownedIndicator.SetActive(isOwned);
    }

    private string GetKeyForProduct(string id)
    {
        switch (id)
        {
            case IAPManager.ID_VIP1: return IAPManager.KEY_VIP1;
            case IAPManager.ID_VIP2: return IAPManager.KEY_VIP2;
            case IAPManager.ID_VIP3: return IAPManager.KEY_VIP3;
            case IAPManager.ID_REMOVE_ADS: return IAPManager.KEY_REMOVE_ADS;
            default: return "";
        }
    }
}