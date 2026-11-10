using System;
using System.Collections.Generic;

/// <summary>
/// Oyuncunun üstüne giydiği satın alınmış ürünler. Statik: sahne geçişlerinde korunur (kayıt sistemi yok).
/// Görünüm <see cref="PlayerOutfitView"/> ile karaktere yansır. Hiçbir ürün giyili değilken oyuncu ilk günkü
/// (ev) kıyafetlerindedir (<see cref="IsHomeClothes"/>): yatmadan önce o giyilir, dışarı çıkmadan önce alınanların hepsi
/// tek seferde giyilir (<see cref="WearAllOwned"/>).
/// </summary>
public static class PlayerOutfit
{
    private static readonly HashSet<ProductType> worn = new HashSet<ProductType>();

    /// <summary>Bir ürün giyildi ya da çıkarıldı (<see cref="IsWorn"/> yeni durumu verir).</summary>
    public static event Action<ProductType> Changed;

    public static bool IsWorn(ProductType type) => worn.Contains(type);

    /// <summary>Üstünde alınmış hiçbir ürün yok: ilk günkü kıyafetler.</summary>
    public static bool IsHomeClothes => worn.Count == 0;

    /// <summary>Satın alınan her ürün giyili (hiç ürün alınmadıysa da doğru).</summary>
    public static bool AllOwnedWorn
    {
        get
        {
            foreach (ProductType type in Enum.GetValues(typeof(ProductType)))
                if (PlayerPurchases.Owns(type) && !worn.Contains(type))
                    return false;
            return true;
        }
    }

    public static void Reset()
    {
        foreach (ProductType type in new List<ProductType>(worn))
            Remove(type);
    }

    public static void Wear(ProductType type)
    {
        if (worn.Add(type))
            Changed?.Invoke(type);
    }

    public static void Remove(ProductType type)
    {
        if (worn.Remove(type))
            Changed?.Invoke(type);
    }

    /// <summary>Alınan tüm ürünleri tek seferde giyer.</summary>
    public static void WearAllOwned()
    {
        foreach (ProductType type in Enum.GetValues(typeof(ProductType)))
            if (PlayerPurchases.Owns(type))
                Wear(type);
    }

    /// <summary>Üstündeki tüm ürünleri çıkarır, ilk günkü kıyafetler kalır.</summary>
    public static void ChangeToHome() => Reset();
}
