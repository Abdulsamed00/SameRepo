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

## HouseScene blokajı (1+1 ev)
`HouseBlockout` objesi (pos y = dış zemin üstü ≈ 1.21). Altında `Yapi` (Zemin, Duvarlar, Kapilar, Pencereler, Tavan) ve oda grupları: `Salon`, `Mutfak`, `Yatak_Odasi`, `Hol`, `Banyo`, `Kapali_Oda`, `Dis_Mekan`. Birim metre, dış ölçü 16x12 (x -8..8, z -6..6), duvar 0.2 kalın. Yerel koordinatlar `HouseBlockout`'a göredir.
- Ev zemini dış zeminden 0.15 yüksek (`Zemin` kutusu). Girişte veranda + bir basamak var. Tüm iç yükseklikler bu 0.15'in üstünden ölçülür.
- Ölçüler karakterden türetilir (CC boy 1.8, yarıçap 0.3, göz 1.6): kapı 1.2x2.3 (boy + 0.5 baş boşluğu, genişlik = 4 x yarıçap), duvar yüksekliği 3.0, pencere 1.6 geniş / denizlik 0.9 / üstü 2.3. Karakter ölçüsü değişirse ev de bu oranlarla yeniden üretilmeli.
- Salon + amerikan mutfak x[-8,2] z[-1,6]: TV ünitesi + TV (kuzey duvar), kanepe (TV'ye bakar, üstünde `Kumanda`), sehpa, halı, abajur, yan sehpa, kitaplık ve boy aynası (x=2 duvarı). Mutfak batıda: L tezgâh, evye, ocak, üst dolap, aspiratör, buzdolabı, 4 sandalyeli yemek masası.
- Yatak odası x[2,8] z[-1,6]: yatak (başucu doğu duvarda), 2 komodin, dolap (güney duvar, GDD'deki etkileşimli dolap), çalışma masası + sandalye, kişisel eşya rafı, halı. Salondan kapısı x=2 duvarında z=0.5.
- Hol x[-3,2] z[-6,-1]: ana kapı güney duvarda x=-0.5, ayakkabılık, anahtarlık, portmanto, 2 tablo, paspas. Salona z=-1 duvarında 3 m kemer.
- Banyo x[-8,-3] z[-6,-1]: küvet, klozet, lavabo + ayna, havluluk, yüksek pencere. Holden kapısı x=-3 duvarında z=-3.5.
- Kapalı oda x[2,8] z[-6,-1]: kapısı (x=2, z=-3.5) KAPALI, içinde birkaç kutu.
- Dış mekan: veranda + basamak, `Eski_Esya_Alani` boş objesi (GDD'de kapı önünde biriken eski eşyaların konumu).
- Kapılar: `Kapi_*/Kanat_Pivot` menteşe noktasıdır, açma/kapama scripti bu pivotu Y ekseninde döndürmeli. Giriş, yatak odası ve banyo kapıları açık, kapalı oda kapısı kapalı.
- Pencereler: kasa, cam (`Blockout_Cam`, şeffaf), iç denizlik, perde.
- Tavanın gölge atması kapalı (ışıklandırma yapılana kadar iç mekan karanlık kalmasın). Avizeler sadece şekil, ışık yok.
- Malzemeler `Assets/Materials/Blockout_*.mat`: Gri (duvar), Zemin, Fayans, Mobilya, Kumas, Beyaz, Koyu, Kapi, Ayna, Vurgu, Cam.
- Küçük süs objelerinin (kitap, yastık, perde, halı vb.) collider'ı yok. Büyük mobilyaların ve kapı kanatlarının var.
- Player salonda (0.8, 3.2) doğar, batıya (kanepe/TV tarafına) bakar.
- Boşlukları olan duvarlar CSG değil, parçalara bölünmüş kutulardır. Kapı/pencere yerini değiştirirken ilgili duvar parçalarını yeniden böl.
- Edit modunda fizik sorgusu (OverlapCapsule vb.) yapmadan önce `Physics.SyncTransforms()` çağır. Yoksa yeni oluşturulan veya dönen collider'lar görünmez ve sonuç yanlış "boş" çıkar.

## Notlar
- UI'da yazılar legacy `UnityEngine.UI.Text` ile (TMP kurulu değil). Kod içinde `Image` adı bir namespace ile çakışırsa `UnityEngine.UI.Image` olarak tam yaz.
- Unity MCP kullanılırken script değişikliğinden sonra domain reload sırasında bağlantı kısa süre kopar ("Unity not detected"). Derleme bitince tekrar dene.
- `gh` PATH'te değil. Tam yol: `C:\Program Files\GitHub CLI\gh.exe`.
