using System.Collections;
using UnityEngine;

/// <summary>
/// Oyun sonu (GDD): beş ürün de alınıp giyilmiş, son poz verilmiş, oyuncu evden sokağa çıkmıştır
/// (<see cref="DayRoutine.FinalReady"/>). Sokakta biraz yürüyünce kamera birinci şahıstan çıkıp yukarı ve geriye
/// doğru uzaklaşır; oyuncu kalabalığın içinde küçülür. Sonra ekran kararır, jenerik akar, ana menüye dönülür
/// ve oyun sıfırlanır. StreetScene'de bir objeye eklenir; final hazır değilse kendini kapatır.
/// </summary>
public class FinalSequence : MonoBehaviour
{
    [Header("Başlangıç")]
    [Tooltip("Oyuncu doğduğu yerden bu kadar uzaklaşınca kamera uzaklaşmaya başlar (m).")]
    [SerializeField] private float triggerDistance = 24f;

    [Header("Kamera")]
    [SerializeField] private float pullBackDuration = 16f;
    [Tooltip("Son kare: oyuncunun bu kadar üstünde (m).")]
    [SerializeField] private float finalHeight = 45f;
    [Tooltip("Son kare: oyuncunun bu kadar gerisinde (m, cadde ekseninde).")]
    [SerializeField] private float finalBack = 18f;
    [SerializeField] private float finalFieldOfView = 45f;
    [SerializeField] private float holdDuration = 2.5f;

    [Header("Jenerik")]
    [Tooltip("Görüntünün solup jeneriğin belirdiği süre (sn).")]
    [SerializeField] private float fadeToBlackDuration = 2.5f;
    [SerializeField] private float creditsDuration = 8f;
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
        player.Frozen = true;
        if (interactor != null)
            interactor.enabled = false;

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

        // Birinci şahısta gizlenen kafa (saç, gözlük) artık görünsün.
        int headLayer = LayerMask.NameToLayer("PlayerHead");
        if (headLayer >= 0)
            cam.cullingMask |= 1 << headLayer;

        Transform target = player.transform;
        Vector3 flatForward = camTransform.forward;
        flatForward.y = 0f;
        Vector3 back = flatForward.z >= 0f ? Vector3.back : Vector3.forward;

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
        CreditsView view = CreditsView.Create(canvas.transform, 0.6f, clickSound, () => skipped = true);
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
