using UnityEngine;

/// <summary>
/// 2. gün, saç boyanana kadar: yakından geçen sarışın kadınlara oyuncunun kafası bir anda döner, kısa süre onu izler,
/// sonra yavaşça geri gelir. Kontrol oyuncudadır: yürüme ve fare çalışır, dönüş kameranın bakışına eklenir;
/// kadın gövdenin çok arkasında kalırsa bakış kendiliğinden biter.
/// Kamera kökün (CameraRoot) altında olduğu için yerel dönüşü boştur, bakış oraya yazılır. Kafa IK'sı kamerayı izler.
/// </summary>
public class BlondeGlance : MonoBehaviour
{
    [Tooltip("Hangi günde çalışır (0'dan: 1 = 2. gün).")]
    [SerializeField] private int dayIndex = 1;
    [SerializeField] private string hairPart = "BlondeHair";
    [SerializeField] private Vector2 range = new Vector2(1.5f, 13f);
    [Tooltip("Görüş yönüne göre en fazla kaç derecedeki kadın fark edilir.")]
    [SerializeField] private float noticeAngle = 80f;
    [Tooltip("Bakış gövdenin önünden bu kadar ayrılırsa bakış biter.")]
    [SerializeField] private float maxAngle = 115f;
    [Tooltip("İki bakış arası bekleme (sn).")]
    [SerializeField] private Vector2 cooldown = new Vector2(5f, 11f);
    [SerializeField] private float turnTime = 0.3f;
    [Tooltip("Kadını izleme süresi (sn).")]
    [SerializeField] private Vector2 holdTime = new Vector2(1.6f, 2.6f);
    [SerializeField] private float returnTime = 1.1f;
    [SerializeField] private float headHeight = 1.5f;

    private FirstPersonController player;
    private Transform view;
    private NpcDailyOutfit[] women = new NpcDailyOutfit[0];
    private Transform target;
    private Quaternion lastLook = Quaternion.identity;
    private float weight;
    private float holdLeft;
    private float wait = 3f;
    private float scanWait;

    private bool Active =>
        GameDay.Index == dayIndex && !PlayerLook.HairDyed && !player.Frozen && player.InputEnabled && !PauseMenu.IsPaused;

    private void Start()
    {
        player = FindFirstObjectByType<FirstPersonController>();
        if (player != null)
            view = player.CameraRoot.GetComponentInChildren<Camera>().transform;
    }

    private void LateUpdate()
    {
        if (player == null || view == null || view.parent != player.CameraRoot)
            return;

        if (target == null && weight <= 0f)
        {
            view.localRotation = Quaternion.identity;
            wait -= Time.deltaTime;
            if (Active && wait <= 0f)
                FindTarget();
            return;
        }

        // Hedef var ve geçerliyse döner, yoksa geri gelir.
        if (target != null && (!Active || !target.gameObject.activeInHierarchy || !StillInReach()))
            Release();

        if (target != null)
        {
            lastLook = LookAtTarget();
            weight = Mathf.Min(1f, weight + Time.deltaTime / turnTime);
            if (weight >= 1f)
            {
                holdLeft -= Time.deltaTime;
                if (holdLeft <= 0f)
                    Release();
            }
        }
        else
        {
            weight = Mathf.Max(0f, weight - Time.deltaTime / returnTime);
        }

        view.localRotation = Quaternion.Slerp(Quaternion.identity, lastLook, Mathf.SmoothStep(0f, 1f, weight));
    }

    private void Release()
    {
        target = null;
        wait = Random.Range(cooldown.x, cooldown.y);
    }

    private Quaternion LookAtTarget()
    {
        Vector3 to = target.position + Vector3.up * headHeight - view.position;
        return Quaternion.Inverse(view.parent.rotation) * Quaternion.LookRotation(to, view.parent.up);
    }

    private bool StillInReach()
    {
        Vector3 to = target.position - player.transform.position;
        to.y = 0f;
        return to.sqrMagnitude < range.y * range.y * 1.5f && Vector3.Angle(player.transform.forward, to) < maxAngle;
    }

    private void FindTarget()
    {
        scanWait -= Time.deltaTime;
        if (scanWait > 0f)
            return;
        scanWait = 0.25f;

        women = FindObjectsByType<NpcDailyOutfit>(FindObjectsSortMode.None);
        Transform best = null;
        float bestDistance = float.MaxValue;
        foreach (NpcDailyOutfit npc in women)
        {
            Transform hair = npc.transform.Find(hairPart);
            if (hair == null || !hair.gameObject.activeSelf)
                continue;

            Vector3 head = npc.transform.position + Vector3.up * headHeight;
            Vector3 to = head - view.position;
            float distance = to.magnitude;
            if (distance < range.x || distance > range.y || distance >= bestDistance
                || Vector3.Angle(view.forward, to) > noticeAngle)
                continue;
            // Binanın arkasındaki kadına bakılmaz. NPC gövdeleri Ignore Raycast katmanında.
            if (Physics.Linecast(view.position, head, ~(1 << 2), QueryTriggerInteraction.Ignore))
                continue;
            best = npc.transform;
            bestDistance = distance;
        }

        if (best == null)
            return;
        target = best;
        holdLeft = Random.Range(holdTime.x, holdTime.y);
    }
}
