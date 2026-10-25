using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Hareket")]
    [Tooltip("Yürüme klibi doğal hızında ~1.4 m/s; çok yükseltilirse animasyon aşırı hızlı oynar.")]
    [SerializeField] private float walkSpeed = 2f;
    [Tooltip("Yürüme hızıyla aynıysa Shift hızı değiştirmez (sadece FOV kick ve adım ritmi değişir).")]
    [SerializeField] private float runSpeed = 3f;
    [Tooltip("Koşarken baş sallanması ritmine eklenen hız çarpanı.")]
    [Range(0f, 1f)] [SerializeField] private float runstepLengthen = 0.7f;
    [SerializeField] private float jumpSpeed = 10f;
    [Tooltip("Yerdeyken aşağı itme hızı: rampa ve basamak inerken zemine yapışık kalır.")]
    [SerializeField] private float stickToGroundForce = 10f;
    [SerializeField] private float gravityMultiplier = 2f;
    [Tooltip("Bir baş sallanması döngüsünün kaç birim hızda tamamlandığı.")]
    [SerializeField] private float stepInterval = 5f;
    [SerializeField] private float acceleration = 12f;

    [Header("Bakış")]
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private float xSensitivity = 2f;
    [SerializeField] private float ySensitivity = 2f;
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 90f;
    [SerializeField] private bool smooth;
    [SerializeField] private float smoothTime = 5f;
    [SerializeField] private bool lockCursor = true;

    [Header("Baş Sallanması")]
    [SerializeField] private bool useHeadBob = true;
    [SerializeField] private float horizontalBobRange = 0.1f;
    [SerializeField] private float verticalBobRange = 0.1f;
    [SerializeField] private float verticalToHorizontalRatio = 2f;
    [SerializeField] private AnimationCurve bobCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f),
        new Keyframe(1.5f, -1f), new Keyframe(2f, 0f));
    [Tooltip("Durunca kameranın merkeze dönüş hızı.")]
    [SerializeField] private float bobReturnSpeed = 10f;

    [Header("Zıplama/İniş Sarsıntısı")]
    [SerializeField] private float jumpBobDuration = 0.2f;
    [SerializeField] private float jumpBobAmount = 0.1f;

    [Header("FOV Kick")]
    [SerializeField] private bool useFovKick = true;
    [SerializeField] private float fovIncrease = 3f;
    [SerializeField] private float fovTimeToIncrease = 1f;
    [SerializeField] private float fovTimeToDecrease = 1f;
    [SerializeField] private AnimationCurve fovCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool InputEnabled { get; set; } = true;

    /// <summary>Yatay hız (m/sn). Karakter animasyonu bunu okur.</summary>
    public float HorizontalSpeed => horizontalVelocity.magnitude;

    private CharacterController controller;
    private Camera playerCamera;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private Quaternion targetBodyRotation;
    private Quaternion targetCameraRotation;
    private float verticalVelocity;
    private Vector3 horizontalVelocity;
    private bool wasGrounded;
    private int framesToSkip;

    private Vector3 cameraBasePosition;
    private Vector2 bobOffset;
    private float bobCycleX;
    private float bobCycleY;
    private float landBobTimer = float.MaxValue;

    private float baseFov;
    private float fovKick01;

    // Eski Input Manager'da "Mouse X/Y" ekseni piksel delta'sının 0.1 katıydı; hassasiyet 2 bu ölçeğe göre.
    private const float LegacyMouseAxisScale = 0.1f;
    private const float MaxLookDeltaPerFrame = 50f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = cameraRoot.GetComponentInChildren<Camera>(true);
        cameraBasePosition = cameraRoot.localPosition;
        targetBodyRotation = transform.localRotation;
        targetCameraRotation = cameraRoot.localRotation;
        if (playerCamera != null)
            baseFov = playerCamera.fieldOfView;

        InputActionMap playerMap = inputActions.FindActionMap("Player", true);
        moveAction = playerMap.FindAction("Move", true);
        lookAction = playerMap.FindAction("Look", true);
        sprintAction = playerMap.FindAction("Sprint", true);
        jumpAction = playerMap.FindAction("Jump", true);
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        sprintAction.Enable();
        jumpAction.Enable();
        // İmleç kilitlenirken gelen ilk fare delta'sı kamerayı aşağı çekiyordu.
        framesToSkip = 3;
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        sprintAction.Disable();
        jumpAction.Disable();
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        bool sprinting = false;
        if (InputEnabled)
        {
            Look();
            sprinting = Move();
        }
        else
        {
            horizontalVelocity = Vector3.zero;
            ApplyVertical(false);
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }

        HandleLanding();
        UpdateCameraPosition(sprinting);
        UpdateFovKick(sprinting);
    }

    private void Look()
    {
        Vector2 rawDelta = lookAction.ReadValue<Vector2>();
        if (framesToSkip > 0)
        {
            framesToSkip--;
            // SpawnOverride dönüşü Start'ta değiştirir; hedef bakış o andaki yönden başlasın.
            targetBodyRotation = transform.localRotation;
            targetCameraRotation = cameraRoot.localRotation;
            return;
        }

        Vector2 delta = Vector2.ClampMagnitude(rawDelta, MaxLookDeltaPerFrame) * LegacyMouseAxisScale;
        targetBodyRotation *= Quaternion.Euler(0f, delta.x * xSensitivity, 0f);
        targetCameraRotation = ClampPitch(targetCameraRotation * Quaternion.Euler(-delta.y * ySensitivity, 0f, 0f));

        if (smooth)
        {
            float t = smoothTime * Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetBodyRotation, t);
            cameraRoot.localRotation = Quaternion.Slerp(cameraRoot.localRotation, targetCameraRotation, t);
        }
        else
        {
            transform.localRotation = targetBodyRotation;
            cameraRoot.localRotation = targetCameraRotation;
        }
    }

    private Quaternion ClampPitch(Quaternion q)
    {
        q.x /= q.w;
        q.y /= q.w;
        q.z /= q.w;
        q.w = 1f;
        float pitch = Mathf.Clamp(2f * Mathf.Rad2Deg * Mathf.Atan(q.x), minPitch, maxPitch);
        q.x = Mathf.Tan(0.5f * Mathf.Deg2Rad * pitch);
        return q;
    }

    /// <returns>Koşuyor mu (sadece ileri giderken).</returns>
    private bool Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        bool sprinting = sprintAction.IsPressed() && input.y > 0f;
        float speed = sprinting ? runSpeed : walkSpeed;
        Vector3 wishDir = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f);

        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wishDir * speed, acceleration * Time.deltaTime * speed);

        ApplyVertical(jumpAction.WasPressedThisFrame());
        CollisionFlags flags = controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        // Tavana çarpınca yukarı hız sıfırlanır, yoksa kafa tavana yapışık kalır.
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;

        UpdateHeadBob(speed, sprinting);
        return sprinting;
    }

    private void ApplyVertical(bool jump)
    {
        if (controller.isGrounded)
        {
            verticalVelocity = jump ? jumpSpeed : -stickToGroundForce;
        }
        else
        {
            verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        }
    }

    private void HandleLanding()
    {
        bool grounded = controller.isGrounded;
        if (grounded && !wasGrounded)
            landBobTimer = 0f;
        wasGrounded = grounded;
    }

    private void UpdateHeadBob(float speed, bool sprinting)
    {
        float moveSpeed = horizontalVelocity.magnitude;
        if (!useHeadBob || !controller.isGrounded || moveSpeed < 0.05f)
        {
            bobCycleX = bobCycleY = 0f;
            bobOffset = Vector2.Lerp(bobOffset, Vector2.zero, bobReturnSpeed * Time.deltaTime);
            return;
        }

        float bobSpeed = moveSpeed + speed * (sprinting ? runstepLengthen : 1f);
        float curveLength = bobCurve[bobCurve.length - 1].time;
        float step = bobSpeed * Time.deltaTime / stepInterval;
        bobCycleX = Mathf.Repeat(bobCycleX + step, curveLength);
        bobCycleY = Mathf.Repeat(bobCycleY + step * verticalToHorizontalRatio, curveLength);
        bobOffset = new Vector2(bobCurve.Evaluate(bobCycleX) * horizontalBobRange,
                                bobCurve.Evaluate(bobCycleY) * verticalBobRange);
    }

    private void UpdateCameraPosition(bool sprinting)
    {
        if (!InputEnabled)
            bobOffset = Vector2.Lerp(bobOffset, Vector2.zero, bobReturnSpeed * Time.deltaTime);

        cameraRoot.localPosition = cameraBasePosition
                                   + new Vector3(bobOffset.x, bobOffset.y - LandBobOffset(), 0f);
    }

    // İnişte kamera jumpBobDuration içinde aşağı iner, aynı sürede geri çıkar.
    private float LandBobOffset()
    {
        if (landBobTimer >= jumpBobDuration * 2f)
            return 0f;

        landBobTimer += Time.deltaTime;
        float t = landBobTimer / jumpBobDuration;
        return t < 1f ? Mathf.Lerp(0f, jumpBobAmount, t) : Mathf.Lerp(jumpBobAmount, 0f, t - 1f);
    }

    private void UpdateFovKick(bool sprinting)
    {
        if (!useFovKick || playerCamera == null)
            return;

        bool kick = sprinting && horizontalVelocity.sqrMagnitude > 0.01f;
        float rate = kick ? 1f / fovTimeToIncrease : 1f / fovTimeToDecrease;
        fovKick01 = Mathf.MoveTowards(fovKick01, kick ? 1f : 0f, rate * Time.deltaTime);
        playerCamera.fieldOfView = baseFov + fovCurve.Evaluate(fovKick01) * fovIncrease;
    }
}
