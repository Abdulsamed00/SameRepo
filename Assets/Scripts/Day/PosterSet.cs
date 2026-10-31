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

    /// <summary>
    /// Pano oranına (genişlik / yükseklik) uyan afişlerden <paramref name="index"/>'incisi. Aynı yönde (yatay/dikey)
    /// afiş yoksa ürünün tüm afişleri arasından seçilir.
    /// </summary>
    public Texture2D Pick(ProductType product, float boardAspect, int index)
    {
        foreach (Entry entry in entries)
        {
            if (entry.product != product || entry.images == null || entry.images.Length == 0)
                continue;

            int matching = 0;
            foreach (Texture2D image in entry.images)
                if (SameOrientation(image, boardAspect))
                    matching++;

            int pick = Mathf.Abs(index) % (matching > 0 ? matching : entry.images.Length);
            foreach (Texture2D image in entry.images)
            {
                if (matching > 0 && !SameOrientation(image, boardAspect))
                    continue;
                if (pick-- == 0)
                    return image;
            }
        }
        return null;
    }

    private static bool SameOrientation(Texture2D image, float boardAspect) =>
        image != null && (image.width >= image.height) == (boardAspect >= 1f);
}
