using UnityEngine;

/// <summary>
/// Oturma odasında biriken kıyafet yığınları (11.11): N. ürün alınınca N. yığın görünür ve kalır
/// (eve dönünce oda bir öncekinden daha dolu). Yığınlar bu objenin çocukları, sırayla <see cref="stages"/>'e bağlı.
/// Sayı ürün sayısından az olabilir: fazladan alınan ürün yeni yığın getirmez.
/// </summary>
public class ClutterPiles : MonoBehaviour
{
    [Tooltip("Sırayla: 1. ürün alınınca ilki, 2. ürün alınınca ikincisi görünür.")]
    [SerializeField] private GameObject[] stages;

    private void OnEnable()
    {
        PlayerPurchases.Purchased += OnPurchased;
        Apply();
    }

    private void OnDisable()
    {
        PlayerPurchases.Purchased -= OnPurchased;
    }

    private void OnPurchased(ProductType _) => Apply();

    private void Apply()
    {
        int count = PlayerPurchases.Count;
        for (int i = 0; i < stages.Length; i++)
            if (stages[i] != null)
                stages[i].SetActive(i < count);
    }
}
