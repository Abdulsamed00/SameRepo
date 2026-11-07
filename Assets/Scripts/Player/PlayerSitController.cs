using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Oyuncunun <see cref="Seat"/>'e oturması ve kalkması. Oturma sırasında FirstPersonController dondurulur,
/// oyuncu oturma noktasına kayar, kamera oturan göz yüksekliğine iner ve gövde animasyonu oynar.
/// Otururken fareyle sınırlı etrafa bakılabilir; E (Sit action'ı) ile kalkılır.
/// </summary>
public class PlayerSitController : MonoBehaviour
{
    private enum State { Standing, SittingDown, Seated, StandingUp }

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private PlayerBody body;

    [Header("Oturan Kamera")]
    [Tooltip("Otururken kameranın oyuncu köküne göre yüksekliği (m).")]
    [SerializeField] private float seatedCameraHeight = 1.18f;
    [Tooltip("Otururken kameranın oyuncu köküne göre ileri kayması (m).")]
    [SerializeField] private float seatedCameraForward = -0.09f;
    [SerializeField] private float seatedYawLimit = 70f;
    [SerializeField] private float seatedMinPitch = -60f;
    [SerializeField] private float seatedMaxPitch = 45f;
    [Tooltip("FirstPersonController hassasiyetiyle aynı ölçek (piksel delta x 0.1 x hassasiyet).")]
    [SerializeField] private float lookSensitivity = 2f;

    [Header("Süreler (animasyon yoksa)")]
    [SerializeField] private float fallbackDuration = 1.2f;

    private FirstPersonController controller;
    private CharacterController characterController;
    private PlayerInteractor interactor;
    private InputAction sitAction;
    private InputAction lookAction;

    private State state = State.Standing;
    private Seat seat;
    private Transform sitPoint;
    private Vector3 standingCameraPosition;
    private float seatedYaw;
    private float seatedPitch;

    public bool IsSeated => state != State.Standing;
    // Elde eşya (kumanda) varken de oturulur: oturup TV açılabilsin. Eşya sağ avuçta kalır, G ile yerine bırakılır.
    public bool CanSit => state == State.Standing;

    /// <summary>True iken oturan oyuncu girdiyi dinlemez (E ile kalkmaz, fareyle bakmaz). Ana menü: karakter kanepede, kontrol oyuncuda değil.</summary>
    public bool ControlsLocked { get; set; }

    /// <summary>Kalkış sürerken 0'dan 1'e ilerler (kalkış bitince 1). Menüdeki kamera geçişi buna bağlı.</summary>
    public float StandProgress { get; private set; }

    private void Awake()
    {
        controller = GetComponent<FirstPersonController>();
        characterController = GetComponent<CharacterController>();
        interactor = GetComponent<PlayerInteractor>();
        if (body == null)
            body = GetComponentInChildren<PlayerBody>(true);
        standingCameraPosition = cameraRoot.localPosition;

        KeyBindings.Ensure(inputActions); // kayıtlı tuş atamaları
        InputActionMap playerMap = inputActions.FindActionMap("Player", true);
        sitAction = playerMap.FindAction("Sit", true);
        lookAction = playerMap.FindAction("Look", true);
    }

    private void OnEnable()
    {
        sitAction.Enable();
        lookAction.Enable();
    }

    private void OnDisable()
    {
        sitAction.Disable();
        lookAction.Disable();
    }

    public void Sit(Seat target)
    {
        if (!CanSit || target == null || target.Occupied)
            return;
        StartCoroutine(SitRoutine(target));
    }

    /// <summary>
    /// Animasyonsuz, doğrudan oturmuş hâlde başlatır (oyun ana menüde oyuncu kanepede otururken açılır).
    /// <paramref name="near"/> en yakın oturma noktasını seçer.
    /// </summary>
    public void SitImmediately(Seat target, Vector3 near)
    {
        if (!CanSit || target == null || target.Occupied)
            return;

        seat = target;
        seat.Occupied = true;
        sitPoint = seat.ClosestSitPoint(near);

        controller.Frozen = true;
        characterController.enabled = false;
        transform.SetPositionAndRotation(sitPoint.position, Quaternion.Euler(0f, sitPoint.eulerAngles.y, 0f));
        cameraRoot.localPosition = new Vector3(0f, seatedCameraHeight, seatedCameraForward);
        cameraRoot.localRotation = Quaternion.identity;
        seatedYaw = seatedPitch = 0f;

        if (body != null)
            body.PlaySittingIdle(true);
        state = State.Seated;
    }

    /// <summary>Oturuyorsa koddan ayağa kalkar (E tuşu gerekmez).</summary>
    public void StandUp()
    {
        if (state == State.Seated)
            StartCoroutine(StandRoutine());
    }

    private void Update()
    {
        if (state != State.Seated || ControlsLocked)
            return;

        // Oturma tuşuyla aynı karede kalkmasın diye kalkış sadece Seated durumunda dinlenir.
        if (sitAction.WasPressedThisFrame())
        {
            StartCoroutine(StandRoutine());
            return;
        }

        Vector2 delta = Vector2.ClampMagnitude(lookAction.ReadValue<Vector2>(), 50f) * 0.1f * lookSensitivity
            * GameSettings.LookSensitivityMultiplier;
        if (GameSettings.InvertY)
            delta.y = -delta.y;
        seatedYaw = Mathf.Clamp(seatedYaw + delta.x, -seatedYawLimit, seatedYawLimit);
        seatedPitch = Mathf.Clamp(seatedPitch - delta.y, seatedMinPitch, seatedMaxPitch);
        cameraRoot.localRotation = Quaternion.Euler(seatedPitch, seatedYaw, 0f);
    }

    private IEnumerator SitRoutine(Seat target)
    {
        state = State.SittingDown;
        seat = target;
        seat.Occupied = true;
        sitPoint = seat.ClosestSitPoint(transform.position);

        controller.Frozen = true;
        characterController.enabled = false;

        float duration = body != null ? body.PlaySitDown() : fallbackDuration;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 endPos = sitPoint.position;
        Quaternion endRot = Quaternion.Euler(0f, sitPoint.eulerAngles.y, 0f);

        Vector3 camStartPos = cameraRoot.localPosition;
        Quaternion camStartRot = cameraRoot.localRotation;
        Vector3 camEndPos = new Vector3(0f, seatedCameraHeight, seatedCameraForward);

        // Önce oturma yönüne dönüp geri geri oturur: dönüş ilk %35'te biter.
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, k),
                Quaternion.Slerp(startRot, endRot, Mathf.Clamp01(t / 0.35f)));
            cameraRoot.localPosition = Vector3.Lerp(camStartPos, camEndPos, k);
            cameraRoot.localRotation = Quaternion.Slerp(camStartRot, Quaternion.identity, k);
            yield return null;
        }

        transform.SetPositionAndRotation(endPos, endRot);
        cameraRoot.localPosition = camEndPos;
        cameraRoot.localRotation = Quaternion.identity;
        seatedYaw = seatedPitch = 0f;

        if (body != null)
            body.PlaySittingIdle();
        state = State.Seated;
    }

    private IEnumerator StandRoutine()
    {
        state = State.StandingUp;

        // Otururken kameraya verilen yatay bakış gövdeye aktarılır, görüntü zıplamaz.
        transform.rotation *= Quaternion.Euler(0f, seatedYaw, 0f);
        cameraRoot.localRotation = Quaternion.Euler(seatedPitch, 0f, 0f);

        float duration = body != null ? body.PlayStandUp() : fallbackDuration;

        Vector3 startPos = transform.position;
        Vector3 endPos = seat.StandPosition(sitPoint);
        Vector3 camStartPos = cameraRoot.localPosition;
        Quaternion camStartRot = cameraRoot.localRotation;

        StandProgress = 0f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            StandProgress = t;
            transform.position = Vector3.Lerp(startPos, endPos, k);
            cameraRoot.localPosition = Vector3.Lerp(camStartPos, standingCameraPosition, k);
            cameraRoot.localRotation = Quaternion.Slerp(camStartRot, Quaternion.identity, k);
            yield return null;
        }

        StandProgress = 1f;
        transform.position = endPos;
        cameraRoot.localPosition = standingCameraPosition;
        cameraRoot.localRotation = Quaternion.identity;

        characterController.enabled = true;
        controller.SyncLookToTransforms();
        controller.Frozen = false;
        if (body != null)
            body.PlayLocomotion();

        seat.Occupied = false;
        seat = null;
        sitPoint = null;
        state = State.Standing;
    }
}
