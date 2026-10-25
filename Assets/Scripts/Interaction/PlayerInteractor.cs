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

    public Interactable Focused { get; private set; }

    /// <summary>Bir bölgenin (<see cref="SceneZone"/>) içindeyken ışın bir şeye bakmasa da F ile tetiklenen etkileşim.</summary>
    public Interactable Zone { get; set; }
    public PickupItem HeldItem { get; private set; }
    public bool IsHolding => HeldItem != null;

    public string InteractKeyLabel => KeyLabel(interactAction);
    public string DropKeyLabel => KeyLabel(dropAction);

    private void Awake()
    {
        InputActionMap playerMap = inputActions.FindActionMap("Player", true);
        interactAction = playerMap.FindAction("Interact", true);
        dropAction = playerMap.FindAction("Drop", true);
    }

    private void OnEnable()
    {
        interactAction.Enable();
        dropAction.Enable();
    }

    private void OnDisable()
    {
        interactAction.Disable();
        dropAction.Disable();
        Focused = null;
        Zone = null;
    }

    private void Update()
    {
        Focused = FindFocused();
        if (Focused == null && Zone != null && Zone.CanInteract(this))
            Focused = Zone;

        if (Focused != null && interactAction.WasPressedThisFrame())
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
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
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

    private static string KeyLabel(InputAction action)
    {
        string label = action.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse"));
        return string.IsNullOrEmpty(label) ? action.GetBindingDisplayString() : label;
    }
}
