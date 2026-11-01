using System;

/// <summary>
/// Günlük döngü: TV'de günün reklamını izle, mağazaya git, günün ürününü al, eve dön, giy, aynada poz ver, uyu.
/// Reklam izlenmeden mağazada ürün alınamaz ve evden çıkılamaz (<see cref="HouseExitLock"/>); ürün alınınca da
/// o gün evden çıkılamaz; yatak ancak giyip poz verdikten sonra uyutur (<see cref="Bed"/>).
/// Son günde (gözlük) uyku yok: giyip poz verince <see cref="FinalReady"/> olur, kapı açılır ve sokakta final başlar.
/// Statik: sahne geçişlerinde korunur.
/// </summary>
public static class DayRoutine
{
    /// <summary>Uykunun geldiği an (aynada poz bitince; giyilecek kıyafet yoksa eve girince): yatak esneme sesini çalar.</summary>
    public static event Action Sleepy;

    /// <summary>Bugünün ürünü aldıktan ve giydikten sonra aynada poz verildi.</summary>
    public static bool PosedToday { get; private set; }

    /// <summary>Bugünün reklamı sonuna kadar izlendi (<see cref="Television.AdWatched"/>).</summary>
    public static bool AdWatchedToday { get; private set; }

    /// <summary>Reklam şartı sağlandı mı (test bayrağı <see cref="GameDay.AllProductsAvailable"/> şartı atlar).</summary>
    public static bool AdRequirementMet => AdWatchedToday || GameDay.AllProductsAvailable;

    public static bool BoughtToday => PlayerPurchases.Owns(GameDay.Product);

    /// <summary>
    /// Bugünün ürünü giyildi. Evde o ürün için dolapta kıyafet yoksa (<see cref="WardrobeClothing"/>)
    /// giyme adımı atlanır, yoksa döngü o günde takılırdı.
    /// </summary>
    public static bool WornToday =>
        PlayerOutfit.IsWorn(GameDay.Product) || !WardrobeClothing.ExistsFor(GameDay.Product);

    /// <summary>Uyunabilir: son gün hariç (orada uyku yerine final var).</summary>
    public static bool CanSleep => !GameDay.IsLastDay && BoughtToday && WornToday && PosedToday;

    /// <summary>Son günün ürünü alındı, giyildi, poz verildi: evden çıkınca final başlar.</summary>
    public static bool FinalReady => GameDay.IsLastDay && BoughtToday && WornToday && PosedToday;

    public static void NotifyAdWatched(ProductType product)
    {
        if (product == GameDay.Product)
            AdWatchedToday = true;
    }

    /// <summary>Ev dışına çıkıldı: eve dönünce hava kararmıştır (<see cref="HouseNightfall"/>). Ertesi güne kadar sürer.</summary>
    public static bool NightFell { get; private set; }

    public static void NotifyLeftHouse() => NightFell = true;

    // Esneme giyince değil, aynada poz verince gelir (NotifyPoseEnded).
    public static void NotifyWorn(ProductType product)
    {
    }

    public static void NotifyPoseEnded()
    {
        if (!BoughtToday || !WornToday)
            return;
        PosedToday = true;
        if (!GameDay.IsLastDay)
            Sleepy?.Invoke();
    }

    /// <summary>Eve dönüldüğünde giyilecek bir şey yoksa uyku hemen gelir.</summary>
    public static void NotifyArrivedHome()
    {
        if (BoughtToday && !WardrobeClothing.ExistsFor(GameDay.Product) && !PosedToday && !GameDay.IsLastDay)
            Sleepy?.Invoke();
    }

    public static void StartNextDay()
    {
        PosedToday = false;
        AdWatchedToday = false;
        NightFell = false;
        GameDay.Advance();
    }

    /// <summary>Yeni oyun: tüm ilerleme sıfırlanır (ana menüde, finalden sonra).</summary>
    public static void ResetAll()
    {
        PosedToday = false;
        AdWatchedToday = false;
        NightFell = false;
        PlayerPurchases.Reset();
        PlayerOutfit.Reset();
        GameDay.Reset();
    }
}
