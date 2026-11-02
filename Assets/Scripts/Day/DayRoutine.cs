using System;

/// <summary>
/// Günlük döngü: TV'de günün reklamını izle, mağazaya git, günün ürününü al, eve dön, giy, aynada poz ver, uyu.
/// Reklam izlenmeden mağazada ürün alınamaz ve evden çıkılamaz (<see cref="HouseExitLock"/>); ürün alınınca da
/// o gün evden çıkılamaz; yatak ancak giyip poz verdikten sonra uyutur (<see cref="Bed"/>).
/// Son günde (gözlük) de uyunur ama gün ilerlemez: sabah olur (<see cref="FinalMorning"/>), <see cref="FinalReady"/> olur,
/// kapı açılır ve sokakta final başlar. Eskiden son gün uyku yoktu; eve dönünce ev gece oluyordu ama poz verip
/// çıkınca sokak akşamüstüydü (01.11).
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

    /// <summary>Uyunabilir: günün ürünü alındı, giyildi, poz verildi (son günde bir kez, finalden önce).</summary>
    public static bool CanSleep => BoughtToday && WornToday && PosedToday && !FinalMorning;

    /// <summary>Son günün gecesi uyunup sabah olduğu (gün sayısı ilerlemez, ürün gözlükte kalır).</summary>
    public static bool FinalMorning { get; private set; }

    /// <summary>Son günün ürünü alındı, giyildi, poz verildi ve uyunup sabah oldu: evden çıkınca final başlar.</summary>
    public static bool FinalReady => GameDay.IsLastDay && BoughtToday && WornToday && PosedToday && FinalMorning;

    /// <summary>Ekranda görünen gün numarası (1'den). Final sabahı son günün ertesidir.</summary>
    public static int DayNumber => GameDay.Index + 1 + (FinalMorning ? 1 : 0);

    /// <summary>Uyuyup sabah olunca (ekran karanlıkken): ev gecesi biter (<see cref="HouseNightfall"/>).</summary>
    public static event Action Morning;

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
        NightFell = false;
        if (GameDay.IsLastDay)
        {
            // Son günün sabahı: ürün ve bayraklar yerinde kalır, kapı finale açılır.
            FinalMorning = true;
        }
        else
        {
            PosedToday = false;
            AdWatchedToday = false;
            GameDay.Advance();
        }
        Morning?.Invoke();
    }

    /// <summary>Yeni oyun: tüm ilerleme sıfırlanır (ana menüde, finalden sonra).</summary>
    public static void ResetAll()
    {
        PosedToday = false;
        AdWatchedToday = false;
        NightFell = false;
        FinalMorning = false;
        PlayerPurchases.Reset();
        PlayerOutfit.Reset();
        GameDay.Reset();
    }
}
