using UnityEngine;

/// <summary>
/// Alınıp elde tutulabilen eşya. F ile alınır, <see cref="PlayerInteractor"/> bırakma tuşuyla geri konur.
/// Elde tutulurken fizik kapanır; eşya el noktasına yumuşakça takip eder (kafa sallanması doğal yansır).
/// Varsayılan olarak bırakılan eşya sahnedeki ilk yerine döner.
/// Ev alanı verilirse oyuncu eşyayla alanın dışına çıkınca eşya kendiliğinden yerine döner (ör. kumanda odadan çıkmaz).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PickupItem : Interactable
{
    // Elle alınan eşya: dibine gelince (ayakta ~1 m, otururken sehpadaki kumanda 1.35 m).
    protected override float DefaultMaxDistance => 1.5f;

    [Header("Bırakma")]
    [Tooltip("Açıksa bırakılan eşya ilk bulunduğu konuma döner; kapalıysa oyuncunun önüne düşer.")]
    [SerializeField] private bool returnToStartOnDrop = true;
    [Tooltip("Açıksa eşya yerindeyken de fizik almaz (standda/askıda duran ürünler düşmesin).")]
    [SerializeField] private bool staysKinematic;
    [Tooltip("Eşyanın taşınabileceği alan (trigger collider). Oyuncu dışına çıkınca eşya yerine döner. Boşsa sınır yok.")]
    [SerializeField] private Collider homeArea;

    [Header("Elde Tutma")]
    [Tooltip("Açıksa eşya karakterin sağ avucuna oturur; kapalıysa kameradaki tutma noktasını takip eder.")]
    [SerializeField] private bool holdInHand = true;
    [Tooltip("El noktasına göre konum farkı.")]
    [SerializeField] private Vector3 holdPositionOffset;
    [Tooltip("El noktasına göre dönüş farkı (derece).")]
    [SerializeField] private Vector3 holdEulerOffset;
    [SerializeField] private float followSharpness = 18f;

    /// <summary>Eşyanın tutma noktasına göre ek aşağı kayması (m).</summary>
    protected virtual float HoldLowering => 0f;

    [Header("Yürürken sallanma")]
    [Tooltip("Eşya kameraya bağlı olduğu için kamerayla birlikte salındığında ekranda sabit duruyordu. Kameranın sallanmasına ters yönde ek salınır (çarpan).")]
    [SerializeField] private float walkSwayPosition = 0.3f;
    [Tooltip("Yatay sallanmayla gelen yan yatma (derece / m).")]
    [SerializeField] private float walkSwayRoll = 25f;
    [Tooltip("Dikey sallanmayla gelen öne arkaya eğilme (derece / m).")]
    [SerializeField] private float walkSwayPitch = 18f;

    [Header("Tek el / iki el (IK)")]
    [Tooltip("Eşyanın elle tutulan noktası. Tek verilirse sağ el, ikisi de verilirse iki el tutar (hangisinin sağ olduğu tutuş pozundaki yerine göre seçilir). Boşsa eski davranış: eşya kameradaki tutma noktasında elsiz durur. Dönüş: ileri = başparmak tarafı, yukarı = avuçtan eşyaya doğru.")]
    [SerializeField] private Transform gripA;
    [SerializeField] private Transform gripB;

    [Header("Engel payı (10.11)")]
    [Tooltip("Eldeki eşya duvara/rafa girecekse en fazla bu kadar (m) gövdeye doğru çekilir; yine sığmazsa oyuncu geri itilir.")]
    [SerializeField] private float maxClearancePull = 0.3f;

    [Header("Ses")]
    [Tooltip("Ele alınınca ve bırakılınca çalar (ör. mağazada kıyafet hışırtısı). Boşsa ses yok.")]
    [SerializeField] private AudioClip handleSound;

    private Rigidbody body;
    private Collider[] colliders;
    private PlayerInteractor holder;
    private Transform holdPoint;
    private float attachTime;
    private bool attached;
    private float releaseTime = -1f;
    private Vector3 pendingDropPosition;
    private bool settling;
    private Vector3 settlePosition;
    private Quaternion settleRotation;
    private float radius;
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;

    /// <summary>Elde ya da bırakılma animasyonunda (henüz elden çıkmadı).</summary>
    public bool IsHeld => holdPoint != null;

    public virtual bool HoldInHand => holdInHand;

    /// <summary>Eşyanın IK ile tutulan noktaları var (<see cref="PlayerBody.BeginCarry"/>).</summary>
    public bool HasGrips => gripA != null;

    /// <summary>
    /// Tutuş noktalarını eller arasında dağıtır. İki nokta varsa tutma noktasındaki (<paramref name="holdPoint"/>) pozda
    /// sağda kalan sağ el olur; nokta tek ise sağ el, sol boş kalır.
    /// </summary>
    public void GetGrips(Transform holdPoint, out Transform right, out Transform left)
    {
        right = gripA;
        left = gripB;
        if (gripA == null || gripB == null || holdPoint == null)
            return;

        Quaternion held = holdPoint.rotation * Quaternion.Euler(holdEulerOffset);
        Quaternion shelf = transform.rotation;
        Vector3 a = held * Quaternion.Inverse(shelf) * (gripA.position - transform.position);
        Vector3 b = held * Quaternion.Inverse(shelf) * (gripB.position - transform.position);
        if (Vector3.Dot(a - b, holdPoint.right) < 0f)
        {
            right = gripB;
            left = gripA;
        }
    }

    /// <summary>Açıksa alırken önce sağ el uzanır, eşya gelince sol el de tutar (iki el tutuşlu ürünlerde).</summary>
    public virtual bool ReachWithOneHand => false;

    /// <summary>Yerinin dünya konumu (ilk konum).</summary>
    public Vector3 HomePosition => StartPosition;

    /// <summary>Şu an bırakılabilir mi (<paramref name="eye"/>: oyuncunun göz konumu). Varsayılan: her yerde.</summary>
    public virtual bool CanDropAt(Vector3 eye) => true;

    /// <summary>Bırakılırken duvar/zemin payı için yaklaşık yarıçap.</summary>
    public float Radius => radius;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
        if (staysKinematic)
        {
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        else
        {
            // İnce eşyalar zemini yarıp geçmesin.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        colliders = GetComponentsInChildren<Collider>();

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (Collider c in colliders)
            bounds.Encapsulate(c.bounds);
        radius = Mathf.Clamp(bounds.extents.magnitude, 0.03f, 0.3f);

        // Eşyanın kendi uzayındaki kutusu (engel payı için). BoxCollider'lardan; yoksa renderer'lardan.
        hasLocalBox = false;
        foreach (Collider c in colliders)
        {
            if (c is BoxCollider box)
                EncapsulateLocal(box.transform, box.center, box.size);
        }
        if (!hasLocalBox)
        {
            foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh != null)
                    EncapsulateLocal(mf.transform, mf.sharedMesh.bounds.center, mf.sharedMesh.bounds.size);
            }
        }
    }

    private Bounds localBox;
    private bool hasLocalBox;
    private float clearancePull;

    private void EncapsulateLocal(Transform t, Vector3 center, Vector3 size)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = center + Vector3.Scale(size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 local = transform.InverseTransformPoint(t.TransformPoint(corner));
            if (!hasLocalBox)
            {
                localBox = new Bounds(local, Vector3.zero);
                hasLocalBox = true;
            }
            else
                localBox.Encapsulate(local);
        }
    }

    public override bool CanInteract(PlayerInteractor interactor) => !IsHeld && !settling && !interactor.IsHolding;

    public override void Interact(PlayerInteractor interactor) => interactor.Hold(this);

    /// <summary>Bırakılınca eşyanın konacağı dünya konumu (ilk yeri ya da verilen yedek konum).</summary>
    public Vector3 DropPosition(Vector3 fallbackPosition) =>
        returnToStartOnDrop ? StartPosition : fallbackPosition;

    private Vector3 StartPosition =>
        transform.parent != null ? transform.parent.TransformPoint(startLocalPosition) : startLocalPosition;

    /// <param name="attachDelay">Eşya bu süre yerinde bekler, sonra ele uçar (alma animasyonunda elin eşyaya uzandığı an).</param>
    public void OnPickedUp(PlayerInteractor by, Transform hand, float attachDelay = 0f)
    {
        holder = by;
        holdPoint = hand;
        attachTime = Time.time + attachDelay;
        attached = false;
        releaseTime = -1f;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;
        SetCollidersEnabled(false);
        GameAudio.PlayAt(handleSound, transform.position);
    }

    /// <param name="fallbackPosition">İlk konuma dönülmüyorsa eşyanın konacağı dünya konumu.</param>
    /// <param name="releaseDelay">Eşya bu süre elde kalır (bırakma animasyonunda elin yere ulaştığı an), sonra konur.</param>
    public void OnDropped(Vector3 fallbackPosition, float releaseDelay = 0f)
    {
        holder = null;
        pendingDropPosition = fallbackPosition;
        GameAudio.PlayAt(handleSound, transform.position);
        if (releaseDelay > 0f)
            releaseTime = Time.time + releaseDelay;
        else
            Release(false);
    }

    // Animasyonlu bırakmada el yere yetişmeyebilir (alçak sehpa); eşya elden yerine kayarak gider.
    private void Release(bool smooth)
    {
        holdPoint = null;
        releaseTime = -1f;
        if (returnToStartOnDrop)
        {
            Transform parent = transform.parent;
            settlePosition = StartPosition;
            settleRotation = parent != null ? parent.rotation * startLocalRotation : startLocalRotation;
        }
        else
        {
            settlePosition = pendingDropPosition;
            settleRotation = transform.rotation;
        }

        settling = smooth && (transform.position - settlePosition).sqrMagnitude > 0.0004f;
        if (!settling)
            FinishSettle();
    }

    private void FinishSettle()
    {
        settling = false;
        transform.SetPositionAndRotation(settlePosition, settleRotation);
        SetCollidersEnabled(true);
        body.isKinematic = staysKinematic;
    }

    private void Update()
    {
        if (holder != null && homeArea != null && !homeArea.bounds.Contains(holder.transform.position))
            holder.DropHeld(false);
    }

    private void LateUpdate()
    {
        if (releaseTime >= 0f && Time.time >= releaseTime)
        {
            Release(true);
            return;
        }

        if (settling)
        {
            float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, settlePosition, k),
                Quaternion.Slerp(transform.rotation, settleRotation, k));
            if ((transform.position - settlePosition).sqrMagnitude < 0.0001f)
                FinishSettle();
            return;
        }

        UpdateHeldPose();
    }

    /// <summary>
    /// Eşyayı o karedeki el/kamera noktasına taşır. Eller (IK) animasyon aşamasında, <see cref="LateUpdate"/>'ten ÖNCE
    /// eşyanın tutuş noktalarına uzanır: eşya hâlâ bir önceki karenin konumundaysa kamera hareket ederken eller eşyanın
    /// ~2 cm gerisinde kalıp titriyordu (05.11). <see cref="PlayerBody"/> IK'dan önce bunu çağırır.
    /// </summary>
    public void UpdateHeldPose()
    {
        if (!IsHeld || Time.time < attachTime || settling)
            return;

        HoldFrame(out Vector3 framePosition, out Quaternion frameRotation);
        Vector3 targetPosition = framePosition + frameRotation * (holdPositionOffset + Vector3.down * HoldLowering);
        Quaternion targetRotation = frameRotation * Quaternion.Euler(holdEulerOffset);
        ApplyWalkSway(frameRotation, ref targetPosition, ref targetRotation);
        ApplyClearance(ref targetPosition, targetRotation);

        // Ele ulaştıktan sonra yumuşatma olmadan takip eder, yoksa hızlı el hareketinde geride kalıyor.
        if (attached || (transform.position - targetPosition).sqrMagnitude < 0.0004f)
        {
            attached = true;
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            return;
        }

        // Aynı karede iki kez çağrılırsa (IK + LateUpdate) yaklaşma hızı ikiye katlanmasın.
        if (approachFrame == Time.frameCount)
            return;
        approachFrame = Time.frameCount;
        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, targetPosition, t),
            Quaternion.Slerp(transform.rotation, targetRotation, t));
    }

    // Tutma noktası kameranın çocuğuysa (mağaza ürünleri) eşya kameranın sadece yatay dönüşünü izler (08.11, kullanıcı
    // isteği): aşağı/yukarı bakınca eşya da kamerayla dönüp karakterin gövdesine giriyordu. Artık eşya göğüs önünde
    // sabit yükseklikte kalır, bakışla sadece kafa oynar. Ele bağlı tutuşta (kumanda) noktanın kendisi kullanılır.
    private void HoldFrame(out Vector3 position, out Quaternion rotation)
    {
        Transform camera = holdPoint.parent;
        if (camera == null || camera.GetComponent<Camera>() == null)
        {
            position = holdPoint.position;
            rotation = holdPoint.rotation;
            return;
        }

        Vector3 forward = camera.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f)
            forward = camera.up.y > 0f ? -camera.up : camera.up;
        forward.y = 0f;
        Quaternion level = Quaternion.LookRotation(forward.normalized, Vector3.up);
        rotation = level * holdPoint.localRotation;
        position = camera.position + level * holdPoint.localPosition;
    }

    private FirstPersonController walker;
    private int clearanceFrame = -1;

    // Eldeki eşya duvara/rafa/dolaba girmesin (10.11, kullanıcı: "elinde eşya varken objelerin içine geçiyor"): eşyanın
    // kutusu hedef konumda bir engele değiyorsa gövdeye doğru (yatayda) çekilir; eller IK ile birlikte gelir. En fazla
    // maxClearancePull çekilip yine sığmıyorsa oyuncu o yönden geri itilir, yani eşyayla engele daha fazla yaklaşılamaz.
    private void ApplyClearance(ref Vector3 position, Quaternion rotation)
    {
        if (holder == null || !hasLocalBox || holdPoint.parent == null || holdPoint.parent.GetComponent<Camera>() == null)
            return;
        Vector3 eye = holdPoint.parent.position; // kamera (göz)

        Vector3 away = position - eye;
        away.y = 0f;
        if (away.sqrMagnitude < 1e-6f)
            return;
        away.Normalize();

        bool newFrame = clearanceFrame != Time.frameCount;
        if (newFrame)
        {
            clearanceFrame = Time.frameCount;
            // Kutu gözün yatay hizasından (eşyanın yüksekliğinde) eşyanın hedefine süpürülür: ilk çarptığı engelin
            // önünde durmalı. Çakışma testi yetmiyordu: ince ürün (katlı pantolon 9 cm) duvarın öbür yanına geçince
            // hiçbir şeye değmiyor sayılıyordu. Başlangıçta zaten değen (yandaki raf) engeller sayılmaz.
            Vector3 start = new Vector3(eye.x, position.y, eye.z);
            float distance = Vector3.Dot(position - start, away);
            float needed = 0f;
            if (distance > 0.01f)
            {
                Vector3 half = localBox.extents * 0.95f;
                Vector3 center = start + rotation * localBox.center;
                int count = Physics.BoxCastNonAlloc(center, half, away, castBuffer, rotation, distance, ~0, QueryTriggerInteraction.Ignore);
                Transform player = holder.transform.root;
                float allowed = distance;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = castBuffer[i];
                    if (hit.collider == null || hit.distance <= 0f || hit.collider.transform.IsChildOf(player)
                        || hit.collider.transform.IsChildOf(transform))
                        continue;
                    allowed = Mathf.Min(allowed, hit.distance - 0.02f);
                }
                needed = Mathf.Max(0f, distance - allowed);
            }

            if (needed > maxClearancePull)
            {
                needed = maxClearancePull;
                PushHolderBack(away);
            }
            // Engele girerken hemen, açılınca yumuşakça geri gelir.
            clearancePull = needed > clearancePull ? needed : Mathf.MoveTowards(clearancePull, needed, Time.deltaTime * 0.8f);
        }

        position -= away * clearancePull;
    }

    private static readonly RaycastHit[] castBuffer = new RaycastHit[16];

    private void PushHolderBack(Vector3 away)
    {
        if (walker == null)
            walker = holder.GetComponentInParent<FirstPersonController>();
        if (walker == null || walker.Frozen)
            return;
        CharacterController cc = walker.GetComponent<CharacterController>();
        if (cc != null && cc.enabled)
            cc.Move(-away * (2.4f * Time.deltaTime));
    }

    // Yürürken eşya kamerayla aynı sallandığı için ekranda sabit görünüyordu: kameranın sallanmasına ters yönde
    // (daha geç/ağır bir nesne gibi) ek konum ve eğilme verilir. Kamera eksenlerinde uygulanır.
    private void ApplyWalkSway(Quaternion frame, ref Vector3 position, ref Quaternion rotation)
    {
        if (holder == null)
            return;
        if (walker == null)
            walker = holder.GetComponentInParent<FirstPersonController>();
        if (walker == null)
            return;

        Vector2 bob = walker.BobOffset;
        if (bob.sqrMagnitude < 1e-8f)
            return;

        Vector3 local = new Vector3(-bob.x * walkSwayPosition, -bob.y * walkSwayPosition, 0f);
        position += frame * local;
        Quaternion sway = Quaternion.AngleAxis(bob.x * walkSwayRoll, frame * Vector3.forward)
                          * Quaternion.AngleAxis(-bob.y * walkSwayPitch, frame * Vector3.right);
        rotation = sway * rotation;
    }

    private int approachFrame = -1;

    private void SetCollidersEnabled(bool value)
    {
        foreach (Collider c in colliders)
            c.enabled = value;
    }
}
