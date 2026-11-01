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
    }

    private void Update()
    {
        Focused = FindFocused();
        if (Focused == null && Zone != null && Zone.CanInteract(this))
            Focused = Zone;

        if (Focused != null && WasPressed(Focused.Key))
            Focused.Interact(this);
        else if (IsHolding && dropAction.WasPressedThisFrame())
            DropHeld();
    }

    public void Hold(PickupItem item)
    {
        if (IsHolding)
            return;

        HeldItem = item;
        bool inHand = body != null && item.HoldInHand;
        float reachTime = body != null ? body.PlayPickup(inHand ? item.transform.position : (Vector3?)null) : 0f;
        if (inHand)
            item.OnPickedUp(this, body.RightHandHold, reachTime);
        else
            item.OnPickedUp(this, holdPoint);
    }

    /// <summary>
    /// Elde tutulan eşyayı yerine koymadan elden çıkarır (ör. kasada satın alınan ürün). Eşyanın ne olacağına çağıran karar verir.
    /// </summary>
    public PickupItem ReleaseHeld()
    {
        PickupItem item = HeldItem;
        HeldItem = null;
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
        float releaseTime = animate && body != null && item.HoldInHand
            ? body.PlayPutDown(item.DropPosition(fallback))
            : 0f;
        item.OnDropped(fallback, releaseTime);
    }

    private Interactable FindFocused()
    {
        FocusCollider = null;
        Ray ray = new Ray(viewCamera.position, viewCamera.forward);
        bool hitSomething = RaycastIgnoringSelf(ray, interactDistance, out RaycastHit hit);
        if (hitSomething)
        {
            Interactable direct = hit.collider.GetComponentInParent<Interactable>();
            if (direct != null && direct.CanInteract(this))
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
            if (candidate == null || !candidate.CanInteract(this) || !InSight(candidate, hits[i].point))
                continue;
            best = candidate;
            bestHit = hits[i];
            bestDistance = hits[i].distance;
        }
        if (best != null)
            SetFocusHit(bestHit);
        return best;
    }

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
