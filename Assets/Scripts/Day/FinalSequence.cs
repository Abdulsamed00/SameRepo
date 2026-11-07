using System.Collections;
using UnityEngine;

/// <summary>
/// Oyun sonu (GDD): beş ürün de alınıp giyilmiş, son poz verilmiş, oyuncu evden sokağa çıkmıştır
/// (<see cref="DayRoutine.FinalReady"/>). Ana caddeye çıkınca kontrol alınır, karakter kendiliğinden caddenin
/// ortasına yürür ve caddeye (mağazaya doğru) döner; kalabalık (hepsi aynı ürünlerle) onun çevresine toplanır
/// (<see cref="NpcCrowd.BeginFinale"/>). Görünüm birinci şahıstan üçüncü şahsa geçer: kamera kafadan
/// ayrılıp karakterin önüne ve yukarısına çıkar, ona tepeden bakar. Sonra ekran kararır, özlü sözlü jenerik akar,
/// ana menüye dönülür ve oyun sıfırlanır. StreetScene'de bir objeye eklenir; final hazır değilse kendini kapatır.
/// </summary>
public class FinalSequence : MonoBehaviour
{
    [Header("Başlangıç")]
    [Tooltip("Oyuncu doğduğu yerden bu kadar uzaklaşınca (ara sokaktan ana caddeye çıkınca) final başlar (m).")]
    [SerializeField] private float triggerDistance = 8f;

    [Header("Caddenin ortasına yürüme")]
    [Tooltip("Caddenin orta çizgisi (dünya x).")]
    [SerializeField] private float streetCenterX = 0f;
    [Tooltip("Varınca karakterin döndüğü yön (dünya; +z caddenin sonundaki mağaza).")]
    [SerializeField] private Vector3 finalFacing = Vector3.forward;
    [SerializeField] private float walkSpeed = 1.4f;
    [Tooltip("Varınca caddeye dönme süresi (sn).")]
    [SerializeField] private float turnDuration = 1.2f;
    [Tooltip("Yürüme bu süreyi aşarsa (bir şeye takıldıysa) olduğu yerde durur (sn).")]
    [SerializeField] private float maxWalkDuration = 15f;

    [Header("Kamera")]
    [Tooltip("Karakter durduktan sonra üçüncü şahsa geçmeden önce beklenen süre (sn).")]
    [SerializeField] private float stopDelay = 1.2f;
    [SerializeField] private float pullBackDuration = 5f;
    [Tooltip("Son kare: karakterin bu kadar üstünde (m).")]
    [SerializeField] private float finalHeight = 3.5f;
    [Tooltip("Son kare: karakterin bu kadar önünde (m).")]
    [SerializeField] private float finalFront = 4f;
    [Tooltip("Kameranın baktığı nokta: karakterin kökünden bu kadar yukarı (m).")]
    [SerializeField] private float lookHeight = 1.2f;
    [SerializeField] private float finalFieldOfView = 50f;
    [SerializeField] private float holdDuration = 1.5f;
    [Tooltip("Son kareden önce kalabalık toplansın diye en fazla beklenen süre (sn).")]
    [SerializeField] private float maxRingWait = 12f;
    [Tooltip("Kalabalığı beklerken kameranın karakterin çevresinde yavaşça döndüğü hız (derece/sn).")]
    [SerializeField] private float waitOrbitSpeed = 3f;

    [Header("Son vinç çekimi")]
    [Tooltip("Kalabalık toplanınca kamera yükselerek karakterin çevresinden dolaşır ve arkasına geçer, hafif eğik bakar: " +
             "karakter sırtı dönük, aynı giyinmiş kalabalığın ortasında, önünde cadde ve mağaza.")]
    [SerializeField] private float craneDuration = 12f;
    [SerializeField] private float craneHeight = 7.5f;
    [Tooltip("Son karede kameranın karakterin arkasındaki yatay uzaklığı (m).")]
    [SerializeField] private float craneBack = 7f;
    [Tooltip("Son karede bakılan nokta: karakterin bu kadar önü (m); bakış hafif eğik kalır, ufuk/mağaza görünür.")]
    [SerializeField] private float craneLookAhead = 2.5f;
    [SerializeField] private float craneLookHeight = 0.8f;
    [SerializeField] private float craneFieldOfView = 45f;
    [Tooltip("Vinç sonunda her şey donar ve ses kesilir; bu kadar sessizlikte tutulur (sn).")]
    [SerializeField] private float silenceDuration = 2.2f;
    [Tooltip("Sinema bantlarının ekran yüksekliğine oranı (her biri).")]
    [SerializeField, Range(0f, 0.2f)] private float letterbox = 0.11f;

    [Header("Jenerik")]
    [Tooltip("Görüntünün solup jeneriğin belirdiği süre (sn).")]
    [SerializeField] private float fadeToBlackDuration = 2.5f;
    [SerializeField] private float creditsDuration = 12f;
    [SerializeField] private string quote = "Sahip olduğun şeyler, sonunda seni sahiplenir.";
    [SerializeField] private string quoteAuthor = "Fight Club";
    [SerializeField] private string menuScene = "MainMenu";
    [SerializeField] private AudioClip clickSound;
    [Tooltip("Jenerik başlarken çalan oyun sonu müziği.")]
    [SerializeField] private AudioClip endMusic;
    [SerializeField, Range(0f, 1f)] private float endMusicVolume = 0.6f;

    private FirstPersonController player;
    private Vector3 startPosition;
    private bool started;

    private void Start()
    {
        if (!DayRoutine.FinalReady)
        {
            enabled = false;
            return;
        }
        player = FindFirstObjectByType<FirstPersonController>();
        if (player != null)
            startPosition = player.transform.position;
    }

    private void Update()
    {
        if (started || player == null)
            return;

        Vector3 offset = player.transform.position - startPosition;
        offset.y = 0f;
        if (offset.magnitude >= triggerDistance)
        {
            started = true;
            StartCoroutine(Run());
        }
    }

    private IEnumerator Run()
    {
        PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
        player.InputEnabled = false;
        if (interactor != null)
            interactor.enabled = false;
        // Kalabalık (hepsi oyuncu gibi giyinmiş) caddenin ortasına, karakterin çevresine toplanır.
        NpcCrowd crowd = FindFirstObjectByType<NpcCrowd>();
        if (crowd != null)
            crowd.BeginFinale(new Vector3(streetCenterX, player.transform.position.y, player.transform.position.z));
        StartCoroutine(Letterbox());
        yield return WalkToCenter();
        yield return new WaitForSeconds(stopDelay);
        player.Frozen = true;

        Camera cam = player.GetComponentInChildren<Camera>();
        yield return PullBack(cam);
        // Kalabalık (hepsi aynı giyinmiş, en içte oyuncuya benzeyenler) toplanmadan son çekime geçilmez; bu arada
        // kamera karakterin çevresinde çok yavaş döner.
        // Dönüş hızı yavaşça açılır (çekilme duruşla bitiyor), vinç de bu hızla devralır: geçişlerde kesinti olmaz.
        float orbitSpeed = 0f;
        for (float t = 0f; t < maxRingWait && (crowd == null || !crowd.RingFormed || t < holdDuration); t += Time.deltaTime)
        {
            orbitSpeed = waitOrbitSpeed * Mathf.SmoothStep(0f, 1f, t / 2f);
            Orbit(cam.transform, orbitSpeed * Time.deltaTime);
            yield return null;
        }
        yield return Crane(cam, orbitSpeed);

        // Son an: herkes olduğu pozda donar, ses tamamen kesilir.
        if (crowd != null)
            crowd.FreezeFinale();
        Animator playerAnimator = player.GetComponentInChildren<Animator>();
        if (playerAnimator != null)
            playerAnimator.speed = 0f;
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            source.Pause();
        yield return new WaitForSeconds(silenceDuration);
        AudioListener.volume = GameSettings.MasterVolume / 100f;
        yield return Credits();

        DayRoutine.ResetAll();
        SceneLoader.Load(menuScene);
    }

    // Karakter caddenin orta çizgisine yürür (girdi kapalı, FirstPersonController.ScriptedVelocity), bakış yürüdüğü
    // yöne ve ufka döner; varınca caddeye döner. Bir şeye takılırsa maxWalkDuration sonunda olduğu yerde durur.
    private IEnumerator WalkToCenter()
    {
        Transform root = player.transform;
        Transform cameraRoot = player.CameraRoot;
        Vector3 target = new Vector3(streetCenterX, root.position.y, root.position.z);

        for (float time = 0f; time < maxWalkDuration; time += Time.deltaTime)
        {
            Vector3 toTarget = target - root.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < 0.1f)
                break;
            Vector3 direction = toTarget / distance;
            // Son metrede yavaşlar, hedefi geçmez.
            player.ScriptedVelocity = direction * walkSpeed * Mathf.Clamp01(distance / 0.6f + 0.15f);
            root.rotation = Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(direction), 120f * Time.deltaTime);
            cameraRoot.localRotation = Quaternion.RotateTowards(cameraRoot.localRotation, Quaternion.identity, 40f * Time.deltaTime);
            yield return null;
        }
        player.ScriptedVelocity = Vector3.zero;

        Vector3 facing = finalFacing;
        facing.y = 0f;
        Quaternion from = root.rotation;
        Quaternion to = Quaternion.LookRotation(facing.normalized);
        Quaternion camFrom = cameraRoot.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / turnDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            root.rotation = Quaternion.Slerp(from, to, k);
            cameraRoot.localRotation = Quaternion.Slerp(camFrom, Quaternion.identity, k);
            yield return null;
        }
        root.rotation = to;
        cameraRoot.localRotation = Quaternion.identity;
        player.SyncLookToTransforms();
    }

    // Kamera karakterin başından ayrılıp önüne ve yukarısına çıkar, dönüp karaktere tepeden bakar.
    private IEnumerator PullBack(Camera cam)
    {
        Transform camTransform = cam.transform;
        camTransform.SetParent(null, true);

        // Birinci şahısta gizlenen kafa (saç, gözlük) artık görünsün, kafasız kopya gizlensin.
        PlayerBody body = player.GetComponentInChildren<PlayerBody>();
        if (body != null)
            body.SetThirdPerson(true);

        Transform target = player.transform;
        Vector3 forward = target.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 startPos = camTransform.position;
        Quaternion startRot = camTransform.rotation;
        float startFov = cam.fieldOfView;
        Vector3 focus = target.position + Vector3.up * lookHeight;
        Vector3 endPos = target.position + Vector3.up * finalHeight + forward * finalFront;

        for (float t = 0f; t < pullBackDuration; t += Time.deltaTime)
        {
            float k = t / pullBackDuration;
            float smooth = Mathf.SmoothStep(0f, 1f, k);
            // Önce yükselir (başın içinden çıkar), sonra öne açılır; yükselmeden öne giderse yüzün içinden geçer.
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(k));
            Vector3 pos = Vector3.Lerp(startPos, endPos, smooth);
            pos.y = Mathf.Lerp(startPos.y, endPos.y, rise);
            camTransform.position = pos;

            // Bakış ileriden karaktere döner (~180°); kamera başın üstüne çıkarken dönmeye başlar. Yatay ve dikey açı
            // ayrı çevrilir: Slerp yarım turu yuvarlanarak (baş aşağı) yapabiliyor.
            Vector3 startEuler = startRot.eulerAngles;
            Vector3 lookEuler = Quaternion.LookRotation(focus - pos, Vector3.up).eulerAngles;
            float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.6f, k));
            camTransform.rotation = Quaternion.Euler(Mathf.LerpAngle(startEuler.x, lookEuler.x, turn),
                Mathf.LerpAngle(startEuler.y, lookEuler.y, turn), 0f);
            cam.fieldOfView = Mathf.Lerp(startFov, finalFieldOfView, smooth);
            yield return null;
        }
    }

    private void Orbit(Transform cam, float degrees)
    {
        Vector3 pivot = player.transform.position;
        cam.RotateAround(pivot, Vector3.up, degrees);
        cam.rotation = Quaternion.LookRotation(pivot + Vector3.up * lookHeight - cam.position, Vector3.up);
    }

    // Son çekim: kamera yükselerek karakterin çevresinden (bekleme dönüşünün yönünde, aynı hızla başlayıp) dolaşır ve
    // arkasına geçer; bakış karakterden önündeki caddeye kayar, hafif eğik kalır. Konum, yükseklik, bakış ve FOV
    // smootherstep ile, yatay açı Hermite eğrisiyle (başta bekleme hızı, sonda 0) ilerler: başta ve sonda sarsıntı yok.
    // Bu sırada sokağın sesi yavaş yavaş kısılır.
    private IEnumerator Crane(Camera cam, float startYawSpeed)
    {
        Transform camTransform = cam.transform;
        Transform target = player.transform;
        Vector3 pivot = target.position;
        Vector3 forward = target.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;

        Vector3 startOffset = camTransform.position - pivot;
        float startYaw = Mathf.Atan2(startOffset.x, startOffset.z) * Mathf.Rad2Deg;
        float startDistance = new Vector2(startOffset.x, startOffset.z).magnitude;
        float startHeight = startOffset.y;
        float startFov = cam.fieldOfView;
        Vector3 startFocus = pivot + Vector3.up * lookHeight;
        Vector3 endFocus = pivot + forward * craneLookAhead + Vector3.up * craneLookHeight;
        float startVolume = AudioListener.volume;

        // Arkası: karakterin baktığı yönün tersi. Bekleme dönüşü hangi yöne gidiyorsa o yönde devam edilir.
        float behindYaw = Mathf.Atan2(-forward.x, -forward.z) * Mathf.Rad2Deg;
        float dir = startYawSpeed < 0f ? -1f : 1f;
        float delta = Mathf.Repeat((behindYaw - startYaw) * dir, 360f);
        if (delta < 45f)
            delta += 360f;
        float endYaw = startYaw + delta * dir;
        float m0 = startYawSpeed * craneDuration;

        for (float t = 0f; t < craneDuration; t += Time.deltaTime)
        {
            float k = t / craneDuration;
            float s = SmootherStep(k);
            // Yükseliş biraz önden gider: kamera önce kalabalığın üstüne çıkar, sonra arkaya dolaşır.
            float rise = SmootherStep(Mathf.Clamp01(k * 1.25f));
            float k2 = k * k, k3 = k2 * k;
            float yaw = (2f * k3 - 3f * k2 + 1f) * startYaw + (k3 - 2f * k2 + k) * m0 + (-2f * k3 + 3f * k2) * endYaw;
            float yawRad = yaw * Mathf.Deg2Rad;
            float distance = Mathf.Lerp(startDistance, craneBack, s);
            float height = Mathf.Lerp(startHeight, craneHeight, rise);
            camTransform.position = pivot + new Vector3(Mathf.Sin(yawRad) * distance, height, Mathf.Cos(yawRad) * distance);
            Vector3 focus = Vector3.Lerp(startFocus, endFocus, s);
            camTransform.rotation = Quaternion.LookRotation(focus - camTransform.position, Vector3.up);
            cam.fieldOfView = Mathf.Lerp(startFov, craneFieldOfView, s);
            AudioListener.volume = startVolume * (1f - Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        AudioListener.volume = 0f;
    }

    private static float SmootherStep(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * x * (x * (x * 6f - 15f) + 10f);
    }

    // Final başlarken üstten ve alttan siyah sinema bantları girer.
    private IEnumerator Letterbox()
    {
        if (letterbox <= 0f)
            yield break;
        Canvas canvas = UiKit.CreateCanvas("FinalSinemaBantlari", short.MaxValue - 2);
        RectTransform top = Bar(canvas.transform, 1f);
        RectTransform bottom = Bar(canvas.transform, 0f);
        for (float t = 0f; t < 2.5f; t += Time.deltaTime)
        {
            float h = letterbox * Mathf.SmoothStep(0f, 1f, t / 2.5f);
            top.anchorMin = new Vector2(0f, 1f - h);
            bottom.anchorMax = new Vector2(1f, h);
            yield return null;
        }
        top.anchorMin = new Vector2(0f, 1f - letterbox);
        bottom.anchorMax = new Vector2(1f, letterbox);
    }

    private static RectTransform Bar(Transform parent, float edge)
    {
        var go = new GameObject(edge > 0.5f ? "Ust_Bant" : "Alt_Bant", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, edge);
        rt.anchorMax = new Vector2(1f, edge);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    // Son kare renginden tamamen solar ve kararır, üstünde Hazırlayanlar ekranı belirir (ESC ile atlanır).
    private IEnumerator Credits()
    {
        Canvas canvas = UiKit.CreateCanvas("FinalJenerik", short.MaxValue - 1);
        // ScreenFader perdesinin (short.MaxValue) altında kalır, yükleme geçişi bunun üstünden kararır.
        bool skipped = false;
        AudioSource music = StartEndMusic();
        CreditsView view = CreditsView.Create(canvas.transform, 0.6f, clickSound, () => skipped = true, quote, quoteAuthor);
        view.Group.alpha = 0f;
        Cursor.visible = false;

        float startFade = Shader.GetGlobalFloat("_Solma");
        for (float t = 0f; t < fadeToBlackDuration && !skipped; t += Time.deltaTime)
        {
            float k = t / fadeToBlackDuration;
            Shader.SetGlobalFloat("_Solma", Mathf.Lerp(startFade, 1f, k));
            view.Group.alpha = k;
            yield return null;
        }
        Shader.SetGlobalFloat("_Solma", 1f);
        view.Group.alpha = 1f;

        for (float t = 0f; t < creditsDuration && !skipped; t += Time.deltaTime)
            yield return null;

        // Müzik menüye geçerken sönerek biter (sahne yüklemesi kaynağı zaten yok eder).
        if (music != null)
        {
            float start = music.volume;
            for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
            {
                music.volume = start * (1f - t / 1.5f);
                yield return null;
            }
        }
    }

    private AudioSource StartEndMusic()
    {
        if (endMusic == null)
            return null;
        AudioSource source = new GameObject("OyunSonuMuzigi").AddComponent<AudioSource>();
        source.clip = endMusic;
        source.spatialBlend = 0f;
        source.loop = false;
        source.volume = endMusicVolume * GameSettings.Volume(AudioCategory.Music);
        source.Play();
        return source;
    }
}
