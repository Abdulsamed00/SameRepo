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
    public float rollA;
    public float rollB;
    public Ease ease = Ease.Smooth;

    [Header("Takip")]
    [Tooltip("Açıksa konum ve bakış noktaları oyuncunun köküne göre (kökün yatay dönüşüyle) verilir: kamera karakteri izler.")]
    public bool followPlayer;
    [Tooltip("Açıksa bakış, çekim başında kameranın önündeki en yakın sarışın NPC'nin kafasına kilitlenir; saçı parlar.")]
    public bool lookAtBlonde;

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
    private static Transform player;
    private Transform blondeHead;
    private bool blondeSearched;
    private Vector3 lastPosition, lastLook;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => player = null;

    public float End => start + duration;

    public bool Contains(float time) => time >= start && time < End;

    /// <summary>Çekimin o andaki netlik mesafesi (m).</summary>
    public float FocusAt(float time)
    {
        if (focusDistance > 0f)
            return focusDistance;
        return Vector3.Distance(lastPosition, lastLook);
    }

    public void Apply(float time)
    {
        if (cam == null)
            cam = GetComponent<CinemachineCamera>();

        float k = Progress(time);
        Vector3 position = Position(k);
        Vector3 look = Vector3.Lerp(lookA, lookB, k);
        if (followPlayer && Application.isPlaying)
        {
            if (player == null)
            {
                FirstPersonController fpc = FindFirstObjectByType<FirstPersonController>();
                player = fpc != null ? fpc.transform : null;
            }
            if (player != null)
            {
                Quaternion yaw = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
                position = player.position + yaw * position;
                look = player.position + yaw * look;
            }
        }
        if (lookAtBlonde && Application.isPlaying && time >= start)
        {
            if (!blondeSearched)
            {
                blondeSearched = true;
                blondeHead = FindBlonde(position, look - position);
            }
            if (blondeHead != null)
                look = Vector3.Lerp(look, blondeHead.position + Vector3.up * 0.06f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - start) / 0.5f)));
        }
        lastPosition = position;
        lastLook = look;
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

    // Kameranın önünde (görüş merkezine en yakın), 3-16 m'deki sarışın NPC; saçına parıltı malzemesi eklenir.
    private static Transform FindBlonde(Vector3 eye, Vector3 forward)
    {
        forward.y = 0f;
        forward.Normalize();
        Transform best = null;
        float bestScore = float.MaxValue;
        Renderer bestHair = null;
        foreach (SkinnedMeshRenderer hair in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
        {
            if (hair.name != "BlondeHair" || !hair.enabled || !hair.gameObject.activeInHierarchy)
                continue;
            if (player != null && hair.transform.IsChildOf(player))
                continue;
            Animator animator = hair.GetComponentInParent<Animator>();
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null)
                continue;
            Vector3 to = head.position - eye;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 3f || distance > 16f)
                continue;
            float angle = Vector3.Angle(forward, to);
            if (angle > 30f)
                continue;
            float score = angle + distance * 0.8f;
            if (score < bestScore)
            {
                bestScore = score;
                best = head;
                bestHair = hair;
            }
        }
        if (bestHair != null)
        {
            Material glow = Resources.Load<Material>("Hair/Hair_Glow");
            if (glow != null)
            {
                Material instance = new Material(glow);
                if (instance.HasProperty("_Glow"))
                    instance.SetFloat("_Glow", 1f);
                Material[] mats = bestHair.sharedMaterials;
                System.Array.Resize(ref mats, mats.Length + 1);
                mats[mats.Length - 1] = instance;
                bestHair.sharedMaterials = mats;
            }
        }
        return best;
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
