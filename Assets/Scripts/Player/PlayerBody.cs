using UnityEngine;

/// <summary>
/// Birinci şahıs oyuncunun görünür gövdesi (AnaKarakter). Yürüme animasyonunu hıza bağlar.
/// Saç ve gözlük "PlayerHead" katmanına alınır ve sadece oyuncunun kendi kamerasından gizlenir
/// (kamera kafanın içinde kalıyor). Ayna vb. başka kameralarda kafa tam görünür.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerBody : MonoBehaviour
{
    private const string HeadLayerName = "PlayerHead";

    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float animationSpeedPerMeter = 0.75f;
    [SerializeField] private float speedSmoothing = 12f;
    [Tooltip("Oyuncu kamerasından gizlenecek renderer adları (kafaya ait ayrı parçalar).")]
    [SerializeField] private string[] hiddenRendererNames = { "Hair", "TopuzSac", "Glasses" };

    private Animator animator;
    private float smoothedSpeed;

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
        float target = controller != null ? controller.HorizontalSpeed : 0f;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, target, speedSmoothing * Time.deltaTime);
        // Durunca poz donar, yürürken hızla orantılı oynar.
        animator.speed = smoothedSpeed < 0.1f ? 0f : Mathf.Max(0.5f, smoothedSpeed * animationSpeedPerMeter);
    }
}
