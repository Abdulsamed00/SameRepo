using System.Collections;
using UnityEngine;

/// <summary>
/// Yatak: günün döngüsü tamamlanınca (<see cref="DayRoutine.CanSleep"/>) E ile uyunur. Oyuncu yatağın ayak
/// ucuna oturur, uzanır (kamera kafayı izler), ekran yavaşça kararır; karanlıkta ertesi güne geçilir,
/// ekran açılınca oyuncu koltuktaki kalkma animasyonuyla yataktan kalkar.
/// Esneme sesini de çalar (<see cref="DayRoutine.Sleepy"/>): uyku geldiğini oyuncuya bu söyler.
/// </summary>
public class Bed : Interactable
{
    [Header("Konum")]
    [Tooltip("Oturan oyuncunun kök noktası (zeminde), yüzü yataktan dışarı bakar; uzanınca kafa arkaya, yastığa gider.")]
    [SerializeField] private Transform sitPoint;
    [Tooltip("Otururken kökün zeminden yüksekliği (oturma klibi sandalye yüksekliğine göre, yatak daha yüksek).")]
    [SerializeField] private float seatedHeight = 0.35f;
    [Tooltip("Uzanma klibinin başında kökün yüksekliği (klipteki oturuş oturma klibinden yüksek).")]
    [SerializeField] private float lyingStartHeight = 0.12f;
    [Tooltip("Uzanma bitince kökün oturma noktasına göre yeri: kafa yastığa gelsin diye geri kayar.")]
    [SerializeField] private Vector3 lyingEndOffset = new Vector3(0f, 0.22f, -0.35f);
    [Tooltip("Uzanma klibinde geri kaymanın başladığı ve bittiği an (sn).")]
    [SerializeField] private Vector2 lyingMoveWindow = new Vector2(1.5f, 3.5f);
    [Tooltip("Kalkınca oyuncunun duracağı yer: oturma noktasının bu kadar önü (m).")]
    [SerializeField] private float standDistance = 0.45f;
    [SerializeField] private Vector3 seatedCameraPosition = new Vector3(0f, 1.15f, -0.15f);

    [Header("Uyku")]
    [Tooltip("Ekran karardıktan sonra karanlıkta geçen süre (sn).")]
    [SerializeField] private float sleepDuration = 4.5f;
    [SerializeField] private float wakeFadeDuration = 2.5f;
    [Tooltip("Ekran açılırken kalkmanın başladığı an (açılma süresinin oranı).")]
    [Range(0f, 1f)]
    [SerializeField] private float standUpAt = 0.6f;

    [Header("Ses")]
    [SerializeField] private AudioClip yawnSound;
    [SerializeField] private AudioClip wakeSound;
    [Tooltip("Esneme, onu tetikleyen sesle (giyme, ayna) üst üste binmesin diye bu kadar gecikir.")]
    [SerializeField] private float yawnDelay = 1f;

    [Header("Bildirimler")]
    [SerializeField] private string sleepPrompt = "Uyu";
    [SerializeField] private string notTiredPrompt = "Henüz uykum yok";
    [SerializeField] private string wearFirstPrompt = "Önce yeni kıyafetimi giyeyim";
    [SerializeField] private string poseFirstPrompt = "Önce aynada kendime bakayım";

    private bool busy;

    public override InteractionKey Key => InteractionKey.Secondary;

    public override string Prompt
    {
        get
        {
            if (!DayRoutine.BoughtToday)
                return notTiredPrompt;
            if (!DayRoutine.WornToday)
                return wearFirstPrompt;
            if (!DayRoutine.PosedToday)
                return poseFirstPrompt;
            return sleepPrompt;
        }
    }

    private void OnEnable() => DayRoutine.Sleepy += OnSleepy;

    private void OnDisable() => DayRoutine.Sleepy -= OnSleepy;

    private void Start() => DayRoutine.NotifyArrivedHome();

    private void OnSleepy() => StartCoroutine(Yawn());

    private IEnumerator Yawn()
    {
        yield return new WaitForSeconds(yawnDelay);
        GameAudio.Play2D(yawnSound);
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        // Final sabahı yatakla işimiz yok: evden çıkılır (final).
        if (busy || interactor.IsHolding || DayRoutine.FinalMorning)
            return false;
        PlayerSitController sitter = interactor.GetComponent<PlayerSitController>();
        return sitter == null || !sitter.IsSeated;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (!busy && DayRoutine.CanSleep)
            StartCoroutine(Sleep(interactor));
    }

    private IEnumerator Sleep(PlayerInteractor interactor)
    {
        FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
        CharacterController cc = interactor.GetComponent<CharacterController>();
        PlayerBody body = interactor.GetComponentInChildren<PlayerBody>();
        Camera cam = interactor.GetComponentInChildren<Camera>();
        if (controller == null || body == null || cam == null)
            yield break;

        busy = true;
        Transform player = interactor.transform;
        Transform cameraRoot = cam.transform.parent;
        Vector3 standingCameraPosition = cameraRoot.localPosition;

        controller.Frozen = true;
        cc.enabled = false;
        // Uyurken F/E başka bir şeyi (TV bölgesi, ayna) tetiklemesin.
        interactor.enabled = false;

        // 1) Yatağın ayak ucuna oturur.
        Quaternion sitRotation = Quaternion.Euler(0f, sitPoint.eulerAngles.y, 0f);
        Vector3 seated = sitPoint.position + Vector3.up * seatedHeight;
        float duration = body.PlaySitDown();
        Vector3 startPos = player.position;
        Quaternion startRot = player.rotation;
        Vector3 camStartPos = cameraRoot.localPosition;
        Quaternion camStartRot = cameraRoot.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            player.SetPositionAndRotation(Vector3.Lerp(startPos, seated, k),
                Quaternion.Slerp(startRot, sitRotation, Mathf.Clamp01(t / 0.35f)));
            cameraRoot.localPosition = Vector3.Lerp(camStartPos, seatedCameraPosition, k);
            cameraRoot.localRotation = Quaternion.Slerp(camStartRot, Quaternion.identity, k);
            yield return null;
        }
        player.SetPositionAndRotation(seated, sitRotation);

        // 2) Uzanır, ekran klip boyunca kararır. Kamera kafa kemiğini izler.
        Animator animator = body.GetComponent<Animator>();
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        float lieLength = body.PlayLieDown(out float blend);
        // Uzanırken kamera gövdenin içinden kafaya geçiyor, karakterin içi görünüyordu: ekran açılana kadar gövde çizilmez.
        Renderer[] bodyRenderers = body.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in bodyRenderers)
            r.forceRenderingOff = true;
        StartCoroutine(ScreenFader.FadeOut(lieLength));

        Vector3 lieStart = sitPoint.position + Vector3.up * lyingStartHeight;
        Vector3 lieEnd = sitPoint.position + sitRotation * lyingEndOffset;
        Vector3 camFrom = cameraRoot.position;
        Quaternion camFromRot = cameraRoot.rotation;
        for (float time = 0f; time < lieLength; time += Time.deltaTime)
        {
            // Geçişte klipteki oturuş yüksekliği farkı kökle karşılanır (kalça yerinde kalır).
            float move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lyingMoveWindow.x, lyingMoveWindow.y, time));
            Vector3 lying = Vector3.Lerp(lieStart, lieEnd, move);
            player.position = Vector3.Lerp(seated, lying, Mathf.Clamp01(time / blend));

            float follow = Mathf.SmoothStep(0f, 1f, time / 0.6f);
            Vector3 eye = head.position + head.up * 0.08f + head.forward * 0.12f;
            Quaternion look = Quaternion.LookRotation(head.forward, head.up);
            cameraRoot.SetPositionAndRotation(Vector3.Lerp(camFrom, eye, follow),
                Quaternion.Slerp(camFromRot, look, follow));
            camFrom = cameraRoot.position;
            camFromRot = cameraRoot.rotation;
            yield return null;
        }

        // 3) Karanlıkta ertesi gün olur; oyuncu yatağın ucunda oturur hâle getirilir.
        DayRoutine.StartNextDay();
        body.PlaySittingIdle();
        player.SetPositionAndRotation(seated, sitRotation);
        cameraRoot.SetLocalPositionAndRotation(seatedCameraPosition, Quaternion.identity);
        foreach (Renderer r in bodyRenderers)
            r.forceRenderingOff = false;
        // Gün geçişi yazısı karanlıkta belirir, ekran açılmadan söner.
        StartCoroutine(ScreenFader.Caption("ERTESİ SABAH", $"{DayRoutine.DayNumber}. GÜN", Mathf.Max(1f, sleepDuration - 0.5f)));
        yield return new WaitForSeconds(sleepDuration);

        // 4) Ekran açılır, oyuncu kalkar.
        GameAudio.Play2D(wakeSound);
        StartCoroutine(ScreenFader.FadeIn(wakeFadeDuration));
        yield return new WaitForSeconds(wakeFadeDuration * standUpAt);

        duration = body.PlayStandUp();
        Vector3 standPos = sitPoint.position + sitRotation * Vector3.forward * standDistance;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            player.position = Vector3.Lerp(seated, standPos, k);
            cameraRoot.localPosition = Vector3.Lerp(seatedCameraPosition, standingCameraPosition, k);
            yield return null;
        }
        player.position = standPos;
        cameraRoot.SetLocalPositionAndRotation(standingCameraPosition, Quaternion.identity);

        cc.enabled = true;
        controller.SyncLookToTransforms();
        controller.Frozen = false;
        body.PlayLocomotion();
        interactor.enabled = true;
        busy = false;
    }
}
