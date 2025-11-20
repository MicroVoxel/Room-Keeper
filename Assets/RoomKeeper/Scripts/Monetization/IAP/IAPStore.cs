using System;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;

public class IAPStore : MonoBehaviour
{
    [Header("Consumable")]
    public TextMeshProUGUI coinText;

    [Header("Non Consumable")]
    public GameObject adsPurchasedWindow;
    public GameObject adsBanner;

    [Header("Subscription")]
    public GameObject subActivatedWindow;
    public GameObject premiumLogo;

    private const string TotalCoinsKey = "totalCoins";

    void Start()
    {
        UpdateCoinText(PlayerPrefs.GetInt(TotalCoinsKey));
    }

    public void ResetPlayerPref()
    {
        PlayerPrefs.DeleteAll();
        UpdateCoinText(PlayerPrefs.GetInt(TotalCoinsKey));
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.LogError($"Purchase failed for product: {product.definition.id}, Reason: {failureDescription.reason}");
    }

    #region Consumable
    public void OnPurchaseCoinComplete150(Product product)
    {
        OnPurchaseCoinComplete(product, 150);
    }

    public void OnPurchaseCoinComplete300(Product product)
    {
        OnPurchaseCoinComplete(product, 300);
    }

    public void OnPurchaseCoinComplete450(Product product)
    {
        OnPurchaseCoinComplete(product, 450);
    }

    public void OnPurchaseCoinComplete600(Product product)
    {
        OnPurchaseCoinComplete(product, 600);
    }

    public void OnPurchaseCoinComplete750(Product product)
    {
        OnPurchaseCoinComplete(product, 750);
    }

    public void OnPurchaseCoinComplete900(Product product)
    {
        OnPurchaseCoinComplete(product, 900);
    }

    private void OnPurchaseCoinComplete(Product product, int coinAmount)
    {
        Debug.Log($"Purchase completed for product: {product.definition.id}");
        AddCoins(coinAmount);
    }

    private void AddCoins(int amount)
    {
        int coins = PlayerPrefs.GetInt(TotalCoinsKey);
        coins += amount;
        PlayerPrefs.SetInt(TotalCoinsKey, coins);
        UpdateCoinText(coins);
    }

    private void UpdateCoinText(int coins)
    {
        coinText.text = coins.ToString();
    }
    #endregion

    #region Non Consumable

    private void SetBoostState(bool isActive)
    {
        adsPurchasedWindow.SetActive(isActive);
        adsBanner.SetActive(isActive);
    }

    public void OnPurchaseBoostComplete(Product product)
    {
        Debug.Log($"Purchase completed for product: {product.definition.id}");
        SetBoostState(true);
    }

    public void CheckNonConsumable(Product product)
    {
        if (product == null) return;

        SetBoostState(product.hasReceipt);
    }

    #endregion

    #region Subscription

    private void SetSubscriptionState(bool isActive)
    {
        subActivatedWindow.SetActive(isActive);
        premiumLogo.SetActive(isActive);
    }

    public void OnPurchaseActivateBPComplete(Product product)
    {
        Debug.Log($"Purchase completed for product: {product.definition.id}");
        SetSubscriptionState(true);
    }

    public void CheckSubscription(Product product)
    {
        if (product == null)
        {
            Debug.LogError("Product is null. Cannot check subscription.");
            return;
        }

        if (!product.hasReceipt)
        {
            Debug.Log("No receipt found. Deactivating subscription.");
            SetSubscriptionState(false);
            return;
        }

        try
        {
            var subManager = new SubscriptionManager(product, null);
            var info = subManager.getSubscriptionInfo();

            bool isSubscribed = info.IsSubscribed() == Result.True;
            Debug.Log(isSubscribed ? "User is subscribed." : "User is not subscribed.");
            SetSubscriptionState(isSubscribed);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Subscription check failed: {ex.Message}");
            Debug.Log("Ensure you're using a supported store (Google Play, Apple App Store, or Amazon Store).");
        }
    }

    #endregion
}
