using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Kameranın baktığı yönde <see cref="Interactable"/> arar, Interact tuşunda tetikler
/// ve elde tutulan eşyayı yönetir. Eşyaya özel davranış burada değil, Interactable türevlerindedir.
/// Input, FirstPersonController ile aynı InputSystem_Actions asset'indeki Player map'inden okunur.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Algılama")]
    [SerializeField] private Transform viewCamera;
    [SerializeField] private float interactDistance = 2.5f;
    [SerializeField] private LayerMask interactMask = ~0;
    [Tooltip("Işın doğrudan bir etkileşime değmezse çevresinde bu yarıçapta aranır (küçük eşyalar için, m).")]
    [SerializeField] private float aimAssistRadius = 0.12f;
    [Tooltip("Hedef yardımında bakılan yüzeyin bu kadar arkasına kadar olan etkileşimler de sayılır (m).")]
    [SerializeField] private float aimAssistDepth = 0.3f;

    [Header("Elde Tutma")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float dropDistance = 1.2f;
    [Tooltip("Eşya alınınca alma animasyonu oynatılan gövde. Boşsa alt objelerde aranır.")]
    [SerializeField] private PlayerBody body;

    private InputAction interactAction;
    private InputAction dropAction;
    private InputAction sitAction;

    public Interactable Focused { get; private set; }

    /// <summary>Bir bölgenin (<see cref="SceneZone"/>) içindeyken ışın bir şeye bakmasa da F ile tetiklenen etkileşim.</summary>
    public Interactable Zone { get; set; }
    /// <summary>Ekranın altında gösterilen ek tuş ipucu (ör. incelerken "Geri as"). Boşsa gösterilmez.</summary>
    public string Hint { get; set; }
    /// <summary><see cref="Hint"/>'in tuş etiketi ("G").</summary>
    public string HintKey { get; set; }
    public PickupItem HeldItem { get; private set; }
    public bool IsHolding => HeldItem != null;

    /// <summary>Elde tutulan eşya şu an bırakılabilir (mağaza ürünleri sadece yerlerinin yakınında).</summary>
    public bool CanDropHeld => HeldItem != null && HeldItem.CanDropAt(viewCamera.position);

    /// <summary>Oyuncunun görünür gövdesi (el IK'sı için: kapı/dolap açarken el kapağa uzanır).</summary>
    public PlayerBody Body => body;

    /// <summary>
    /// Işının değdiği nokta, yüzey normali ve collider'ı (<see cref="Focused"/> bir ışın vuruşundan geldiyse). Bölgeden
    /// (<see cref="Zone"/>) gelen etkileşimde collider boştur.
    /// </summary>
    public Vector3 EyePosition => viewCamera.position;
    public Collider FocusCollider { get; private set; }
    public Vector3 FocusPoint { get; private set; }
    public Vector3 FocusNormal { get; private set; }

    public string InteractKeyLabel => KeyLabel(interactAction);
    public string DropKeyLabel => KeyLabel(dropAction);
    public string SitKeyLabel => KeyLabel(sitAction);

    public string KeyLabelFor(InteractionKey key) => key == InteractionKey.Secondary ? SitKeyLabel : InteractKeyLabel;

    private void Awake()
    {
        // Ignore Raycast katmanı etkileşim ışınını hiç kesmez (NPC kapsülleri, dolabın önündeki görünmez engel; 10.11).
        interactMask &= ~(1 << 2);
        KeyBindings.Ensure(inputActions); // kayıtlı tuş atamaları
        InputActionMap playerMap = inputActions.FindActionMap("Player", true);
        interactAction = playerMap.FindAction("Interact", true);
        dropAction = playerMap.FindAction("Drop", true);
        sitAction = playerMap.FindAction("Sit", true);
        if (body == null)
            body = GetComponentInChildren<PlayerBody>(true);
    }

    private void OnEnable()
    {
        interactAction.Enable();
        dropAction.Enable();
        sitAction.Enable();
    }

    private void OnDisable()
    {
        interactAction.Disable();
        dropAction.Disable();
        sitAction.Disable();
        Focused = null;
        Zone = null;
        // Kapanırken süren kilitler (coroutine durur) açılmazsa oyuncu sonsuza kadar kilitli kalır.
        for (; walkLocks > 0; walkLocks--)
            walker.UnlockMovement();
        busyUntil = 0f;
    }

    /// <summary>
    /// Etkileşilebilir ve söyleyecek bir bildirimi var (05.11: kilitli/şartı sağlanmamış durumlarda oyuncuya ne yapması
    /// gerektiği yazılmıyor; bildirimi boş olan etkileşim hiç seçilmez, halka da çıkmaz).
    /// </summary>
    private bool Usable(Interactable target) => target.CanInteract(this) && !string.IsNullOrEmpty(target.Prompt);

    private void Update()
    {
        Focused = FindFocused();
        if (Focused == null && Zone != null && Usable(Zone))
            Focused = Zone;

        if (IsBusy)
            return; // alma/bırakma bitmeden yeni eylem yok

        if (Focused != null && WasPressed(Focused.Key))
            Focused.Interact(this);
        else if (IsHolding && dropAction.WasPressedThisFrame() && CanDropHeld)
            DropHeld();
    }

    public void Hold(PickupItem item)
    {
        if (IsHolding)
            return;

        HeldItem = item;
        if (body != null && item.HasGrips)
        {
            // Tek/iki el IK: eller önce eşyanın durduğu yere uzanır, sonra eşya kameranın önüne gelir, eller tutuşta kalır.
            item.GetGrips(holdPoint, out Transform right, out Transform left);
            bool oneHandFirst = item.ReachWithOneHand && left != null;
            float reach = body.BeginCarry(right, left, item.UpdateHeldPose, oneHandFirst);
            item.OnPickedUp(this, holdPoint, reach);
            // Eşya ele gelip eller tutana kadar (palto: sol el sonradan katılır) yürünemez.
            BusyFor(reach + 0.5f + (oneHandFirst ? body.LeftJoinTotal : 0f));
            return;
        }

        bool inHand = body != null && item.HoldInHand;
        float reachTime = body != null ? body.PlayPickup(inHand ? item.transform.position : (Vector3?)null) : 0f;
        if (inHand)
            item.OnPickedUp(this, body.RightHandHold, reachTime);
        else
            item.OnPickedUp(this, holdPoint);
        BusyFor(reachTime + 0.4f);
    }

    private float busyUntil;
    private FirstPersonController walker;

    /// <summary>Alma/bırakma animasyonu sürüyor: yeni etkileşim ve yürüme kapalı.</summary>
    public bool IsBusy => Time.time < busyUntil;

    /// <summary>Oyuncu <paramref name="seconds"/> sn boyunca yürüyemez ve başka etkileşime giremez (eylem bitene kadar).</summary>
    public void BusyFor(float seconds)
    {
        if (seconds <= 0f)
            return;
        if (walker == null)
            walker = GetComponent<FirstPersonController>();
        busyUntil = Mathf.Max(busyUntil, Time.time + seconds);
        if (walker != null)
            StartCoroutine(LockWalking(seconds));
    }

    private System.Collections.IEnumerator LockWalking(float seconds)
    {
        FirstPersonController controller = walker;
        controller.LockMovement();
        walkLocks++;
        yield return new WaitForSeconds(seconds);
        if (walkLocks > 0)
        {
            walkLocks--;
            controller.UnlockMovement();
        }
    }

    private int walkLocks;

    /// <summary>
    /// Elde tutulan eşyayı yerine koymadan elden çıkarır (ör. kasada satın alınan ürün). Eşyanın ne olacağına çağıran karar verir.
    /// </summary>
    public PickupItem ReleaseHeld()
    {
        PickupItem item = HeldItem;
        HeldItem = null;
        if (item != null && item.HasGrips && body != null)
            body.EndCarry(0.2f);
        return item;
    }

    /// <param name="animate">Açıksa ve eşya eldeyse alma animasyonu tersten oynar, eşya el yerine değince bırakılır.</param>
    public void DropHeld(bool animate = true)
    {
        if (!IsHolding)
            return;

        PickupItem item = HeldItem;
        HeldItem = null;
        Vector3 fallback = FindDropPosition(item.Radius);
        if (body != null && item.HasGrips)
        {
            // Eşya elde tutuluyormuş gibi yerine kayar, eller onunla gider, sonra bırakır.
            item.OnDropped(fallback, animate ? 0.05f : 0f);
            body.EndCarry(animate ? 0.45f : 0f);
            if (animate)
                BusyFor(0.45f + 0.5f);
            return;
        }

        float releaseTime = animate && body != null && item.HoldInHand
            ? body.PlayPutDown(item.DropPosition(fallback))
            : 0f;
        item.OnDropped(fallback, releaseTime);
        if (animate)
            BusyFor(releaseTime + 0.4f);
    }

    private Interactable FindFocused()
    {
        FocusCollider = null;
        Ray ray = new Ray(viewCamera.position, viewCamera.forward);
        bool hitSomething = RaycastIgnoringSelf(ray, interactDistance, out RaycastHit hit);
        if (hitSomething)
        {
            Interactable direct = hit.collider.GetComponentInParent<Interactable>();
            if (direct != null && InReach(direct, hit.distance) && Usable(direct))
            {
                SetFocusHit(hit);
                return direct;
            }
        }

        // Hedef yardımı: kumanda gibi küçük eşyalar tam ortalanmasa da (özellikle otururken uzaktan)
        // ışının çevresinde, bakılan yüzeyin hemen önünde/yakınında kalan etkileşimler de seçilir.
        float limit = hitSomething ? hit.distance + aimAssistDepth : interactDistance;
        int count = Physics.SphereCastNonAlloc(ray, aimAssistRadius, hits, limit, interactMask, QueryTriggerInteraction.Ignore);
        Interactable best = null;
        RaycastHit bestHit = default;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            // distance 0: küre başlangıçta zaten içindeydi (kameranın dibindeki kapı kanadı vb.), sayılmaz.
            if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance <= 0f || hits[i].distance >= bestDistance)
                continue;
            Interactable candidate = hits[i].collider.GetComponentInParent<Interactable>();
            if (candidate == null || !InReach(candidate, hits[i].distance) || !Usable(candidate)
                || !InSight(candidate, hits[i].point))
                continue;
            best = candidate;
            bestHit = hits[i];
            bestDistance = hits[i].distance;
        }
        if (best != null)
            SetFocusHit(bestHit);
        return best;
    }

    // Küçük eşyalar (kumanda) ortak 2.5 m'den algılanınca odanın ortasından alınabiliyordu; kendi sınırları var.
    private static bool InReach(Interactable target, float distance)
        => target.MaxDistance <= 0f || distance <= target.MaxDistance;

    private void SetFocusHit(RaycastHit hit)
    {
        FocusCollider = hit.collider;
        FocusPoint = hit.point;
        FocusNormal = hit.normal;
    }

    // Hedef yardımı bakılan yüzeyin arkasına da uzandığı için 20 cm'lik duvarın öbür yüzündeki eşyayı (yan odadaki ayna,
    // koridordan dolap) buluyordu. Kameradan eşyanın değilen noktasına kadar arada başka bir şey olmamalı.
    private bool InSight(Interactable candidate, Vector3 point)
    {
        Vector3 origin = viewCamera.position;
        Vector3 toPoint = point - origin;
        float distance = toPoint.magnitude - 0.02f;
        if (distance <= 0f)
            return true;

        int count = Physics.RaycastNonAlloc(origin, toPoint.normalized, sightHits, distance, interactMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = sightHits[i].collider;
            if (!c.transform.IsChildOf(transform) && c.GetComponentInParent<Interactable>() != candidate)
                return false;
        }
        return true;
    }

    // Bakılan yönde dropDistance kadar ileri; duvar/zemin/mobilya varsa onun önünde dur.
    private Vector3 FindDropPosition(float radius)
    {
        Vector3 origin = viewCamera.position;
        Vector3 direction = viewCamera.forward;
        float distance = dropDistance;

        if (Physics.SphereCast(origin, radius, direction, out RaycastHit hit, dropDistance, interactMask, QueryTriggerInteraction.Ignore))
            distance = hit.distance;

        return origin + direction * distance;
    }

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private readonly RaycastHit[] sightHits = new RaycastHit[16];

    // Kamera CharacterController kapsülünün tepesine yakın (1.7 / 1.8); aşağı bakınca ışın önce
    // oyuncunun kendi kapsülüne çarpıyordu. Oyuncuya ait collider'lar atlanır.
    private bool RaycastIgnoringSelf(Ray ray, float distance, out RaycastHit closest)
    {
        int count = Physics.RaycastNonAlloc(ray, hits, distance, interactMask, QueryTriggerInteraction.Ignore);
        closest = default;
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= best)
                continue;
            best = hits[i].distance;
            closest = hits[i];
        }
        return best < float.MaxValue;
    }

    private bool WasPressed(InteractionKey key) =>
        (key == InteractionKey.Secondary ? sitAction : interactAction).WasPressedThisFrame();

    private static string KeyLabel(InputAction action)
    {
        string label = action.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"));
        return string.IsNullOrEmpty(label) ? action.GetBindingDisplayString() : label;
    }
}
