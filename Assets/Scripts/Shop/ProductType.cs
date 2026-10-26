/// <summary>Mağazada satın alınabilen ürünler (GDD: 5 gün, her gün bir ürün).</summary>
public enum ProductType
{
    Palto,
    Pantolon,
    Ayakkabi,
    Canta,
    Gozluk
}

public static class ProductTypeNames
{
    /// <summary>Ekranda görünen ad: "Gözlük".</summary>
    public static string DisplayName(this ProductType type) => type switch
    {
        ProductType.Palto => "Palto",
        ProductType.Pantolon => "Pantolon",
        ProductType.Ayakkabi => "Ayakkabı",
        ProductType.Canta => "Çanta",
        ProductType.Gozluk => "Gözlük",
        _ => type.ToString()
    };

    /// <summary>Belirtme hâli, bildirimlerde: "Gözlüğü al".</summary>
    public static string Accusative(this ProductType type) => type switch
    {
        ProductType.Palto => "Paltoyu",
        ProductType.Pantolon => "Pantolonu",
        ProductType.Ayakkabi => "Ayakkabıyı",
        ProductType.Canta => "Çantayı",
        ProductType.Gozluk => "Gözlüğü",
        _ => type.ToString()
    };
}
