using UnityEngine;

/// <summary>
/// Mağaza standındaki ürün. E ile ele alınır (G ile standdaki yerine döner), kasada F ile satın alınır.
/// Sadece o günün ürünü (<see cref="GameDay.Product"/>, TV'de reklamı verilen) alınabilir; diğer standlar
/// etkileşime kapalıdır. Günün reklamı izlenmeden (<see cref="DayRoutine.AdWatchedToday"/>) hiçbir ürün
/// alınamaz. Aynı türden ürün satın alındıysa standdaki diğerleri artık alınamaz.
/// </summary>
public class ShopProduct : PickupItem
{
    // Ürün standın/masanın kenarına gelince alınır (dipteyken masadakine 0.8-1.06 m, askıdakine ~0.3 m; ölçüldü).
    protected override float DefaultMaxDistance => 1.35f;

    [SerializeField] private ProductType product;

    public ProductType Product => product;

    public override InteractionKey Key => InteractionKey.Secondary;

    // Ürünlerin tutma ofsetleri kameradaki tutma noktasına göre ayarlı; ele bağlanmazlar.
    public override bool HoldInHand => false;

    // Palto: tek elle alınır, iki elle tutulur ve bırakılır.
    public override bool ReachWithOneHand => product == ProductType.Palto;

    [Tooltip("Taşırken ürünü kameranın önünde bu kadar aşağı indirir (m): yukarıda tutulunca önü görmek zorlaşıyordu (06.11).")]
    [SerializeField] private float holdLowering = 0.16f;
    protected override float HoldLowering => holdLowering;

    public override string Prompt => "Al";

    public override string Title => product.DisplayName().ToUpper(new System.Globalization.CultureInfo("tr-TR"));

    [Tooltip("Ürün sadece yerinin bu kadar yakınında (göz hizasından) bırakılabilir: standdan alınan ürün mağazanın ortasına bırakılamaz.")]
    [SerializeField] private float dropRange = 1.9f;

    public override bool CanDropAt(Vector3 eye) => Vector3.Distance(eye, HomePosition) <= dropRange;

    public override bool CanInteract(PlayerInteractor interactor) =>
        base.CanInteract(interactor) && GameDay.IsAvailable(product) && DayRoutine.AdRequirementMet
        && !PlayerPurchases.Owns(product);
}
