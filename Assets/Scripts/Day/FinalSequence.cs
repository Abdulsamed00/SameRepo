using System.Collections;
using UnityEngine;

/// <summary>
/// Oyun sonu (GDD): beş ürün de alınıp giyilmiş, son poz verilmiş, oyuncu evden sokağa çıkmıştır
/// (<see cref="DayRoutine.FinalReady"/>). Ana caddeye çıkınca kontrol alınır, karakter kendiliğinden caddenin
/// ortasına yürür ve caddeye (mağazaya doğru) döner; kalabalık (hepsi aynı ürünlerle) onun çevresine toplanır
/// (<see cref="NpcCrowd.BeginFinale"/>). Görünüm birinci şahıstan üçüncü şahsa geçer: kamera kafadan
/// ayrılıp karakterin önüne ve yukarısına çıkar, ona tepeden bakar. Sonra ekran kararır, özlü sözlü jenerik akar,
/// ana menüye dönülür ve oyun sıfırlanır. StreetScene'de bir objeye eklenir; final hazır değilse kendini kapatır.
/// </summary>
public class FinalSequence : MonoBehaviour
{
    [Header("Başlangıç")]
    [Tooltip("Oyuncu doğduğu yerden bu kadar uzaklaşınca (ara sokaktan ana caddeye çıkınca) final başlar (m).")]
    [SerializeField] private float triggerDistance = 8f;

    [Header("Caddenin ortasına yürüme")]
    [Tooltip("Caddenin orta çizgisi (dünya x).")]
    [SerializeField] private float streetCenterX = 0f;
    [Tooltip("Varınca karakterin döndüğü yön (dünya; +z caddenin sonundaki mağaza).")]
    [SerializeField] private Vector3 finalFacing = Vector3.forward;
    [SerializeField] private float walkSpeed = 1.4f;
    [Tooltip("Varınca caddeye dönme süresi (sn).")]
    [SerializeField] private float turnDuration = 1.2f;
    [Tooltip("Yürüme bu süreyi aşarsa (bir şeye takıldıysa) olduğu yerde durur (sn).")]
    [SerializeField] private float maxWalkDuration = 15f;

    [Header("Kamera")]
    [Tooltip("Karakter durduktan sonra üçüncü şahsa geçmeden önce beklenen süre (sn).")]
    [SerializeField] private float stopDelay = 1.2f;
    [SerializeField] private float pullBackDuration = 5f;
    [Tooltip("Son kare: karakterin bu kadar üstünde (m).")]
    [SerializeField] private float finalHeight = 3.5f;
    [Tooltip("Son kare: karakterin bu kadar önünde (m).")]
    [SerializeField] private float finalFront = 4f;
    [Tooltip("Kameranın baktığı nokta: karakterin kökünden bu kadar yukarı (m).")]
    [SerializeField] private float lookHeight = 1.2f;
    [SerializeField] private float finalFieldOfView = 50f;
    [SerializeField] private float holdDuration = 3f;

    [Header("Jenerik")]
    [Tooltip("Görüntünün solup jeneriğin belirdiği süre (sn).")]
    [SerializeField] private float fadeToBlackDuration = 2.5f;
    [SerializeField] private float creditsDuration = 12f;
    [SerializeField] private string quote = "Sahip olduğun şeyler, sonunda seni sahiplenir.";
    [SerializeField] private string quoteAuthor = "Fight Club";
    [SerializeField] private string menuScene = "MainMenu";
    [SerializeField] private AudioClip clickSound;

    private FirstPersonController player;
    private Vector3 startPosition;
    private bool started;

    private void Start()
    {
        if (!DayRoutine.FinalReady)
        {
            enabled = false;
            return;
        }
        player = FindFirstObjectByType<FirstPersonController>();
        if (player != null)
            startPosition = player.transform.position;
    }

    private void Update()
    {
        if (started || player == null)
            return;

        Vector3 offset = player.transform.position - startPosition;
        offset.y = 0f;
        if (offset.magnitude >= triggerDistance)
        {
            started = true;
            StartCoroutine(Run());
        }
    }

    private IEnumerator Run()
    {
        PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
        player.InputEnabled = false;
        if (interactor != null)
            interactor.enabled = false;
        // Kalabalık (hepsi oyuncu gibi giyinmiş) caddenin ortasına, karakterin çevresine toplanır.
        NpcCrowd crowd = FindFirstObjectByType<NpcCrowd>();
        if (crowd != null)
            crowd.BeginFinale(new Vector3(streetCenterX, player.transform.position.y, player.transform.position.z));
        yield return WalkToCenter();
        yield return new WaitForSeconds(stopDelay);
        player.Frozen = true;

        Camera cam = player.GetComponentInChildren<Camera>();
        yield return PullBack(cam);
        yield return new WaitForSeconds(holdDuration);
        yield return Credits();

        DayRoutine.ResetAll();
        SceneLoader.Load(menuScene);
    }

    // Karakter caddenin orta çizgisine yürür (girdi kapalı, FirstPersonController.ScriptedVelocity), bakış yürüdüğü
    // yöne ve ufka döner; varınca caddeye döner. Bir şeye takılırsa maxWalkDuration sonunda olduğu yerde durur.
    private IEnumerator WalkToCenter()
    {
        Transform root = player.transform;
        Transform cameraRoot = player.CameraRoot;
        Vector3 target = new Vector3(streetCenterX, root.position.y, root.position.z);

        for (float time = 0f; time < maxWalkDuration; time += Time.deltaTime)
        {
            Vector3 toTarget = target - root.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < 0.1f)
                break;
            Vector3 direction = toTarget / distance;
            // Son metrede yavaşlar, hedefi geçmez.
            player.ScriptedVelocity = direction * walkSpeed * Mathf.Clamp01(distance / 0.6f + 0.15f);
            root.rotation = Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(direction), 120f * Time.deltaTime);
            cameraRoot.localRotation = Quaternion.RotateTowards(cameraRoot.localRotation, Quaternion.identity, 40f * Time.deltaTime);
            yield return null;
        }
        player.ScriptedVelocity = Vector3.zero;

        Vector3 facing = finalFacing;
        facing.y = 0f;
        Quaternion from = root.rotation;
        Quaternion to = Quaternion.LookRotation(facing.normalized);
        Quaternion camFrom = cameraRoot.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / turnDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            root.rotation = Quaternion.Slerp(from, to, k);
            cameraRoot.localRotation = Quaternion.Slerp(camFrom, Quaternion.identity, k);
            yield return null;
        }
        root.rotation = to;
        cameraRoot.localRotation = Quaternion.identity;
        player.SyncLookToTransforms();
    }

    // Kamera karakterin başından ayrılıp önüne ve yukarısına çıkar, dönüp karaktere tepeden bakar.
    private IEnumerator PullBack(Camera cam)
    {
        Transform camTransform = cam.transform;
        camTransform.SetParent(null, true);

        // Birinci şahısta gizlenen kafa (saç, gözlük) artık görünsün, kafasız kopya gizlensin.
        PlayerBody body = player.GetComponentInChildren<PlayerBody>();
        if (body != null)
            body.SetThirdPerson(true);

        Transform target = player.transform;
        Vector3 forward = target.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 startPos = camTransform.position;
        Quaternion startRot = camTransform.rotation;
        float startFov = cam.fieldOfView;
        Vector3 focus = target.position + Vector3.up * lookHeight;
        Vector3 endPos = target.position + Vector3.up * finalHeight + forward * finalFront;

        for (float t = 0f; t < pullBackDuration; t += Time.deltaTime)
        {
            float k = t / pullBackDuration;
            float smooth = Mathf.SmoothStep(0f, 1f, k);
            // Önce yükselir (başın içinden çıkar), sonra öne açılır; yükselmeden öne giderse yüzün içinden geçer.
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(k));
            Vector3 pos = Vector3.Lerp(startPos, endPos, smooth);
            pos.y = Mathf.Lerp(startPos.y, endPos.y, rise);
            camTransform.position = pos;

            // Bakış ileriden karaktere döner (~180°); kamera başın üstüne çıkarken dönmeye başlar. Yatay ve dikey açı
            // ayrı çevrilir: Slerp yarım turu yuvarlanarak (baş aşağı) yapabiliyor.
            Vector3 startEuler = startRot.eulerAngles;
            Vector3 lookEuler = Quaternion.LookRotation(focus - pos, Vector3.up).eulerAngles;
            float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.6f, k));
            camTransform.rotation = Quaternion.Euler(Mathf.LerpAngle(startEuler.x, lookEuler.x, turn),
                Mathf.LerpAngle(startEuler.y, lookEuler.y, turn), 0f);
            cam.fieldOfView = Mathf.Lerp(startFov, finalFieldOfView, smooth);
            yield return null;
        }
    }

    // Son kare renginden tamamen solar ve kararır, üstünde Hazırlayanlar ekranı belirir (ESC ile atlanır).
    private IEnumerator Credits()
    {
        Canvas canvas = UiKit.CreateCanvas("FinalJenerik", short.MaxValue - 1);
        // ScreenFader perdesinin (short.MaxValue) altında kalır, yükleme geçişi bunun üstünden kararır.
        bool skipped = false;
        CreditsView view = CreditsView.Create(canvas.transform, 0.6f, clickSound, () => skipped = true, quote, quoteAuthor);
        view.Group.alpha = 0f;
        Cursor.visible = false;

        float startFade = Shader.GetGlobalFloat("_Solma");
        for (float t = 0f; t < fadeToBlackDuration && !skipped; t += Time.deltaTime)
        {
            float k = t / fadeToBlackDuration;
            Shader.SetGlobalFloat("_Solma", Mathf.Lerp(startFade, 1f, k));
            view.Group.alpha = k;
            yield return null;
        }
        Shader.SetGlobalFloat("_Solma", 1f);
        view.Group.alpha = 1f;

        for (float t = 0f; t < creditsDuration && !skipped; t += Time.deltaTime)
            yield return null;
    }
}
