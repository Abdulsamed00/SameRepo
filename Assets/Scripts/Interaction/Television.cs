using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

/// <summary>
/// Televizyon (05.11): hiç kapanmaz, reklamı döngüde oynatır. Oynayan reklam <see cref="DayRoutine.TvProduct"/>:
/// gündüz günün ürünü, gece (ürün alındıktan sonra) ertesi günün ürünü. Oyuncu oturma bölgesindeyken reklam bir kez
/// sonuna kadar oynayınca izlenmiş sayılır (<see cref="DayRoutine.NotifyAdWatched"/>).
/// Gece evin tek ışığı TV'dir: <see cref="tvLight"/> ekrandaki görüntünün ortalama rengini alır (RenderTexture'ın en
/// küçük mip'i, AsyncGPUReadback) ve sadece gece yanar.
/// Eskiden kumanda eldeyken F ile açılıp kapanıyordu (01.11).
/// </summary>
[RequireComponent(typeof(Collider))]
public class Television : MonoBehaviour
{
    [Serializable]
    private struct Ad
    {
        public ProductType product;
        public VideoClip clip;
    }

    [Header("Reklamlar")]
    [SerializeField] private Ad[] ads;

    [Header("Ekran")]
    [SerializeField] private Renderer screen;
    [Tooltip("Kapalıyken (hazırlanırken) ekranın malzemesi (siyah).")]
    [SerializeField] private Material offMaterial;
    [Tooltip("Açıkken ekranın malzemesi (Unlit). Reklam bu malzemenin dokusuna çizilir.")]
    [SerializeField] private Material onMaterial;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;

    [Header("Gece ışığı")]
    [Tooltip("Ekranın önündeki ışık: gece evin tek ışığı, rengi görüntüden gelir.")]
    [SerializeField] private Light tvLight;
    [SerializeField] private float lightIntensity = 4f;
    [Tooltip("Görüntü kararınca ışık bu kadardan aşağı inmez.")]
    [SerializeField] private float minBrightness = 0.25f;
    [SerializeField] private float colorSmoothing = 8f;

    private PlayerInteractor interactor;
    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private RenderTexture target;
    private Material screenInstance;
    private Collider area;
    private Ad current;
    private bool hasCurrent;
    private bool watchedThisLoop;
    private bool preparing;
    private bool restartPending;
    private bool pausedByMenu;
    private AudioLowPassFilter lowPass;
    private Transform listener;
    private float occlusion;
    private readonly RaycastHit[] occlusionHits = new RaycastHit[24];
    private Color sampled = Color.black;
    private Color lightColor = Color.black;
    private bool readbackPending;
    private float nextSample;

    /// <summary>Reklam oyuncu bölgedeyken sonuna kadar oynadı.</summary>
    public static event Action<ProductType> AdWatched;

    private const float OpenCutoff = 22000f;
    private const float MuffledCutoff = 700f;
    private const float MuffledVolume = 0.25f;

    /// <summary>
    /// Uyku çarpanı (0-1): <see cref="Bed"/> uyurken TV sesini yavaşça kısar (<see cref="FadeSleepFactor"/>),
    /// uyanırken geri açar.
    /// </summary>
    public static float SleepFactor { get; private set; } = 1f;

    /// <summary>TV sesinin uyku çarpanını verilen süre içinde hedefe taşır (Bed başlatır).</summary>
    public static System.Collections.IEnumerator FadeSleepFactor(float target, float duration)
    {
        float start = SleepFactor;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SleepFactor = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        SleepFactor = target;
    }

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true;

        // Ses TV_Alani'nın (oturma alanı kutusu) merkezinden değil ekranın bulunduğu yerden gelir: duvar/kapı arkasından
        // duyma (engel hesabı) ekran konumuna göre yapılır.
        GameObject soundObject = new GameObject("TV_Ses");
        soundObject.transform.SetParent(transform, false);
        soundObject.transform.position = screen != null ? screen.bounds.center : transform.position;
        audioSource = soundObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        // Tam 3D ve noktasal ses, oyuncu başını çevirince tek kulağa kayıp diğerinde kopuyordu: biraz 2D karışır,
        // spread ile ses iki kulağa yayılır, yön hissi kalır.
        audioSource.spatialBlend = 0.75f;
        audioSource.spread = 140f;
        audioSource.volume = volume;
        audioSource.minDistance = 3f;
        audioSource.maxDistance = 12f;
        // Arada duvar/kapı varsa ses kısılır ve boğuklaşır (düşük geçiren filtre).
        lowPass = soundObject.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = OpenCutoff;

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
        if (tvLight != null)
            tvLight.enabled = false;
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
            videoPlayer.Play();
        }

        // Gün/ürün değişince (satın alma, uyku) reklam değişir.
        if ((!hasCurrent || current.product != DayRoutine.TvProduct) && TryGetAd(DayRoutine.TvProduct, out Ad ad))
            Play(ad);

        if (restartPending)
        {
            restartPending = false;
            videoPlayer.Stop();
            preparing = true;
            videoPlayer.Prepare();
        }

        if (interactor == null)
            interactor = FindFirstObjectByType<PlayerInteractor>();
        // Oyuncu döngünün bir anında bölgede bulunduysa o döngü izlenmiş sayılır (otururken CC kapalı, konuma bakılır).
        if (interactor != null && area.bounds.Contains(interactor.transform.position))
            watchedThisLoop = true;

        UpdateLight();
        UpdateSound();
    }

    /// <summary>
    /// Ses: dinleyici ile ekran arasındaki engeller (duvar, kapı, uzun mobilya; alçak mobilya sayılmaz, kanepeden
    /// sehpa yüzünden boğuklaşmasın) sesi kısar ve boğuklaştırır; uyku çarpanı da burada uygulanır.
    /// </summary>
    private float menuFactor; // 0'dan başlar: menü açılırken ilk karelerde ses kaçmasın

    private void UpdateSound()
    {
        if (listener == null)
        {
            AudioListener al = FindFirstObjectByType<AudioListener>();
            listener = al != null ? al.transform : null;
        }

        float target = 0f;
        if (listener != null)
        {
            Vector3 from = listener.position;
            Vector3 to = audioSource.transform.position;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance > 0.5f)
            {
                int count = Physics.RaycastNonAlloc(from, delta / distance, occlusionHits, distance,
                    ~(1 << 2), QueryTriggerInteraction.Ignore);
                float blocking = 0f;
                for (int i = 0; i < count; i++)
                {
                    Collider c = occlusionHits[i].collider;
                    if (c.transform.root == listener.root || occlusionHits[i].distance > distance - 0.9f)
                        continue;
                    // Yükseklik ağırlığı: 0.8 m ve altı (sehpa, kanepe) hiç, 2 m ve üstü (duvar, kapı, dolap) tam.
                    blocking += Mathf.Clamp01((c.bounds.size.y - 0.8f) / 1.2f);
                }
                target = Mathf.Clamp01(blocking);
            }
        }

        occlusion = Mathf.MoveTowards(occlusion, target, 3f * Time.unscaledDeltaTime);
        float category = GameSettings.Volume(AudioCategory.Effects);
        // Ana menüdeyken reklam sesi çıkmaz; menü kapanınca 1 sn'de açılır.
        menuFactor = Mathf.MoveTowards(menuFactor, IntroMenu.MenuOpen ? 0f : 1f, Time.unscaledDeltaTime);
        audioSource.volume = volume * category * Mathf.Lerp(1f, MuffledVolume, occlusion) * SleepFactor * menuFactor;
        lowPass.cutoffFrequency = OpenCutoff * Mathf.Pow(MuffledCutoff / OpenCutoff, occlusion);
    }

    private bool TryGetAd(ProductType product, out Ad ad)
    {
        foreach (Ad candidate in ads)
        {
            if (candidate.clip != null && candidate.product == product)
            {
                ad = candidate;
                return true;
            }
        }
        ad = default;
        return false;
    }

    private void Play(Ad ad)
    {
        current = ad;
        hasCurrent = true;
        watchedThisLoop = false;

        if (target == null)
        {
            // Mip'ler ışığın rengi için: en küçük mip tüm görüntünün ortalaması.
            target = new RenderTexture((int)ad.clip.width, (int)ad.clip.height, 0)
            {
                name = "TV_Ekran_RT",
                useMipMap = true,
                autoGenerateMips = true
            };
            videoPlayer.targetTexture = target;
        }

        // Hazırlanana kadar siyah kalır, hazır olunca ekran açılır ve oynatılır. Play'i doğrudan çağırmak
        // oynatıcıyı hazır ama duraklı bırakıyordu; hazırlık sürerken Stop çağırmak da aynı sonucu veriyordu.
        if (videoPlayer.clip != ad.clip)
        {
            if (videoPlayer.isPrepared)
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
        StartPlayback();
    }

    private void StartPlayback()
    {
        SetScreen(true);
        videoPlayer.Play();
    }

    // Reklam döngüde oynar: bitince Update'te baştan hazırlanıp oynatılır.
    private void OnFinished(VideoPlayer source)
    {
        restartPending = true;
        if (watchedThisLoop && hasCurrent)
        {
            DayRoutine.NotifyAdWatched(current.product);
            AdWatched?.Invoke(current.product);
        }
        watchedThisLoop = false;
    }

    private void UpdateLight()
    {
        if (tvLight == null)
            return;
        bool on = DayRoutine.NightFell && target != null && videoPlayer.isPlaying;
        tvLight.enabled = on;
        if (!on)
            return;

        if (!readbackPending && Time.time >= nextSample && SystemInfo.supportsAsyncGPUReadback)
        {
            readbackPending = true;
            nextSample = Time.time + 0.08f;
            AsyncGPUReadback.Request(target, target.mipmapCount - 1, TextureFormat.RGBA32, OnReadback);
        }

        lightColor = Color.Lerp(lightColor, sampled, 1f - Mathf.Exp(-colorSmoothing * Time.deltaTime));
        float brightness = Mathf.Max(lightColor.maxColorComponent, 0.001f);
        // Renk normalize edilir, parlaklık şiddete gider (karanlık sahnede ışık söner gibi olur ama hiç bitmez).
        Color normalized = lightColor / brightness;
        normalized.a = 1f;
        tvLight.color = normalized;
        tvLight.intensity = lightIntensity * Mathf.Lerp(minBrightness, 1f, brightness);
    }

    private void OnReadback(AsyncGPUReadbackRequest request)
    {
        readbackPending = false;
        if (request.hasError || this == null)
            return;
        var data = request.GetData<Color32>();
        if (data.Length == 0)
            return;
        Color sum = Color.black;
        foreach (Color32 c in data)
            sum += (Color)c;
        sampled = sum / data.Length;
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
