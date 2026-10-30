using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Ayna karşısında poz verme. F ile oyuncu aynanın önüne geçer, aynaya döner ve poz animasyonu oynar;
/// görüntü aynanın kamerasına geçer (yansıma henüz yok, oyuncu kendini aynanın gördüğü gibi görür).
/// Tekrar F ile poz biter ve birinci şahsa dönülür.
/// Ayna yüzeyinin +z ekseni odaya bakmalı; objede ya da altında collider olmalı.
/// </summary>
public class MirrorPose : Interactable
{
    [SerializeField] private string stopPrompt = "Poz vermeyi bırak";

    [Header("Konum")]
    [Tooltip("Ayna yüzeyi (kamera bunun ortasına konur, +z odaya bakar). Boşsa bu obje.")]
    [SerializeField] private Transform surface;
    [Tooltip("Oyuncunun aynadan uzaklığı (m, zeminde). Kamera açısıyla birlikte boydan görünecek şekilde ayarlanır.")]
    [SerializeField] private float standDistance = 1.4f;
    [SerializeField] private float moveDuration = 0.5f;

    [Header("Ayna Görüntüsü")]
    [Tooltip("Aynanın kamerası. Boşsa oluşturulur.")]
    [SerializeField] private Camera mirrorCamera;
    [SerializeField] private float cameraFov = 70f;
    [Tooltip("Kameranın ayna yüzeyinin önünde duracağı mesafe (m).")]
    [SerializeField] private float cameraOffset = 0.05f;
    [Tooltip("Kameranın aşağı eğimi (derece). Karakter ayaklarıyla birlikte kadraja girsin.")]
    [SerializeField] private float cameraPitch = 7f;
    [Tooltip("Görüntüyü yatayda çevirir, ayna gibi görünür.")]
    [SerializeField] private bool flipHorizontally = true;

    [Header("Ses")]
    [Tooltip("Poz vermeye başlarken çalar.")]
    [SerializeField] private AudioClip poseSound;

    private PlayerInteractor posingPlayer;
    private bool busy;

    public override string Prompt => posingPlayer != null ? stopPrompt : base.Prompt;

    private void Awake()
    {
        if (surface == null)
            surface = transform;
        if (mirrorCamera == null)
        {
            mirrorCamera = new GameObject("Ayna_Kamera").AddComponent<Camera>();
            mirrorCamera.transform.SetParent(transform, false);
        }
        mirrorCamera.transform.SetPositionAndRotation(surface.position + surface.forward * cameraOffset,
            Quaternion.LookRotation(surface.forward, Vector3.up) * Quaternion.Euler(cameraPitch, 0f, 0f));
        mirrorCamera.fieldOfView = cameraFov;
        mirrorCamera.nearClipPlane = 0.05f;
        mirrorCamera.enabled = false;
    }

    private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        if (busy)
            return false;
        if (posingPlayer != null)
            return posingPlayer == interactor;
        PlayerSitController sitter = interactor.GetComponent<PlayerSitController>();
        return sitter == null || !sitter.IsSeated;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (busy)
            return;
        StartCoroutine(posingPlayer == null ? StartPose(interactor) : StopPose());
    }

    private IEnumerator StartPose(PlayerInteractor interactor)
    {
        PlayerBody body = interactor.GetComponentInChildren<PlayerBody>();
        FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
        CharacterController cc = interactor.GetComponent<CharacterController>();
        if (body == null || controller == null)
            yield break;

        busy = true;
        posingPlayer = interactor;
        // Kamera aynaya geçince ışın aynaya bakmayabilir; çıkış için F her durumda aynaya gitsin.
        interactor.Zone = this;
        controller.Frozen = true;
        if (cc != null)
            cc.enabled = false;

        Transform player = interactor.transform;
        Vector3 forward = Vector3.ProjectOnPlane(surface.forward, Vector3.up).normalized;
        Vector3 standPos = surface.position + forward * standDistance;
        standPos.y = player.position.y;
        Quaternion standRot = Quaternion.LookRotation(-forward, Vector3.up);

        Vector3 startPos = player.position;
        Quaternion startRot = player.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / moveDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            player.SetPositionAndRotation(Vector3.Lerp(startPos, standPos, k), Quaternion.Slerp(startRot, standRot, k));
            yield return null;
        }
        player.SetPositionAndRotation(standPos, standRot);

        body.PlayPose();
        GameAudio.PlayAt(poseSound, surface.position);
        mirrorCamera.enabled = true;
        Camera playerCamera = Camera.main;
        if (playerCamera != null)
            mirrorCamera.depth = playerCamera.depth + 1f;
        busy = false;
    }

    private IEnumerator StopPose()
    {
        busy = true;
        PlayerInteractor interactor = posingPlayer;
        mirrorCamera.enabled = false;

        PlayerBody body = interactor.GetComponentInChildren<PlayerBody>();
        if (body != null)
            body.StopPose();

        // Poz klibinden locomotion'a geçiş bitene kadar oyuncu beklesin.
        yield return new WaitForSeconds(0.3f);

        CharacterController cc = interactor.GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = true;
        FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
        controller.SyncLookToTransforms();
        controller.Frozen = false;
        if (interactor.Zone == this)
            interactor.Zone = null;
        posingPlayer = null;
        busy = false;
        // Günün kıyafetiyle poz verildiyse uyku gelir (esneme), yatak açılır.
        DayRoutine.NotifyPoseEnded();
    }

    // Yatay çevirme için projeksiyon aynalanır; üçgen sırası tersine döndüğü için culling de ters çevrilir.
    private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam != mirrorCamera || !flipHorizontally)
            return;
        cam.ResetProjectionMatrix();
        cam.projectionMatrix = cam.projectionMatrix * Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
        GL.invertCulling = true;
        RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    private void OnEndCamera(ScriptableRenderContext context, Camera cam)
    {
        if (cam != mirrorCamera)
            return;
        GL.invertCulling = false;
        RenderPipelineManager.endCameraRendering -= OnEndCamera;
    }
}
