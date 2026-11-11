using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Fragmandaki tek bir çekim: sanal kamerayı (CinemachineCamera) çekim süresi boyunca A pozundan B pozuna taşır.
/// Kesmeleri Timeline'daki Cinemachine Track yapar; bu bileşen sadece kameranın o çekimdeki hareketini, merceğini
/// ve netlik (DoF) ayarını tanımlar. Zaman, <see cref="TrailerController"/>'ın verdiği Timeline zamanıdır.
/// Değerler dünya koordinatında: fragman sahnesi oyun sahnesinin üstüne (additive) açılır.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class TrailerShot : MonoBehaviour
{
    public enum Ease { Linear, Smooth, EaseIn, EaseOut }

    [Header("Zaman (Timeline saniyesi)")]
    public float start;
    public float duration = 3f;

    [Header("Kamera yolu")]
    public Vector3 positionA;
    public Vector3 positionB;
    [Tooltip("Açıksa yol A'dan B'ye düz değil, bu noktadan geçen yay (ray üstünde kavis).")]
    public bool useArc;
    public Vector3 arcPoint;
    [Tooltip("Kameranın baktığı nokta (çekimin başında ve sonunda).")]
    public Vector3 lookA;
    public Vector3 lookB;
    [Tooltip("Açıksa bakış noktası sabit değil, karakterin kafa kemiği + headOffset (dünya): karakter kıpırdasa da kadraj ve netlik yüzde kalır.")]
    public bool lookAtPlayerHead;
    public Vector3 headOffset;
    public float rollA;
    public float rollB;
    public Ease ease = Ease.Smooth;

    [Header("Mercek")]
    public float fovA = 40f;
    public float fovB = 40f;

    [Header("Netlik (Depth of Field)")]
    [Tooltip("0 ise netlik bakılan noktaya oturur; değilse bu mesafe (m).")]
    public float focusDistance;
    [Tooltip("Küçük değer = sığ netlik (arka plan daha bulanık).")]
    public float aperture = 4f;
    public float focalLength = 60f;

    private CinemachineCamera cam;
    private Transform playerHead;

    public float End => start + duration;

    public bool Contains(float time) => time >= start && time < End;

    /// <summary>Çekimin o andaki netlik mesafesi (m).</summary>
    public float FocusAt(float time)
    {
        if (focusDistance > 0f)
            return focusDistance;
        float k = Progress(time);
        return Vector3.Distance(Position(k), LookPoint(k));
    }

    private Vector3 LookPoint(float k)
    {
        if (lookAtPlayerHead)
        {
            if (playerHead == null)
            {
                PlayerBody body = FindFirstObjectByType<PlayerBody>();
                Animator animator = body != null ? body.GetComponent<Animator>() : null;
                playerHead = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            }
            if (playerHead != null)
                return playerHead.position + headOffset;
        }
        return Vector3.Lerp(lookA, lookB, k);
    }

    public void Apply(float time)
    {
        if (cam == null)
            cam = GetComponent<CinemachineCamera>();

        float k = Progress(time);
        Vector3 position = Position(k);
        Vector3 look = LookPoint(k);
        Vector3 forward = look - position;
        if (forward.sqrMagnitude > 1e-6f)
        {
            Quaternion rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            transform.SetPositionAndRotation(position, rotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(rollA, rollB, k)));
        }
        else
        {
            transform.position = position;
        }

        LensSettings lens = cam.Lens;
        lens.FieldOfView = Mathf.Lerp(fovA, fovB, k);
        cam.Lens = lens;
    }

    private Vector3 Position(float k)
    {
        if (!useArc)
            return Vector3.Lerp(positionA, positionB, k);
        // A, yay noktası ve B'den geçen ikinci derece eğri (yay noktası yolun ortasında).
        Vector3 control = 2f * arcPoint - 0.5f * (positionA + positionB);
        float u = 1f - k;
        return u * u * positionA + 2f * u * k * control + k * k * positionB;
    }

    private float Progress(float time)
    {
        float t = duration > 0f ? Mathf.Clamp01((time - start) / duration) : 0f;
        switch (ease)
        {
            case Ease.Smooth: return t * t * (3f - 2f * t);
            case Ease.EaseIn: return t * t;
            case Ease.EaseOut: return 1f - (1f - t) * (1f - t);
            default: return t;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 previous = Position(0f);
        for (int i = 1; i <= 16; i++)
        {
            Vector3 next = Position(i / 16f);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(positionA, lookA);
        Gizmos.DrawLine(positionB, lookB);
    }
}
