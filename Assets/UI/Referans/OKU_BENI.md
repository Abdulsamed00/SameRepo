# SAME – UI Paketi (1920×1080)

## Klasörler
- **1_Ekranlar_Onizleme** – Her ekranın arka planlı hali. Sadece referans içindir, oyunda kullanılmaz.
- **2_Ekranlar_Seffaf** – Aynı ekranların şeffaf PNG halleri. Sadece UI katmanı var. Unity'de bir referans Image olarak arkaya koyup üzerine hizalayabilirsiniz. Statik ekranlarda (Gün Geçişi, Jenerik) doğrudan da kullanılabilir.
- **3_Spritelar** – Unity'de Image olarak kullanılacak parçalar. Hepsi 1080p'de birebir boyutta. Image'a koyduktan sonra "Set Native Size" deyin.
- **4_Fontlar** – Inter ve IBM Plex Mono. İkisi de OFL lisanslı, oyunda ücretsiz kullanılabilir.

> Yazıları PNG olarak gömmeyin. Hepsini TextMeshPro ile yazın; aşağıdaki değerler TMP içindir.

## Unity kurulumu
- Canvas Scaler: **Scale With Screen Size**, 1920×1080, Match **0.5**
- Fontlardan TMP Font Asset oluşturun (Window › TextMeshPro › Font Asset Creator). Türkçe karakterler (ğ, ş, ı, İ, ç, ö, ü) için Character Set'i **Extended ASCII** veya **Custom** yapın.
- Yazı gölgesi: TMP materyalinde **Underlay** açın. Renk siyah, alfa 0.5, Softness 0.5, Offset Y −0.5.

## Renkler
| Kullanım | Değer |
|---|---|
| Ana yazı | #FFFFFF |
| Seçili olmayan menü | #FFFFFF, alfa %78 |
| İkincil yazı (sürüm, başlık üstü) | #FFFFFF, alfa %60 |
| Ayırıcı çizgi | #FFFFFF, alfa %8 (sekme altı %14) |
| Karartma: Ana menü | #080A0E, alfa %42 (sol 690 px'te ek %28 şerit) |
| Karartma: Ayarlar / Duraklatma | #080A0E, alfa %55 / %50, arkada blur |
| Fiş kâğıdı / yazı / ikincil / kesik çizgi | #F2EFE8 / #2B2A28 / #5E5B55 / #8F8A80 |

## Yazı tipleri (1080p, TMP Font Size / Character Spacing)
| Öğe | Font | Boyut | Aralık |
|---|---|---|---|
| Logo – ana menü | Inter Light | 81 | 55 |
| Logo – yükleme / jenerik | Inter Light | 39 / 45 | 55 |
| Ekran başlığı (AYARLAR, DURAKLATILDI) | Inter Light | 33 | 45 |
| Menü öğesi (seçili / normal) | Inter Medium / Regular | 26 | 6 |
| Sekme | Inter Medium | 22.5 | 4 |
| Ayar etiketi | Inter Regular | 22.5 | 0 |
| Seçim değeri ("Tam ekran") | Inter Medium | 22.5 | 0 |
| Alt bilgi (ESC Geri) | Inter Regular | 19.5 | 0 |
| Oyun içi ipucu | Inter Regular | 22.5 | 2 |
| Etkileşim yazısı ("Dolabı aç") | Inter Medium | 22.5 | 0 |
| Nesne adı ("YÜN PALTO") | Inter SemiBold | 16.5 | 22 |
| Gün geçişi üst / alt | Inter Medium / Light | 18 / 51 | 40 / 30 |
| Jenerik rol / isim | Inter Medium / Regular | 16.5 / 27 | 24 / 0 |
| Fiş | IBM Plex Mono Regular | 19.5 | 0 |

## Yerleşim (1080p piksel)
- **Ana menü:** Logo ve menü sol alt köşeye anchor'lı. Sol 144, alt 168. Logo ile menü arası 84. Menü satırı 66 yüksekliğinde. Seçili satırda solda `Secim_Cizgisi`, yazıya 24 px uzaklıkta.
- **Ayarlar:** Sol üste anchor'lı. Sol 144, üst 126, genişlik 960. Sekmeler arası 48. Satır yüksekliği 87. Slider genişliği 360. Alt bilgiler alttan 66, iki yandan 144.
- **Duraklatma:** Menü sola 144 px, dikeyde ortalı. Fiş sağdan 225, üstten 144, genişlik 435, **Rotation Z −2.5**.
- **Yükleme:** Logo sol alt (144, 108). Bar sağ alt (sağdan 144, alttan 120), 300×2 px.
- **Oyun içi:** `Nokta` ve `Halka` tam ekran ortasında. Etkileşim satırı ortanın 42 px altında, yatayda ortalı. İpucu yazısı sol üstte (72, 66).
- **Gün geçişi / Jenerik:** Her şey ortalı. Jenerikte sütun arası 180, satır arası 45.

## Davranış önerileri
- `Halka` ve etkileşim yazısı yalnızca raycast etkileşilebilir bir nesneye çarptığında görünür. 0.15 sn'de fade in/out. `Nokta` her zaman görünür.
- İpucu yazısı 0.4 sn'de belirir. Oyuncu hedefe ulaşınca kaybolur. Ekranda kalıcı görev kutusu yok.
- Gün geçişi: siyaha 1 sn fade, yazı 2 sn bekler, sonra sahneye 1 sn fade.
- Solma için URP **Color Adjustments › Saturation** değerleri: 1. gün 0, 2. gün −25, 3. gün −50, 4. gün −72, 5. gün −92. Ana menü arka planına da kayıttaki günün değerini verin.
- Fiş: `Fis_Kagit_Bos` üzerine TMP satırları. Satın alınan her ürün için bir satır eklenir. `Fis_Ornek_*` dosyaları sadece görünüm referansı.
