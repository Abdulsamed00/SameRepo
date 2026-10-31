using UnityEngine;

/// <summary>
/// Birinci şahıs oyuncunun görünür gövdesi (AnaKarakter). Animator'daki yürüme/durma geçişini hıza bağlar,
/// oturma/kalkma animasyonlarını <see cref="PlayerSitController"/> için oynatır.
/// Alma animasyonu sadece üst gövde katmanında (UpperBody) oynar, bacaklar yürümeye devam eder.
/// Poz animasyonu (ayna) tüm gövdeyi alır; oyuncuyu dondurmak çağıranın işidir.
/// Yürürken gövde basılan yöne döner (D ile sağa, A ile sola); kamera dönmez. Dururken basılırsa önce yerinde
/// dönme klibi oynar (gövde klibin kök dönüşüyle döner); oyuncu bu sırada yavaşlamaz. Geri giderken gövde
/// arkasını dönmez, yürüme klibi tersten oynar. Elde tutulan eşyalar sağ avuçtaki RightHandHold'a oturur.
/// Saç ve gözlük "PlayerHead" katmanına alınır ve sadece oyuncunun kendi kamerasından gizlenir
/// (kamera kafanın içinde kalıyor). Ayna vb. başka kameralarda kafa tam görünür.
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
    [Tooltip("Oyuncu kamerasından gizlenecek renderer adları (kafaya ait ayrı parçalar).")]
    [SerializeField] private string[] hiddenRendererNames = { "TopuzSac", "BlondeHair", "Glasses" };

    [Header("Yöne Dönme")]
    [Tooltip("Gövdenin hareket yönüne dönüş hızı (derece/sn).")]
    [SerializeField] private float turnSpeed = 540f;
    [Tooltip("Gövdenin bakış yönüne göre en fazla dönebileceği açı.")]
    [SerializeField] private float maxTurnAngle = 90f;
    [Tooltip("Dururken kafanın gövdeye göre en fazla dönebileceği açı; bakış bunu aşınca gövde döner.")]
    [SerializeField] private float headTurnLimit = 60f;

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

    [Header("Bırakma (alma tersten)")]
    [Tooltip("Tersten oynayan alma klibinde elin eşyayı yerine koyduğu an (sn).")]
    [SerializeField] private float putDownReachTime = 1.0f;
    [Tooltip("Tersten oynayan alma klibinin süresi (sn): klip uzunluğu / state hızı.")]
    [SerializeField] private float putDownDuration = 1.75f;

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
    private float headLookBlend;
    private float lastRootYaw;
    private bool turning;
    private float turnSign;
    private float turnScale;
    private float turnStartTime;

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

    private void Awake()
    {
        animator = GetComponent<Animator>();
        upperBodyLayer = animator.GetLayerIndex(UpperBodyLayerName);
        baseLocalPosition = transform.localPosition;
        RightHandHold = CreateRightHandHold();
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

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (System.Array.IndexOf(hiddenRendererNames, r.name) >= 0)
                r.gameObject.layer = layer;
        }

        if (playerCamera != null)
            playerCamera.cullingMask &= ~(1 << layer);
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
    private Transform CreateRightHandHold()
    {
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null)
            return null;

        Transform hold = new GameObject("RightHandHold").transform;
        hold.SetParent(hand, false);

        Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        if (middle == null || index == null || little == null)
            return hold;

        Vector3 fingers = middle.position - hand.position;
        Vector3 across = (index.position - little.position).normalized;
        Vector3 palm = Vector3.Cross(fingers.normalized, across).normalized;
        hold.SetPositionAndRotation(hand.position + fingers * 0.6f + palm * palmDepth,
            Quaternion.LookRotation(across, -palm));
        return hold;
    }

    /// <returns>Oturma süresi (sn).</returns>
    public float PlaySitDown()
    {
        overridden = true;
        // Kalkma klibi tersten (state hızı -1): sondan, yani ayakta pozdan başlar.
        animator.CrossFade(StandToSitState, crossFade, 0, 1f);
        return ClipLength;
    }

    public void PlaySittingIdle()
    {
        overridden = true;
        animator.CrossFadeInFixedTime(SittingIdleState, crossFade);
    }

    /// <returns>Kalkma süresi (sn).</returns>
    public float PlayStandUp()
    {
        overridden = true;
        animator.CrossFadeInFixedTime(SitToStandState, crossFade);
        return ClipLength;
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
        ApplyHeadLook();
        ApplyReach();
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
        animator.SetLookAtPosition(head.position + playerCamera.transform.forward * 10f);
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

        // IK el kemiğini taşır; avuç (tutma noktası) hedefe gelsin diye aradaki fark düşülür.
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
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

    public void StopPose()
    {
        if (IsPosing)
            PlayLocomotion();
    }

    private float ClipLength => sitToStandClip != null ? sitToStandClip.length : 1.2f;
}
