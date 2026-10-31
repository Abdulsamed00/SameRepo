using UnityEngine;

/// <summary>
/// Mağaza standındaki ürün. E ile ele alınır (G ile standdaki yerine döner), kasada F ile satın alınır.
/// Sadece o günün ürünü (<see cref="GameDay.Product"/>, TV'de reklamı verilen) alınabilir; diğer standlar
/// etkileşime kapalıdır. Günün reklamı izlenmeden (<see cref="DayRoutine.AdWatchedToday"/>) hiçbir ürün
/// alınamaz. Aynı türden ürün satın alındıysa standdaki diğerleri artık alınamaz.
/// </summary>
public class ShopProduct : PickupItem
{
    [SerializeField] private ProductType product;

    public ProductType Product => product;

    public override InteractionKey Key => InteractionKey.Secondary;

    // Ürünlerin tutma ofsetleri kameradaki tutma noktasına göre ayarlı; ele bağlanmazlar.
    public override bool HoldInHand => false;

    public override string Prompt => "Al";

    public override string Title => product.DisplayName().ToUpper(new System.Globalization.CultureInfo("tr-TR"));

    public override bool CanInteract(PlayerInteractor interactor) =>
        base.CanInteract(interactor) && GameDay.IsAvailable(product) && DayRoutine.AdRequirementMet
        && !PlayerPurchases.Owns(product);
}
