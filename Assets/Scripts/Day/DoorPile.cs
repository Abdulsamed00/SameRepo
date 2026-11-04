using System;
using UnityEngine;

/// <summary>
/// Giriş kapısının önünde biriken eski kıyafetler (GDD: kapı önündeki eşya yığını). Her yeni ürün giyildikçe bir katman
/// daha görünür: ilk günden son güne yığın büyür. Etkileşimsizdir; katmanın collider'ı (varsa) onunla açılır, oyuncu
/// yığının içinden geçmez. HouseScene'de <c>Koridor/Eski_Esya_Yigini</c>.
/// </summary>
public class DoorPile : MonoBehaviour
{
    [Serializable]
    private struct Layer
    {
        public GameObject root;
        [Tooltip("Bu kadar ürün giyilince görünür.")]
        [Min(1)] public int wornCount;
    }

    [SerializeField] private Layer[] layers;

    private void OnEnable()
    {
        PlayerOutfit.Changed += OnChanged;
        Refresh();
    }

    private void OnDisable() => PlayerOutfit.Changed -= OnChanged;

    private void OnChanged(ProductType type) => Refresh();

    private void Refresh()
    {
        int worn = 0;
        foreach (ProductType type in Enum.GetValues(typeof(ProductType)))
        {
            if (PlayerOutfit.IsWorn(type))
                worn++;
        }

        foreach (Layer layer in layers)
        {
            bool show = worn >= layer.wornCount;
            if (layer.root != null && layer.root.activeSelf != show)
                layer.root.SetActive(show);
        }
    }
}
