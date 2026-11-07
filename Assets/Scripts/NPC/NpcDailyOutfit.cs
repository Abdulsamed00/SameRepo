using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// NPC'nin oyunun ürünlerini (palto, pantolon...) giyip giymediği: <see cref="NpcFashion"/>'a göre açılışta uygulanır.
/// Giyen NPC'de ürün parçası açılır, aynı yuvadaki diğer seçenekler kapanır; giymeyen NPC'de ürün parçası kapanır,
/// yuvada açık parça kalmadıysa seçeneklerden biri açılır. NPC prefab'larında durur: sahneye konan NPC'lerin kendi
/// kombini ve kalabalığın rastgele kombini (<see cref="NpcCrowd"/>, Start'tan önce uygulanır) bunun altında kalır.
/// </summary>
public class NpcDailyOutfit : MonoBehaviour
{
    [Serializable]
    public class ProductLook
    {
        public ProductType product;
        [Tooltip("Ürünü giyince açılan parçalar ('+' ile birden çok).")]
        public string wear;
        [Tooltip("Aynı yuvadaki diğer seçenekler: giyince kapanır, giymeyen NPC'de biri açık kalır. Boşsa yuva boş kalır (çanta).")]
        public string[] alternatives;
    }

    [Serializable]
    public class ColorVariant
    {
        [Tooltip("Rengi değişen parça (ürün olmayan, örn. erkek kazağı/pantolonu).")]
        public string part;
        [Tooltip("Parçanın kendi malzemesi de bir seçenek; bunlar ek renkler.")]
        public Material[] materials;
    }

    [Tooltip("Oranın ayrı tutturulduğu grup (erkek/kadın).")]
    [SerializeField] private string group = "Kadin";
    [SerializeField] private ProductLook[] looks;
    [Tooltip("Sarışın saç parçası (boşsa saç güne göre değişmez: erkekler).")]
    [SerializeField] private string blondeHair;
    [SerializeField] private string[] otherHair;
    [Tooltip("Ürün dışı parçaların renk çeşitleri (07.11: erkek modelinde ürün dışı tek kazak ve tek pantolon var, herkes aynı görünüyordu).")]
    [SerializeField] private ColorVariant[] colorVariants;

    public string Group => group;
    /// <summary>NPC'nin kimliği (ad + ilk konum): sahnedeki NPC her açılışta aynı kimlikle gelir, giyip giymediği günden güne korunur.</summary>
    public string Key { get; private set; }

    private void Awake()
    {
        Vector3 p = transform.position;
        Key = $"{name}@{Mathf.RoundToInt(p.x * 10f)},{Mathf.RoundToInt(p.z * 10f)}";
    }

    private void OnEnable() => NpcFashion.Register(this);

    private void OnDisable() => NpcFashion.Unregister(this);

    private void Start()
    {
        if (looks == null)
            return;

        foreach (ProductLook look in looks)
            Apply(look, NpcFashion.Wears(this, look.product));

        if (!string.IsNullOrEmpty(blondeHair))
            Apply(new ProductLook { wear = blondeHair, alternatives = otherHair }, NpcFashion.IsBlonde(this));

        ApplyColors();
    }

    // Renk NPC'nin kimliğinden seçilir: aynı NPC her gün aynı renkte, NPC'ler birbirinden farklı.
    private void ApplyColors()
    {
        if (colorVariants == null)
            return;
        foreach (ColorVariant variant in colorVariants)
        {
            if (variant == null || variant.materials == null || variant.materials.Length == 0)
                continue;
            Transform t = transform.Find(variant.part);
            Renderer renderer = t != null ? t.GetComponent<Renderer>() : null;
            if (renderer == null)
                continue;
            int pick = (int)((uint)(Key + "#" + variant.part).GetHashCode() % (uint)(variant.materials.Length + 1));
            if (pick > 0 && variant.materials[pick - 1] != null)
                renderer.sharedMaterial = variant.materials[pick - 1];
        }
    }

    private void Apply(ProductLook look, bool wears)
    {
        SetParts(look.wear, wears);
        if (look.alternatives == null || look.alternatives.Length == 0)
            return;

        if (wears)
        {
            foreach (string option in look.alternatives)
                SetParts(option, false);
            return;
        }

        foreach (string option in look.alternatives)
        {
            if (IsOn(option))
                return;
        }
        SetParts(look.alternatives[Random.Range(0, look.alternatives.Length)], true);
    }

    private void SetParts(string option, bool on)
    {
        foreach (string part in option.Split('+'))
        {
            Transform t = transform.Find(part);
            if (t != null)
                t.gameObject.SetActive(on);
        }
    }

    private bool IsOn(string option)
    {
        Transform t = transform.Find(option.Split('+')[0]);
        return t != null && t.gameObject.activeSelf;
    }
}
