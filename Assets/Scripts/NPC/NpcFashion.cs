using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sokağın modası: her ürün kendi günü gelince NPC'lerin ~%30'unda görünür (oyuncu daha almadan), sonraki günlerde
/// giyen oranı artar, son gün herkeste olur. Son ürün de alınınca herkes oyuncuyla aynı giyinir.
/// NPC'ler <see cref="NpcDailyOutfit"/> ile sorar; oran her grup (erkek/kadın) için ayrı tutturulur.
/// </summary>
public static class NpcFashion
{
    /// <summary>Ürün kendi gününde NPC'lerin bu kadarında görünür.</summary>
    public const float FirstDayShare = 0.3f;

    private static readonly int ProductCount = Enum.GetValues(typeof(ProductType)).Length;
    private static readonly Dictionary<string, List<NpcDailyOutfit>> groups = new Dictionary<string, List<NpcDailyOutfit>>();
    // Oyun başına bir kez: hangi NPC'lerin önce giydiği her oyunda farklı, oyun içinde günden güne aynı.
    private static readonly int seed = Guid.NewGuid().GetHashCode();

    /// <summary>Bugün ürünü giyen NPC oranı (0-1).</summary>
    public static float Share(ProductType product)
    {
        if (PlayerPurchases.OwnsAll)
            return 1f;

        int first = (int)product, last = ProductCount - 1, day = GameDay.Index;
        if (day < first)
            return 0f;
        if (first >= last)
            return FirstDayShare;
        return FirstDayShare + (1f - FirstDayShare) * (day - first) / (last - first);
    }

    // Sarışın kadın oranı, güne göre: 2. günde sokakta göze batar (oyuncu kuaföre gider), sonra çoğalır.
    private static readonly float[] BlondeShares = { 0.1f, 0.5f, 0.6f, 0.75f, 0.9f };

    /// <summary>Bu kadın NPC bugün sarışın mı. Son ürün de alınınca herkes.</summary>
    public static bool IsBlonde(NpcDailyOutfit npc)
    {
        if (PlayerPurchases.OwnsAll)
            return true;
        return InShare(npc, ProductCount, BlondeShares[Mathf.Clamp(GameDay.Index, 0, BlondeShares.Length - 1)]);
    }

    // Estetikli (küçük burun, dolgun dudak) kadın oranı, güne göre (09.11, kullanıcı: "estetiği o mantıkta NPC kadınlara da yap"):
    // sarışınlık gibi: klinik günü (4. gün, Index 3) sokakta göze batar, sonra çoğalır.
    private static readonly float[] SurgeryShares = { 0.1f, 0.2f, 0.35f, 0.6f, 0.8f };

    /// <summary>Bu kadın NPC bugün estetikli mi. Son ürün de alınınca herkes.</summary>
    public static bool HasSurgery(NpcDailyOutfit npc)
    {
        if (PlayerPurchases.OwnsAll)
            return true;
        return InShare(npc, ProductCount + 1, SurgeryShares[Mathf.Clamp(GameDay.Index, 0, SurgeryShares.Length - 1)]);
    }

    public static void Register(NpcDailyOutfit npc)
    {
        if (!groups.TryGetValue(npc.Group, out List<NpcDailyOutfit> list))
            groups[npc.Group] = list = new List<NpcDailyOutfit>();
        if (!list.Contains(npc))
            list.Add(npc);
    }

    public static void Unregister(NpcDailyOutfit npc)
    {
        if (groups.TryGetValue(npc.Group, out List<NpcDailyOutfit> list))
            list.Remove(npc);
    }

    /// <summary>
    /// NPC ürünü giysin mi. Gruptaki NPC'ler ürün başına sabit bir sıraya dizilir, ilk (oran x kişi sayısı) kadarı giyer:
    /// 6 kadından %30'u hep 2 kişi olur, dün giyen bugün de giyer (sahnedeki NPC'ler her gün aynı yerde).
    /// </summary>
    public static bool Wears(NpcDailyOutfit npc, ProductType product)
    {
        return InShare(npc, (int)product, Share(product));
    }

    private static bool InShare(NpcDailyOutfit npc, int salt, float share)
    {
        if (share <= 0f)
            return false;
        if (share >= 1f || !groups.TryGetValue(npc.Group, out List<NpcDailyOutfit> list) || list.Count == 0)
            return share >= 1f;

        int wearers = Mathf.Max(1, Mathf.RoundToInt(share * list.Count));
        double score = Score(npc, salt);
        int rank = 0;
        foreach (NpcDailyOutfit other in list)
        {
            if (other != npc && Score(other, salt) < score)
                rank++;
        }
        return rank < wearers;
    }

    private static double Score(NpcDailyOutfit npc, int salt) =>
        new System.Random(unchecked(npc.Key.GetHashCode() * 31 + salt * 7919 + seed)).NextDouble();
}
