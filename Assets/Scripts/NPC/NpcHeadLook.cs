using UnityEngine;

/// <summary>
/// Sabit NPC'nin (kasiyer) kafası oyuncu yaklaşınca ona döner ve yürüdükçe onu izler (09.11, kullanıcı isteği).
/// Animasyon (Playables/Animator) bittikten sonra LateUpdate'te boyun ve kafa kemiği dünya uzayında oyuncunun gözüne
/// çevrilir; gövde yerinde kalır. Dönüş gövdenin önüne göre yatayda <see cref="maxYaw"/>, dikeyde <see cref="maxPitch"/> ile sınırlı;
/// oyuncu menzil dışına ya da gövdenin çok arkasına çıkınca kafa yumuşakça normal pozuna döner.
/// </summary>
[DefaultExecutionOrder(200)]
public class NpcHeadLook : MonoBehaviour
{
    [SerializeField] private float range = 6f;
    [Tooltip("Menzilin içinde bu mesafeden (m) yakında tam ağırlık; dışında yumuşakça söner.")]
    [SerializeField] private float fullRange = 4f;
    [SerializeField] private float maxYaw = 75f;
    [SerializeField] private float maxPitch = 30f;
    [Tooltip("Dönüşün kafaya düşen payı; kalanı boyna gider.")]
    [Range(0f, 1f)] [SerializeField] private float headShare = 0.65f;
    [SerializeField] private float follow = 6f;

    private Animator animator;
    private Transform head;
    private Transform neck;
    private Transform target;
    private float weight;
    private Vector3 smoothDirection;
    private bool hasDirection;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        }
    }

    private void LateUpdate()
    {
        if (head == null)
            return;

        if (target == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                target = cam.transform;
        }
        if (target == null)
            return;

        Vector3 eye = head.position;
        Vector3 to = target.position - eye;
        float distance = to.magnitude;
        Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
        float behind = flat.sqrMagnitude > 0.0001f ? Vector3.Angle(transform.forward, flat) : 0f;

        // Menzilde ve gövdenin makul önünde (en çok maxYaw + 35°) ise bakar; yoksa söner.
        bool wants = distance < range && behind < maxYaw + 35f;
        float goal = !wants ? 0f : Mathf.Clamp01(1f - (distance - fullRange) / Mathf.Max(0.01f, range - fullRange));
        goal *= 1f - Mathf.Clamp01((behind - maxYaw) / 35f);
        weight = Mathf.MoveTowards(weight, goal, Time.deltaTime * 2.5f);
        if (weight <= 0.001f)
        {
            hasDirection = false;
            Restore(head, headBase, ref headWritten);
            if (neck != null)
                Restore(neck, neckBase, ref neckWritten);
            return;
        }

        // Bakış yönü gövde uzayında yaw/pitch sınırlanır, yumuşakça izlenir.
        Vector3 local = transform.InverseTransformDirection(to.normalized);
        float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
        float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -maxPitch, maxPitch);
        Vector3 desired = transform.TransformDirection(Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward);
        if (!hasDirection)
        {
            smoothDirection = transform.forward;
            hasDirection = true;
        }
        smoothDirection = Vector3.Slerp(smoothDirection, desired, 1f - Mathf.Exp(-follow * Time.deltaTime)).normalized;

        // Animasyondaki kafa yönü gövdenin ileri yönü sayılır; farkı kadar (ağırlıkla) döndürülür.
        Quaternion full = Quaternion.FromToRotation(transform.forward, smoothDirection);
        Quaternion total = Quaternion.Slerp(Quaternion.identity, full, weight);
        if (neck != null)
        {
            Rotate(neck, Quaternion.Slerp(Quaternion.identity, total, 1f - headShare), ref neckBase, ref neckWritten);
            Rotate(head, Quaternion.Slerp(Quaternion.identity, total, headShare), ref headBase, ref headWritten);
        }
        else
            Rotate(head, total, ref headBase, ref headWritten);
    }

    // Animasyon kemiği her karede yazmayabilir (durağan klip): yazdığımız değer kalırsa üstüne yeniden dönüş eklenip
    // birikirdi. Kemiğin yerel dönüşü bizim son yazdığımızdan farklıysa animasyon yazmıştır ve yeni taban odur.
    private static void Rotate(Transform bone, Quaternion worldDelta, ref Quaternion baseLocal, ref Quaternion written)
    {
        if (Quaternion.Angle(bone.localRotation, written) > 0.01f)
            baseLocal = bone.localRotation;
        else
            bone.localRotation = baseLocal;
        bone.rotation = worldDelta * bone.rotation;
        written = bone.localRotation;
    }

    // Ağırlık sıfırlanınca bizim yazdığımız dönüş duruyorsa (animasyon üzerine yazmadıysa) tabana geri alınır.
    private static void Restore(Transform bone, Quaternion baseLocal, ref Quaternion written)
    {
        if (written.w == 0f && written.x == 0f && written.y == 0f && written.z == 0f)
            return;
        if (Quaternion.Angle(bone.localRotation, written) <= 0.01f)
            bone.localRotation = baseLocal;
        written = new Quaternion(0f, 0f, 0f, 0f);
    }

    private Quaternion headBase = Quaternion.identity;
    private Quaternion headWritten = new Quaternion(0f, 0f, 0f, 0f);
    private Quaternion neckBase = Quaternion.identity;
    private Quaternion neckWritten = new Quaternion(0f, 0f, 0f, 0f);
}
