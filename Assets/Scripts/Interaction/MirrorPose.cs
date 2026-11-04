using System.Collections;
using UnityEngine;

/// <summary>
/// Ayna karşısında poz verme. F ile oyuncu aynanın önüne geçer, aynaya döner, kamera camın ortasına eğilir ve
/// poz animasyonu oynar; oyuncu kendini birinci şahıstan aynanın gerçek yansımasında (<see cref="PlanarMirror"/>) görür.
/// Tekrar F ile poz biter.
/// Ayna yüzeyinin +z ekseni odaya bakmalı; objede ya da altında collider olmalı.
/// </summary>
public class MirrorPose : Interactable
{
    // Ayna boy aynası, önünde durulur.
    protected override float DefaultMaxDistance => 2f;

    [SerializeField] private string stopPrompt = "Poz vermeyi bırak";

    [Header("Konum")]
    [Tooltip("Ayna yüzeyi (camın ortası, +z odaya bakar). Boşsa bu obje.")]
    [SerializeField] private Transform surface;
    [Tooltip("Oyuncunun aynadan uzaklığı (m, zeminde). Camın tamamı görüş açısına sığacak kadar uzak olmalı.")]
    [SerializeField] private float standDistance = 1.6f;
    [SerializeField] private float moveDuration = 0.5f;

    [Header("Ses")]
    [Tooltip("Poz vermeye başlarken çalar.")]
    [SerializeField] private AudioClip poseSound;

    private PlayerInteractor posingPlayer;
    private bool busy;
    private bool playedDefeat;

    public override string Prompt => posingPlayer != null ? stopPrompt : base.Prompt;

    private void Awake()
    {
        if (surface == null)
            surface = transform;
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
        // Kamera eğilince ışın aynaya bakmayabilir; çıkış için F her durumda aynaya gitsin.
        interactor.Zone = this;
        controller.Frozen = true;
        if (cc != null)
            cc.enabled = false;

        Transform player = interactor.transform;
        Transform cameraRoot = controller.CameraRoot;
        Vector3 forward = Vector3.ProjectOnPlane(surface.forward, Vector3.up).normalized;
        Vector3 standPos = surface.position + forward * standDistance;
        standPos.y = player.position.y;
        Quaternion standRot = Quaternion.LookRotation(-forward, Vector3.up);

        // Duruş noktasındaki kameradan camın ortasına bakış açısı.
        Vector3 eye = standPos + Vector3.up * cameraRoot.localPosition.y;
        Vector3 toMirror = surface.position - eye;
        float pitch = -Mathf.Atan2(toMirror.y, Vector3.ProjectOnPlane(toMirror, Vector3.up).magnitude) * Mathf.Rad2Deg;
        Quaternion cameraEndRot = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 startPos = player.position;
        Quaternion startRot = player.rotation;
        Quaternion cameraStartRot = cameraRoot.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / moveDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            player.SetPositionAndRotation(Vector3.Lerp(startPos, standPos, k), Quaternion.Slerp(startRot, standRot, k));
            cameraRoot.localRotation = Quaternion.Slerp(cameraStartRot, cameraEndRot, k);
            yield return null;
        }
        player.SetPositionAndRotation(standPos, standRot);
        cameraRoot.localRotation = cameraEndRot;

        // 4. gün sabahı aynadaki kendini beğenmez; günün sonundaki poz normaldir.
        playedDefeat = DayRoutine.MorningMirrorPending;
        if (playedDefeat)
            body.PlayDefeat();
        else
            body.PlayPose();
        GameAudio.PlayAt(poseSound, surface.position);
        busy = false;
    }

    private IEnumerator StopPose()
    {
        busy = true;
        PlayerInteractor interactor = posingPlayer;

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
        if (playedDefeat)
        {
            playedDefeat = false;
            DayRoutine.NotifyMorningMirrorSeen();
            yield break;
        }
        // Günün kıyafetiyle poz verildiyse uyku gelir (esneme), yatak açılır.
        DayRoutine.NotifyPoseEnded();
    }
}
