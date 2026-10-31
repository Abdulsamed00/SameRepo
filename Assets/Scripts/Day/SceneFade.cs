using UnityEngine;

/// <summary>
/// Solma mekaniği (GDD): her alışverişten sonra çevrenin rengi biraz daha solar, reklamlar ve yeni ürünler parlak kalır.
/// Ekranı "Solma" Fullscreen Shader Graph'ı soldurur (PC_Renderer'daki "Solma" feature'ı, global <c>_Solma</c> 0-1).
/// "Reklam" katmanındaki objeler solmadan sonra ayrıca çizilir (Reklam_Opaque/Transparent feature'ları), solmaz.
/// Sahne başına bir tane: miktar = alınan ürün oranı x <see cref="sceneStrength"/>. Değişim sahne açılırken uygulanır,
/// mağazada satın alınca değil (GDD: oyuncu soluklaşmayı eve döndüğünde fark eder).
/// </summary>
public class SceneFade : MonoBehaviour
{
    public const string ExemptLayerName = "Reklam";
    private static readonly int SolmaId = Shader.PropertyToID("_Solma");

    [Tooltip("Bu sahnenin ne kadar solacağı (ev en çok).")]
    [Range(0f, 1f)]
    [SerializeField] private float sceneStrength = 1f;
    [Tooltip("Beş ürün de alındığında ulaşılan en yüksek solma.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxFade = 0.85f;
    [Tooltip("Solmayan objeler (reklam panoları, TV ekranı...). Altındaki tüm renderer'lar Reklam katmanına alınır.")]
    [SerializeField] private GameObject[] exempt;

    private void Awake()
    {
        float bought = PlayerPurchases.Count / (float)System.Enum.GetValues(typeof(ProductType)).Length;
        Shader.SetGlobalFloat(SolmaId, bought * maxFade * sceneStrength);

        foreach (GameObject go in exempt)
            if (go != null)
                MarkExempt(go);
        foreach (ShopProduct product in FindObjectsByType<ShopProduct>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            MarkExempt(product.gameObject);
        foreach (WardrobeClothing clothing in FindObjectsByType<WardrobeClothing>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            MarkExempt(clothing.gameObject);
    }

    private void OnDestroy() => Shader.SetGlobalFloat(SolmaId, 0f);

    /// <summary>Objeyi ve altındaki renderer'ları solmayan katmana alır.</summary>
    public static void MarkExempt(GameObject root)
    {
        int layer = LayerMask.NameToLayer(ExemptLayerName);
        if (layer < 0)
            return;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            r.gameObject.layer = layer;
    }
}
