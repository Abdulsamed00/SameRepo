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
    [Tooltip("Yürürken. Koşarken hız oranında küçülür.")]
    [SerializeField] private float horizontalBobRange = 0.04f;
    [Tooltip("Yürürken. Koşarken hız oranında küçülür.")]
    [SerializeField] private float verticalBobRange = 0.05f;
    [SerializeField] private float verticalToHorizontalRatio = 2f;
    [SerializeField] private AnimationCurve bobCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f),
        new Keyframe(1.5f, -1f), new Keyframe(2f, 0f));
    [Tooltip("Durunca kameranın merkeze dönüş hızı.")]
    [SerializeField] private float bobReturnSpeed = 10f;
    [Tooltip("Varsa sallanma ritmi gövdenin yürüme animasyonundan alınır (her adımda bir iniş). Boşsa alt objelerde aranır.")]
    [SerializeField] private PlayerBody body;
    [Tooltip("Yürüme klibinde kafanın en alçak olduğu an ile eğrinin dibi arasındaki faz farkı (adım döngüsü oranı).")]
    [SerializeField] private float verticalBobPhase = 0.35f;
    [Tooltip("Klip döngüsünde kafanın en sağda olduğu an ile eğrinin tepesi arasındaki faz farkı.")]
    [SerializeField] private float horizontalBobPhase = 0.25f;

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

    /// <summary>Oyuncunun input action asset'i (duraklatma girdiyi buradan kapatır).</summary>
    public InputActionAsset InputActions => inputActions;

    /// <summary>
    /// True iken kontrolcü hiçbir şey yapmaz (hareket, yer çekimi, bakış, kamera konumu).
    /// Oturma gibi oyuncuyu ve kamerayı başka bir scriptin taşıdığı durumlar için.
    /// </summary>
    public bool Frozen { get; set; }

    /// <summary>Yatay hız (m/sn). Karakter animasyonu bunu okur.</summary>
    public float HorizontalSpeed => horizontalVelocity.magnitude;

    /// <summary>Yatay hız vektörü (dünya). Gövde bununla hareket yönüne döner.</summary>
    public Vector3 HorizontalVelocity => horizontalVelocity;

    /// <summary>Kamera kökü (bakış eğimi bunun yerel dönüşüdür).</summary>
    public Transform CameraRoot => cameraRoot;

    private CharacterController controller;
    private Camera playerCamera;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;

    private Quaternion targetBodyRotation;
    private Quaternion targetCameraRotation;
    private float verticalVelocity;
    private float moveStartFeetY;
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
        if (body == null)
            body = GetComponentInChildren<PlayerBody>(true);

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
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>Gövde/kamera dönüşü dışarıdan değiştirildiyse bakış hedeflerini o anki yöne eşitler.</summary>
    public void SyncLookToTransforms()
    {
        targetBodyRotation = transform.localRotation;
        targetCameraRotation = cameraRoot.localRotation;
    }

    private void Update()
    {
        if (Frozen)
            return;

        bool sprinting = false;
        if (InputEnabled)
        {
            Look();
            sprinting = Move();
        }
        else
        {
            horizontalVelocity = Vector3.zero;
            ApplyVertical();
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
        // Ayarlar ekranı: hassasiyet çarpanı ve Y ekseni ters çevirme.
        delta *= GameSettings.LookSensitivityMultiplier;
        if (GameSettings.InvertY)
            delta.y = -delta.y;
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

        ApplyVertical();
        moveStartFeetY = transform.position.y;
        CollisionFlags flags = controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        // Tavana çarpınca yukarı hız sıfırlanır, yoksa kafa tavana yapışık kalır.
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;

        // CharacterController basamaktan yüksek eşyalara çarpınca (özellikle köşe ve kenarlarda) kendini
        // stepOffset kadar yukarı atıp eşyanın kenarına oturtuyordu: kamera zıplıyor, bazen eşyanın üstüne
        // çıkıyordu (çoğu zaman altında hiçbir şey yokken yan temasla havada kalıyordu). Yükselinen karede
        // oyuncunun altında taşıyan bir yüzey yoksa ya da o yüzey basamak sınırından yüksekse yükselme geri alınır.
        float rise = transform.position.y - moveStartFeetY;
        // Eşik küçük: kenara sıkışınca kare başına birkaç mm'lik yavaş tırmanma da oluyor.
        if (verticalVelocity <= 0f && rise > 0.001f)
        {
            float support = SupportHeight();
            if (float.IsNegativeInfinity(support) || support - moveStartFeetY > controller.stepOffset + 0.02f)
            {
                // Move ile aşağı itmek işe yaramıyor: kapsül eşyanın kenarına sıkışıp yerinde kalıyor.
                Vector3 position = transform.position;
                position.y = moveStartFeetY;
                controller.enabled = false;
                transform.position = position;
                controller.enabled = true;
            }
        }

        UpdateHeadBob(speed, sprinting);
        return sprinting;
    }

    // Kapsülün alt yarım küresinin altındaki ilk temas noktasının yüksekliği (oyuncuyu taşıyan nokta).
    private float SupportHeight()
    {
        float radius = controller.radius * 0.95f;
        Vector3 bottom = transform.position + controller.center + Vector3.down * (controller.height * 0.5f - controller.radius);
        float distance = 0.1f + controller.skinWidth;
        if (Physics.SphereCast(bottom + Vector3.up * 0.05f, radius, Vector3.down, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return float.NegativeInfinity;
    }

    private void ApplyVertical()
    {
        if (controller.isGrounded)
        {
            verticalVelocity = -stickToGroundForce;
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

        float curveLength = bobCurve[bobCurve.length - 1].time;
        if (body != null && body.TryGetWalkCycle(out float cycle))
        {
            // Hızdan hesaplanan ritim (Standard Assets) 5 m/s'ye göreydi; 2 m/s'de kamera saniyede ~0.8 kez inerken
            // bacaklar ~2.4 adım atıyordu. Ritim yürüme klibinden alınır: bir klip döngüsü iki adım, dikey sallanma
            // döngüde iki kez (her adımda bir), yatay bir kez.
            bobCycleX = Mathf.Repeat(cycle + horizontalBobPhase, 1f) * curveLength;
            bobCycleY = Mathf.Repeat(cycle * verticalToHorizontalRatio + verticalBobPhase, 1f) * curveLength;
        }
        else
        {
            float bobSpeed = moveSpeed + speed * (sprinting ? runstepLengthen : 1f);
            float step = bobSpeed * Time.deltaTime / stepInterval;
            bobCycleX = Mathf.Repeat(bobCycleX + step, curveLength);
            bobCycleY = Mathf.Repeat(bobCycleY + step * verticalToHorizontalRatio, curveLength);
        }

        // Kalkışta sallanma faz ortasından başlayıp kamerayı sıçratmasın diye hızla birlikte açılır.
        // Yürüme hızının üstünde ritim hızla birlikte arttığı için genlik aynı oranda küçülür; kameranın
        // inip çıkma hızı koşarken de yürümedeki kadar kalır (koşarken çok hızlı sallanıyordu).
        float amount = Mathf.Clamp01(moveSpeed / walkSpeed) * Mathf.Min(1f, walkSpeed / moveSpeed);
        bobOffset = new Vector2(bobCurve.Evaluate(bobCycleX) * horizontalBobRange,
                                bobCurve.Evaluate(bobCycleY) * verticalBobRange) * amount;
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
