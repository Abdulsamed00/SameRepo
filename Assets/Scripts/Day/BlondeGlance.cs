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
    [SerializeField] private float turnTime = 0.9f;
    [Tooltip("Kadını izleme süresi (sn).")]
    [SerializeField] private Vector2 holdTime = new Vector2(2.5f, 3.5f);
    [SerializeField] private float returnTime = 1.6f;
    [SerializeField] private float headHeight = 1.5f;
    [Tooltip("Bakışın yürüyen kadına yetişme hızı (büyük = sert).")]
    [SerializeField] private float followSharpness = 3f;
    [Tooltip("Kilitlendikten sonra takip sıkılığı: nişan (ekran ortası) kadının kafasında kalır.")]
    [SerializeField] private float holdFollowSharpness = 16f;

    private FirstPersonController player;
    private Transform view;
    private NpcDailyOutfit[] women = new NpcDailyOutfit[0];
    private Transform target;
    private Transform headBone;
    private Transform headBoneOwner;
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

    private void OnDisable()
    {
        if (player != null)
            player.LookLocked = false;
        SetMovementLock(false);
    }

    // Bakış sürerken karakter durur (07.11, kullanıcı isteği): kafa dönüp kadına bakarken yürüyemez, bakış bitip
    // kamera geri gelince kontrol oyuncuya döner.
    private bool movementLocked;

    private void SetMovementLock(bool locked)
    {
        if (player == null || locked == movementLocked)
            return;
        movementLocked = locked;
        if (locked)
            player.LockMovement();
        else
            player.UnlockMovement();
    }

    private void LateUpdate()
    {
        if (player == null || view == null || view.parent != player.CameraRoot)
            return;

        // Kilitlenirken ve bakış sürerken fare bakışı kapalı (05.11, kullanıcı isteği).
        player.LookLocked = target != null || weight > 0f;
        SetMovementLock(target != null || weight > 0f);

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
            // Hedef yürürken bakış ona yumuşakça yetişir (05.11: doğrudan kilitlenince kamera sert takip ediyordu).
            // Dönüş sırasında yumuşak, kilitlenince nişan kafada kalsın diye sıkı takip.
            float sharp = Mathf.Lerp(followSharpness, holdFollowSharpness, Mathf.SmoothStep(0f, 1f, weight));
            lastLook = Quaternion.Slerp(lastLook, LookAtTarget(), 1f - Mathf.Exp(-sharp * Time.deltaTime));
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
        Vector3 to = HeadPoint() - view.position;
        return Quaternion.Inverse(view.parent.rotation) * Quaternion.LookRotation(to, view.parent.up);
    }

    // Nişan kadının kafasının tam ortasına gelsin: kafa kemiği (yoksa sabit yükseklik).
    private Vector3 HeadPoint()
    {
        if (headBone == null || headBoneOwner != target)
        {
            headBoneOwner = target;
            headBone = null;
            foreach (Transform t in target.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("Head", System.StringComparison.OrdinalIgnoreCase))
                {
                    headBone = t;
                    break;
                }
            }
        }
        return headBone != null ? headBone.position + Vector3.up * 0.06f : target.position + Vector3.up * headHeight;
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
        if (weight <= 0f)
            lastLook = Quaternion.identity;
        target = best;
        holdLeft = Random.Range(holdTime.x, holdTime.y);
    }
}
