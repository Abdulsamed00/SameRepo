using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fragman çekimini yönetir. Fragman sahnesi (Trailer_*) oyun sahnesinin üstüne additive açılır; bu bileşen:
/// (1) oyunun durumunu çekime göre kurar (gün, alınan/giyilen ürünler, saç, estetik) ve oyuncuyu gizler ya da kanepeye
/// oturtur, (2) Timeline'ı (PlayableDirector) başlatır, çekimlerin kamera hareketini ve netliğini uygular,
/// (3) ses geçişlerini (asansör müziği → house → boğulma → kesilme + yazar kasa fişi) fragmanın ortak zaman çizgisinde
/// yürütür, (4) post-process eğrilerini (renk sapması, hareket bulanıklığı...) ve kapanıştaki "SAME" logosunu sürer.
/// Müzik sonradan eklenecek: ses kaynakları boşsa ses kısmı sessizce atlanır.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class TrailerController : MonoBehaviour
{
    public enum PlayerMode { Hidden, SeatedOnCouch }

    [Header("Akış")]
    [SerializeField] private PlayableDirector director;
    [Tooltip("Sahne otursun diye (NPC'ler doğsun, yürümeye başlasın) Timeline'dan önce beklenen süre (sn).")]
    [SerializeField] private float preRoll = 2f;
    [Tooltip("Bu sahnenin Timeline'ı fragmanın kaçıncı saniyesinde başlıyor (ses ve eğriler fragman zamanıyla çalışır).")]
    [SerializeField] private float trailerTimeOffset;
    [SerializeField] private Camera trailerCamera;

    [Header("Oyun durumu")]
    [SerializeField] private int dayIndex;
    [Tooltip("Satın alınmış ve giyilmiş sayılacak ürünler.")]
    [SerializeField] private ProductType[] ownedProducts;
    [SerializeField] private bool hairDyed;
    [SerializeField] private bool surgeryDone;
    [SerializeField] private PlayerMode playerMode = PlayerMode.Hidden;
    [Tooltip("Oyuncu gizliyken kök, kamerayı izler: NPC'ler kamerayı oyuncu sanıp etrafından dolaşır.")]
    [SerializeField] private bool playerFollowsCamera = true;
    [SerializeField] private string seatName = "Kanepe";
    [Tooltip("Sokak/mağaza kalabalığının başlangıç nüfusu (0 = sahnedeki değer). Çekimde kalabalık daha dolu görünsün diye.")]
    [SerializeField] private int crowdPopulation;

    [Header("Ses (fragman zamanı, sn)")]
    [SerializeField] private AudioSource elevatorMusic;
    [SerializeField] private AudioSource houseMusic;
    [SerializeField] private AudioSource receiptSound;
    [SerializeField] private float houseStartTime = 15f;
    [SerializeField] private float crossfade = 0.25f;
    [SerializeField] private float muffleStartTime = 30f;
    [SerializeField] private float muffleEndTime = 45f;
    [SerializeField] private float muffledCutoff = 500f;
    [SerializeField] private float musicCutTime = 45f;
    [SerializeField] private float receiptTime = 45.3f;
    [Tooltip("Oyunun kendi sesleri (TV, adım, kalabalık) çekimde kapalı; müzik sonradan eklenecek.")]
    [SerializeField] private bool muteGameAudio = true;

    [Header("Post-process (Timeline zamanına göre eğriler)")]
    [SerializeField] private Volume volume;
    [SerializeField] private AnimationCurve chromaticAberration = AnimationCurve.Constant(0f, 1f, 0f);
    [SerializeField] private AnimationCurve motionBlur = AnimationCurve.Constant(0f, 1f, 0f);
    [SerializeField] private AnimationCurve lensDistortion = AnimationCurve.Constant(0f, 1f, 0f);
    [SerializeField] private AnimationCurve vignette = AnimationCurve.Constant(0f, 1f, 0.2f);

    [Header("Logo")]
    [SerializeField] private CanvasGroup logo;
    [SerializeField] private float logoTime = 1.6f;
    [SerializeField] private float logoFade = 1.2f;

    /// <summary>Timeline başladığı kare true olur; kayıt aracı (editör) bunu bekler.</summary>
    public static bool Rolling { get; private set; }
    public static float Duration { get; private set; }

    private TrailerShot[] shots;
    private FirstPersonController player;
    private Renderer[] playerRenderers;
    private AudioLowPassFilter lowPass;
    private DepthOfField dof;
    private ChromaticAberration ca;
    private MotionBlur blur;
    private LensDistortion distortion;
    private Vignette vig;
    private bool receiptPlayed;
    private bool started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Rolling = false;
        Duration = 0f;
    }

    private void Awake()
    {
        // Oyunun statik durumu, diğer script'lerin Awake/Start'ından önce.
        DayRoutine.ResetAll();
        for (int i = 0; i < dayIndex; i++)
            GameDay.Advance();
        if (ownedProducts != null)
        {
            foreach (ProductType product in ownedProducts)
            {
                PlayerPurchases.Buy(product);
                PlayerOutfit.Wear(product);
            }
        }
        if (hairDyed)
            PlayerLook.DyeHair();
        if (surgeryDone)
            PlayerLook.HaveSurgery();

        if (crowdPopulation > 0)
        {
            System.Reflection.FieldInfo field = typeof(NpcCrowd).GetField("population",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (NpcCrowd crowd in FindObjectsByType<NpcCrowd>(FindObjectsSortMode.None))
                field?.SetValue(crowd, crowdPopulation);
        }

        shots = GetComponentsInChildren<TrailerShot>(true);
        if (director != null)
        {
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.extrapolationMode = DirectorWrapMode.Hold;
            Duration = (float)director.duration;
        }
        if (logo != null)
            logo.alpha = 0f;

        if (volume != null && volume.profile != null)
        {
            volume.profile.TryGet(out dof);
            volume.profile.TryGet(out ca);
            volume.profile.TryGet(out blur);
            volume.profile.TryGet(out distortion);
            volume.profile.TryGet(out vig);
        }
        if (houseMusic != null)
            lowPass = houseMusic.GetComponent<AudioLowPassFilter>();
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        SetupPlayer();
        if (muteGameAudio)
            MuteGameAudio();
        ApplyShots(0f);
        yield return new WaitForSeconds(preRoll);

        if (director != null)
        {
            director.time = 0;
            director.Play();
        }
        started = true;
        Rolling = true;
    }

    private void SetupPlayer()
    {
        player = FindFirstObjectByType<FirstPersonController>();
        foreach (BlondeGlance glance in FindObjectsByType<BlondeGlance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            glance.enabled = false;
        foreach (FastTravel travel in FindObjectsByType<FastTravel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            travel.enabled = false;
        if (player == null)
            return;

        PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
        PauseMenu pause = player.GetComponent<PauseMenu>();
        Camera playerCamera = player.CameraRoot.GetComponentInChildren<Camera>(true);
        if (interactor != null)
            interactor.enabled = false;
        if (pause != null)
            pause.enabled = false;
        foreach (Canvas canvas in player.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = false;
        foreach (AudioSource source in player.GetComponentsInChildren<AudioSource>(true))
            source.mute = true;

        // Görüntüyü fragman kamerası verir; oyuncunun kamerası ve dinleyicisi kapanır.
        if (playerCamera != null)
        {
            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
            playerCamera.enabled = false;
            playerCamera.tag = "Untagged";
        }
        if (trailerCamera != null)
            trailerCamera.tag = "MainCamera";

        if (playerMode == PlayerMode.SeatedOnCouch)
        {
            PlayerSitController sitter = player.GetComponent<PlayerSitController>();
            PlayerBody body = player.GetComponentInChildren<PlayerBody>(true);
            Seat seat = null;
            foreach (Seat candidate in FindObjectsByType<Seat>(FindObjectsSortMode.None))
            {
                if (candidate.name == seatName)
                    seat = candidate;
            }
            if (sitter != null && seat != null)
            {
                sitter.ControlsLocked = true;
                Vector3 near = trailerCamera != null ? trailerCamera.transform.position : seat.transform.position;
                sitter.SitImmediately(seat, near);
            }
            if (body != null)
                body.SetThirdPerson(true);
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            player.enabled = false;
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
            playerRenderers = player.GetComponentsInChildren<Renderer>(true);
        }
    }

    private void MuteGameAudio()
    {
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (source != elevatorMusic && source != houseMusic && source != receiptSound)
                source.mute = true;
        }
    }

    private void LateUpdate()
    {
        if (playerMode == PlayerMode.Hidden && playerRenderers != null)
        {
            foreach (Renderer r in playerRenderers)
            {
                if (r != null)
                    r.forceRenderingOff = true;
            }
            if (playerFollowsCamera && player != null && trailerCamera != null)
            {
                Vector3 p = trailerCamera.transform.position;
                player.transform.position = new Vector3(p.x, player.transform.position.y, p.z);
            }
        }

        float time = started && director != null ? (float)director.time : 0f;
        ApplyShots(time);
        ApplyPost(time);
        ApplyAudio(trailerTimeOffset + time);

        if (logo != null)
            logo.alpha = started ? Mathf.Clamp01((time - logoTime) / Mathf.Max(0.01f, logoFade)) : 0f;
    }

    private void ApplyShots(float time)
    {
        TrailerShot live = null;
        foreach (TrailerShot shot in shots)
        {
            // Kesmeden bir kare önce/sonra da doğru pozda olsun diye küçük pay.
            if (time >= shot.start - 0.1f && time <= shot.End + 0.1f)
                shot.Apply(time);
            if (shot.Contains(time))
                live = shot;
        }
        if (live != null && dof != null)
        {
            dof.focusDistance.value = live.FocusAt(time);
            dof.aperture.value = live.aperture;
            dof.focalLength.value = live.focalLength;
        }
    }

    private void ApplyPost(float time)
    {
        if (ca != null)
            ca.intensity.value = chromaticAberration.Evaluate(time);
        if (blur != null)
            blur.intensity.value = motionBlur.Evaluate(time);
        if (distortion != null)
            distortion.intensity.value = lensDistortion.Evaluate(time);
        if (vig != null)
            vig.intensity.value = vignette.Evaluate(time);
    }

    // Fragman zamanı: 0-15 asansör müziği, 15'te house'a ani geçiş, 30-45 boğulma, 45'te kesilme + fiş sesi.
    private void ApplyAudio(float t)
    {
        if (!started)
            return;

        if (elevatorMusic != null && elevatorMusic.clip != null)
        {
            if (!elevatorMusic.isPlaying && t < houseStartTime)
                elevatorMusic.Play();
            elevatorMusic.volume = 1f - Mathf.Clamp01((t - houseStartTime) / Mathf.Max(0.01f, crossfade));
            if (t >= houseStartTime + crossfade && elevatorMusic.isPlaying)
                elevatorMusic.Stop();
        }

        if (houseMusic != null && houseMusic.clip != null)
        {
            if (t >= houseStartTime && t < musicCutTime && !houseMusic.isPlaying)
            {
                houseMusic.time = Mathf.Clamp(t - houseStartTime, 0f, houseMusic.clip.length - 0.05f);
                houseMusic.Play();
            }
            houseMusic.volume = Mathf.Clamp01((t - houseStartTime) / Mathf.Max(0.01f, crossfade));
            if (lowPass != null)
            {
                float k = Mathf.InverseLerp(muffleStartTime, muffleEndTime, t);
                lowPass.cutoffFrequency = Mathf.Lerp(22000f, muffledCutoff, Mathf.Sqrt(k));
            }
            if (t >= musicCutTime && houseMusic.isPlaying)
                houseMusic.Stop();
        }

        if (receiptSound != null && receiptSound.clip != null && !receiptPlayed && t >= receiptTime)
        {
            receiptPlayed = true;
            receiptSound.Play();
        }
    }
}
