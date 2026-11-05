using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Birinci şahıs oyuncunun görünür gövdesi (AnaKarakter). Animator'daki yürüme/durma geçişini hıza bağlar,
/// oturma/kalkma animasyonlarını <see cref="PlayerSitController"/> için oynatır.
/// Alma animasyonu sadece üst gövde katmanında (UpperBody) oynar, bacaklar yürümeye devam eder.
/// Poz animasyonu (ayna) tüm gövdeyi alır; oyuncuyu dondurmak çağıranın işidir.
/// Yürürken gövde basılan yöne döner (D ile sağa, A ile sola); kamera dönmez. Dururken basılırsa önce yerinde
/// dönme klibi oynar (gövde klibin kök dönüşüyle döner); oyuncu bu sırada yavaşlamaz. Geri giderken gövde
/// arkasını dönmez, yürüme klibi tersten oynar. Elde tutulan eşyalar sağ avuçtaki RightHandHold'a oturur.
/// Saç ve gözlük "PlayerHead" katmanına alınır ve sadece oyuncunun kendi kamerasından gizlenir
/// (kamera kafanın içinde kalıyor). Kafa gövde mesh'iyle tek parça: gövde de bu katmana alınır, oyuncu kamerası
/// kafası ensede bir noktaya çökmüş bir kopyasını görür. Ayna vb. başka kameralarda kafa tam görünür.
/// Dururken fareyle sağa sola bakınca gövde yerinde kalır, sadece kafa döner; bakış gövdeden
/// <c>headTurnLimit</c>'ten fazla ayrılınca gövde peşinden gelir.
/// </summary>
// Kökün fareyle dönüşü (FirstPersonController.Update) bu karede görülsün.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Animator))]
public class PlayerBody : MonoBehaviour
{
    private const string HeadLayerName = "PlayerHead";

    // Animator Controller'daki state ve parametre adları (Assets/Anims/AnaKarakterAnims/AnaKarakter.controller).
    private const string LocomotionState = "Locomotion";
    private const string StandToSitState = "StandToSit";
    private const string SittingIdleState = "SittingIdle";
    private const string SitToStandState = "SitToStand";
    private const string PoseState = "Pose";
    private const string LyingDownState = "LyingDown";
    private const string DefeatState = "Defeat";
    private const string UpperBodyLayerName = "UpperBody";
    private const string PickupState = "Pickup";
    private const string PutDownState = "PutDown";
    private const string EmptyState = "Empty";
    private const string TurnLeftState = "TurnLeft";
    private const string TurnRightState = "TurnRight";
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int WalkAnimSpeedId = Animator.StringToHash("WalkAnimSpeed");

    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Camera playerCamera;
    [Tooltip("Yürüme klibinin doğal hızı (m/sn). Oyuncu daha hızlıysa klip orantılı hızlanır.")]
    [SerializeField] private float walkClipSpeed = 1.4f;
    [SerializeField] private float speedSmoothing = 12f;
    [SerializeField] private float crossFade = 0.2f;
    [Tooltip("Kalkma klibi (tersten oynatılınca oturma). Süreler buradan okunur.")]
    [SerializeField] private AnimationClip sitToStandClip;
    [Tooltip("Oturuştan uzanmaya geçiş süresi (sn).")]
    [SerializeField] private float lieDownBlend = 0.4f;
    [Tooltip("Oyuncu kamerasından gizlenecek renderer adları (kafaya ait ayrı parçalar).")]
    [SerializeField] private string[] hiddenRendererNames = { "TopuzSac", "BlondeHair", "Glasses" };
    [Tooltip("Kafayı da içeren gövde renderer'ı: oyuncu kamerası bunun kafasız kopyasını görür.")]
    [SerializeField] private string bodyRendererName = "Body";

    [Header("Yöne Dönme")]
    [Tooltip("Gövdenin hareket yönüne dönüş hızı (derece/sn).")]
    [SerializeField] private float turnSpeed = 540f;
    [Tooltip("Gövdenin bakış yönüne göre en fazla dönebileceği açı.")]
    [SerializeField] private float maxTurnAngle = 90f;
    [Tooltip("Dururken kafanın gövdeye göre en fazla dönebileceği açı; bakış bunu aşınca gövde döner.")]
    [SerializeField] private float headTurnLimit = 60f;
    [Tooltip("Gövde bakıştan bu kadar fazla ayrılınca (hızlı arkaya dönüş) oyuncu kamerası gövdeyi çizmez: kamera yakanın önünden gövdenin içini görüyordu. Gölge kalır.")]
    [SerializeField] private float hideBodyAngle = 100f;

    [Header("Dönme Animasyonu")]
    [Tooltip("Dururken ya da yürümeye yeni başlarken gövde en az bu kadar dönecekse (A/D) dönme klibi oynar.")]
    [SerializeField] private float turnClipMinAngle = 60f;
    [Tooltip("Dönme klibi sadece bu hızın altındayken başlar (m/sn); yürürken yön değiştirmek hızlı dönüşle olur.")]
    [SerializeField] private float turnClipMaxSpeed = 0.6f;
    [SerializeField] private float turnCrossFade = 0.1f;
    [Tooltip("Dönme klibinin giriş geçişiyle birlikte gerçekte döndürdüğü açı (ölçüldü: 90° klip, 0.1 sn geçişle ~84°). " +
             "Klibin dönüşü, dönülecek açı / bu oranında ölçeklenir.")]
    [SerializeField] private float turnClipDegrees = 84f;

    [Header("Kafa")]
    [Tooltip("Ayakta/yürürken kafa IK ile kameranın baktığı yöne sabitlenir; yürüme klibinin kafayı öne arkaya " +
             "sallaması aynada kötü görünüyordu. 0 = sadece animasyon.")]
    [Range(0f, 1f)] [SerializeField] private float headLookWeight = 1f;
    [Tooltip("Kafanın bakış yönünden en fazla ne kadar dönebileceği (0 = sınırsız, 1 = hiç dönmez).")]
    [Range(0f, 1f)] [SerializeField] private float headLookClamp = 0.6f;
    [Tooltip("Oturma/poz gibi kendi kafa hareketi olan animasyonlara geçişte IK'nın açılıp kapanma hızı (1/sn).")]
    [SerializeField] private float headLookBlendSpeed = 4f;

    [Header("Alma")]
    [Tooltip("Alma animasyonunda elin eşyaya ulaştığı an (sn). Eşya bu ana kadar yerinde bekler.")]
    [SerializeField] private float pickupReachTime = 0.75f;
    [Tooltip("Tutma noktasının avuç yüzeyinden uzaklığı (m).")]
    [SerializeField] private float palmDepth = 0.03f;
    [Tooltip("Eşyayı tuttuktan sonra elin IK'dan animasyona dönüş süresi (sn).")]
    [SerializeField] private float pickupReleaseTime = 0.5f;
    [Tooltip("Eşyaya yetişmek için gövdenin alma klibinin üstüne en fazla ne kadar öne eğileceği (derece). 0 = kapalı: " +
             "alçak sehpada (37 cm) omuz eşyadan 1.2 m uzakta, bel ekseni omuza 0.3 m yakın olduğundan eğilmek açığı kapatmıyor, " +
             "klibin eğilmesinin üstüne binince gövde ikiye katlanıyor. Orada gerçek çözüm çömelme.")]
    [SerializeField] private float pickupMaxLean = 0f;

    [Header("Bırakma (alma tersten)")]
    [Tooltip("Tersten oynayan alma klibinde elin eşyayı yerine koyduğu an (sn).")]
    [SerializeField] private float putDownReachTime = 1.0f;
    [Tooltip("Tersten oynayan alma klibinin süresi (sn): klip uzunluğu / state hızı.")]
    [SerializeField] private float putDownDuration = 1.75f;

    [Header("Ürün Taşıma (tek/iki el IK)")]
    [Tooltip("Ürün bırakılınca ellerin serbest kalma süresi (sn).")]
    [SerializeField] private float carryReleaseTime = 0.3f;
    [Tooltip("Avatarın IK el dönüşünde ölçülen sabit kayma (derece, el kemiğinin x ekseni). Model değişirse yeniden ölç: istenen ve gerçekleşen el dönüşü arasındaki fark.")]
    [SerializeField] private float handIkTwist = 96f;
    [Tooltip("Ellerin ürüne uzanma süresi (sn); ürün bundan sonra kameranın önüne gelir.")]
    [SerializeField] private float carryReachTime = 0.45f;
    [Tooltip("Kamera gövdenin önünde olduğu için kollar yetişmezse gövdenin öne eğilebileceği en büyük açı (derece).")]
    [SerializeField] private float carryMaxLean = 35f;
    [Tooltip("Dirsek ipucunun gücü (0-1): dirsekler yana ve aşağı bakar.")]
    [SerializeField, Range(0f, 1f)] private float carryElbowWeight = 0.35f;

    [Header("Kapı / Dolap (el IK)")]
    [Tooltip("Elin kapağa uzanma süresi (sn).")]
    [SerializeField] private float touchReachTime = 0.25f;
    [Tooltip("Kapak durduktan sonra elin animasyona dönüş süresi (sn).")]
    [SerializeField] private float touchReleaseTime = 0.3f;
    [Tooltip("Kapağa yetişmek için gövdenin en fazla öne eğilebileceği açı (derece).")]
    [SerializeField] private float touchMaxLean = 20f;
    [Tooltip("Omuzdan kapak noktasına bu mesafeye kadar el tam uzanır (m). Kol yetişmiyorsa kapağa doğru tam uzanır.")]
    [SerializeField] private float touchFullReach = 1.5f;
    [Tooltip("Bu mesafeden uzaktaki kapaklar için el hiç uzanmaz (m); arası yumuşak azalır.")]
    [SerializeField] private float touchMaxReach = 2.1f;

    private Animator animator;
    private int upperBodyLayer;
    private float smoothedSpeed;
    private bool overridden;
    private Vector3 baseLocalPosition;
    private float bodyYaw;
    private Vector3? reachPoint;
    private float reachStartTime;
    private float reachDuration;
    private float putDownEndTime = -1f;
    private float pendingLean;
    private Transform carryRight;
    private Transform carryLeft;
    // IK'dan önce eşyayı o karenin kamera konumuna taşır (eller bir kare geride kalıp titremesin).
    private System.Action carrySync;
    private float carryLean;
    private float carryStart;
    private float carryReach;
    private float carryEndTime = -1f;
    private Transform touchAnchor;
    private Vector3 touchLocalPoint;
    private float touchStartTime;
    private float touchHoldTime;
    private float headLookBlend;
    private float lastRootYaw;
    private bool turning;
    private float turnSign;
    private float turnScale;
    private float turnStartTime;
    // Kafasız kopyada çöktürülen kemik (boyun) ve onun yerine geçen vekil.
    private Transform head;
    private Transform headProxy;
    private SkinnedMeshRenderer headlessBody;
    private bool thirdPerson;
    private bool bodyHidden;
    private readonly List<SkinnedMeshRenderer> hiddenBody = new List<SkinnedMeshRenderer>();

    public bool IsPosing { get; private set; }

    /// <summary>
    /// Yürüme döngüsünün fazı (Locomotion state'inin normalize zamanı; bir döngü = iki adım).
    /// Kamera sallanması adımlarla aynı ritimde olsun diye <see cref="FirstPersonController"/> okur.
    /// </summary>
    public bool TryGetWalkCycle(out float cycle)
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        cycle = info.normalizedTime;
        return !overridden && info.IsName(LocomotionState);
    }

    /// <summary>Sağ avuç içindeki tutma noktası. İleri ekseni avuç boyunca (işaret parmağından serçeye ters), yukarısı avuçtan dışarı.</summary>
    public Transform RightHandHold { get; private set; }

    /// <summary>Sol avuç içindeki tutma noktası (iki elle taşınan ürünler).</summary>
    public Transform LeftHandHold { get; private set; }


    /// <summary>
    /// Çanta (ve askısı) tek kemiğe, sağ köprücüğe (RightShoulder) %100 bağlı. Poz verirken omuz/kol dönünce çanta
    /// 1 m'lik kaldıraçla savrulup vücuttan ~15 cm açılıyordu (05.11). Köprücük yerine gövdeye (Spine1) sabit bir
    /// vekil kemik konur: çanta bağlanma pozunda aynı yerde başlar, ama kol/omuz hareketinden değil gövdeden etkilenir.
    /// </summary>
    private void AnchorBagToTorso()
    {
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name != "Bag" || smr.sharedMesh == null)
                continue;
            Transform[] bones = smr.bones;
            Matrix4x4[] bindPoses = smr.sharedMesh.bindposes;
            Transform torso = null;
            foreach (Transform b in bones)
            {
                if (b != null && b.name.EndsWith("Spine1"))
                    torso = b;
            }
            if (torso == null)
                continue;

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null || !bones[i].name.EndsWith("RightShoulder"))
                    continue;
                Matrix4x4 bindWorld = smr.transform.localToWorldMatrix * bindPoses[i].inverse;
                Matrix4x4 local = torso.worldToLocalMatrix * bindWorld;
                Transform proxy = new GameObject("Canta_Vekil_Kemik").transform;
                proxy.SetParent(torso, false);
                proxy.localPosition = local.GetColumn(3);
                proxy.localRotation = local.rotation;
                proxy.localScale = local.lossyScale;
                bones[i] = proxy;
            }
            smr.bones = bones;
        }
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        upperBodyLayer = animator.GetLayerIndex(UpperBodyLayerName);
        baseLocalPosition = transform.localPosition;
        RightHandHold = CreateHandHold(true);
        LeftHandHold = CreateHandHold(false);
        AnchorBagToTorso();
        if (controller == null)
            controller = GetComponentInParent<FirstPersonController>();
        if (playerCamera == null && controller != null)
            playerCamera = controller.GetComponentInChildren<Camera>(true);

        int layer = LayerMask.NameToLayer(HeadLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"'{HeadLayerName}' katmanı tanımlı değil, kafa parçaları gizlenemiyor.", this);
            return;
        }

        // Oyuncu solmaz (aynada kendini soluk görmesin): gövde solmadan sonra çizilen Reklam katmanında,
        // kafa parçaları PlayerHead'de (PC_Renderer o katmanı da solmadan sonra çizer).
        int exemptLayer = LayerMask.NameToLayer(SceneFade.ExemptLayerName);
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (System.Array.IndexOf(hiddenRendererNames, r.name) >= 0)
                r.gameObject.layer = layer;
            else if (exemptLayer >= 0)
                r.gameObject.layer = exemptLayer;
        }

        CreateHeadlessBody(layer);

        if (playerCamera != null)
            playerCamera.cullingMask &= ~(1 << layer);
    }

    // Kamera 1.7'de, kafa gövdeyle birlikte kökün 0.28 gerisinde. Gövde yürürken yöne dönünce ya da bakış hızlı
    // çevrilince kafa kameranın yanına/önüne geliyor, aşağı bakınca görünüyordu (yakın kırpma 0.05 olduğundan beri).
    // Kopyada boyun, kafa ve alt kemikleri yerine sıfır ölçekli bir vekil kemik var: hepsi boynun dibinde (omuz
    // hizası, yakanın içi) bir noktaya çöker. Önce sadece kafa çöküyordu, aşağı bakınca boyun sivri bir uç gibi
    // görünüyordu. Asıl gövde PlayerHead katmanında kalır, ayna onu tam görür.
    private void CreateHeadlessBody(int headLayer)
    {
        head = animator.GetBoneTransform(HumanBodyBones.Neck);
        if (head == null)
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        SkinnedMeshRenderer source = null;
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name == bodyRendererName)
            {
                source = r;
                break;
            }
        }
        if (source == null || head == null)
            return;

        headProxy = new GameObject("Kafa_Vekil").transform;
        headProxy.SetParent(head.parent, false);
        SyncHeadProxy();

        Transform[] bones = source.bones;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null && bones[i].IsChildOf(head))
                bones[i] = headProxy;
        }

        GameObject copy = Instantiate(source.gameObject, source.transform.parent);
        copy.name = source.name + "_Kafasiz";
        headlessBody = copy.GetComponent<SkinnedMeshRenderer>();
        headlessBody.bones = bones;
        source.gameObject.layer = headLayer;
    }

    private void SyncHeadProxy()
    {
        headProxy.SetLocalPositionAndRotation(head.localPosition, head.localRotation);
        headProxy.localScale = Vector3.one * 0.0001f;
    }

    private void LateUpdate()
    {
        // Animasyon ve kafa IK'sından sonra: kopyanın çöken boynu gerçek boynun dibini izlesin.
        if (headProxy != null)
            SyncHeadProxy();
    }

    private void Update()
    {
        float target = overridden || controller == null ? 0f : controller.HorizontalSpeed;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, target, speedSmoothing * Time.deltaTime);

        float targetYaw = 0f;
        float playback = 1f;
        if (target > 0.1f)
        {
            Vector3 local = controller.transform.InverseTransformDirection(controller.HorizontalVelocity);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            // Geriye doğru: arkasını dönmek yerine öne bakarak geri geri yürür (kamera gövdenin içine girmesin).
            if (Mathf.Abs(angle) > maxTurnAngle + 1f)
            {
                angle = Mathf.DeltaAngle(0f, angle + 180f);
                playback = -1f;
            }
            targetYaw = Mathf.Clamp(angle, -maxTurnAngle, maxTurnAngle);
        }

        // Dururken kökün (bakışın) dönüşü gövdeye yansımaz: gövde dünyada yerinde kalır, kafayı IK çevirir.
        // Işınlama gibi tek karelik büyük dönüşler (doğma noktası, ayna, oturma) sayılmaz.
        float rootYaw = controller != null ? controller.transform.eulerAngles.y : 0f;
        float rootDelta = Mathf.DeltaAngle(lastRootYaw, rootYaw);
        lastRootYaw = rootYaw;
        if (Mathf.Abs(rootDelta) > 45f)
            rootDelta = 0f;
        bool standing = !overridden && controller != null && !controller.Frozen && target <= 0.1f;

        // Dönme klibi: A/D ile yürümeye başlarken gövde hareket yönüne, dururken (A/D bırakılınca ya da fareyle
        // bakış gövdeden headTurnLimit'ten fazla ayrılınca) bakış yönüne döner.
        float turnAngle = Mathf.DeltaAngle(bodyYaw, targetYaw);
        if (turning)
            UpdateTurn(target, turnAngle);
        else if (!overridden && target > 0.1f && smoothedSpeed < turnClipMaxSpeed && Mathf.Abs(turnAngle) >= turnClipMinAngle)
            StartTurn(turnAngle);
        else if (standing && Mathf.Abs(bodyYaw - rootDelta) > headTurnLimit + 1f)
            StartTurn(-(bodyYaw - rootDelta));

        // Gövde kameranın altında, oyuncu kökü etrafında döner; kameranın gövdeye göre yeri değişmez.
        // Dönme klibi sürerken dönüşü klibin kök hareketi verir (OnAnimatorMove).
        if (turning)
            bodyYaw -= rootDelta;
        else if (standing)
        {
            bodyYaw -= rootDelta;
            float limited = Mathf.Clamp(bodyYaw, -headTurnLimit, headTurnLimit);
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, limited, turnSpeed * Time.deltaTime);
        }
        else
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, targetYaw, turnSpeed * Time.deltaTime);
        ApplyBodyYaw();
        UpdateBodyVisibility();

        animator.SetFloat(SpeedId, smoothedSpeed);
        animator.SetFloat(WalkAnimSpeedId, playback * Mathf.Max(1f, smoothedSpeed / walkClipSpeed));
        headLookBlend = Mathf.MoveTowards(headLookBlend, overridden ? 0f : 1f, headLookBlendSpeed * Time.deltaTime);

        // Tersten oynayan state'te exit time geçişi çalışmıyor; bitişi buradan yapılır.
        if (putDownEndTime >= 0f && Time.time >= putDownEndTime)
        {
            putDownEndTime = -1f;
            animator.CrossFadeInFixedTime(EmptyState, crossFade, upperBodyLayer);
        }
    }

    // Hızlı arkaya dönünce gövde (headTurnLimit'ten sonra dönme klibiyle) peşinden gelir; o yarım saniyede kamera
    // gövdenin önüne bakıyor ve boynu çöken yakadan gövdenin içi görünüyor. Gövde bakıştan çok ayrılmışken parçalar
    // sadece gölge atar (ShadowsOnly), çizilmez; gövde dönünce geri gelir. Üçüncü şahısta (menü, final) dokunulmaz.
    private void UpdateBodyVisibility()
    {
        bool hide = !thirdPerson && Mathf.Abs(Mathf.DeltaAngle(0f, bodyYaw)) > hideBodyAngle;
        if (hide == bodyHidden)
            return;
        bodyHidden = hide;

        if (hide)
        {
            int headLayer = LayerMask.NameToLayer(HeadLayerName);
            foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (r.gameObject.layer == headLayer || r.shadowCastingMode != ShadowCastingMode.On)
                    continue;
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                hiddenBody.Add(r);
            }
            return;
        }
        RestoreBody();
    }

    private void RestoreBody()
    {
        foreach (SkinnedMeshRenderer r in hiddenBody)
            if (r != null)
                r.shadowCastingMode = ShadowCastingMode.On;
        hiddenBody.Clear();
        bodyHidden = false;
    }

    private void OnDisable() => RestoreBody();

    private void ApplyBodyYaw()
    {
        Quaternion yaw = Quaternion.Euler(0f, bodyYaw, 0f);
        transform.SetLocalPositionAndRotation(yaw * baseLocalPosition, yaw);
    }

    // Yerinde 90° dönme klibi (Mixamo "Left Turn 90"; sağa dönüş aynı klibin aynası). Klibin dönüşü istenen
    // açıya ölçeklenir, ayak adımları aynı kalır.
    private void StartTurn(float angle)
    {
        turning = true;
        turnSign = Mathf.Sign(angle);
        turnScale = Mathf.Clamp(Mathf.Abs(angle) / turnClipDegrees, 0.5f, 1.5f);
        turnStartTime = Time.time;
        animator.CrossFadeInFixedTime(turnSign > 0f ? TurnRightState : TurnLeftState, turnCrossFade, 0, 0f);
    }

    // Klip bitince, oyuncu ters yöne basınca ya da başka bir animasyon (oturma, poz) gelince dönme biter.
    // Oyuncu tuşu bıraksa da dönüş tamamlanır.
    private void UpdateTurn(float target, float turnAngle)
    {
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        bool inTurnState = info.IsName(TurnLeftState) || info.IsName(TurnRightState);
        bool clipDone = inTurnState && !animator.IsInTransition(0) && info.normalizedTime >= 1f;
        bool reversed = target > 0.1f && Mathf.Abs(turnAngle) > 10f && Mathf.Sign(turnAngle) != turnSign;
        bool timedOut = Time.time - turnStartTime > 3f;
        if (overridden || clipDone || reversed || timedOut)
            EndTurn();
    }

    private void EndTurn()
    {
        turning = false;
        if (!overridden)
            animator.CrossFadeInFixedTime(LocomotionState, crossFade);
    }

    // Kök hareketi sadece dönme klibinde kullanılır (sadece yatay dönüşü); diğer kliplerde yok sayılır.
    private void OnAnimatorMove()
    {
        if (!turning)
            return;
        bodyYaw += Mathf.DeltaAngle(0f, animator.deltaRotation.eulerAngles.y) * turnScale;
        ApplyBodyYaw();
    }

    // Avuç içi parmak köklerinden hesaplanır: model değişse de eşya avuca oturur.
    // Eksenler iki elde de aynı anlamda: ileri = başparmak tarafı, yukarı = avuçtan dışarı (eşyaya doğru).
    // Sol el sağın aynası olduğundan çapraz çarpımın işareti ters çıkar, avuç yönü buna göre seçilir.
    private Transform CreateHandHold(bool right)
    {
        Transform hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        if (hand == null)
            return null;

        Transform hold = new GameObject(right ? "RightHandHold" : "LeftHandHold").transform;
        hold.SetParent(hand, false);

        Transform middle = animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
        Transform index = animator.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        Transform little = animator.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
        if (middle == null || index == null || little == null)
            return hold;

        Vector3 fingers = middle.position - hand.position;
        Vector3 across = (index.position - little.position).normalized;
        Vector3 palm = Vector3.Cross(fingers.normalized, across).normalized;
        Vector3 outOfPalm = right ? -palm : palm;
        hold.SetPositionAndRotation(hand.position + fingers * 0.6f - outOfPalm * palmDepth,
            Quaternion.LookRotation(across, outOfPalm));
        return hold;
    }

    /// <summary>
    /// Kapı/dolap açıp kapatırken sağ el kapağa uzanır ve kapak hareket ederken üstünde kalır (IK), sonra bırakır.
    /// Nokta <paramref name="anchor"/>'a göre saklanır: kapak dönerken el onunla gelir.
    /// </summary>
    /// <param name="worldPoint">Kapakta bakılan nokta (yüzeyin biraz önünde olmalı).</param>
    /// <param name="holdTime">Elin ulaştıktan sonra kapakta kalacağı süre (sn): kapak bu sürede hareket eder.</param>
    /// <returns>Elin kapağa ulaştığı an (sn); kapak bundan sonra hareket etmeli.</returns>
    public float BeginTouch(Transform anchor, Vector3 worldPoint, float holdTime)
    {
        if (anchor == null || RightHandHold == null)
            return 0f;

        touchAnchor = anchor;
        touchLocalPoint = anchor.InverseTransformPoint(worldPoint);
        touchStartTime = Time.time;
        touchHoldTime = holdTime;
        return touchReachTime;
    }

    private void ApplyTouch()
    {
        if (touchAnchor == null)
            return;

        float t = Time.time - touchStartTime;
        float weight;
        if (t < touchReachTime)
            weight = Mathf.SmoothStep(0f, 1f, t / touchReachTime);
        else if (t < touchReachTime + touchHoldTime)
            weight = 1f;
        else
        {
            float release = (t - touchReachTime - touchHoldTime) / touchReleaseTime;
            if (release >= 1f)
            {
                touchAnchor = null;
                return;
            }
            weight = 1f - Mathf.SmoothStep(0f, 1f, release);
        }

        Vector3 point = touchAnchor.TransformPoint(touchLocalPoint);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform palm = RightHandHold;

        // Kol yetişmeyen kapakta önce gövde öne eğilir; hâlâ yetmiyorsa el, kapağa doğru kolun tam uzandığı noktaya
        // gider (kısmi ağırlıkla çekilirse el göğüs hizasında, ekranın dışında kalıyordu). Çok uzak kapaklarda el
        // hiç uzanmaz.
        if (shoulder != null)
        {
            float armLength = ArmLength();
            float distance = (point - shoulder.position).magnitude;
            weight *= Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(touchFullReach, touchMaxReach, distance));
            float lean = LeanAngle(point, shoulder, armLength, touchMaxLean);
            pendingLean = Mathf.Max(pendingLean, lean * weight);
            Vector3 leanedShoulder = LeanedPosition(shoulder.position, lean * weight);
            Vector3 toPoint = point - leanedShoulder;
            float leanedDistance = toPoint.magnitude;
            if (leanedDistance > armLength * 0.97f)
                point = leanedShoulder + toPoint / leanedDistance * armLength * 0.97f;
        }
        if (weight <= 0f)
            return;

        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
        animator.SetIKPosition(AvatarIKGoal.RightHand, point + (hand.position - palm.position));
    }

    /// <returns>Oturma süresi (sn).</returns>
    public float PlaySitDown()
    {
        overridden = true;
        // Kalkma klibi tersten (state hızı -1): sondan, yani ayakta pozdan başlar.
        animator.CrossFade(StandToSitState, crossFade, 0, 1f);
        return ClipLength;
    }

    /// <param name="instant">Geçişsiz: ilk karede doğrudan oturma pozunda (ana menü açılışı).</param>
    public void PlaySittingIdle(bool instant = false)
    {
        overridden = true;
        if (instant)
        {
            animator.Play(SittingIdleState, 0, 0f);
            animator.Update(0f);
            return;
        }
        animator.CrossFadeInFixedTime(SittingIdleState, crossFade);
    }

    /// <summary>
    /// Üçüncü şahıs görünüm (ana menü): kafa, saç ve gözlük oyuncu kamerasında da görünür, çöken boyunlu
    /// kafasız kopya gizlenir (asıl gövdeyle üst üste binmesin). Kapatınca birinci şahıs görünümü döner.
    /// </summary>
    public void SetThirdPerson(bool on)
    {
        int layer = LayerMask.NameToLayer(HeadLayerName);
        if (layer >= 0 && playerCamera != null)
        {
            if (on)
                playerCamera.cullingMask |= 1 << layer;
            else
                playerCamera.cullingMask &= ~(1 << layer);
        }
        if (headlessBody != null)
            headlessBody.enabled = !on;
        thirdPerson = on;
        if (on)
            RestoreBody();
    }

    /// <returns>Kalkma süresi (sn).</returns>
    public float PlayStandUp()
    {
        overridden = true;
        animator.CrossFadeInFixedTime(SitToStandState, crossFade);
        return ClipLength;
    }

    /// <summary>Otururken yatağa uzanma (yatak). Klip sonunda sırtüstü yatar pozda kalır.</summary>
    /// <param name="blend">Oturuştan uzanma klibine geçiş süresi (sn).</param>
    /// <returns>Uzanma süresi (sn).</returns>
    public float PlayLieDown(out float blend)
    {
        overridden = true;
        blend = lieDownBlend;
        animator.CrossFadeInFixedTime(LyingDownState, lieDownBlend);
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == LyingDownState)
                return clip.length;
        return 5f;
    }

    public void PlayLocomotion()
    {
        overridden = false;
        IsPosing = false;
        animator.CrossFadeInFixedTime(LocomotionState, crossFade);
    }

    /// <summary>
    /// Eşyaya uzanıp alma. Üst gövdede bir kez oynar, sonra kendiliğinden locomotion'a döner.
    /// Hedef verilirse el IK ile eşyanın gerçek yerine uzanır (birinci şahısta el, bakılan eşyanın üstünde görünür).
    /// </summary>
    /// <returns>Elin eşyaya ulaştığı an (sn).</returns>
    public float PlayPickup(Vector3? target = null)
    {
        if (upperBodyLayer < 0)
            return 0f;
        putDownEndTime = -1f;
        animator.CrossFadeInFixedTime(PickupState, crossFade, upperBodyLayer, 0f);
        StartReach(target, pickupReachTime);
        return pickupReachTime;
    }

    /// <summary>
    /// Eşyayı geri bırakma: alma klibi tersten oynar, el IK ile eşyanın konacağı yere uzanır.
    /// </summary>
    /// <returns>Elin bırakma noktasına ulaştığı an (sn); eşya bu anda elden çıkmalı.</returns>
    public float PlayPutDown(Vector3 target)
    {
        if (upperBodyLayer < 0)
            return 0f;
        // State hızı negatif: klibin sonundan (el aşağıda, eşya tutuluyor) başlar.
        animator.CrossFade(PutDownState, crossFade, upperBodyLayer, 1f);
        putDownEndTime = Time.time + putDownDuration;
        StartReach(target, putDownReachTime);
        return putDownReachTime;
    }

    private void StartReach(Vector3? target, float duration)
    {
        reachPoint = target;
        reachStartTime = Time.time;
        reachDuration = duration;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != upperBodyLayer)
            return;
        pendingLean = 0f;
        ApplyHeadLook();
        ApplyReach();
        ApplyTouch();
        ApplyCarry();
        ApplyLean();
    }

    /// <summary>
    /// Ürünü tek ya da iki elle tutma: eller eşyanın tutuş noktalarına (<see cref="PickupItem"/> grip'leri) uzanır ve
    /// eşya nereye giderse oraya eşlik eder (IK, kapıdaki gibi). Sadece sağ verilirse tek el.
    /// </summary>
    /// <returns>Ellerin tutuş noktalarına ulaşma süresi (sn); eşya bundan sonra kameranın önüne gelmeli.</returns>
    public float BeginCarry(Transform right, Transform left, System.Action syncItem = null)
    {
        carrySync = syncItem;
        carryRight = right;
        carryLeft = left;
        carryStart = Time.time;
        carryReach = carryReachTime;
        carryEndTime = -1f;
        putDownEndTime = -1f;
        return carryReach;
    }

    /// <summary>Eşya bırakılınca/satın alınınca: <paramref name="delay"/> sn sonra eller <see cref="carryReleaseTime"/> sürede serbest kalır.</summary>
    public void EndCarry(float delay)
    {
        if (carryRight == null && carryLeft == null)
            return;
        carryEndTime = Time.time + delay;
    }

    private void ApplyCarry()
    {
        if (carryRight == null && carryLeft == null)
        {
            carryLean = 0f;
            return;
        }

        carrySync?.Invoke();
        float weight = Mathf.SmoothStep(0f, 1f, (Time.time - carryStart) / carryReach);
        if (carryEndTime >= 0f && Time.time >= carryEndTime)
        {
            float release = (Time.time - carryEndTime) / carryReleaseTime;
            if (release >= 1f)
            {
                carryRight = carryLeft = null;
                carryEndTime = -1f;
                return;
            }
            weight *= 1f - Mathf.SmoothStep(0f, 1f, release);
        }

        // Kamera gövdenin 0.28 önünde: kollar yetişmezse gövde öne eğilir (tutuş noktalarına en uzak el belirler).
        Transform shoulderR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (shoulderR != null && carryMaxLean > 0f)
        {
            float lean = 0f;
            float armLength = ArmLength();
            if (carryRight != null)
                lean = Mathf.Max(lean, LeanAngle(carryRight.position, shoulderR, armLength, carryMaxLean));
            Transform shoulderL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (carryLeft != null && shoulderL != null)
                lean = Mathf.Max(lean, LeanAngle(carryLeft.position, shoulderL, armLength, carryMaxLean));
            // Eğilme açısı 2.5° basamaklarla hesaplanıyor; yürürken omuz/eşya mesafesi eşikte gidip gelince gövde
            // sıçrıyor ve eller titriyordu: yumuşatılır.
            carryLean = Mathf.Lerp(carryLean, lean, 1f - Mathf.Exp(-6f * Time.deltaTime));
            pendingLean = Mathf.Max(pendingLean, carryLean * weight);
        }

        ApplyHandGrip(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, carryRight, RightHandHold, weight, 1f);
        ApplyHandGrip(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, carryLeft, LeftHandHold, weight, -1f);
    }

    // Avuç tutma noktası (eksenleri CreateHandHold'da) eşyanın tutuş noktasıyla çakışacak şekilde el kemiği taşınır ve
    // döndürülür; dirsek yana/aşağı bakar, kol ters bükülmesin.
    private void ApplyHandGrip(AvatarIKGoal goal, AvatarIKHint hint, Transform grip, Transform hold, float weight, float side)
    {
        if (grip == null || hold == null || weight <= 0f)
            return;

        bool right = goal == AvatarIKGoal.RightHand;
        Transform hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        Transform shoulder = animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        // Avuç noktasının "yukarısı" (CreateHandHold) elin sırtına bakar: eşyanın yüzeyini ve elin kapanacağı yönü
        // vermeyi bozmadan (kumanda ofsetleri buna göre ayarlı), avuç yüzeyi tutuş noktasına dönsün diye başparmak
        // ekseni etrafında 180° çevrilir.
        Quaternion toGoal = grip.rotation * Quaternion.Inverse(hold.rotation * Quaternion.AngleAxis(180f, Vector3.forward));

        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKPosition(goal, grip.position + toGoal * (hand.position - hold.position));
        animator.SetIKRotationWeight(goal, weight);
        // Bu avatarda IK dönüş hedefi el kemiğinin x ekseni (kol ekseni) etrafında handIkTwist kadar kayıyor (ölçüldü,
        // iki elde de aynı, ürüne bağlı değil): istenen dönüş bu kadar ters çevrilerek verilir, yoksa bilekler kırık durur.
        animator.SetIKRotation(goal, toGoal * hand.rotation * Quaternion.AngleAxis(-handIkTwist, Vector3.right));

        Transform basis = controller != null ? controller.transform : animator.transform;
        Vector3 hintPosition = shoulder.position + basis.right * side * 0.4f - basis.up * 0.3f - basis.forward * 0.1f;
        animator.SetIKHintPositionWeight(hint, weight * carryElbowWeight);
        animator.SetIKHintPosition(hint, hintPosition);
    }

    private float ArmLength()
    {
        Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (shoulder == null || elbow == null || hand == null)
            return 0.65f;
        return Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);
    }

    // Omuzun, belden (Spine) öne eğilince varacağı yer.
    private Vector3 LeanedPosition(Vector3 shoulderPosition, float angle)
    {
        Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        if (spine == null || angle <= 0f)
            return shoulderPosition;
        return spine.position + Quaternion.AngleAxis(angle, animator.transform.right) * (shoulderPosition - spine.position);
    }

    // Kolun yetişmediği hedefe kadar gereken en küçük öne eğilme açısı (derece), en fazla maxLean.
    private float LeanAngle(Vector3 target, Transform shoulder, float armLength, float maxLean)
    {
        for (float angle = 0f; angle < maxLean; angle += 2.5f)
        {
            if (Vector3.Distance(LeanedPosition(shoulder.position, angle), target) <= armLength * 0.93f)
                return angle;
        }
        return maxLean;
    }

    private void ApplyLean()
    {
        Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        if (spine == null || pendingLean <= 0.01f)
            return;
        Quaternion lean = Quaternion.AngleAxis(pendingLean, animator.transform.right);
        animator.SetBoneLocalRotation(HumanBodyBones.Spine, Quaternion.Inverse(spine.parent.rotation) * lean * spine.rotation);
    }

    // Uzak bir hedef: kafanın klipteki küçük kaymaları bakış yönünü değiştirmez, sadece eğilme sönümlenir.
    private void ApplyHeadLook()
    {
        float weight = headLookWeight * headLookBlend;
        if (weight <= 0f || playerCamera == null)
            return;
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        // Dönüşün bir kısmını gövdenin üstü alır, boyun tek başına bükülmesin.
        animator.SetLookAtWeight(weight, 0.2f, 1f, 0f, headLookClamp);
        // Üçüncü şahısta (final) kamera karakterin gözü değil, karaktere bakıyor: kafa onu izlerse öne eğiliyordu.
        // O zaman gövdenin baktığı yöne, ufka bakar.
        Vector3 direction = thirdPerson ? animator.transform.forward : playerCamera.transform.forward;
        animator.SetLookAtPosition(head.position + direction * 10f);
    }

    private void ApplyReach()
    {
        if (reachPoint == null || RightHandHold == null)
            return;

        // Uzanırken 0→1, eşyayı tutunca/bırakınca kısa süre 1, sonra animasyona geri bırakılır.
        float t = Time.time - reachStartTime;
        float weight = t < reachDuration
            ? Mathf.SmoothStep(0f, 1f, t / reachDuration)
            : 1f - Mathf.SmoothStep(0f, 1f, (t - reachDuration) / pickupReleaseTime);
        if (weight <= 0f)
        {
            if (t > reachDuration)
                reachPoint = null;
            return;
        }

        // İsteğe bağlı: kol yetişmiyorsa gövde öne eğilerek eli eşyaya götürür (varsayılan kapalı, bkz. pickupMaxLean).
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (shoulder != null && pickupMaxLean > 0f)
            pendingLean = Mathf.Max(pendingLean, LeanAngle(reachPoint.Value, shoulder, ArmLength(), pickupMaxLean) * weight);

        // IK el kemiğini taşır; avuç (tutma noktası) hedefe gelsin diye aradaki fark düşülür.
        Vector3 goal = reachPoint.Value + (hand.position - RightHandHold.position);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
        animator.SetIKPosition(AvatarIKGoal.RightHand, goal);
    }

    /// <summary>Ayna karşısında poz (döngülü). <see cref="StopPose"/> ile biter.</summary>
    public void PlayPose()
    {
        overridden = true;
        IsPosing = true;
        animator.CrossFadeInFixedTime(PoseState, crossFade * 2f);
    }

    /// <summary>4. gün sabahı aynada yenilgi (tek sefer oynar, son karede kalır). <see cref="StopPose"/> ile biter.</summary>
    public void PlayDefeat()
    {
        overridden = true;
        IsPosing = true;
        animator.CrossFadeInFixedTime(DefeatState, crossFade * 2f);
    }

    public void StopPose()
    {
        if (IsPosing)
            PlayLocomotion();
    }

    private float ClipLength => sitToStandClip != null ? sitToStandClip.length : 1.2f;
}
