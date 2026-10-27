using System;
using System.Collections.Generic;

/// <summary>
/// Oyuncunun üstüne giydiği satın alınmış ürünler. Statik: sahne geçişlerinde korunur (kayıt sistemi yok).
/// Görünüm <see cref="PlayerOutfitView"/> ile karaktere yansır.
/// </summary>
public static class PlayerOutfit
{
    private static readonly HashSet<ProductType> worn = new HashSet<ProductType>();

    public static event Action<ProductType> Changed;

    public static bool IsWorn(ProductType type) => worn.Contains(type);

    public static void Wear(ProductType type)
    {
        if (worn.Add(type))
            Changed?.Invoke(type);
    }
}
