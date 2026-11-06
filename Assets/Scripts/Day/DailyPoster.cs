using UnityEngine;

/// <summary>
/// Reklam panosundaki afiş yüzeyi: o günün ürününün (<see cref="GameDay.Product"/>) afişini, kuaför/klinik gününde
/// ürün alındıktan sonra o hizmetin afişini gösterir (<see cref="PosterSet.PickToday"/>).
/// Afiş hiç kırpılmaz ve esnetilmez: pano (üst obje, afişin arkasındaki yüzey) ilk boyutunu aşmadan afişin oranına
/// getirilir, merkezi yerinde kalır. Yataydaki afişlerin oranı ürüne göre değiştiği için boyut her gün yeniden hesaplanır.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class DailyPoster : MonoBehaviour
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [SerializeField] private PosterSet posters;
    [Tooltip("Aynı yöndeki afişlerden hangisi (komşu panolar farklı afiş göstersin diye sırayla verilir).")]
    [SerializeField] private int index;

    private Renderer target;
    private MaterialPropertyBlock block;
    private Transform board;
    private Vector2 maxSize;

    private void Awake()
    {
        target = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        board = transform.parent;
        maxSize = board.localScale;
    }

    private void OnEnable()
    {
        GameDay.Changed += Apply;
        PlayerPurchases.Purchased += OnPurchased;
        Apply();
    }

    private void OnDisable()
    {
        GameDay.Changed -= Apply;
        PlayerPurchases.Purchased -= OnPurchased;
    }

    private void OnPurchased(ProductType _) => Apply();

    private void Apply()
    {
        if (posters == null)
            return;

        float boardAspect = maxSize.x / maxSize.y;
        Texture2D image = posters.PickToday(boardAspect, index);
        if (image == null)
            return;

        float imageAspect = (float)image.width / image.height;
        Vector2 size = imageAspect > boardAspect
            ? new Vector2(maxSize.x, maxSize.x / imageAspect)
            : new Vector2(maxSize.y * imageAspect, maxSize.y);
        board.localScale = new Vector3(size.x, size.y, board.localScale.z);

        target.GetPropertyBlock(block);
        block.SetTexture(BaseMapId, image);
        target.SetPropertyBlock(block);
    }
}
