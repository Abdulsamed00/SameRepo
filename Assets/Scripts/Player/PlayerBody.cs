using UnityEngine;

/// <summary>
/// Birinci şahıs oyuncunun görünür gövdesi (AnaKarakter). Animator'daki yürüme/durma geçişini hıza bağlar,
/// oturma/kalkma animasyonlarını <see cref="PlayerSitController"/> için oynatır.
/// Saç ve gözlük "PlayerHead" katmanına alınır ve sadece oyuncunun kendi kamerasından gizlenir
/// (kamera kafanın içinde kalıyor). Ayna vb. başka kameralarda kafa tam görünür.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerBody : MonoBehaviour
{
    private const string HeadLayerName = "PlayerHead";

    // Animator Controller'daki state ve parametre adları (Assets/Anims/AnaKarakterAnims/AnaKarakter.controller).
    private const string LocomotionState = "Locomotion";
    private const string StandToSitState = "StandToSit";
    private const string SittingIdleState = "SittingIdle";
    private const string SitToStandState = "SitToStand";
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int WalkAnimSpeedId = Animator.StringToHash("WalkAnimSpeed");

    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Camera playerCamera;
    [Tooltip("Yürüme klibinin doğal hızı (m/sn). Oyuncu daha hızlıysa klip orantılı hızlanır.")]
    [SerializeField] private float walkClipSpeed = 1.4f;
    [SerializeField] private float speedSmoothing = 12f;
    [SerializeField] private float crossFade = 0.2f;
    [Tooltip("Kalkma klibi (tersten oynatılınca oturma). Süreler buradan okunur.")]
    [SerializeField] private AnimationClip sitToStandClip;
    [Tooltip("Oyuncu kamerasından gizlenecek renderer adları (kafaya ait ayrı parçalar).")]
    [SerializeField] private string[] hiddenRendererNames = { "TopuzSac", "BlondeHair", "Glasses" };

    private Animator animator;
    private float smoothedSpeed;
    private bool overridden;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (controller == null)
            controller = GetComponentInParent<FirstPersonController>();
        if (playerCamera == null && controller != null)
            playerCamera = controller.GetComponentInChildren<Camera>(true);

        int layer = LayerMask.NameToLayer(HeadLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"'{HeadLayerName}' katmanı tanımlı değil, kafa parçaları gizlenemiyor.", this);
            return;
        }

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (System.Array.IndexOf(hiddenRendererNames, r.name) >= 0)
                r.gameObject.layer = layer;
        }

        if (playerCamera != null)
            playerCamera.cullingMask &= ~(1 << layer);
    }

    private void Update()
    {
        float target = overridden || controller == null ? 0f : controller.HorizontalSpeed;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, target, speedSmoothing * Time.deltaTime);
        animator.SetFloat(SpeedId, smoothedSpeed);
        animator.SetFloat(WalkAnimSpeedId, Mathf.Max(1f, smoothedSpeed / walkClipSpeed));
    }

    /// <returns>Oturma süresi (sn).</returns>
    public float PlaySitDown()
    {
        overridden = true;
        // Kalkma klibi tersten (state hızı -1): sondan, yani ayakta pozdan başlar.
        animator.CrossFade(StandToSitState, crossFade, 0, 1f);
        return ClipLength;
    }

    public void PlaySittingIdle()
    {
        overridden = true;
        animator.CrossFadeInFixedTime(SittingIdleState, crossFade);
    }

    /// <returns>Kalkma süresi (sn).</returns>
    public float PlayStandUp()
    {
        overridden = true;
        animator.CrossFadeInFixedTime(SitToStandState, crossFade);
        return ClipLength;
    }

    public void PlayLocomotion()
    {
        overridden = false;
        animator.CrossFadeInFixedTime(LocomotionState, crossFade);
    }

    private float ClipLength => sitToStandClip != null ? sitToStandClip.length : 1.2f;
}
