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

    [Header("Tek el / iki el (IK)")]
    [Tooltip("Eşyanın elle tutulan noktası. Tek verilirse sağ el, ikisi de verilirse iki el tutar (hangisinin sağ olduğu tutuş pozundaki yerine göre seçilir). Boşsa eski davranış: eşya kameradaki tutma noktasında elsiz durur. Dönüş: ileri = başparmak tarafı, yukarı = avuçtan eşyaya doğru.")]
    [SerializeField] private Transform gripA;
    [SerializeField] private Transform gripB;

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

        Vector3 targetPosition = holdPoint.TransformPoint(holdPositionOffset);
        Quaternion targetRotation = holdPoint.rotation * Quaternion.Euler(holdEulerOffset);

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

    private int approachFrame = -1;

    private void SetCollidersEnabled(bool value)
    {
        foreach (Collider c in colliders)
            c.enabled = value;
    }
}
