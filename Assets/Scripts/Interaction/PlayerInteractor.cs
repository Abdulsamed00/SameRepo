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

    [Header("Elde Tutma")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float dropDistance = 1.2f;

    private InputAction interactAction;
    private InputAction dropAction;
    private InputAction sitAction;

    public Interactable Focused { get; private set; }

    /// <summary>Bir bölgenin (<see cref="SceneZone"/>) içindeyken ışın bir şeye bakmasa da F ile tetiklenen etkileşim.</summary>
    public Interactable Zone { get; set; }
    public PickupItem HeldItem { get; private set; }
    public bool IsHolding => HeldItem != null;

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
        item.OnPickedUp(holdPoint);
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

    public void DropHeld()
    {
        if (!IsHolding)
            return;

        PickupItem item = HeldItem;
        HeldItem = null;
        item.OnDropped(FindDropPosition(item.Radius));
    }

    private Interactable FindFocused()
    {
        Ray ray = new Ray(viewCamera.position, viewCamera.forward);
        if (!RaycastIgnoringSelf(ray, interactDistance, out RaycastHit hit))
            return null;

        Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
        return interactable != null && interactable.CanInteract(this) ? interactable : null;
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
