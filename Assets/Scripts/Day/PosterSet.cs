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
