using UnityEngine;

/// <summary>
/// Reklam panosundaki afiş yüzeyi: o günün ürününün (<see cref="GameDay.Product"/>) afişini gösterir.
/// Afiş hiç kırpılmaz ve esnetilmez: pano (üst obje, afişin arkasındaki yüzey) ilk boyutunu aşmadan afişin oranına
/// getirilir, merkezi yerinde kalır. Yataydaki afişlerin oranı ürüne göre değiştiği için boyut her gün yeniden hesaplanır.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class DailyPoster : MonoBehaviour
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [SerializeField] private PosterSet posters;
    [Header("Kuaför ve klinik günleri")]
    [Tooltip("Kuaför gününde (GameDay.Index 1) dikey panolarda gösterilen afiş.")]
    [SerializeField] private Texture2D salonPoster;
    [Tooltip("Klinik gününde (GameDay.Index 3) dikey panolarda gösterilen afiş.")]
    [SerializeField] private Texture2D clinicPoster;
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
        Apply();
    }

    private void OnDisable() => GameDay.Changed -= Apply;

    private void Apply()
    {
        if (posters == null)
            return;

        float boardAspect = maxSize.x / maxSize.y;
        Texture2D image = null;
        // O günün mekânı (kuaför/klinik) dikey panolarda, ikiden birinde kendi afişiyle tanıtılır.
        if (boardAspect < 1f && index % 2 == 0)
        {
            if (GameDay.Index == DayRoutine.SalonDay)
                image = salonPoster;
            else if (GameDay.Index == DayRoutine.ClinicDay)
                image = clinicPoster;
        }
        if (image == null)
            image = posters.Pick(GameDay.Product, boardAspect, index);
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
