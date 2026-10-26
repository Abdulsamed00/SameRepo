using UnityEngine;

/// <summary>
/// Alınıp elde tutulabilen eşya. F ile alınır, <see cref="PlayerInteractor"/> bırakma tuşuyla geri konur.
/// Elde tutulurken fizik kapanır; eşya el noktasına yumuşakça takip eder (kafa sallanması doğal yansır).
/// Varsayılan olarak bırakılan eşya sahnedeki ilk yerine döner.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PickupItem : Interactable
{
    [Header("Bırakma")]
    [Tooltip("Açıksa bırakılan eşya ilk bulunduğu konuma döner; kapalıysa oyuncunun önüne düşer.")]
    [SerializeField] private bool returnToStartOnDrop = true;
    [Tooltip("Açıksa eşya yerindeyken de fizik almaz (standda/askıda duran ürünler düşmesin).")]
    [SerializeField] private bool staysKinematic;

    [Header("Elde Tutma")]
    [Tooltip("El noktasına göre konum farkı.")]
    [SerializeField] private Vector3 holdPositionOffset;
    [Tooltip("El noktasına göre dönüş farkı (derece).")]
    [SerializeField] private Vector3 holdEulerOffset;
    [SerializeField] private float followSharpness = 18f;

    private Rigidbody body;
    private Collider[] colliders;
    private Transform holdPoint;
    private float radius;
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;

    public bool IsHeld => holdPoint != null;

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

    public override bool CanInteract(PlayerInteractor interactor) => !IsHeld && !interactor.IsHolding;

    public override void Interact(PlayerInteractor interactor) => interactor.Hold(this);

    public void OnPickedUp(Transform hand)
    {
        holdPoint = hand;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;
        SetCollidersEnabled(false);
    }

    /// <param name="fallbackPosition">İlk konuma dönülmüyorsa eşyanın konacağı dünya konumu.</param>
    public void OnDropped(Vector3 fallbackPosition)
    {
        holdPoint = null;
        if (returnToStartOnDrop)
            transform.SetLocalPositionAndRotation(startLocalPosition, startLocalRotation);
        else
            transform.position = fallbackPosition;
        SetCollidersEnabled(true);
        body.isKinematic = staysKinematic;
    }

    private void LateUpdate()
    {
        if (!IsHeld)
            return;

        Vector3 targetPosition = holdPoint.TransformPoint(holdPositionOffset);
        Quaternion targetRotation = holdPoint.rotation * Quaternion.Euler(holdEulerOffset);
        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, targetPosition, t),
            Quaternion.Slerp(transform.rotation, targetRotation, t));
    }

    private void SetCollidersEnabled(bool value)
    {
        foreach (Collider c in colliders)
            c.enabled = value;
    }
}
