using System;
using UnityEngine;

/// <summary>
/// F ile açılıp kapanan her şey: kapı, dolap kapağı, çekmece. Her parça kapalı ve açık yerel pozunu bilir;
/// kapı/kapak için menteşe pivotu döndürülür (Euler), çekmece için konum kaydırılır.
/// Collider'lar parçaların altında olduğundan açık kapak da fiziksel olarak engel olur.
/// </summary>
public class Openable : Interactable
{
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

    private void OnDisable() => UnlockPlayer();

    public override void Interact(PlayerInteractor interactor)
    {
        Toggle(interactor, duration, interactor.FocusCollider, interactor.FocusPoint, interactor.FocusNormal, interactor.EyePosition);
    }

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
