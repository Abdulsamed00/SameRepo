using System.Collections;
using UnityEngine;

/// <summary>
/// Sokaktaki kuaför (2. gün) ve klinik (4. gün): o gün (kıyafet alınmadan önce ya da sonra) kapının önünde F ile girilir, ekran
/// kararır, karanlıkta oyuncunun görünümü değişir (<see cref="PlayerLook"/>), ekran aydınlanınca oyuncu dükkânın önündedir.
/// O iş bitmeden eve dönülemez (<see cref="homeZone"/> kilitlenir). Bölge <see cref="SceneZone"/> gibi konumdan çalışır.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BeautyVisit : Interactable
{
    public enum Service
    {
        Hair,
        Surgery
    }

    [SerializeField] private Service service;
    [Tooltip("Bu işin yapıldığı gün (0'dan: 1 = 2. gün).")]
    [SerializeField] private int dayIndex = 1;
    [SerializeField] private string buyFirstPrompt = "";
    [Tooltip("İş bitene kadar kilitlenen eve dönüş bölgesi.")]
    [SerializeField] private SceneZone homeZone;
    [SerializeField] private string homeLockedPrompt = "";
    [Tooltip("Çıkınca oyuncunun durduğu nokta ve yönü.")]
    [SerializeField] private Transform exitPoint;
    [SerializeField] private string captionTop = "BİRKAÇ SAAT SONRA";
    [SerializeField] private string captionMain = "";
    [SerializeField] private float blackDuration = 3f;
    [SerializeField] private AudioClip enterSound;

    private Collider area;
    private PlayerInteractor interactor;
    private bool busy;

    private bool Done => service == Service.Hair ? PlayerLook.HairDyed : PlayerLook.SurgeryDone;
    public bool Pending => GameDay.Index == dayIndex && !Done;

    // 10.11 (kullanıcı: "sarışın olmak için mağazadan dönmesine gerek yok, giderken de girebilsin"): kıyafet alınmadan da
    // girilir; buyFirstPrompt artık kullanılmıyor.
    public override string Prompt => base.Prompt;

    private void Awake() => area = GetComponent<Collider>();

    public override bool CanInteract(PlayerInteractor who) => Pending && !busy;

    public override void Interact(PlayerInteractor who)
    {
        if (!busy && Pending)
            StartCoroutine(Visit(who));
    }

    private void Update()
    {
        if (interactor == null)
            interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor == null || !interactor.isActiveAndEnabled)
            return;

        bool inside = area.bounds.Contains(interactor.transform.position + Vector3.up * 0.5f);
        if (inside && interactor.Zone == null)
            interactor.Zone = this;
        else if (!inside && interactor.Zone == this)
            interactor.Zone = null;
    }

    private IEnumerator Visit(PlayerInteractor who)
    {
        busy = true;
        FirstPersonController controller = who.GetComponent<FirstPersonController>();
        CharacterController cc = who.GetComponent<CharacterController>();
        controller.Frozen = true;
        who.enabled = false;
        GameAudio.PlayAt(enterSound, transform.position);

        yield return ScreenFader.FadeOut(1f);

        if (service == Service.Hair)
            PlayerLook.DyeHair();
        else
            PlayerLook.HaveSurgery();
        if (homeZone != null)
            homeZone.Locked = false;

        if (exitPoint != null)
        {
            cc.enabled = false;
            who.transform.SetPositionAndRotation(exitPoint.position, Quaternion.Euler(0f, exitPoint.eulerAngles.y, 0f));
            controller.CameraRoot.localRotation = Quaternion.identity;
            cc.enabled = true;
            controller.SyncLookToTransforms();
        }

        yield return ScreenFader.Caption(captionTop, captionMain, blackDuration);
        yield return ScreenFader.FadeIn(1.5f);

        controller.Frozen = false;
        who.enabled = true;
        busy = false;
    }
}
