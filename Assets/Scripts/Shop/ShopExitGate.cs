using UnityEngine;

/// <summary>
/// Mağaza çıkışı: o günün ürünü satın alınana kadar otomatik kapı kapalı ve sokağa çıkış bölgesi kilitli.
/// Satın alınınca kanatlar iki yana kayar ve bölge açılır.
/// <see cref="requiredProducts"/> boşsa o günün ürünü (<see cref="GameDay.Product"/>) alınmış olmalı;
/// daha önce alındıysa (mağazaya tekrar girildiyse) kapı baştan açıktır.
/// </summary>
public class ShopExitGate : MonoBehaviour
{
    [SerializeField] private SceneZone exitZone;
    [SerializeField] private Transform leftLeaf;
    [SerializeField] private Transform rightLeaf;
    [Tooltip("Kanatların kapalı konumdan açılırken yana kayma mesafesi (m).")]
    [SerializeField] private float slideDistance = 1.65f;
    [SerializeField] private float duration = 0.8f;
    [Tooltip("Kapının açılması için alınması gereken ürünler. Boşsa o günün ürünü yeter.")]
    [SerializeField] private ProductType[] requiredProducts;

    [Tooltip("Açık: kanatlar ürün alınınca da kayıp açılmaz, kapalı görünür (çıkış yine bölgeyle yapılır).")]
    [SerializeField] private bool keepLeavesClosed = true;

    private void SetLeafColliders(bool enabled)
    {
        foreach (Transform leaf in new[] { leftLeaf, rightLeaf })
            foreach (Collider c in leaf.GetComponentsInChildren<Collider>(true))
                c.enabled = enabled;
    }

    private Vector3 leftOpen;
    private Vector3 rightOpen;
    private bool open;
    private float amount;
    private bool purchasedThisVisit;

    private void Awake()
    {
        // Sahnede kanatlar açık konumda duruyor; kapalı konum oradan hesaplanır.
        leftOpen = leftLeaf.localPosition;
        rightOpen = rightLeaf.localPosition;
    }

    private void OnEnable() => PlayerPurchases.Purchased += OnPurchased;

    private void OnDisable() => PlayerPurchases.Purchased -= OnPurchased;

    private void Start()
    {
        open = IsSatisfied();
        amount = open ? 1f : 0f;
        Apply();
    }

    private void OnPurchased(ProductType type)
    {
        purchasedThisVisit = true;
        if (IsSatisfied())
            open = true;
    }

    private bool IsSatisfied()
    {
        if (requiredProducts != null && requiredProducts.Length > 0)
        {
            foreach (ProductType p in requiredProducts)
                if (!PlayerPurchases.Owns(p))
                    return false;
            return true;
        }
        // Geçici mod: her ürün alınabilirken bu ziyarette bir ürün almak yeter (alınacak ürün kalmadıysa kapı açık).
        if (GameDay.AllProductsAvailable)
            return purchasedThisVisit || PlayerPurchases.OwnsAll;
        return PlayerPurchases.Owns(GameDay.Product);
    }

    private void Update()
    {
        float goal = open ? 1f : 0f;
        if (Mathf.Approximately(amount, goal))
            return;
        amount = Mathf.MoveTowards(amount, goal, Time.deltaTime / duration);
        Apply();
    }

    private void Apply()
    {
        // 09.11 (kullanıcı: "mağazanın kapısı açılmasın"): kanatlar görsel olarak hep kapalı kalır, çıkış sadece
        // "Sokağa çık" bölgesinin kilidine bağlı (F). Çıkış açıkken kanat collider'ları da kapanır, yoksa yol kapalı kalırdı.
        float shown = keepLeavesClosed ? 0f : amount;
        if (keepLeavesClosed)
            SetLeafColliders(!open);
        float closed = (1f - Mathf.SmoothStep(0f, 1f, shown)) * slideDistance;
        leftLeaf.localPosition = leftOpen + Vector3.right * closed;
        rightLeaf.localPosition = rightOpen + Vector3.left * closed;
        if (exitZone != null)
            exitZone.Locked = !open;
    }
}
