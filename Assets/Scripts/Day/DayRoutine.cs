using System;

/// <summary>
/// Günlük döngü: evden mağazaya git, günün ürününü al, eve dön, giy, aynada poz ver, uyu.
/// Ürün alınınca evden çıkılamaz (<see cref="HouseExitLock"/>); yatak ancak giyip poz verdikten sonra
/// uyutur (<see cref="Bed"/>). Uyuyunca <see cref="StartNextDay"/> ertesi güne geçer.
/// Statik: sahne geçişlerinde korunur.
/// </summary>
public static class DayRoutine
{
    /// <summary>Uykunun geldiği anlar (giyince, pozdan sonra): yatak esneme sesini çalar.</summary>
    public static event Action Sleepy;

    /// <summary>Bugünün ürünü aldıktan ve giydikten sonra aynada poz verildi.</summary>
    public static bool PosedToday { get; private set; }

    public static bool BoughtToday => PlayerPurchases.Owns(GameDay.Product);

    /// <summary>
    /// Bugünün ürünü giyildi. Evde o ürün için dolapta kıyafet yoksa (<see cref="WardrobeClothing"/>
    /// henüz sadece palto için var) giyme adımı atlanır, yoksa döngü o günde takılırdı.
    /// </summary>
    public static bool WornToday =>
        PlayerOutfit.IsWorn(GameDay.Product) || !WardrobeClothing.ExistsFor(GameDay.Product);

    public static bool CanSleep => BoughtToday && WornToday && PosedToday;

    public static void NotifyWorn(ProductType product)
    {
        if (product == GameDay.Product && BoughtToday)
            Sleepy?.Invoke();
    }

    public static void NotifyPoseEnded()
    {
        if (!BoughtToday || !WornToday)
            return;
        PosedToday = true;
        Sleepy?.Invoke();
    }

    /// <summary>Eve dönüldüğünde giyilecek bir şey yoksa uyku hemen gelir.</summary>
    public static void NotifyArrivedHome()
    {
        if (BoughtToday && !WardrobeClothing.ExistsFor(GameDay.Product) && !PosedToday)
            Sleepy?.Invoke();
    }

    public static void StartNextDay()
    {
        PosedToday = false;
        GameDay.Advance();
    }
}
