using System;
using System.Collections.Generic;

/// <summary>
/// Günlük döngü (05.11): oyun gece başlar (<see cref="Prologue"/>), oyuncu TV'de ilk ürünün reklamını izler, uyur.
/// Sabah cüzdanı alır (<see cref="WalletTaken"/>), mağazada günün ürününü alır, eve döner (gece), giyer, aynada poz
/// verir, TV'de ertesi günün ürününün reklamını izler (<see cref="TvProduct"/>), uyur. TV hiç kapanmaz.
/// Ürünün reklamı izlenmeden mağazada alınamaz; ürün alınınca o gün evden çıkılamaz; yatak ancak giyip poz verip
/// gecenin reklamını izledikten sonra uyutur (<see cref="Bed"/>).
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

    // Reklamı sonuna kadar izlenmiş ürünler (Television.AdWatched). Reklam bir önceki gece izlenir.
    private static readonly HashSet<ProductType> adsSeen = new HashSet<ProductType>();

    /// <summary>Bugünün ürününün reklamı izlendi (önceki gece).</summary>
    public static bool AdWatchedToday => adsSeen.Contains(GameDay.Product);

    /// <summary>Reklam şartı sağlandı mı (test bayrağı <see cref="GameDay.AllProductsAvailable"/> şartı atlar).</summary>
    public static bool AdRequirementMet => true; // 05.11: TV izleme şartı kaldırıldı (kullanıcı isteği), reklam sadece oynar.

    public static bool BoughtToday => PlayerPurchases.Owns(GameDay.Product);

    /// <summary>Oyunun ilk gecesi (menü ve sonrası): henüz gün başlamadı, reklam izlenip uyunur.</summary>
    public static bool Prologue { get; private set; } = true;

    /// <summary>
    /// TV'nin oynattığı reklamın ürünü: günün ürünü alındıysa ertesi günün ürünü (gece onu izler), yoksa bugünün.
    /// Son günde ertesi gün yok, gözlükte kalır.
    /// </summary>
    public static ProductType TvProduct =>
        BoughtToday && !GameDay.IsLastDay ? (ProductType)(GameDay.Index + 1) : GameDay.Product;

    /// <summary>Bu gecenin reklamı izlendi mi (son günün gecesinde izlenecek reklam yok).</summary>
    public static bool NightAdSeen => (GameDay.IsLastDay && !Prologue) || adsSeen.Contains(TvProduct);

    /// <summary>Bu sabah cüzdan masadan alındı (<see cref="Wallet"/>): alınmadan evden çıkılmaz.</summary>
    public static bool WalletTaken { get; private set; }

    public static void NotifyWalletTaken() => WalletTaken = true;

    /// <summary>
    /// Bugünün ürünü giyildi. Evde o ürün için dolapta kıyafet yoksa (<see cref="WardrobeClothing"/>)
    /// giyme adımı atlanır, yoksa döngü o günde takılırdı.
    /// </summary>
    public static bool WornToday =>
        PlayerOutfit.IsWorn(GameDay.Product) || !WardrobeClothing.ExistsFor(GameDay.Product);

    /// <summary>Uyunabilir: günün ürünü alındı, giyildi, poz verildi (son günde bir kez, finalden önce).</summary>
    public static bool CanSleep => NightFell && !FinalMorning
                                   && (Prologue || (BoughtToday && WornToday && PosedToday));

    /// <summary>Son günün gecesi uyunup sabah olduğu (gün sayısı ilerlemez, ürün gözlükte kalır).</summary>
    public static bool FinalMorning { get; private set; }

    /// <summary>Son günün ürünü alındı, giyildi, poz verildi ve uyunup sabah oldu: evden çıkınca final başlar.</summary>
    public static bool FinalReady => GameDay.IsLastDay && BoughtToday && WornToday && PosedToday && FinalMorning;

    /// <summary>Ekranda görünen gün numarası (1'den). Final sabahı son günün ertesidir.</summary>
    public static int DayNumber => GameDay.Index + 1 + (FinalMorning ? 1 : 0);

    /// <summary>Uyuyup sabah olunca (ekran karanlıkken): ev gecesi biter (<see cref="HouseNightfall"/>).</summary>
    public static event Action Morning;

    /// <summary>
    /// Bu sabah TV'de reklam sonuna kadar izlendi (05.11, kullanıcı: "TV koşulu sabah için olsun"): cüzdan ancak bundan
    /// sonra parlar ve alınabilir. Gece izlemenin şartı yok.
    /// </summary>
    public static bool MorningAdSeen { get; private set; }

    public static void NotifyAdWatched(ProductType product)
    {
        if (!NightFell)
            MorningAdSeen = true;
        if (adsSeen.Add(product) && CanSleep)
            Sleepy?.Invoke();
    }

    /// <summary>Kuaför günü (2. gün).</summary>
    public const int SalonDay = 1;

    /// <summary>Klinik günü (4. gün) sabahı aynaya bakmadan evden çıkılmaz; aynada yenilgi animasyonu oynar.</summary>
    public const int ClinicDay = 3;

    /// <summary>Kuaför günü (2. gün, sarışınlar).</summary>
    public const int SalonDay = 1;

    public static bool MorningMirrorSeen { get; private set; }

    public static bool MorningMirrorPending => GameDay.Index == ClinicDay && !MorningMirrorSeen && !BoughtToday;

    public static void NotifyMorningMirrorSeen() => MorningMirrorSeen = true;

    /// <summary>
    /// Gece: oyun başında (ilk gece) ve evden çıkıp dönünce (<see cref="HouseNightfall"/>). Uyuyup sabah olunca biter.
    /// </summary>
    public static bool NightFell { get; private set; } = true;

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
        if (CanSleep)
            Sleepy?.Invoke();
    }

    /// <summary>Eve dönüldüğünde giyilecek bir şey yoksa uyku hemen gelir.</summary>
    public static void NotifyArrivedHome()
    {
        if (BoughtToday && !WardrobeClothing.ExistsFor(GameDay.Product) && !PosedToday)
            PosedToday = true;
        if (CanSleep)
            Sleepy?.Invoke();
    }

    public static void StartNextDay()
    {
        NightFell = false;
        WalletTaken = false;
        MorningAdSeen = false;
        if (Prologue)
        {
            // İlk gecenin sabahı: 1. gün başlar, gün sayısı ilerlemez.
            Prologue = false;
        }
        else if (GameDay.IsLastDay)
        {
            // Son günün sabahı: ürün ve bayraklar yerinde kalır, kapı finale açılır.
            FinalMorning = true;
        }
        else
        {
            PosedToday = false;
            GameDay.Advance();
        }
        Morning?.Invoke();
    }

    /// <summary>Yeni oyun: tüm ilerleme sıfırlanır (ana menüde, finalden sonra). Oyun gece başlar.</summary>
    public static void ResetAll()
    {
        PosedToday = false;
        adsSeen.Clear();
        Prologue = true;
        NightFell = true;
        WalletTaken = false;
        MorningAdSeen = false;
        FinalMorning = false;
        MorningMirrorSeen = false;
        PlayerLook.Reset();
        PlayerPurchases.Reset();
        PlayerOutfit.Reset();
        GameDay.Reset();
    }
}
