using System;
using UnityEngine;

/// <summary>
/// <see cref="PlayerOutfit"/>'te giyili ürünleri karakterin parçalarına yansıtır: ürünün parçalarını açar,
/// yerini aldığı parçaları kapatır (ör. palto: "Coat" açılır). Karakter modelinin köküne (AnaKarakter) eklenir.
/// Her sahnede açılışta ve giyildiği anda uygulanır.
/// </summary>
public class PlayerOutfitView : MonoBehaviour
{
    [Serializable]
    private struct Item
    {
        public ProductType product;
        [Tooltip("Giyilince açılan parça adları.")]
        public string[] show;
        [Tooltip("Giyilince kapanan parça adları (aynı slottaki eski parça).")]
        public string[] hide;
    }

    [SerializeField] private Item[] items =
    {
        // Tişört mesh'i paltonun sırtından dışarı taşıyor; palto kapalı gövdeyi örttüğü için tişört gizlenir
        // (önden açık yakada gövdenin kendi atleti görünür).
        new Item { product = ProductType.Palto, show = new[] { "Coat" }, hide = new[] { "Tshırt" } },
    };

    private void OnEnable()
    {
        PlayerOutfit.Changed += OnChanged;
        foreach (Item item in items)
            if (PlayerOutfit.IsWorn(item.product))
                Apply(item);
    }

    private void OnDisable() => PlayerOutfit.Changed -= OnChanged;

    private void OnChanged(ProductType type)
    {
        foreach (Item item in items)
            if (item.product == type)
                Apply(item);
    }

    private void Apply(Item item)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (Array.IndexOf(item.show, t.name) >= 0)
                t.gameObject.SetActive(true);
            else if (Array.IndexOf(item.hide, t.name) >= 0)
                t.gameObject.SetActive(false);
        }
    }
}
