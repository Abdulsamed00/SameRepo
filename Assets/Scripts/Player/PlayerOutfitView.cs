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
        new Item { product = ProductType.Pantolon, show = new[] { "BlueJean" }, hide = new[] { "EsofmanAltı" } },
        new Item { product = ProductType.Ayakkabi, show = new[] { "GoldenShoeLeft", "GoldenShoeRight" }, hide = new[] { "ShoeLeft", "ShoeRight" } },
        new Item { product = ProductType.Canta, show = new[] { "Bag" }, hide = new string[0] },
        new Item { product = ProductType.Gozluk, show = new[] { "Glasses" }, hide = new string[0] },
    };

    [Header("Görünüm (kuaför, klinik)")]
    [SerializeField] private string originalHair = "TopuzSac";
    [SerializeField] private string dyedHair = "BlondeHair";
    [Tooltip("Klinikte değişen blendshape'ler (burun, dudak).")]
    [SerializeField] private string[] blendShapeNames = { "burun", "dudak" };
    [SerializeField] private float surgeryWeight = 100f;
    [Tooltip("Estetikten önce burun blendshape'i: 0 kemikli burun, eksi değer daha da iri ve kötü (05.11, kullanıcı: \"ilk önce burnu çok kötü olmalı\"). -80'den aşağısında burun deliğinde dikiş açılıyor.")]
    [SerializeField] private float badNoseWeight = -70f;
    [SerializeField] private string noseShape = "burun";

    private void OnEnable()
    {
        PlayerOutfit.Changed += OnChanged;
        PlayerLook.Changed += ApplyLook;
        ApplyLook();
        foreach (Item item in items)
            if (PlayerOutfit.IsWorn(item.product))
                Apply(item);
    }

    private void OnDisable()
    {
        PlayerOutfit.Changed -= OnChanged;
        PlayerLook.Changed -= ApplyLook;
    }

    private void ApplyLook()
    {
        if (PlayerLook.HairDyed)
        {
            Transform before = transform.Find(originalHair);
            Transform after = transform.Find(dyedHair);
            if (before != null)
                before.gameObject.SetActive(false);
            if (after != null)
                after.gameObject.SetActive(true);
        }

        // 0: doğal yüz (iri burun, ince dudak), 100: estetikli yüz.
        float weight = PlayerLook.SurgeryDone ? surgeryWeight : 0f;
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            foreach (string shape in blendShapeNames)
            {
                int index = r.sharedMesh != null ? r.sharedMesh.GetBlendShapeIndex(shape) : -1;
                if (index >= 0)
                    r.SetBlendShapeWeight(index, !PlayerLook.SurgeryDone && shape == noseShape ? badNoseWeight : weight);
            }
        }
    }

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
            {
                t.gameObject.SetActive(true);
                // Yeni ürün solmaz (GDD: reklam dünyası parlak kalır). Gözlük PlayerHead'de kalır, yoksa kameraya girer.
                if (t.gameObject.layer != LayerMask.NameToLayer("PlayerHead"))
                    SceneFade.MarkExempt(t.gameObject, item.product == ProductType.Ayakkabi);
            }
            else if (Array.IndexOf(item.hide, t.name) >= 0)
                t.gameObject.SetActive(false);
        }
    }
}
