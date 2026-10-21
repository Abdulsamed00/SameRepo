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

### Interaction/ (etkileşim sistemi)
Ortak mantık ile eşyaya özel davranış ayrıdır. Yeni etkileşim = `Interactable` türevi yeni script; algılama, F bildirimi ve input'a dokunulmaz.
- `Interactable` (abstract): `Prompt`, `CanInteract(PlayerInteractor)`, `Interact(PlayerInteractor)`. Collider'ı olan objeye (ya da üst objesine) eklenir.
- `PlayerInteractor` (Player kökünde): kameradan `interactDistance` (2.5 m) ışın atar, `GetComponentInParent<Interactable>` bulur, `Interact` tuşunda `Interact`'i çağırır. Elde tutma (`Hold`/`DropHeld`/`HeldItem`) de burada. Trigger collider'lar ışına girmez.
- `PickupItem : Interactable` (+Rigidbody): alınınca kinematic ve collider'sız olur, `PlayerInteractor.holdPoint`'i yumuşak takip eder (LateUpdate, kafa sallanması yansır). G ile bırakılınca eşya sahnedeki **ilk konumuna (yerel poz) geri döner** (`returnToStartOnDrop`, varsayılan açık). Kapatılırsa bakılan yönde `dropDistance` (1.2 m) önüne, duvar varsa onun önüne düşer. Eşya başına `holdPositionOffset`/`holdEulerOffset` ayarlanabilir.
- `Openable : Interactable`: kapı, dolap kapağı, çekmece. Her `Part` (target, kapalı/açık yerel konum + Euler) tek bir `amount` (0-1, `duration` 0.6 sn, SmoothStep) ile birlikte hareket eder. Kapı/kapak için menteşe pivotu Euler Y ile döndürülür (açı 180'den küçük olmalı, Slerp kısa yolu seçer). `startsOpen`, `openPrompt`/`closePrompt` ("Kapıyı aç"/"Kapıyı kapat"). Parçaların altındaki collider'lar hareketle gelir. Yeni kapı/çekmece script değil kurulum işi: kökte `Openable`, pivotta menteşe, sonra kapalı/açık pozlar.
- `InteractionPromptView` (Player/`InteractionUI` canvas'ı): sadece gösterir. Ekran ortasının altında "F  Kumandayı al", elde eşya varken altta "G  Bırak". Tuş etiketi input binding'inden okunur. Legacy `Text` kullanır.
- Input (`InputSystem_Actions` > Player): `Interact` = **F** (eski `E` ve `Hold` interaction'ı kaldırıldı), yeni `Drop` = **G** (gamepad: dpad aşağı).
- Player prefab'ında: `PlayerInteractor` (Player kökü), `HoldPoint` (Main Camera altında, local (0.28, -0.22, 0.6)), `InteractionUI`. Üç sahne aynı prefab'ı kullandığı için hepsinde çalışır.
- Test eşyası: HouseScene `Kanepe/Kumanda` (BoxCollider + Rigidbody + `PickupItem`, prompt "Kumandayı al"). Elde eşya varken başka `PickupItem` alınamaz; başka tür `Interactable`'lar çalışmaya devam eder.
- Açılabilirler (HouseScene): `Kapi_Giris`, `Kapi_Oturma_Odasi`, `Kapi_Yatak_Odasi` (kökte `Openable`, pivot `Kanat_Pivot`, açık başlar: kapalı Euler Y = 0, açık = 270/90) ve `Yatak_Odasi/Dolap` (iki kapak `Kapak_Sol_Pivot`/`Kapak_Sag_Pivot`, ±90° odaya doğru açılır, kapalı başlar, `Ic_Bosluk` koyu iç yüzey). `Kapi_Kapali_Oda` sahte kapıdır, açılmaz. Komodin ve ayakkabılık tek parça kutudur, kapak/çekmece geometrisi yok.
- Henüz yok: oturma, TV/kumanda etkileşimi, mağazada kıyafet alma. Hepsi `Interactable` türevi olarak eklenecek.

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
`StreetBlockout` objesi, dünya koordinatları. Cadde +z yönünde 240 m: z=0'da sıradan bir kapanış binası (`Bina_B_Guney_Kapanis`), z=240'ta büyük mağaza. (İlk 120 m kullanıcı onaylı ilk bölüm; 120-240 aynı kurallarla eklenen uzatma.)
- Oyuncunun evi sokakta YOK (kullanıcı kararı): evden çıkınca yükleme ekranı gelir, oyuncu sokağa yürüyüp gelmiş gibi doğar. Arkasında ev olmamalı.
- `Eve_Giden_Sokak`: sağda z[12,17] ara sokak, x=9'dan 25'e 16 m içeri gider, sonunda güneye döner (x[20,25] z[6,12]). Oyuncu `Oyuncu_Dogma_Noktasi`'nda (17, 14.5) yüzü caddeye (-x) dönük doğar. `Gecis_Ev` trigger'ı dönemecin arkasında (22.5, 7.2), spawn'dan görünmez: oyuncu eve geldiği yoldan döner. Cepheler arası 18 m (x -9..9): ortada 12 m taş yol (y=0), iki yanda 3 m kaldırım (+0.12).
- Şeritler: x[-4.5,4.5] NPC akışı için BOŞ tutulur. Sokak mobilyası (lamba, bank, çöp, pano, saksı, simit arabası) x=±5.0..5.6 şeridinde. Kafe oturma alanları kaldırımda (x=±7.6). Yeni obje eklerken bu şeritleri koru.
- `Zemin`: 3x3 m modüler `Tas_Zemin_*` ve `Kaldirim_*` parçaları, tramvay rayı (collider yok), `Zemin_Taban`.
- `Binalar`: 4 tip bina (A-D, iş planındaki "4 sokak binası"), her yanda 20'şer. B/D'de cumba, dükkânlarda çıkma tabela. Çıkmaz ara sokaklar: sol z[42,46] ve z[170,174], sağ z[70,74] ve z[214,218]. 5 binada cephe reklam panosu (7x4.5 m).
- `Kahve_Dukkani_*` (5 adet, Starbucks tarzı, marka logosu yok): sol z[24,36], sağ z[46,58], sol z[86,98], sol z[150,162], sağ z[186,198]. Cam cephe, yeşil tente, logo diski. Oturma alanları `Kahve_Dukkani_Oturma` altında.
- `Mekanlar` (tekrarı kırmak için, mevcut binaların zemin katına giydirildi; tabela/tente bina altında, dış eşyalar `Mekanlar` altında): Lokanta (sağ z~32, kırmızı tente, örtülü masalar), Döner (sol z~39, ayakta masalar), Maraş dondurmacısı (sol z~52, çizgili tente, tezgâh, dev külah), Sahaf (sol z~79, kitap tezgâhları), Çiçekçi (sağ z~105, basamaklı standlar), Pastane (sağ z~138), Sinema (sağ z~162, ışıklı saçak, gişe, afiş camekânları), Pasaj girişi (sağ z~206, kemer + fenerler), Müzik dükkânı + sokak müzisyeni (sol z~219), Kilise + avlu (sol z[188,212], iki binanın yerine; parmaklıklı avlu, 2 kule, gül pencere).
- Billboardlar: 10 cephe reklam panosu, 2 çatı billboardu (sol z=115, sağ z=126, güneye/oyuncuya dönük), 16 ayaklı pano. Reklam yüzeyleri hep `Blockout_Vurgu`.
- `Buyuk_Magaza_Binasi` (z=240, Boyner tarzı, 24 m geniş, 5 kat): 2 m derin vitrinler (8 manken, afişler), ortada girinti içinde otomatik kapılar, saçak, büyük tabela, cam bantlı üst katlar, dikey cephe afişleri, `Gecis_Magaza` trigger'ı girintide.
- `Sokak_Mobilyasi`: 16 lamba, 16 bank, 18 çöp kutusu, 12 ayaklı reklam panosu, 12 çiçek saksısı, 4 simit arabası, 2 büfe, 20 sınır dubası. `Bayrak_Dizileri`: caddenin üstünde 8 dizi (collider yok).
- Ara sokak dubaları 1.6 m aralıklı: NavMesh ajanı (yarıçap 0.5) 1 m'den dar aralıktan geçemez. Duba/engel eklerken bunu koru.
- `NPC_Alani`: `Rota_Kuzey` (x=-2.5), `Rota_Guney` (x=2.5), `Rota_Kaldirim_Sol/Sag` (x=±7, kafe önleri atlanır) her 10 m'de nokta. `Dogma_Noktalari` (cadde uçları, ara sokaklar, mağaza girişi). `NPC_Yer_Tutuculari`: collider'sız kapsüller, sadece yoğunluğu göstermek için (NPC sistemi gelince silinecek).
- NavMesh: `StreetBlockout` üzerinde `NavMeshSurface` (PhysicsColliders), veri `Assets/Scenes/StreetScene/NavMesh-StreetBlockout.asset`. Sahne değişince yeniden pişir.
- Gecis_* trigger'ları sadece kutu. Sahne geçiş scripti henüz YOK (SceneLoader.Load ile bağlanacak).
- Oyuncu `Assets/Prefabs/Player.prefab` örneği, `Eve_Giden_Sokak` içinde doğar. Ev, sokak ve mağaza aynı prefab'ı kullanır: Player'ı değiştirirken prefab'ı değiştir.
- Ölçüler İstiklal'den yaklaşık (hafızadan, ölçülmüş değil).

## ShopScene blokajı (Boyner tarzı kıyafet mağazası)
`ShopBlockout` (dünya orijininde), iç ölçü 24x28 m (x -12..12, z 0..28), tavan 4.6 m, duvar 0.2. Giriş güneyde (z=0), mağaza +z yönünde. Sol/Sağ girişten içeri bakana göre (sol = -x). Ön cephe sokaktaki `Buyuk_Magaza_Binasi` ile birebir: 24 m, iki yanda 2'şer vitrin bölmesi (2 m derin, 0.3 platform, bölme başına 2 manken, arkadan duvarla kapalı), ortada 2 m girintili 3.2x2.6 otomatik kapı (kanatlar açık konumda).
- Hiyerarşi: `Yapi` (Zemin, Duvarlar, Cephe, Giris, Tavan, Kolonlar), `Vitrinler`, `Giris_Alani`, `Reyon_Kadin`, `Reyon_Erkek`, `Odak_Alani`, `Kasa`, `Deneme_Kabinleri`, `Personel_Alani`, `Dis_Mekan`.
- Ana koridor x[-2.5,2.5] girişten arka duvardaki manken podyumuna kadar BOŞ. Diğer geçitler ≥1.8 m. Yeni obje eklerken bunu koru.
- Reyonlar: sol kadın (duvar askılıkları, yuvarlak/düz askılık, ürün masaları), sağ erkek (duvar rafları, askılıklar, masalar, gondol raflar). 4 kolon (±6.5, z 11.25 / 18.25), ana koridora bakan yüzlerinde ayna.
- `Kasa`: sağ önde girişin yanında, banko x[3.6,10.4] z[4.25,5.05]; kasiyer alanına doğu uçtan girilir. `Kasiyer_Noktasi_1/2`, `Musteri_Noktasi_1/2` boş objeleri.
- `Deneme_Kabinleri`: sol arkada 5 kabin (iç 1.3x1.7, bölme 2.3 m, yarı açık perde, ayna, oturak), bekleme bankı, görevli bankosu, ortak ayna.
- `Personel_Kapisi` arka duvarda açılmayan sahte kapı. `Dis_Mekan` sadece kapıdan görünen sokak (gölge atmaz, collider sadece kaldırımda).
- Mobilya kökünde tek `BoxCollider` (askıdaki kıyafet hacmi dahil); çocuklarda collider yok. Kıyafet/ürün kutuları `Blockout_Kumas`.
- `Giris/Gecis_Sokak` trigger'ı girintide, önünde `Gorunmez_Sinir` (oyuncu dışarı çıkamaz). Sahne geçiş scripti henüz YOK.
- Sahnede ayrı kamera yok; `Player.prefab` örneği (0, 0, 4)'te içeri (+z) bakarak doğar. Tavan gölge atmaz. NavMesh henüz pişirilmedi.

## Notlar
- Unity MCP komutlarında `AssetDatabase.DeleteAsset` kullanma: onay istiyor ve komut hiç çalışmıyor. Var olan asset'i güncellemek için `EditorUtility.CopySerialized` kullan.
- UI'da yazılar legacy `UnityEngine.UI.Text` ile (TMP kurulu değil). Kod içinde `Image` adı bir namespace ile çakışırsa `UnityEngine.UI.Image` olarak tam yaz.
- Unity MCP kullanılırken script değişikliğinden sonra domain reload sırasında bağlantı kısa süre kopar ("Unity not detected"). Derleme bitince tekrar dene.
- `gh` PATH'te değil. Tam yol: `C:\Program Files\GitHub CLI\gh.exe`.
