using System;

/// <summary>
/// Oyunun günü (GDD: 5 gün, her gün bir ürün: palto, pantolon, ayakkabı, çanta, gözlük).
/// TV o günün reklamını oynatır, mağazada sadece o günün ürünü alınabilir.
/// Statik: sahne geçişlerinde korunur. Gün yatakta uyuyunca ilerler (<see cref="DayRoutine.StartNextDay"/>).
/// </summary>
public static class GameDay
{
    private static readonly int ProductCount = Enum.GetValues(typeof(ProductType)).Length;

    /// <summary>0'dan başlayan gün sırası.</summary>
    public static int Index { get; private set; }

    /// <summary>Bugün reklamı verilen ve mağazada alınabilen ürün.</summary>
    public static ProductType Product => (ProductType)Math.Min(Index, ProductCount - 1);

    /// <summary>
    /// Test kolaylığı: true iken gün kısıtı yok, 5 ürünün her biri tek tek satın alınabilir ve mağaza kapısı
    /// bu ziyarette herhangi bir ürün alınınca açılır. Oyunda false (uyku döngüsü her gün bir ürün açar).
    /// </summary>
    public static bool AllProductsAvailable = false;

    /// <summary>Ürün bugün mağazada alınabilir mi.</summary>
    public static bool IsAvailable(ProductType product) => AllProductsAvailable || product == Product;

    public static event Action Changed;

    /// <summary>Ertesi güne geçer (yatakta uyuyunca).</summary>
    public static void Advance()
    {
        if (Index >= ProductCount - 1)
            return;
        Index++;
        Changed?.Invoke();
    }
}
