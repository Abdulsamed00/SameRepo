using UnityEngine;

/// <summary>
/// Kasa: elde bir <see cref="ShopProduct"/> varken F ile satın alınır. Ürün elden çıkar ve
/// <see cref="PlayerPurchases"/>'a eklenir. Collider'ı olan kasa bankosuna (ya da üst objesine) eklenir.
/// </summary>
public class Checkout : Interactable
{
    public override string Prompt => "Satın al";

    public override bool CanInteract(PlayerInteractor interactor) =>
        interactor.HeldItem is ShopProduct product && product.Product == GameDay.Product
        && !PlayerPurchases.Owns(product.Product);

    public override void Interact(PlayerInteractor interactor)
    {
        if (!(interactor.HeldItem is ShopProduct product) || !PlayerPurchases.Buy(product.Product))
            return;

        interactor.ReleaseHeld();
        // Satın alınan ürün poşete girdi sayılır; sahneden kalkar.
        product.gameObject.SetActive(false);
    }
}
