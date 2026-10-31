using UnityEngine;

/// <summary>
/// Reklam panosundaki afiş yüzeyi: o günün ürününün (<see cref="GameDay.Product"/>) afişini gösterir.
/// Afiş panoyu boşluk bırakmadan kaplar: oran farkında ortalanıp kırpılır, fark küçükse kırpmanın yarısı hafif esnetmeyle karşılanır.
/// Pano oranı yüzeyin dünya ölçeğinden okunur (x = genişlik, y = yükseklik; quad).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class DailyPoster : MonoBehaviour
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    [SerializeField] private PosterSet posters;
    [Tooltip("Aynı yöndeki afişlerden hangisi (komşu panolar farklı afiş göstersin diye sırayla verilir).")]
    [SerializeField] private int index;
    [Tooltip("Oran farkı küçükken afişin en fazla bu kadar esnetilmesine izin verilir (kalanı kırpılır).")]
    [SerializeField] private float maxStretch = 0.12f;

    private Renderer target;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        target = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        GameDay.Changed += Apply;
        Apply();
    }

    private void OnDisable() => GameDay.Changed -= Apply;

    private void Apply()
    {
        if (posters == null)
            return;

        Vector3 size = transform.lossyScale;
        float boardAspect = Mathf.Abs(size.x / size.y);
        Texture2D image = posters.Pick(GameDay.Product, boardAspect, index);
        if (image == null)
            return;

        float imageAspect = (float)image.width / image.height;
        // Kırpılan oran (< 1). Fark küçükse yarısı kırpılır, yarısı esnetilir: afiş kenarındaki yazılar kesilmesin.
        float ratio = imageAspect > boardAspect ? boardAspect / imageAspect : imageAspect / boardAspect;
        float crop = ratio >= 1f - maxStretch * 2f ? Mathf.Sqrt(ratio) : ratio;
        Vector4 st = new Vector4(1f, 1f, 0f, 0f);
        if (imageAspect > boardAspect)
        {
            st.x = crop;
            st.z = (1f - st.x) * 0.5f;
        }
        else
        {
            st.y = crop;
            st.w = (1f - st.y) * 0.5f;
        }

        target.GetPropertyBlock(block);
        block.SetTexture(BaseMapId, image);
        block.SetVector(BaseMapStId, st);
        target.SetPropertyBlock(block);
    }
}
