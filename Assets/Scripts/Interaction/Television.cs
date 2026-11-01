using System;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Televizyon: kumanda eldeyken oturma bölgesinde F ile açılır, o günün ürününün (<see cref="GameDay.Product"/>)
/// reklamını ekranda oynatır, oyuncu F'ye tekrar basana ya da kumandayı elinden bırakana kadar döngüde oynar.
/// Bölge mantığı <see cref="SceneZone"/> ile aynıdır (ışın gerekmez), ama trigger olayı yerine konum kontrolüyle:
/// otururken CharacterController kapandığı için trigger çıkışı tetiklenip bölgeyi sıfırlıyordu.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Television : Interactable
{
    [Serializable]
    private struct Ad
    {
        public ProductType product;
        public VideoClip clip;
    }

    [Header("Reklamlar")]
    [SerializeField] private Ad[] ads;

    [Header("Etkileşim")]
    [Tooltip("TV'yi açmak için eldeki olması gereken kumanda.")]
    [SerializeField] private PickupItem remote;
    [SerializeField] private string turnOnPrompt = "TV'yi aç";
    [SerializeField] private string turnOffPrompt = "TV'yi kapat";

    [Header("Ekran")]
    [SerializeField] private Renderer screen;
    [Tooltip("Kapalıyken ekranın malzemesi (siyah).")]
    [SerializeField] private Material offMaterial;
    [Tooltip("Açıkken ekranın malzemesi (Unlit). Reklam bu malzemenin dokusuna çizilir.")]
    [SerializeField] private Material onMaterial;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;

    private PlayerInteractor interactor;
    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private RenderTexture target;
    private Material screenInstance;
    private Collider area;
    private Ad current;
    private bool adWatchedRaised;
    private bool preparing;
    private bool restartPending;
    private bool pausedByMenu;

    /// <summary>Reklam sonuna kadar izlenince tetiklenir (günlük görev/yönlendirme sistemi buna bağlanır).</summary>
    public static event Action<ProductType> AdWatched;

    public bool IsOn { get; private set; }

    public override string Prompt => IsOn ? turnOffPrompt : turnOnPrompt;

    public override bool CanInteract(PlayerInteractor who) =>
        who.HeldItem == remote && (IsOn || TryGetTodaysAd(out _));

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        // Tam 3D ve noktasal ses, oyuncu başını çevirince tek kulağa kayıp diğerinde kopuyordu: biraz 2D karışır,
        // spread ile ses iki kulağa yayılır, yön hissi kalır.
        audioSource.spatialBlend = 0.75f;
        audioSource.spread = 140f;
        audioSource.volume = volume;
        audioSource.minDistance = 3f;
        audioSource.maxDistance = 12f;
        gameObject.AddComponent<CategoryVolume>();

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        // Yerleşik isLooping açıkken oynatıcı hiç başlamıyordu; döngü Update'te elle kuruluyor (OnFinished).
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);
        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.loopPointReached += OnFinished;

        SetScreen(false);
    }

    private void OnDestroy()
    {
        if (target != null)
            target.Release();
        if (screenInstance != null)
            Destroy(screenInstance);
    }

    private void Update()
    {
        // VideoPlayer ne Time.timeScale'e ne AudioListener.pause'a bakıyor: duraklatma menüsünde reklam ve sesi
        // sürüyordu. Menü açılınca durdurulur, kapanınca kaldığı yerden devam eder.
        if (PauseMenu.IsPaused)
        {
            if (!pausedByMenu && videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
                pausedByMenu = true;
            }
            return;
        }
        if (pausedByMenu)
        {
            pausedByMenu = false;
            if (IsOn)
                videoPlayer.Play();
        }

        if (restartPending)
        {
            restartPending = false;
            if (IsOn)
            {
                videoPlayer.Stop();
                preparing = true;
                videoPlayer.Prepare();
            }
        }

        if (interactor == null)
            interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor == null || !interactor.isActiveAndEnabled)
            return;

        // Kumanda elden çıkınca (G ile bırakma, odadan çıkınca kendiliğinden yerine dönme) TV kapanır.
        if (IsOn && interactor.HeldItem != remote)
            TurnOff();

        bool inside = area.bounds.Contains(interactor.transform.position);
        if (inside && interactor.Zone == null)
            interactor.Zone = this;
        else if (!inside && interactor.Zone == this)
            interactor.Zone = null;
    }

    public override void Interact(PlayerInteractor who)
    {
        if (IsOn)
            TurnOff();
        else if (TryGetTodaysAd(out Ad ad))
            TurnOn(ad);
    }

    private bool TryGetTodaysAd(out Ad ad)
    {
        foreach (Ad candidate in ads)
        {
            if (candidate.clip != null && candidate.product == GameDay.Product)
            {
                ad = candidate;
                return true;
            }
        }
        ad = default;
        return false;
    }

    private void TurnOn(Ad ad)
    {
        current = ad;
        IsOn = true;
        adWatchedRaised = false;

        if (target == null)
        {
            target = new RenderTexture((int)ad.clip.width, (int)ad.clip.height, 0);
            target.name = "TV_Ekran_RT";
            videoPlayer.targetTexture = target;
        }

        // Hazırlanana kadar siyah kalır, hazır olunca ekran açılır ve oynatılır. Play'i doğrudan çağırmak
        // oynatıcıyı hazır ama duraklı bırakıyordu; hazırlık sürerken Stop çağırmak da aynı sonucu veriyordu.
        if (videoPlayer.clip != ad.clip)
        {
            videoPlayer.Stop();
            videoPlayer.clip = ad.clip;
            preparing = false;
        }

        if (videoPlayer.isPrepared)
            StartPlayback();
        else if (!preparing)
        {
            preparing = true;
            videoPlayer.Prepare();
        }
    }

    private void OnPrepared(VideoPlayer source)
    {
        preparing = false;
        if (IsOn)
            StartPlayback();
    }

    private void StartPlayback()
    {
        SetScreen(true);
        videoPlayer.Play();
    }

    // Reklam döngüde oynar, oyuncu kapatana kadar sürer: bitince Update'te baştan hazırlanıp oynatılır.
    // Olay her açılışta sadece ilk bitişte tetiklenir.
    private void OnFinished(VideoPlayer source)
    {
        if (!IsOn)
            return;
        restartPending = true;
        if (adWatchedRaised)
            return;
        adWatchedRaised = true;
        DayRoutine.NotifyAdWatched(current.product);
        AdWatched?.Invoke(current.product);
    }

    private void TurnOff()
    {
        IsOn = false;
        // Sadece hazır oynatıcı durdurulur (başa sarar); hazırlık sürerken Stop çağrılırsa oynatıcı takılıyor,
        // o durumda hazır olunca OnPrepared kapalı olduğunu görür.
        if (videoPlayer.isPrepared)
            videoPlayer.Stop();
        SetScreen(false);
    }

    private void SetScreen(bool on)
    {
        if (screen == null)
            return;

        if (!on)
        {
            screen.sharedMaterial = offMaterial;
            return;
        }

        if (screenInstance == null)
        {
            screenInstance = new Material(onMaterial);
            // Ekran küpünün UV'si RenderTexture'a göre 180° dönük: sadece dikey çevirince görüntü düzeliyor ama
            // yazılar aynalı kalıyordu (gözlük reklamı). İki eksen de çevrilir.
            screenInstance.mainTextureScale = new Vector2(-1f, -1f);
            screenInstance.mainTextureOffset = new Vector2(1f, 1f);
        }
        screenInstance.mainTexture = target;
        screen.sharedMaterial = screenInstance;
    }
}
