using System.Collections;
using UnityEngine;

/// <summary>
/// Oyun sonu (GDD): beş ürün de alınıp giyilmiş, son poz verilmiş, oyuncu evden sokağa çıkmıştır
/// (<see cref="DayRoutine.FinalReady"/>). Sokakta biraz yürüyünce oyuncu durur, görünüm birinci şahıstan üçüncü
/// şahsa geçer (kamera kafadan ayrılıp karakterin arkasına çıkar). Sonra ekran kararır, özlü sözlü jenerik akar,
/// ana menüye dönülür ve oyun sıfırlanır. StreetScene'de bir objeye eklenir; final hazır değilse kendini kapatır.
/// </summary>
public class FinalSequence : MonoBehaviour
{
    [Header("Başlangıç")]
    [Tooltip("Oyuncu doğduğu yerden bu kadar uzaklaşınca kamera uzaklaşmaya başlar (m).")]
    [SerializeField] private float triggerDistance = 14f;

    [Header("Kamera")]
    [Tooltip("Oyuncu durduktan sonra üçüncü şahsa geçmeden önce beklenen süre (sn).")]
    [SerializeField] private float stopDelay = 1.2f;
    [SerializeField] private float pullBackDuration = 4.5f;
    [Tooltip("Son kare: oyuncunun bu kadar üstünde (m).")]
    [SerializeField] private float finalHeight = 2.2f;
    [Tooltip("Son kare: oyuncunun bu kadar gerisinde (m).")]
    [SerializeField] private float finalBack = 4.5f;
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
        player.InputEnabled = false; // durur, gövde Idle'a döner
        if (interactor != null)
            interactor.enabled = false;
        yield return new WaitForSeconds(stopDelay);
        player.Frozen = true;

        Camera cam = player.GetComponentInChildren<Camera>();
        yield return PullBack(cam);
        yield return new WaitForSeconds(holdDuration);
        yield return Credits();

        DayRoutine.ResetAll();
        SceneLoader.Load(menuScene);
    }

    // Kamera oyuncunun başından ayrılıp yukarı-geriye doğru çıkar, bakışı oyuncuda kalır.
    private IEnumerator PullBack(Camera cam)
    {
        Transform camTransform = cam.transform;
        camTransform.SetParent(null, true);

        // Birinci şahısta gizlenen kafa (saç, gözlük) artık görünsün, kafasız kopya gizlensin.
        PlayerBody body = player.GetComponentInChildren<PlayerBody>();
        if (body != null)
            body.SetThirdPerson(true);

        Transform target = player.transform;
        Vector3 flatForward = camTransform.forward;
        flatForward.y = 0f;
        Vector3 back = -flatForward.normalized;

        Vector3 startPos = camTransform.position;
        Quaternion startRot = camTransform.rotation;
        float startFov = cam.fieldOfView;
        Vector3 endPos = target.position + Vector3.up * finalHeight + back * finalBack;

        for (float t = 0f; t < pullBackDuration; t += Time.deltaTime)
        {
            float k = t / pullBackDuration;
            float smooth = Mathf.SmoothStep(0f, 1f, k);
            // Önce yükselir (başın içinden çıkar), sonra geriye açılır.
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(k));
            Vector3 pos = Vector3.Lerp(startPos, endPos, smooth);
            pos.y = Mathf.Lerp(startPos.y, endPos.y, rise);
            camTransform.position = pos;

            Vector3 look = target.position + Vector3.up * 1.0f - pos;
            Quaternion lookRot = Quaternion.LookRotation(look, Vector3.up);
            camTransform.rotation = Quaternion.Slerp(startRot, lookRot, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 3f)));
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
