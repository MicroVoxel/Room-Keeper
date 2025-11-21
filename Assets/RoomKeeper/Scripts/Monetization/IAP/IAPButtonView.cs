using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Purchasing; // ใช้เพื่อเข้าถึง CodelessIAPStoreListener
using TMPro;

public class IAPButtonView : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("ใส่ ID ให้ตรงกับใน IAPManager (เช่น vip1, vip4)")]
    public string productId;

    [Header("UI Elements")]
    public Button buyButton;
    public GameObject purchasedObject;
    public GameObject notPurchasedObject;
    public TMP_Text priceText;

    private void Start()
    {
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(OnBuyClick);
        }
    }

    private void Update()
    {
        if (IAPManager.Instance == null) return;

        // เช็คสถานะความเป็นเจ้าของ (IAPManager จะไปถาม StoreController ให้)
        bool isOwned = IAPManager.Instance.IsOwned(productId);
        UpdateUI(isOwned);
    }

    private void UpdateUI(bool isOwned)
    {
        // 1. จัดการปุ่มและ Object ตามสถานะ
        if (buyButton != null) buyButton.interactable = !isOwned;
        if (purchasedObject != null) purchasedObject.SetActive(isOwned);
        if (notPurchasedObject != null) notPurchasedObject.SetActive(!isOwned);

        // 2. ดึงราคาจาก StoreController มาแสดง (ถ้ายังไม่มีราคา)
        if (priceText != null && (string.IsNullOrEmpty(priceText.text) || priceText.text == "-"))
        {
            // เข้าถึง StoreController ผ่าน Codeless Instance
            var controller = CodelessIAPStoreListener.Instance.StoreController;
            if (controller != null)
            {
                var product = controller.products.WithID(productId);
                if (product != null)
                {
                    priceText.text = product.metadata.localizedPriceString;
                }
            }
        }
    }

    private void OnBuyClick()
    {
        // สั่งซื้อผ่าน IAPManager
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.BuyProduct(productId);
        }
    }
}