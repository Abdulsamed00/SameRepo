# Same – Unity Projesi

Unity 6.3 LTS (6000.3.26f1), URP, yeni Input System. Birinci şahıs oyun. Çalışma branch'i: `Samed`.

## Sahneler ve akış
Build Settings sırası: `MainMenu` → `Loading` → `HouseScene` → `StreetScene` → `ShopScene`

- Oyun `MainMenu` sahnesinden başlar. OYNA butonu `SceneLoader.Load("HouseScene")` çağırır.
- Sahne geçişleri doğrudan `SceneManager.LoadScene` ile değil, `SceneLoader.Load(...)` ile yapılır. Böylece yükleme ekranı araya girer.
- `GameScene` ve `SampleScene` artık yok. Kodda bu isimlere referans bırakma.

## Script'ler (`Assets/Scripts`)

### Player/FirstPersonController.cs
- Gerekli bileşen: `CharacterController`. Input, `InputSystem_Actions` asset'indeki `Player` map'inden okunur (`Move`, `Look`, `Sprint`).
- Inspector: `Input Actions` ve `Camera Root` alanları doldurulmalı.
- Hareket ivmelenmeli (`acceleration`). Koşma yalnızca ileri giderken çalışır.
- Head bob `CameraRoot`'un local pozisyonunu oynatır (`bobHeight`, `bobSway`, `bobFrequency`, `sprintBobMultiplier`, `bobReturnSpeed`).
- İmleç kilitlenirken gelen ilk fare delta'sı kamerayı aşağı çekiyordu. `OnEnable`'da ilk 3 karenin bakış girdisi atlanır, tek karedeki delta da `MaxLookDeltaPerFrame` ile sınırlanır.

### Loading/
- `SceneLoader` (static): `Load(sceneName)` hedefi saklar, `Loading` sahnesini açar.
- `LoadingScreen`: hedef sahneyi `LoadSceneAsync` ile yükler, ilerleme çubuğunu doldurur. `minimumDuration` (1.5 sn) kadar ekranda kalır, bitince sahneyi etkinleştirir.
  - **Önemli:** `Resources.UnloadUnusedAssets()` sahne yüklemesi başlamadan ÖNCE çağrılır. `allowSceneActivation = false` iken çağrılırsa yükleme kuyruğunun arkasında sonsuza kadar bekler ("Hazırlanıyor..." ekranında takılır).
- `MainMenu`: `OnPlayClicked` ve `OnQuitClicked`. Hedef sahne `gameScene` alanında (varsayılan `HouseScene`).
- Sahnelerde serialize edilmiş değerler koddaki varsayılanı ezer. Hedef sahneyi değiştirirken `MainMenu` ve `Loading` sahnelerindeki alanı da güncelle.

## Player kurulum kuralları
Hiyerarşi:
```
Player          (FirstPersonController + CharacterController)
├── Body        (MeshFilter + MeshRenderer, capsule)
└── CameraRoot  (localPos y = 1.6)
    └── Main Camera
```
- `CharacterController`: Height 1.8, Radius 0.3, Center (0, 0.9, 0). Pivot ayak hizasındadır.
- Capsule mesh'i Player'ın köküne KOYMA. Primitif capsule'ün pivotu ortasındadır, CC Center 0.9 yukarıda olduğu için mesh zeminin içinde kalır. Mesh `Body` alt objesinde: localPos (0, 0.9, 0), localScale (0.6, 0.9, 0.6).
- Yeni sahneye Player eklerken aynı yapıyı kullan.

## HouseScene blokajı
Evde sadece oturma odası ve yatak odası var. İkisi de giriş koridoruna açılır. Mutfak ve banyo YOK (kullanıcı kararı, tekrar ekleme).

`HouseBlockout` objesi (pos y = dış zemin üstü ≈ 1.21). Altında `Yapi` (Zemin, Duvarlar, Kapilar, Pencereler, Tavan) ve gruplar: `Oturma_Odasi`, `Yatak_Odasi`, `Koridor`, `Dis_Mekan`. Birim metre, dış ölçü 13x8 (x -7..6, z -4..4), duvar 0.2 kalın. Yerel koordinatlar `HouseBlockout`'a göredir.
- Ev zemini dış zeminden 0.15 yüksek (`Zemin` kutusu). Girişte veranda + bir basamak var. Tüm iç yükseklikler bu 0.15'in üstünden ölçülür.
- Ölçüler karakterden türetilir (CC boy 1.8, yarıçap 0.3, göz 1.6): kapı 1.2x2.3 (boy + 0.5 baş boşluğu, genişlik = 4 x yarıçap), duvar yüksekliği 3.0, pencere 1.6 geniş / denizlik 0.9 / üstü 2.3. Karakter ölçüsü değişirse ev de bu oranlarla yeniden üretilmeli.
- Koridor x[-7,6] z[-4,-1.8], iç genişlik 2.0 (açık giriş kapısının yanından geçilebilsin diye). Ana kapı güney duvarda x=-0.5. Ayakkabılık, anahtarlık, portmanto, 2 tablo, paspas, 2 tavan lambası.
- Oturma odası x[-7,0] z[-1.8,4]: koridordan kapısı z=-1.8 duvarında x=-2.5. TV ünitesi + TV (kuzey duvar), kanepe (TV'ye bakar, üstünde `Kumanda`), sehpa, halı, abajur, yan sehpa, berjer, kitaplık ve boy aynası (x=0 duvarı).
- Yatak odası x[0,6] z[-1.8,4]: koridordan kapısı z=-1.8 duvarında x=2.5. Yatak (başucu doğu duvarda), 2 komodin, dolap (koridor duvarına dayalı, GDD'deki etkileşimli dolap), çalışma masası + sandalye, kişisel eşya rafı, halı.
- Kapalı oda (GDD): koridorun doğu ucunda duvara yapışık, açılmayan sahte kapı (`Kapi_Kapali_Oda`). Arkasında oda yok.
- Dış mekan: veranda + basamak, `Eski_Esya_Alani` boş objesi (GDD'de kapı önünde biriken eski eşyaların konumu).
- Kapılar: `Kapi_*/Kanat_Pivot` menteşe noktasıdır, açma/kapama scripti bu pivotu Y ekseninde döndürmeli. Giriş ve oda kapıları açık (içeri doğru 90°).
- Pencereler: kasa, cam (`Blockout_Cam`, şeffaf), iç denizlik, perde. Oturma odasında 3, yatak odasında 2.
- Tavanın gölge atması kapalı (ışıklandırma yapılana kadar iç mekan karanlık kalmasın). Avizeler sadece şekil, ışık yok.
- Malzemeler `Assets/Materials/Blockout_*.mat`: Gri (duvar), Zemin, Mobilya, Kumas, Beyaz, Koyu, Kapi, Ayna, Vurgu, Cam. (`Blockout_Fayans` artık kullanılmıyor.)
- Küçük süs objelerinin (kitap, yastık, perde, halı vb.) collider'ı yok. Büyük mobilyaların ve kapı kanatlarının var.
- Player oturma odasında (-1.6, 1.6) doğar, batıya (kanepe/TV tarafına) bakar.
- Tasarımcılar için obje listesi ve ölçüler: Claude Docs "Same – Ev Obje Listesi (Meshy.ai)" (https://claude.ai/code/artifact/49c405e7-c794-4a20-8d1e-05d04de5028c). Blokajda ölçü değişirse orayı da güncelle.
- Boşlukları olan duvarlar CSG değil, parçalara bölünmüş kutulardır. Kapı/pencere yerini değiştirirken ilgili duvar parçalarını yeniden böl.
- Edit modunda fizik sorgusu (OverlapCapsule vb.) yapmadan önce `Physics.SyncTransforms()` çağır. Yoksa yeni oluşturulan veya dönen collider'lar görünmez ve sonuç yanlış "boş" çıkar.

## StreetScene blokajı (İstiklal Caddesi referanslı)
`StreetBlockout` objesi, dünya koordinatları. Cadde +z yönünde 72 m: z=0'da oyuncunun evi, z=72'de mağaza. Cepheler arası 12 m (x -6..6), ortası taş yol (zemin üstü y=0), iki yanda 1.5 m kaldırım (+0.12).
- `Zemin`: 3x3 m modüler `Tas_Zemin_*` parçaları, 1.5x3 m `Kaldirim_*` parçaları, ortada tramvay rayı (collider yok), altında `Zemin_Taban`.
- `Binalar`: 4 tip bina (A, B, C, D, iş planındaki "4 sokak binası"), iki yanda 6'şar tane. Zemin kat 4.5 m dükkân katı (vitrin, kapı, tabela), üst katlar 3.2 m. B ve D tiplerinde cumba var. Sol z[22,26] ve sağ z[34,38]'de çıkmaz ara sokak (ağızlarında dubalar). İki binada cephe reklam panosu (6x4 m).
- `Oyuncu_Evi_Binasi` (z=0): zemin katta oyuncunun kapısı, veranda + basamak, `Eski_Esya_Alani`. `Gecis_Ev` trigger'ı kapının önünde.
- `Magaza_Binasi` (z=72): iki vitrin (mankenler ve afişler), cam giriş kapısı, tabela. `Gecis_Magaza` trigger'ı kapının önünde.
- `Kafe`: sol D binasının zemin katı, tente + 3 masa, 6 sandalye.
- `Sokak_Mobilyasi`: 6 sokak lambası (12 m arayla, sırayla iki yanda), 5 park bankı, 7 çöp kutusu, 4 ayaklı reklam panosu (iki yüzlü), 12 sınır dubası.
- Gecis_* trigger'ları sadece kutu. Sahne geçiş scripti henüz YOK (SceneLoader.Load ile bağlanacak).
- Oyuncu `Assets/Prefabs/Player.prefab` örneği, evin önünde (0, 3) doğar, mağazaya (+z) bakar. HouseScene'deki Player da aynı prefab'a bağlı: Player'ı değiştirirken prefab'ı değiştir.
- Ölçüler İstiklal'den yaklaşık (hafızadan, ölçülmüş değil).

## Notlar
- UI'da yazılar legacy `UnityEngine.UI.Text` ile (TMP kurulu değil). Kod içinde `Image` adı bir namespace ile çakışırsa `UnityEngine.UI.Image` olarak tam yaz.
- Unity MCP kullanılırken script değişikliğinden sonra domain reload sırasında bağlantı kısa süre kopar ("Unity not detected"). Derleme bitince tekrar dene.
- `gh` PATH'te değil. Tam yol: `C:\Program Files\GitHub CLI\gh.exe`.
