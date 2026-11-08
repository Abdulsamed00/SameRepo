using System;
using UnityEngine;

/// <summary>
/// Ürün başına reklam afişleri (sokaktaki panolar o günün ürününün afişini gösterir, <see cref="DailyPoster"/>).
/// Bir ürünün birden fazla afişi olabilir (yatay, dikey...); pano kendi oranına uyanları sırayla kullanır.
/// </summary>
[CreateAssetMenu(menuName = "Same/Afiş Seti", fileName = "Afis_Seti")]
public class PosterSet : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public ProductType product;
        public Texture2D[] images;
    }

    [SerializeField] private Entry[] entries;

    [Tooltip("Kuaför günü (GameDay.Index = DayRoutine.SalonDay) kıyafet alındıktan sonra panolarda.")]
    [SerializeField] private Texture2D[] salonImages;
    [Tooltip("Klinik günü (GameDay.Index = DayRoutine.ClinicDay) kıyafet alındıktan sonra panolarda.")]
    [SerializeField] private Texture2D[] clinicImages;

    /// <summary>
    /// O gün panolarda ne görünür: kuaför/klinik gününde ürün alındıktan sonra o hizmetin afişi (oyuncu mağazadan
    /// çıkınca oraya gidecek), diğer durumlarda günün ürününün afişi.
    /// </summary>
    public Texture2D PickToday(float boardAspect, int index)
    {
        if (DayRoutine.BoughtToday)
        {
            Texture2D[] beauty = GameDay.Index == DayRoutine.SalonDay ? salonImages
                : GameDay.Index == DayRoutine.ClinicDay ? clinicImages : null;
            Texture2D image = Pick(beauty, boardAspect, index);
            if (image != null)
                return image;
        }
        return Pick(GameDay.Product, boardAspect, index);
    }

    /// <summary>
    /// Panolar karışık (08.11, kullanıcı: "reklam panoları karışık olsun, her şeyden"): her pano kendi tohumuna göre beş
    /// ürünün ve kuaför/klinik afişlerinin arasından birini gösterir. Kuaför/klinik gününde ürün alındıktan sonra
    /// panoların üçte biri o hizmetin afişi olur (oyuncu oraya yönlensin).
    /// </summary>
    public Texture2D PickMixed(float boardAspect, int seed)
    {
        seed = Mathf.Abs(seed);
        if (DayRoutine.BoughtToday && seed % 3 == 0)
        {
            Texture2D[] beauty = GameDay.Index == DayRoutine.SalonDay ? salonImages
                : GameDay.Index == DayRoutine.ClinicDay ? clinicImages : null;
            Texture2D image = Pick(beauty, boardAspect, seed / 3);
            if (image != null)
                return image;
        }

        ProductType[] products = (ProductType[])Enum.GetValues(typeof(ProductType));
        int slots = products.Length + 2;
        int slot = (seed * 7 + seed / 5) % slots;
        Texture2D picked = slot < products.Length
            ? Pick(products[slot], boardAspect, seed / slots)
            : Pick(slot == products.Length ? salonImages : clinicImages, boardAspect, seed / slots);
        return picked != null ? picked : Pick(GameDay.Product, boardAspect, seed);
    }

    /// <summary>
    /// Pano oranına (genişlik / yükseklik) uyan afişlerden <paramref name="index"/>'incisi. Aynı yönde (yatay/dikey)
    /// afiş yoksa ürünün tüm afişleri arasından seçilir.
    /// </summary>
    public Texture2D Pick(ProductType product, float boardAspect, int index)
    {
        foreach (Entry entry in entries)
        {
            if (entry.product != product)
                continue;
            Texture2D image = Pick(entry.images, boardAspect, index);
            if (image != null)
                return image;
        }
        return null;
    }

    private static Texture2D Pick(Texture2D[] images, float boardAspect, int index)
    {
        if (images == null || images.Length == 0)
            return null;

        int matching = 0;
        foreach (Texture2D image in images)
            if (SameOrientation(image, boardAspect))
                matching++;

        int pick = Mathf.Abs(index) % (matching > 0 ? matching : images.Length);
        foreach (Texture2D image in images)
        {
            if (matching > 0 && !SameOrientation(image, boardAspect))
                continue;
            if (pick-- == 0)
                return image;
        }
        return null;
    }

    private static bool SameOrientation(Texture2D image, float boardAspect) =>
        image != null && (image.width >= image.height) == (boardAspect >= 1f);
}
