using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// F ile açılıp kapanan her şey: kapı, dolap kapağı, çekmece. Her parça kapalı ve açık yerel pozunu bilir;
/// kapı/kapak için menteşe pivotu döndürülür (Euler), çekmece için konum kaydırılır.
/// Collider'lar parçaların altında olduğundan açık kapak da fiziksel olarak engel olur.
/// </summary>
public class Openable : Interactable
{
    // Kapı/dolap kapağı: önünde durunca (kanat geniş, kenarı ~1.2 m).
    protected override float DefaultMaxDistance => 1.6f;

    [Serializable]
    public class Part
    {
        public Transform target;
        public Vector3 closedPosition;
        public Vector3 closedEuler;
        public Vector3 openPosition;
        public Vector3 openEuler;
    }

    [SerializeField] private string openPrompt = "Aç";
    [SerializeField] private string closePrompt = "Kapat";
    [SerializeField] private bool startsOpen;
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private Part[] parts;

    [Header("Ses")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private float volume = 1f;

    [Header("El (IK)")]
    [Tooltip("Açılırken/kapanırken oyuncunun eli bakılan noktaya uzanır ve kapakla birlikte hareket eder.")]
    [SerializeField] private bool useHandIk = true;
    [Tooltip("El kapağa uzanırken kapak bu kadar bekler (sn); sonra hareket ve ses başlar.")]
    [SerializeField] private float handReachDelay = 0.25f;
    [Tooltip("Kapak durduktan sonra elin kapakta kalma süresi (sn).")]
    [SerializeField] private float handLinger = 0.1f;
    [Tooltip("Elin yüzeye değdiği noktanın yüzeyden uzaklığı (m).")]
    [SerializeField] private float handSurfaceOffset = 0.02f;

    [Header("Oyuncu")]
    [Tooltip("Kapak hareket ederken oyuncu yürüyemez (bakış serbest); kapak tam açılıp kapanınca serbest kalır.")]
    [SerializeField] private bool lockMovementWhileMoving = true;
    [Tooltip("Kanat oyuncunun durduğu yerden geçecekse oyuncu en fazla bu kadar (m) yürüyerek yoldan çekilir, sonra açar/kapar.")]
    [SerializeField] private float maxRepositionDistance = 1.2f;
    [SerializeField] private float repositionSpeed = 1.4f;
    [SerializeField] private float repositionTimeout = 2f;

    [Header("Yürüyerek iterek açma")]
    [Tooltip("Kapalı kapıya doğru yürüyünce oyuncu elini kapağa koyup hafifçe iterek açar. Sadece kapak oyuncudan uzağa " +
             "açılıyorsa (iterek) çalışır; çekilecek taraftan yaklaşınca F gerekir.")]
    [SerializeField] private bool autoPushOpen;
    [Tooltip("Oyuncu kapağa bu kadar yaklaşınca (m, itme noktasından kapağın yüzeyine) itme başlar. Kol bu mesafeye ancak yetişir.")]
    [SerializeField] private float pushDistance = 0.65f;
    [Tooltip("Elin kapağa konduğu yükseklik (m, ayaktan). Kamera 1.55'te: daha alçak olursa eli kamera göremez.")]
    [SerializeField] private float pushHeight = 1.3f;
    [Tooltip("Oyuncunun bu hızdan (m/sn) yavaş yürümesi itme başlatmaz.")]
    [SerializeField] private float pushMinSpeed = 0.5f;
    [Tooltip("İterek açma süresi (sn); F ile açmaktan daha yavaş.")]
    [SerializeField] private float pushDuration = 1.0f;

    private bool isOpen;
    private float amount;
    private float moveStartTime;
    private float currentDuration;
    private AudioClip pendingSound;
    private Vector3 pendingSoundAt;
    private Collider[][] partColliders;
    private FirstPersonController lockedController;
    private FirstPersonController player;
    private PlayerInteractor playerInteractor;
    private float nextPlayerSearch;
    private Coroutine repositioning;
    private FirstPersonController repositionPlayer;
    private PlayerInteractor repositionInteractor;
    private readonly Collider[] overlapBuffer = new Collider[16];
    private readonly RaycastHit[] castBuffer = new RaycastHit[16];

    public bool IsOpen => isOpen;
    public float Duration => duration;

    public override string Prompt => isOpen ? closePrompt : openPrompt;

    /// <summary>Yürüyerek iterek açma bu kapıda geçerli mi (sahne geçişi yapan kapı kendiliğinden açılmaz).</summary>
    protected virtual bool CanAutoPush => true;

    private void Awake()
    {
        isOpen = startsOpen;
        amount = isOpen ? 1f : 0f;
        currentDuration = duration;
        Apply();

        partColliders = new Collider[parts.Length][];
        for (int i = 0; i < parts.Length; i++)
            partColliders[i] = parts[i].target != null ? parts[i].target.GetComponentsInChildren<Collider>(true) : new Collider[0];
    }

    private void OnDisable()
    {
        UnlockPlayer();
        EndReposition();
    }

    public override bool CanInteract(PlayerInteractor who) => repositioning == null && base.CanInteract(who);

    public override void Interact(PlayerInteractor interactor)
    {
        if (repositioning != null)
            return;

        // Kanat oyuncunun durduğu yerden geçecekse (oyuncuyu iterdi) oyuncu önce kendiliğinden yoldan çekilir, sonra açar/kapar.
        if (RepositionBeforeMove && TryFindSafeSpot(interactor, out Vector3 spot))
        {
            repositioning = StartCoroutine(RepositionThenToggle(interactor, spot, interactor.FocusCollider,
                interactor.FocusPoint, interactor.FocusNormal));
            return;
        }

        Toggle(interactor, duration, interactor.FocusCollider, interactor.FocusPoint, interactor.FocusNormal, interactor.EyePosition);
    }

    /// <summary>Kanadın yolundaki oyuncu açmadan önce yürüyerek çekilsin mi (sahne geçişi yapan kapıda gerek yok).</summary>
    protected virtual bool RepositionBeforeMove => true;

    private IEnumerator RepositionThenToggle(PlayerInteractor who, Vector3 spot, Collider hit, Vector3 hitPoint, Vector3 hitNormal)
    {
        repositionPlayer = who.GetComponent<FirstPersonController>();
        repositionInteractor = who;
        repositionPlayer.InputEnabled = false;
        who.enabled = false;

        float until = Time.time + repositionTimeout;
        while (Time.time < until && repositionPlayer != null && repositionPlayer.isActiveAndEnabled)
        {
            Vector3 to = spot - repositionPlayer.transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.04f)
                break;
            // Varınca yavaşlar (son 0.3 m'de), hedefi aşmaz.
            repositionPlayer.ScriptedVelocity = to / distance * Mathf.Min(repositionSpeed, Mathf.Max(0.35f, distance * 4f));
            yield return null;
        }

        // Durmak için bir an: hız sıfırlanınca CC yerinde kalsın.
        if (repositionPlayer != null)
            repositionPlayer.ScriptedVelocity = Vector3.zero;
        yield return null;

        repositioning = null; // kendini durdurmasın
        EndReposition();
        Toggle(who, duration, hit, hitPoint, hitNormal, who.EyePosition);
    }

    private void EndReposition()
    {
        if (repositioning != null)
        {
            StopCoroutine(repositioning);
            repositioning = null;
        }
        if (repositionPlayer != null)
        {
            repositionPlayer.ScriptedVelocity = Vector3.zero;
            repositionPlayer.InputEnabled = true;
            repositionPlayer = null;
        }
        if (repositionInteractor != null)
        {
            repositionInteractor.enabled = true;
            repositionInteractor = null;
        }
    }

    // Oyuncunun kapsülü kanadın şu andan hedef poza kadar taradığı alanla kesişiyorsa, en kısa yürüyüşle kesişmeyen,
    // duvar/eşyaya çarpmayan ve oraya düz yürünebilen bir nokta arar. Kesişmiyorsa false (yerinde açar).
    private bool TryFindSafeSpot(PlayerInteractor who, out Vector3 spot)
    {
        spot = default;
        CharacterController cc = who != null ? who.GetComponent<CharacterController>() : null;
        if (cc == null || parts == null || parts.Length == 0)
            return false;

        Vector3 start = who.transform.position;
        float goal = isOpen ? 0f : 1f;
        if (!SweepHits(cc, start, goal))
            return false;

        // Tercih edilen yön: menteşeden oyuncuya doğru (kanat ucunun yayından dışarı) ve kapaktan geriye.
        Vector3 hinge = parts[0].target != null ? parts[0].target.position : transform.position;
        Vector3 away = start - hinge;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = -who.transform.forward;
        away.Normalize();

        float bestScore = float.MaxValue;
        bool found = false;
        const int directions = 16;
        for (float step = 0.1f; step <= maxRepositionDistance + 0.001f; step += 0.1f)
        {
            for (int d = 0; d < directions; d++)
            {
                Vector3 dir = Quaternion.Euler(0f, d * 360f / directions, 0f) * away;
                Vector3 candidate = start + dir * (step + 0.05f); // kanatla arada küçük pay
                // Kısa yürüyüş öncelikli; eşit mesafede "uzağa" yöne yakın olan.
                float score = step + (1f - Vector3.Dot(dir, away)) * 0.15f;
                if (score >= bestScore)
                    continue;
                if (SweepHits(cc, candidate, goal) || Blocked(cc, start, candidate))
                    continue;
                bestScore = score;
                spot = candidate;
                found = true;
            }
            if (found)
                break;
        }
        return found;
    }

    // Kanat şu anki pozundan hedefe dönerken oyuncunun kapsülüyle (verilen konumda) kesişiyor mu.
    private bool SweepHits(CharacterController cc, Vector3 playerPos, float goal)
    {
        const int samples = 10;
        for (int s = 0; s <= samples; s++)
        {
            float a = Mathf.Lerp(amount, goal, s / (float)samples);
            float t = Mathf.SmoothStep(0f, 1f, a);
            for (int i = 0; i < parts.Length; i++)
            {
                Part part = parts[i];
                if (part.target == null)
                    continue;
                Transform parent = part.target.parent;
                Vector3 lp = Vector3.Lerp(part.closedPosition, part.openPosition, t);
                Quaternion lr = Quaternion.Slerp(Quaternion.Euler(part.closedEuler), Quaternion.Euler(part.openEuler), t);
                Vector3 targetPos = parent != null ? parent.TransformPoint(lp) : lp;
                Quaternion targetRot = parent != null ? parent.rotation * lr : lr;
                Quaternion invNow = Quaternion.Inverse(part.target.rotation);

                foreach (Collider c in partColliders[i])
                {
                    if (c == null || c.isTrigger || !c.enabled)
                        continue;
                    // Collider'ın kapağa göre pozu sabit; kapağın o andaki pozuna taşınır.
                    Vector3 rel = invNow * (c.transform.position - part.target.position);
                    Quaternion relRot = invNow * c.transform.rotation;
                    if (Physics.ComputePenetration(c, targetPos + targetRot * rel, targetRot * relRot,
                            cc, playerPos, cc.transform.rotation, out _, out float depth) && depth > 0.001f)
                        return true;
                }
            }
        }
        return false;
    }

    // Aday noktada kapsül bir şeye giriyor mu, ya da oraya düz yürürken bir şeye çarpıyor mu (oyuncunun kendisi ve
    // bu kapının kanatları hariç; kanat zaten SweepHits'te).
    private bool Blocked(CharacterController cc, Vector3 from, Vector3 to)
    {
        float radius = cc.radius;
        Vector3 bottom = cc.center + Vector3.up * (radius - cc.height * 0.5f + cc.stepOffset);
        Vector3 top = cc.center + Vector3.up * (cc.height * 0.5f - radius);
        Transform self = cc.transform;

        int n = Physics.OverlapCapsuleNonAlloc(to + bottom, to + top, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (!IsIgnored(overlapBuffer[i], self))
                return true;

        Vector3 move = to - from;
        move.y = 0f;
        float length = move.magnitude;
        if (length < 0.001f)
            return false;
        int h = Physics.CapsuleCastNonAlloc(from + bottom, from + top, radius * 0.95f, move / length, castBuffer, length,
            ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < h; i++)
        {
            // Başlangıçta zaten değilen (distance 0) yüzeyler sayılmaz; varış noktası yukarıda ayrıca denetlendi.
            // Kanadın şu anki hâli sayılır: kapalı kapının içinden geçip öbür tarafa yürünmesin.
            if (castBuffer[i].distance > 0f && !IsIgnored(castBuffer[i].collider, self))
                return true;
        }
        return false;
    }

    private static bool IsIgnored(Collider c, Transform self) => c == null || c.transform.IsChildOf(self);

    private void Toggle(PlayerInteractor interactor, float moveDuration, Collider hit, Vector3 hitPoint, Vector3 hitNormal, Vector3 eye)
    {
        isOpen = !isOpen;
        currentDuration = moveDuration;
        Vector3 at = parts != null && parts.Length > 0 && parts[0].target != null ? parts[0].target.position : transform.position;

        float delay = TryReach(interactor, moveDuration, hit, hitPoint, hitNormal, eye) ? handReachDelay : 0f;
        moveStartTime = Time.time + delay;
        LockPlayer(interactor);

        AudioClip sound = isOpen ? openSound : closeSound;
        if (delay <= 0f)
            GameAudio.PlayAt(sound, at, volume);
        else
        {
            pendingSound = sound;
            pendingSoundAt = at;
        }
    }

    // Işının değdiği kapak/çekmece üstünde bir nokta varsa oyuncunun sağ eli oraya uzanır. Kapak dönerken nokta
    // onunla birlikte gelir. Bölgeden (ışınsız) ya da kapaktan başka bir şeye değen etkileşimde el kullanılmaz.
    private bool TryReach(PlayerInteractor interactor, float moveDuration, Collider hit, Vector3 hitPoint, Vector3 hitNormal, Vector3 eye)
    {
        PlayerBody body = interactor != null ? interactor.Body : null;
        if (!useHandIk || body == null || hit == null)
            return false;

        Transform anchor = null;
        foreach (Part part in parts)
        {
            if (part.target != null && hit.transform.IsChildOf(part.target))
            {
                anchor = part.target;
                break;
            }
        }

        Vector3 point;
        if (anchor != null)
            point = hitPoint + hitNormal * handSurfaceOffset;
        else
        {
            // Işın kapağa değmedi (açık dolabın içine, görünmez arka panele gitti): el kameraya en yakın kapağın
            // yakın kenarına uzanır, kapatırken açık kanadı iter.
            float best = float.MaxValue;
            Vector3 closest = default;
            for (int i = 0; i < parts.Length; i++)
            {
                foreach (Collider c in partColliders[i])
                {
                    if (c.isTrigger)
                        continue;
                    Vector3 p = c.ClosestPoint(eye);
                    float d = (p - eye).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        anchor = parts[i].target;
                        closest = p;
                    }
                }
            }
            if (anchor == null)
                return false;
            point = closest + (eye - closest).normalized * handSurfaceOffset;
        }

        return body.BeginTouch(anchor, point, handReachDelay + moveDuration + handLinger) > 0f;
    }

    private void LockPlayer(PlayerInteractor who)
    {
        if (!lockMovementWhileMoving || who == null || lockedController != null)
            return;
        FirstPersonController controller = who.GetComponent<FirstPersonController>();
        if (controller == null)
            return;
        controller.LockMovement();
        lockedController = controller;
    }

    private void UnlockPlayer()
    {
        if (lockedController == null)
            return;
        lockedController.UnlockMovement();
        lockedController = null;
    }

    private void Update()
    {
        if (Time.time < moveStartTime)
            return;
        if (pendingSound != null)
        {
            GameAudio.PlayAt(pendingSound, pendingSoundAt, volume);
            pendingSound = null;
        }

        float goal = isOpen ? 1f : 0f;
        if (Mathf.Approximately(amount, goal))
        {
            // Kapak tam açıldı/kapandı: oyuncu yürüyebilir. Kapalıyken de kapıya yürünüyorsa itme başlayabilir.
            UnlockPlayer();
            if (autoPushOpen && !isOpen)
                TryAutoPush();
            return;
        }

        amount = Mathf.MoveTowards(amount, goal, Time.deltaTime / currentDuration);
        Apply();
    }

    // Oyuncu kapalı kapıya doğru yürüyorsa ve kapak ondan uzağa açılıyorsa eli kapağa koyup iterek açar.
    private void TryAutoPush()
    {
        if (!CanAutoPush)
            return;

        if (player == null)
        {
            if (Time.unscaledTime < nextPlayerSearch)
                return;
            nextPlayerSearch = Time.unscaledTime + 1f;
            player = FindFirstObjectByType<FirstPersonController>();
            playerInteractor = player != null ? player.GetComponent<PlayerInteractor>() : null;
            if (player == null || playerInteractor == null)
                return;
        }

        if (!player.isActiveAndEnabled || !playerInteractor.isActiveAndEnabled || player.Frozen || !player.InputEnabled
            || player.MovementLocked || player.HorizontalSpeed < pushMinSpeed || !CanInteract(playerInteractor))
            return;

        Vector3 chest = player.transform.position + Vector3.up * pushHeight;
        Vector3 velocity = player.HorizontalVelocity;
        velocity.y = 0f;
        velocity.Normalize();

        for (int i = 0; i < parts.Length; i++)
        {
            Part part = parts[i];
            if (part.target == null || part.target.parent == null)
                continue;

            foreach (Collider c in partColliders[i])
            {
                if (c.isTrigger)
                    continue;

                Vector3 closest = c.ClosestPoint(chest);
                Vector3 toDoor = closest - chest;
                if (toDoor.sqrMagnitude > pushDistance * pushDistance)
                    continue;
                toDoor.y = 0f;
                if (toDoor.sqrMagnitude < 0.0001f)
                    continue;
                toDoor.Normalize();
                // Kapıya doğru yürüyor ve ona dönük olmalı.
                if (Vector3.Dot(velocity, toDoor) < 0.7f || Vector3.Dot(player.transform.forward, toDoor) < 0.3f)
                    continue;

                // Kanat açılırken oyuncudan uzaklaşmalı (itme tarafı); çekme tarafından yaklaşınca kanat üstüne gelirdi.
                Vector3 closedCenter = c.bounds.center;
                Vector3 local = part.target.InverseTransformPoint(closedCenter);
                Vector3 openCenter = part.target.parent.TransformPoint(part.openPosition + Quaternion.Euler(part.openEuler) * local);
                Vector3 move = openCenter - closedCenter;
                move.y = 0f;
                Vector3 awayFromPlayer = closedCenter - chest;
                awayFromPlayer.y = 0f;
                if (Vector3.Dot(move, awayFromPlayer) <= 0f)
                    continue;

                Toggle(playerInteractor, pushDuration, c, closest, (chest - closest).normalized, playerInteractor.EyePosition);
                return;
            }
        }
    }

    private void Apply()
    {
        float t = Mathf.SmoothStep(0f, 1f, amount);
        foreach (Part part in parts)
        {
            part.target.localPosition = Vector3.Lerp(part.closedPosition, part.openPosition, t);
            part.target.localRotation = Quaternion.Slerp(Quaternion.Euler(part.closedEuler), Quaternion.Euler(part.openEuler), t);
        }
    }
}
