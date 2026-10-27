using System;

/// <summary>
/// Oyunun günü (GDD: 5 gün, her gün bir ürün: palto, pantolon, ayakkabı, çanta, gözlük).
/// TV o günün reklamını oynatır, mağazada sadece o günün ürünü alınabilir.
/// Statik: sahne geçişlerinde korunur. Gün uyku sistemiyle ilerleyecek (<see cref="Advance"/>); o gelene kadar hep 1. gün.
/// </summary>
public static class GameDay
{
    private static readonly int ProductCount = Enum.GetValues(typeof(ProductType)).Length;

    /// <summary>0'dan başlayan gün sırası.</summary>
    public static int Index { get; private set; }

    /// <summary>Bugün reklamı verilen ve mağazada alınabilen ürün.</summary>
    public static ProductType Product => (ProductType)Math.Min(Index, ProductCount - 1);

    public static event Action Changed;

    /// <summary>Ertesi güne geçer (uyku sistemi çağıracak).</summary>
    public static void Advance()
    {
        if (Index >= ProductCount - 1)
            return;
        Index++;
        Changed?.Invoke();
    }
}
