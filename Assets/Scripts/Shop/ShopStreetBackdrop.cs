using UnityEngine;

/// <summary>
/// Mağazanın kapısından görünen sokak: StreetScene'de mağaza kapısının önünden çekilmiş küp haritalar gökyüzü olarak
/// çizilir (sokak sahnesi buraya yüklenmez). Panolardaki afiş güne göre değiştiği için ürün başına ayrı çekim var,
/// o günün ürününe (<see cref="GameDay.Product"/>) ait olan seçilir.
/// Çekim konumu sokakta (0, 1.6, 122), mağazada kapının hizası; iki sahnenin eksenleri aynı yöne bakar, dönüş yok.
/// </summary>
public class ShopStreetBackdrop : MonoBehaviour
{
    [Tooltip("Ürün sırasıyla (palto, pantolon, ayakkabı, çanta, gözlük) sokak küp haritaları.")]
    [SerializeField] private Cubemap[] streetViews;
    [SerializeField] private Material skyboxMaterial;

    private Material instance;

    private void Awake()
    {
        if (skyboxMaterial == null || streetViews == null || streetViews.Length == 0)
            return;

        int i = Mathf.Clamp((int)GameDay.Product, 0, streetViews.Length - 1);
        if (streetViews[i] == null)
            return;

        instance = new Material(skyboxMaterial);
        instance.SetTexture("_Tex", streetViews[i]);
        RenderSettings.skybox = instance;
    }

    private void OnDestroy()
    {
        if (instance != null)
            Destroy(instance);
    }
}
