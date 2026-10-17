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
`HouseBlockout` objesi (pos y = zemin üstü ≈ 1.21) altında `Duvarlar`, `Tavan`, `Mobilya`. Birim metre, dış ölçü 13x10 (x -6.5..6.5, z -5..5), duvar 0.2 kalın.
- Ölçüler karakterden türetilir (CC boy 1.8, yarıçap 0.3, göz 1.6): kapı 1.2x2.3 (boy + 0.5 baş boşluğu, genişlik = 4 x yarıçap), duvar yüksekliği 3.0, pencere 1.6 geniş / denizlik 0.9 / üstü 2.3. Karakter ölçüsü değişirse ev de bu oranlarla yeniden üretilmeli.
- Salon x[-6.5,1.5] z[-1,5] (8x6): TV ünitesi + TV (batı duvar), kanepe, boy aynası (kuzey duvar), mutfak tezgâhı kutusu.
- Yatak odası x[1.5,6.5] z[-1,5] (5x6): yatak, dolap. Salon ile kapısı x=1.5 duvarında z=2.
- Giriş holü x[-6.5,1.5] z[-5,-1]: ana kapı güney duvarda x=-2.5. Salona z=-1 duvarında 2.8 m açıklık.
- Banyo x[1.5,6.5] z[-5,-1]: holden kapısı x=1.5 duvarında z=-3.
- Zemin, sahnedeki büyük `Cube` slab. Tavanın gölge atması kapalı (ışıklandırma yapılana kadar iç mekan karanlık kalmasın).
- Malzemeler: `Assets/Materials/Blockout_Gri.mat`, `Blockout_Mobilya.mat`. Player salonda (-4.6, 1) doğar, TV'ye (batıya) bakar.
- Boşlukları olan duvarlar CSG değil, parçalara bölünmüş kutulardır. Kapı/pencere yerini değiştirirken ilgili duvar parçalarını yeniden böl.

## Notlar
- UI'da yazılar legacy `UnityEngine.UI.Text` ile (TMP kurulu değil). Kod içinde `Image` adı bir namespace ile çakışırsa `UnityEngine.UI.Image` olarak tam yaz.
- Unity MCP kullanılırken script değişikliğinden sonra domain reload sırasında bağlantı kısa süre kopar ("Unity not detected"). Derleme bitince tekrar dene.
- `gh` PATH'te değil. Tam yol: `C:\Program Files\GitHub CLI\gh.exe`.
