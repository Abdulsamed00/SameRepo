using System.Collections;
using UnityEngine;

/// <summary>
/// Ayna karşısında poz verme. F ile oyuncu aynanın önüne geçer, aynaya döner, kamera camın ortasına eğilir ve
/// poz animasyonu oynar; oyuncu kendini birinci şahıstan aynanın gerçek yansımasında (<see cref="PlanarMirror"/>) görür.
/// Tekrar F ile poz biter.
/// Ayna yüzeyinin +z ekseni odaya bakmalı; objede ya da altında collider olmalı.
/// </summary>
public class MirrorPose : Interactable
{
    // Ayna boy aynası, önünde durulur.
    protected override float DefaultMaxDistance => 2f;

    [SerializeField] private string stopPrompt = "Poz vermeyi bırak";

    [Header("Konum")]
    [Tooltip("Ayna yüzeyi (camın ortası, +z odaya bakar). Boşsa bu obje.")]
    [SerializeField] private Transform surface;
    [Tooltip("Oyuncunun aynadan uzaklığı (m, zeminde). Camın tamamı görüş açısına sığacak kadar uzak olmalı.")]
    [SerializeField] private float standDistance = 1.6f;
    [SerializeField] private float moveDuration = 0.5f;

    [Header("Ses")]
    [Tooltip("Poz vermeye başlarken çalar.")]
    [SerializeField] private AudioClip poseSound;

    [Header("Klinik sabahı: beyaz kenar (07.11)")]
    [Tooltip("Same/OutlineNormal malzemesi (Assets/Shaders/Outline/Ayna_Outline.mat). 4. gün sabahı aynaya bakılana kadar " +
             "çerçevenin çevresinde cüzdandaki gibi nabız atan beyaz kenar çizer (arayüz yönlendirme yazmaz).")]
    [SerializeField] private Material outlineMaterial;
    [Tooltip("Kenarın çizileceği model (boşsa altındaki en büyük renderer).")]
    [SerializeField] private Renderer outlineTarget;
    [SerializeField] private float outlineIntensity = 3f;
    [SerializeField] private float outlineWidth = 0.006f;
    [SerializeField] private float pulseSpeed = 2.2f;

    [Header("Klinik sabahı: buruna yakın çekim (07.11)")]
    [Tooltip("Burun noktası: kafa kemiğinden (yukarı, ileri) metre.")]
    [SerializeField] private Vector2 noseOffset = new Vector2(0.06f, 0.11f);
    [Tooltip("Kameranın burundan yana uzaklığı (eksi = karakterin solu; sağda saç tutamı burnu kesiyor) ve öne kayması (m).")]
    [SerializeField] private Vector2 profileOffset = new Vector2(-0.42f, 0.10f);
    [SerializeField] private float zoomInDuration = 1.1f;
    [SerializeField] private float zoomHold = 2f;
    [Tooltip("Estetikten sonraki ilk poz (08.11): yeni burun ve dudağın yandan gösterildiği süre (sn).")]
    [SerializeField] private float revealHold = 3.5f;
    [Tooltip("Estetik sonrası çekimde bakılan nokta burnun bu kadar altında (m): burun ve dudak birlikte kadrajda.")]
    [SerializeField] private float revealLookDown = 0.035f;

    // Estetikten sonra yeni hâli bir kez gösterildi mi (yeni oyunda PlayerLook sıfırlanınca yeniden).
    private static bool surgeryRevealShown;
    [SerializeField] private float zoomOutDuration = 0.9f;
    [SerializeField] private float zoomFovStart = 34f;
    [SerializeField] private float zoomFovEnd = 26f;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private Material outline;
    private float glow;

    private PlayerInteractor posingPlayer;
    private bool busy;
    private bool playedDefeat;

    public override string Prompt => posingPlayer != null ? stopPrompt : base.Prompt;

    private void Awake()
    {
        if (surface == null)
            surface = transform;

        if (outlineMaterial != null)
        {
            if (outlineTarget == null)
            {
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r is MeshRenderer && (outlineTarget == null
                        || r.bounds.size.sqrMagnitude > outlineTarget.bounds.size.sqrMagnitude))
                        outlineTarget = r;
                }
            }
            if (outlineTarget != null)
            {
                outline = new Material(outlineMaterial);
                outline.SetColor(OutlineColorId, Color.black);
                outline.SetFloat(OutlineWidthId, outlineWidth);
                var mats = new System.Collections.Generic.List<Material>(outlineTarget.sharedMaterials) { outline };
                outlineTarget.sharedMaterials = mats.ToArray();
            }
        }
    }

    private void Update()
    {
        if (outline == null)
            return;
        // 4. gün sabahı, aynaya bakılana kadar çerçeve nabız gibi parlar (cüzdandaki gibi).
        bool waiting = DayRoutine.MorningMirrorPending && posingPlayer == null && !PauseMenu.IsPaused;
        glow = Mathf.MoveTowards(glow, waiting ? 1f : 0f, 2f * Time.unscaledDeltaTime);
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * pulseSpeed);
        float v = outlineIntensity * (0.45f + 0.55f * pulse) * glow;
        outline.SetColor(OutlineColorId, new Color(v, v, v, 1f));
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        if (busy)
            return false;
        if (posingPlayer != null)
            return posingPlayer == interactor;
        // Gece, ayna odasının ışığı kapalıyken poz verilmez (09.11, kullanıcı isteği; bildirim de çıkmaz).
        if (DayRoutine.NightFell && !RoomLit)
            return false;
        PlayerSitController sitter = interactor.GetComponent<PlayerSitController>();
        return sitter == null || !sitter.IsSeated;
    }

    [Tooltip("Ayna odasının ışık kimliği (RoomLights.RoomId). Gece bu oda aydınlık değilse poz verilmez.")]
    [SerializeField] private string roomId = "Yatak";
    private RoomLights roomLights;

    private bool RoomLit
    {
        get
        {
            if (roomLights == null)
            {
                foreach (RoomLights room in FindObjectsByType<RoomLights>(FindObjectsSortMode.None))
                    if (room.RoomId == roomId)
                        roomLights = room;
            }
            return roomLights == null || roomLights.IsOn;
        }
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (busy)
            return;
        StartCoroutine(posingPlayer == null ? StartPose(interactor) : StopPose());
    }

    private IEnumerator StartPose(PlayerInteractor interactor)
    {
        PlayerBody body = interactor.GetComponentInChildren<PlayerBody>();
        FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
        CharacterController cc = interactor.GetComponent<CharacterController>();
        if (body == null || controller == null)
            yield break;

        busy = true;
        posingPlayer = interactor;
        // Kamera eğilince ışın aynaya bakmayabilir; çıkış için F her durumda aynaya gitsin.
        interactor.Zone = this;
        controller.Frozen = true;
        body.PrepareForPose();
        if (cc != null)
            cc.enabled = false;

        Transform player = interactor.transform;
        Transform cameraRoot = controller.CameraRoot;
        Vector3 forward = Vector3.ProjectOnPlane(surface.forward, Vector3.up).normalized;
        Vector3 standPos = surface.position + forward * standDistance;
        standPos.y = player.position.y;
        Quaternion standRot = Quaternion.LookRotation(-forward, Vector3.up);

        // Duruş noktasındaki kameradan camın ortasına bakış açısı.
        Vector3 eye = standPos + Vector3.up * cameraRoot.localPosition.y;
        Vector3 toMirror = surface.position - eye;
        float pitch = -Mathf.Atan2(toMirror.y, Vector3.ProjectOnPlane(toMirror, Vector3.up).magnitude) * Mathf.Rad2Deg;
        Quaternion cameraEndRot = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 startPos = player.position;
        Quaternion startRot = player.rotation;
        Quaternion cameraStartRot = cameraRoot.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / moveDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            player.SetPositionAndRotation(Vector3.Lerp(startPos, standPos, k), Quaternion.Slerp(startRot, standRot, k));
            cameraRoot.localRotation = Quaternion.Slerp(cameraStartRot, cameraEndRot, k);
            yield return null;
        }
        player.SetPositionAndRotation(standPos, standRot);
        cameraRoot.localRotation = cameraEndRot;

        // 4. gün sabahı aynadaki kendini beğenmez; günün sonundaki poz normaldir.
        playedDefeat = DayRoutine.MorningMirrorPending;
        if (playedDefeat)
        {
            // Önce yandan buruna yakın çekim, sonra aynaya dönüp üzülür.
            yield return NoseCloseUp(controller, body, zoomHold, 0f);
            body.PlayDefeat();
        }
        else
        {
            // Estetikten sonraki ilk poz: önce yandan yeni burun ve dudak, sonra normal poz.
            if (!PlayerLook.SurgeryDone)
                surgeryRevealShown = false;
            else if (!surgeryRevealShown)
            {
                surgeryRevealShown = true;
                yield return NoseCloseUp(controller, body, revealHold, revealLookDown);
            }
            body.PlayPose();
        }
        GameAudio.PlayAt(poseSound, surface.position);
        busy = false;
    }

    // Kamera göz noktasından karakterin yanına süzülür, burnu profilden yakın çeker, bekler, göze geri döner.
    private IEnumerator NoseCloseUp(FirstPersonController controller, PlayerBody body, float hold, float lookDown)
    {
        Camera cam = controller.GetComponentInChildren<Camera>();
        Animator animator = body.GetComponent<Animator>();
        Transform head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (cam == null || head == null)
            yield break;

        Transform camTransform = cam.transform;
        Transform parent = camTransform.parent;
        Vector3 localPos = camTransform.localPosition;
        Quaternion localRot = camTransform.localRotation;
        float fov = cam.fieldOfView;
        camTransform.SetParent(null, true);
        body.SetThirdPerson(true);

        Transform player = controller.transform;
        Vector3 Nose() => head.position + Vector3.up * (noseOffset.x - lookDown) + player.forward * noseOffset.y;
        Vector3 ProfilePos() => Nose() + player.right * profileOffset.x + player.forward * profileOffset.y;
        Vector3 eyePos = camTransform.position;
        Quaternion eyeRot = camTransform.rotation;

        // Yaklaşma: hafif yay çizerek (yüzün önünden geçmesin) yana çıkar, bakış buruna döner.
        for (float t = 0f; t < 1f; t += Time.deltaTime / zoomInDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            Vector3 pos = Vector3.Lerp(eyePos, ProfilePos(), k) + player.forward * (Mathf.Sin(k * Mathf.PI) * 0.12f);
            camTransform.SetPositionAndRotation(pos,
                Quaternion.Slerp(eyeRot, Quaternion.LookRotation(Nose() - pos, Vector3.up), k));
            cam.fieldOfView = Mathf.Lerp(fov, zoomFovStart, k);
            cam.nearClipPlane = 0.02f;
            yield return null;
        }
        // Bekleme: yavaşça daha da yaklaşır (FOV daralır).
        for (float t = 0f; t < hold; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / hold);
            Vector3 pos = ProfilePos();
            camTransform.SetPositionAndRotation(pos, Quaternion.LookRotation(Nose() - pos, Vector3.up));
            cam.fieldOfView = Mathf.Lerp(zoomFovStart, zoomFovEnd, k);
            cam.nearClipPlane = 0.02f;
            yield return null;
        }
        // Dönüş: göz noktasına, aynaya bakışa.
        Vector3 fromPos = camTransform.position;
        Quaternion fromRot = camTransform.rotation;
        float fromFov = cam.fieldOfView;
        for (float t = 0f; t < 1f; t += Time.deltaTime / zoomOutDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            Vector3 target = parent.TransformPoint(localPos);
            Vector3 pos = Vector3.Lerp(fromPos, target, k) + player.forward * (Mathf.Sin(k * Mathf.PI) * 0.12f);
            camTransform.SetPositionAndRotation(pos, Quaternion.Slerp(fromRot, parent.rotation * localRot, k));
            cam.fieldOfView = Mathf.Lerp(fromFov, fov, k);
            cam.nearClipPlane = 0.02f;
            yield return null;
        }
        camTransform.SetParent(parent, false);
        camTransform.localPosition = localPos;
        camTransform.localRotation = localRot;
        cam.fieldOfView = fov;
        body.SetThirdPerson(false);
    }

    private IEnumerator StopPose()
    {
        busy = true;
        PlayerInteractor interactor = posingPlayer;

        PlayerBody body = interactor.GetComponentInChildren<PlayerBody>();
        if (body != null)
            body.StopPose();

        // Poz klibinden locomotion'a geçiş bitene kadar oyuncu beklesin.
        yield return new WaitForSeconds(0.3f);

        CharacterController cc = interactor.GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = true;
        FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
        controller.SyncLookToTransforms();
        controller.Frozen = false;
        if (interactor.Zone == this)
            interactor.Zone = null;
        posingPlayer = null;
        busy = false;
        if (playedDefeat)
        {
            playedDefeat = false;
            DayRoutine.NotifyMorningMirrorSeen();
            yield break;
        }
        // Günün kıyafetiyle poz verildiyse uyku gelir (esneme), yatak açılır.
        DayRoutine.NotifyPoseEnded();
    }
}
