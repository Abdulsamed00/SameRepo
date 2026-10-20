using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Hareket")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 5.5f;
    [SerializeField] private float gravity = -20f;

    [Header("Bakış")]
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Yürüme Hissi")]
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float bobFrequency = 1.8f;
    [SerializeField] private float bobHeight = 0.04f;
    [SerializeField] private float bobSway = 0.025f;
    [SerializeField] private float sprintBobMultiplier = 1.5f;
    [SerializeField] private float bobReturnSpeed = 10f;

    public float MouseSensitivity
    {
        get => mouseSensitivity;
        set => mouseSensitivity = value;
    }

    public bool InputEnabled { get; set; } = true;

    /// <summary>Yatay hız (m/sn). Karakter animasyonu bunu okur.</summary>
    public float HorizontalSpeed => horizontalVelocity.magnitude;
    public float SprintSpeed => sprintSpeed;

    private CharacterController controller;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private float pitch;
    private float verticalVelocity;
    private Vector3 horizontalVelocity;
    private Vector3 cameraBasePosition;
    private float bobTimer;
    private int framesToSkip;

    private const float MaxLookDeltaPerFrame = 50f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cameraBasePosition = cameraRoot.localPosition;

        InputActionMap playerMap = inputActions.FindActionMap("Player", true);
        moveAction = playerMap.FindAction("Move", true);
        lookAction = playerMap.FindAction("Look", true);
        sprintAction = playerMap.FindAction("Sprint", true);
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        sprintAction.Enable();
        // İmleç kilitlenirken gelen ilk fare delta'sı kamerayı aşağı çekiyordu.
        framesToSkip = 3;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        sprintAction.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (!InputEnabled)
        {
            ApplyGravityOnly();
            return;
        }

        Look();
        Move();
    }

    private void Look()
    {
        Vector2 rawDelta = lookAction.ReadValue<Vector2>();
        if (framesToSkip > 0)
        {
            framesToSkip--;
            return;
        }

        Vector2 delta = Vector2.ClampMagnitude(rawDelta, MaxLookDeltaPerFrame) * mouseSensitivity;

        transform.Rotate(Vector3.up, delta.x);

        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        bool sprinting = sprintAction.IsPressed() && input.y > 0f;
        float speed = sprinting ? sprintSpeed : walkSpeed;
        Vector3 wishDir = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f);

        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wishDir * speed, acceleration * Time.deltaTime * speed);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        UpdateHeadBob(sprinting);
    }

    private void UpdateHeadBob(bool sprinting)
    {
        float speed01 = horizontalVelocity.magnitude / sprintSpeed;
        Vector3 target = cameraBasePosition;

        if (controller.isGrounded && speed01 > 0.05f)
        {
            float mult = sprinting ? sprintBobMultiplier : 1f;
            bobTimer += Time.deltaTime * bobFrequency * mult * Mathf.PI * 2f * Mathf.Clamp01(speed01 * 1.5f);
            target.y += Mathf.Sin(bobTimer * 2f) * bobHeight * mult * speed01;
            target.x += Mathf.Cos(bobTimer) * bobSway * mult * speed01;
        }
        else
        {
            bobTimer = 0f;
        }

        cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, target, bobReturnSpeed * Time.deltaTime);
    }

    private void ApplyGravityOnly()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }
}
