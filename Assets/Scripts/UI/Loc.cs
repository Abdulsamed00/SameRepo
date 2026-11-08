using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum GameLanguage
{
    English = 0,
    Turkish = 1
}

/// <summary>
/// Dil ayarı ve çeviri (07.11). Oyunun metinleri kodda ve sahnelerde Türkçe yazılı; bu Türkçe metin anahtardır,
/// İngilizce karşılığı <see cref="English"/> tablosunda. Çeviri metnin gösterildiği yerde yapılır
/// (<see cref="UiKit.CreateText"/>, <see cref="InteractionPromptView"/>, <see cref="ScreenFader.Caption"/>), bu yüzden
/// sahnelerde serialize edilmiş bildirimler ("Kapıyı aç") de çevrilir. Tabloda olmayan metin olduğu gibi gösterilir.
/// Yeni bir metin eklerken tabloya da satır ekle. Varsayılan İngilizce; seçim PlayerPrefs "Ayar_Dil".
/// </summary>
public static class Loc
{
    private const string PrefKey = "Ayar_Dil";
    private static readonly CultureInfo TurkishCulture = new CultureInfo("tr-TR");
    private static GameLanguage? current;

    /// <summary>Dil değişince (Ayarlar): açık yazılar (<see cref="LocText"/>) ve ekranlar yeniden çevrilir.</summary>
    public static event Action Changed;

    /// <summary>Her dil değişiminde artar: önbellekli yazılar (bildirim satırı) yeniden kurulsun diye.</summary>
    public static int Version { get; private set; }

    public static GameLanguage Current
    {
        get
        {
            if (current == null)
                current = (GameLanguage)PlayerPrefs.GetInt(PrefKey, (int)GameLanguage.English);
            return current.Value;
        }
    }

    public static bool IsEnglish => Current == GameLanguage.English;

    public static void SetLanguage(GameLanguage language)
    {
        if (language == Current)
            return;
        current = language;
        PlayerPrefs.SetInt(PrefKey, (int)language);
        PlayerPrefs.Save();
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Dilin kendi adı (ayar ekranında çevrilmeden gösterilir).</summary>
    public static string NativeName(GameLanguage language) => language == GameLanguage.Turkish ? "Türkçe" : "English";

    /// <summary>Türkçe metni seçili dile çevirir. Boş/null olduğu gibi döner; tabloda yoksa Türkçesi kalır.</summary>
    public static string T(string turkish)
    {
        if (string.IsNullOrEmpty(turkish) || !IsEnglish)
            return turkish;
        return English.TryGetValue(turkish, out string english) ? english : turkish;
    }

    /// <summary>Seçili dilin kurallarıyla büyük harf (Türkçede i → İ).</summary>
    public static string Upper(string text) =>
        string.IsNullOrEmpty(text) ? text : IsEnglish ? text.ToUpperInvariant() : text.ToUpper(TurkishCulture);

    /// <summary>Gün başlığı: "3. GÜN" / "DAY 3".</summary>
    public static string DayTitle(int day) => IsEnglish ? $"DAY {day}" : $"{day}. GÜN";

    /// <summary>Fişteki gün satırı: "GÜN 3" / "DAY 3".</summary>
    public static string DayShort(int day) => IsEnglish ? $"DAY {day}" : $"GÜN {day}";

    private static readonly Dictionary<string, string> English = new Dictionary<string, string>
    {
        // Etkileşim bildirimleri (kod ve sahneler)
        { "Etkileşime gir", "Interact" },
        { "Aç", "Open" },
        { "Kapat", "Close" },
        { "Kapıyı aç", "Open door" },
        { "Kapıyı kapat", "Close door" },
        { "Dolabı aç", "Open wardrobe" },
        { "Dolabı kapat", "Close wardrobe" },
        { "Kapı kilitli", "The door is locked" },
        { "Işığı aç", "Turn on the light" },
        { "Işığı kapat", "Turn off the light" },
        { "Otur", "Sit" },
        { "Kalk", "Stand up" },
        { "Bırak", "Put down" },
        { "Poz ver", "Pose" },
        { "Poz vermeyi bırak", "Stop posing" },
        { "Kumandayı al", "Take the remote" },
        { "Cüzdanı al", "Take the wallet" },
        { "Uyu", "Sleep" },
        { "Henüz uykum yok", "I'm not sleepy yet" },
        { "Bu saatte dışarı çıkılmaz", "It's too late to go out" },
        { "Bugünlük dışarısı yeter", "That's enough outside for today" },
        { "Giy", "Wear" },
        { "Geri as", "Hang it back" },
        { "Satın al", "Buy" },
        { "Al", "Take" },
        { "Sokağa çık", "Go outside" },
        { "Mağazaya gir", "Enter the store" },
        { "Kuaföre gir", "Enter the hair salon" },
        { "Kliniğe gir", "Enter the clinic" },
        { "Eve dön", "Go home" },

        // Ürünler (ad, bildirimin üstündeki büyük harfli ad, dolapta inceleme)
        { "Palto", "Coat" },
        { "Pantolon", "Jeans" },
        { "Ayakkabı", "Shoes" },
        { "Çanta", "Bag" },
        { "Gözlük", "Glasses" },
        { "PALTO", "COAT" },
        { "PANTOLON", "JEANS" },
        { "AYAKKABI", "SHOES" },
        { "ÇANTA", "BAG" },
        { "GÖZLÜK", "GLASSES" },
        { "Paltoyu incele", "Inspect the coat" },
        { "Pantolonu incele", "Inspect the jeans" },
        { "Ayakkabıyı incele", "Inspect the shoes" },
        { "Çantayı incele", "Inspect the bag" },
        { "Gözlüğü incele", "Inspect the glasses" },

        // Ekran yazıları (gün geçişi, kuaför/klinik, final)
        { "SABAH", "MORNING" },
        { "ERTESİ SABAH", "THE NEXT MORNING" },
        { "BİRKAÇ SAAT SONRA", "A FEW HOURS LATER" },
        { "YENİ BİR YÜZ", "A NEW FACE" },
        { "SARI SAÇLAR", "BLONDE HAIR" },
        { "Sen eşsizsin. Tıpkı diğer herkes gibi.", "You are unique. Just like everyone else." },
        { "Sahip olduğun şeyler, sonunda seni sahiplenir.", "The things you own end up owning you." },

        // Ana menü, duraklatma, yükleme
        { "Oyna", "Play" },
        { "Ayarlar", "Settings" },
        { "Hazırlayanlar", "Credits" },
        { "Çıkış", "Quit" },
        { "DURAKLATILDI", "PAUSED" },
        { "Devam et", "Resume" },
        { "Ana menüye dön", "Main menu" },
        { "YÜKLENİYOR", "LOADING" },

        // Ayarlar
        { "AYARLAR", "SETTINGS" },
        { "Ses", "Audio" },
        { "Kontroller", "Controls" },
        { "Görüntü", "Display" },
        { "Geri", "Back" },
        { "Varsayılanlara dön", "Reset to defaults" },
        { "Genel ses", "Master volume" },
        { "Müzik", "Music" },
        { "Efektler", "Effects" },
        { "Ortam sesleri", "Ambience" },
        { "Fare hassasiyeti", "Mouse sensitivity" },
        { "Y eksenini ters çevir", "Invert Y axis" },
        { "Dil", "Language" },
        { "Ekran modu", "Display mode" },
        { "Tam ekran", "Fullscreen" },
        { "Pencere", "Windowed" },
        { "Çözünürlük", "Resolution" },
        { "Parlaklık", "Brightness" },
        { "Dikey senkronizasyon", "VSync" },
        { "Grafik kalitesi", "Graphics quality" },
        { "Render ölçeği", "Render scale" },
        { "Görsel efektler", "Visual effects" },
        { "Düşük", "Low" },
        { "Orta", "Medium" },
        { "Yüksek", "High" },
        { "Kapalı", "Off" },
        { "Açık", "On" },
        { "Tuşlar", "Key bindings" },
        { "İleri", "Move forward" },
        { "Geri git", "Move back" },
        { "Sola git", "Move left" },
        { "Sağa git", "Move right" },
        { "Koş", "Sprint" },
        { "Etkileşim", "Interact" },
        { "Otur / ürünü al", "Sit / take item" },
        { "Bir tuşa bas  (ESC iptal)", "Press a key  (ESC to cancel)" },

        // Hazırlayanlar
        { "ÇEVRE MODELLEME · UI", "ENVIRONMENT MODELING · UI" },
        { "KARAKTER · SES · REKLAM", "CHARACTERS · SOUND · ADS" },
        { "OYNANIŞ PROGRAMLAMA", "GAMEPLAY PROGRAMMING" },
        { "SİSTEM PROGRAMLAMA", "SYSTEMS PROGRAMMING" },
        { "Atla", "Skip" },

        // Satış fişi
        { "SATIŞ FİŞİ", "RECEIPT" },
        { "Mağaza · Kasa 02", "Store · Register 02" },
        { "1 AD", "1 PC" },
        { "KUAFÖR", "HAIR SALON" },
        { "ESTETİK", "AESTHETICS" },
        { "TOPLAM ADET", "TOTAL ITEMS" },
        { "Yine bekleriz.", "Thank you, come again." },
    };
}
