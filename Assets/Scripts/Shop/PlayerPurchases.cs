using System;
using System.Collections.Generic;

/// <summary>
/// Oyuncunun satın aldığı ürünler. Statik: sahne geçişlerinde (mağaza → sokak → ev) korunur,
/// oyun kapanınca sıfırlanır (kayıt sistemi henüz yok). Her üründen yalnızca bir tane alınabilir.
/// </summary>
public static class PlayerPurchases
{
    private static readonly HashSet<ProductType> owned = new HashSet<ProductType>();

    public static event Action<ProductType> Purchased;

    public static bool Owns(ProductType type) => owned.Contains(type);

    public static int Count => owned.Count;

    public static bool OwnsAll => owned.Count == Enum.GetValues(typeof(ProductType)).Length;

    public static void Reset() => owned.Clear();

    /// <returns>Ürün daha önce alınmadıysa true.</returns>
    public static bool Buy(ProductType type)
    {
        if (!owned.Add(type))
            return false;
        Purchased?.Invoke(type);
        return true;
    }
}
