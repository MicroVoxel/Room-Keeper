using UnityEngine;
using UnityEngine.Purchasing;
using TMPro;

public class IAPButtonView : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text price;
    [SerializeField] private TMP_Text detail;

    public void OnProductFetched(Product product)
    {
        if (product == null || product.metadata == null)
        {
            Debug.LogError("Product or product metadata is null. Cannot update UI.");
            return;
        }

        if (title != null)
        {
            title.text = product.metadata.localizedTitle;
        }
        if (price != null)
        {
            price.text = $"{product.metadata.localizedPrice} $";
        }
        if (detail != null)
        {
            detail.text = product.metadata.localizedDescription;
        }
    }
}
