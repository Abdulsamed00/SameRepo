using UnityEngine;

/// <summary>
/// Mağaza çıkışı: o günün ürünü satın alınana kadar otomatik kapı kapalı ve sokağa çıkış bölgesi kilitli.
/// Satın alınınca kanatlar iki yana kayar ve bölge açılır.
/// Şimdilik (gün sistemi yok) <see cref="requiredProducts"/> boşsa bu ziyarette herhangi bir ürün almak yeter;
/// alınacak ürün kalmadıysa kapı baştan açıktır.
/// </summary>
public class ShopExitGate : MonoBehaviour
{
    [SerializeField] private SceneZone exitZone;
    [SerializeField] private Transform leftLeaf;
    [SerializeField] private Transform rightLeaf;
    [Tooltip("Kanatların kapalı konumdan açılırken yana kayma mesafesi (m).")]
    [SerializeField] private float slideDistance = 1.65f;
    [SerializeField] private float duration = 0.8f;
    [Tooltip("Kapının açılması için alınması gereken ürünler. Boşsa bu ziyarette herhangi bir ürün yeter.")]
    [SerializeField] private ProductType[] requiredProducts;

    private Vector3 leftOpen;
    private Vector3 rightOpen;
    private bool purchasedThisVisit;
    private bool open;
    private float amount;

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
        return purchasedThisVisit || PlayerPurchases.OwnsAll;
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
        float closed = (1f - Mathf.SmoothStep(0f, 1f, amount)) * slideDistance;
        leftLeaf.localPosition = leftOpen + Vector3.right * closed;
        rightLeaf.localPosition = rightOpen + Vector3.left * closed;
        if (exitZone != null)
            exitZone.Locked = !open;
    }
}
