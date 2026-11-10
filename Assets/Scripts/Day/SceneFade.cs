using UnityEngine;

/// <summary>
/// Solma mekaniği (GDD): her alışverişten sonra çevrenin rengi biraz daha solar, reklamlar ve yeni ürünler parlak kalır.
/// Ekranı "Solma" Fullscreen Shader Graph'ı soldurur (PC_Renderer'daki "Solma" feature'ı, global <c>_Solma</c> 0-1).
/// "Reklam" katmanındaki objeler solmadan sonra ayrıca çizilir (Reklam_Opaque/Transparent feature'ları), solmaz.
/// Sahne başına bir tane: miktar = alınan ürün oranı x <see cref="sceneStrength"/>. Değişim sahne açılırken uygulanır,
/// mağazada satın alınca değil (GDD: oyuncu soluklaşmayı eve döndüğünde fark eder).
/// </summary>
// Solma değeri, onu okuyan Awake/OnEnable'lardan (PlayerOutfitView) önce kurulsun.
[DefaultExecutionOrder(-100)]
public class SceneFade : MonoBehaviour
{
    public const string ExemptLayerName = "Reklam";
    private static readonly int SolmaId = Shader.PropertyToID("_Solma");

    [Tooltip("Bu sahnenin ne kadar solacağı (ev en çok).")]
    [Range(0f, 1f)]
    [SerializeField] private float sceneStrength = 1f;
    [Tooltip("Beş ürün de alındığında ulaşılan en yüksek solma.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxFade = 1f;
    [Tooltip("Günden güne artışın eğrisi: solma = 1 - (1 - alınan oran)^üs. 1 = doğrusal; büyüdükçe ilk günler daha çok solar.")]
    [SerializeField] private float curve = 1.8f;
    [Tooltip("Solmayan objeler (reklam panoları, TV ekranı...). Altındaki tüm renderer'lar Reklam katmanına alınır.")]
    [SerializeField] private GameObject[] exempt;

    private void Awake()
    {
        float bought = PlayerPurchases.Count / (float)System.Enum.GetValues(typeof(ProductType)).Length;
        // 10.11 (kullanıcı: "solma efektini her gün için güçlendir"): eskiden doğrusaldı (ev 0.17/0.34/.../0.85) ve
        // shader en fazla doygunluk 0.25'e iniyordu; ilk günlerde neredeyse fark edilmiyordu. Artık eğri + shader 0.08:
        // evde gün gün doygunluk ~0.70 / 0.45 / 0.25 / 0.13 / 0.08.
        float level = 1f - Mathf.Pow(1f - bought, Mathf.Max(0.01f, curve));
        Shader.SetGlobalFloat(SolmaId, level * maxFade * sceneStrength);

        foreach (GameObject go in exempt)
            if (go != null)
                MarkExempt(go);
        foreach (ShopProduct product in FindObjectsByType<ShopProduct>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            MarkExempt(product.gameObject, product.Product == ProductType.Ayakkabi);
        foreach (DailyPoster poster in FindObjectsByType<DailyPoster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            MarkExempt(poster.gameObject);
        foreach (WardrobeClothing clothing in FindObjectsByType<WardrobeClothing>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            MarkExempt(clothing.gameObject, clothing.Product == ProductType.Ayakkabi);
    }

    private void OnDestroy() => Shader.SetGlobalFloat(SolmaId, 0f);

    // Beyaz ayakkabı akşam ışığında (sıcak güneş + bake + post'un sıcak beyaz dengesi) ten rengi/sarı çıkıyordu.
    // Albedo her zaman bu renkle çarpılıp ışığın sıcaklığı dengelenir (HDR çarpan, B > 1). Eskiden solma oranıyla
    // ölçekleniyordu; ilk günlerde solma düşük olduğundan ayakkabı dolapta ve üstünde sarı kalıyordu (01.11, ölçüldü).
    private static readonly Color CoolTint = new Color(1f, 1f, 1f, 1f); // 08.11: buz mavisi çıkıyordu (eski 0.85, 1.1, 1.5), NPC ayakkabılarıyla aynı ton istendi
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// <summary>
    /// Objeyi ve altındaki renderer'ları solmayan katmana alır. <paramref name="neutralizeWarmth"/>: sıcak ışığın
    /// sarıya boyadığı beyaz ürünler (ayakkabı) için solma oranında soğuk bir tonla dengelenir.
    /// </summary>
    public static void MarkExempt(GameObject root, bool neutralizeWarmth = false)
    {
        int layer = LayerMask.NameToLayer(ExemptLayerName);
        if (layer < 0)
            return;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.gameObject.layer = layer;
            if (!neutralizeWarmth || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(BaseColorId))
                continue;

            Color tint = CoolTint;
            Color baseColor = r.sharedMaterial.GetColor(BaseColorId);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a));
            r.SetPropertyBlock(block);
        }
    }
}
