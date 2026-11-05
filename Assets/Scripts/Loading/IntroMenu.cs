using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menü (HouseScene içinde, 01.11). Oyun evde, oyuncu kanepede otururken açılır: kamera üçüncü şahıs bir noktadan
/// (<c>cameraPoint</c>) oturma odasını ve karakteri gösterir, solda menü durur (arayüz koddan kurulur, UI paketi).
/// OYNA'ya basınca arayüz kaybolur, karakter kanepeden kalkar ve kamera, üçüncü şahıstan oyuncunun gözüne
/// (birinci şahıs) uçar; kalkış bitince oyun kontrolü oyuncuya geçer.
/// Menüye <c>SceneLoader.Load("MainMenu")</c> ile gelinir (açılış, duraklatma menüsünden ya da finalden dönüş).
/// HouseScene normal yüklenirse (eve dönüş, editörde doğrudan Play) bu obje kendini kaldırır.
/// Ayarlar ve Hazırlayanlar aynı ekranın üstünde açılır.
/// </summary>
// Kamera geçişi, kontrolcünün ve gövdenin o karedeki işinden sonra yazılsın.
[DefaultExecutionOrder(200)]
public class IntroMenu : MonoBehaviour
{
    [SerializeField] private Seat seat;
    [Tooltip("Menüdeki üçüncü şahıs kameranın konumu ve yönü (sahnede elle ayarlanır).")]
    [SerializeField] private Transform cameraPoint;
    [SerializeField] private float cameraFov = 40f;
    [SerializeField] private AudioClip clickSound;
    [Tooltip("Menü açıkken çalan müzik (OYNA'ya basınca söner).")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField, Range(0f, 1f)] private float menuMusicVolume = 0.5f;
    [Tooltip("Boşsa Resources/UI/Arkaplan/AnaMenu kullanılır (ayarlar ekranının bulanık arka planı).")]
    [SerializeField] private Texture background;

    [Header("Kalkış ve kamera geçişi")]
    [Tooltip("OYNA'ya basınca arayüzün sönme süresi (sn).")]
    [SerializeField] private float uiFadeDuration = 0.5f;
    [Tooltip("Kamera yolunun oyuncunun yanına açılması (m): kafanın yanından geçip yüzün önüne gelir.")]
    [SerializeField] private float arcWidth = 0.9f;
    [Tooltip("Kamera yolunun kontrol noktasının gözden geriye kayması (m).")]
    [SerializeField] private float arcBack = 0.4f;
    [SerializeField] private float swayAmount = 0.03f;

    private Canvas canvas;
    private CanvasGroup root;
    private CanvasGroup front;
    private MenuList menu;
    private RenderTexture blurred;
    private bool leaving;

    private FirstPersonController player;
    private PlayerSitController sitter;
    private PlayerBody body;
    private PlayerInteractor interactor;
    private Camera playerCamera;
    private float liveFov;
    private Vector3 camLocalPosition;
    private Quaternion camLocalRotation;
    private bool cameraOverridden;
    private AudioSource musicSource;
    private float blend;
    private float side = 1f;

    private void Start()
    {
        if (!SceneLoader.ConsumeMenuPending())
        {
            Destroy(gameObject);
            return;
        }

        player = FindFirstObjectByType<FirstPersonController>();
        if (player == null || seat == null || cameraPoint == null)
        {
            Debug.LogWarning("IntroMenu: oyuncu, koltuk ya da kamera noktası eksik, menü açılmadı.", this);
            Destroy(gameObject);
            return;
        }

        sitter = player.GetComponent<PlayerSitController>();
        interactor = player.GetComponent<PlayerInteractor>();
        body = player.GetComponentInChildren<PlayerBody>(true);
        playerCamera = player.CameraRoot.GetComponentInChildren<Camera>(true);
        liveFov = playerCamera.fieldOfView;
        camLocalPosition = playerCamera.transform.localPosition;
        camLocalRotation = playerCamera.transform.localRotation;

        // Finalde jenerik görüntüyü tamamen soldurur; menüye renkli dönülsün.
        Shader.SetGlobalFloat("_Solma", 0f);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UiKit.EnsureEventSystem();

        // Menüde oyuncu oynamaz: etkileşim ve duraklatma kapalı, karakter doğrudan kanepede oturuyor.
        interactor.enabled = false;
        sitter.ControlsLocked = true;
        sitter.SitImmediately(seat, cameraPoint.position);
        body.SetThirdPerson(true);
        side = Vector3.Dot(cameraPoint.position - player.transform.position, player.transform.right) >= 0f ? 1f : -1f;
        cameraOverridden = true;

        Build();
        StartMusic();
    }

    private void StartMusic()
    {
        if (menuMusic == null)
            return;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = menuMusic;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.ignoreListenerPause = true;
        musicSource.volume = menuMusicVolume * GameSettings.Volume(AudioCategory.Music);
        musicSource.Play();
        GameSettings.Changed += ApplyMusicVolume;
    }

    private void ApplyMusicVolume()
    {
        if (musicSource != null)
            musicSource.volume = menuMusicVolume * GameSettings.Volume(AudioCategory.Music);
    }

    private void OnDestroy()
    {
        GameSettings.Changed -= ApplyMusicVolume;
        if (blurred != null)
            blurred.Release();
    }

    private void LateUpdate()
    {
        if (!cameraOverridden || playerCamera == null)
            return;

        // Oyuncu kamerasının gerçek (birinci şahıs) noktası: ebeveyninden hesaplanır, çünkü kameranın kendi
        // dönüşünü her karede biz yazıyoruz.
        Transform cam = playerCamera.transform;
        Vector3 livePosition = cam.parent.TransformPoint(camLocalPosition);
        Quaternion liveRotation = cam.parent.rotation * camLocalRotation;
        Transform bodyRoot = player.transform;

        // Konum: menü noktasından, oyuncunun yanından (kafaya çarpmadan) yüzünün önündeki gerçek kamera noktasına.
        Vector3 start = cameraPoint.position;
        if (blend < 1f)
        {
            Vector3 sway = (cameraPoint.right * Mathf.Sin(Time.time * 0.35f) + cameraPoint.up * Mathf.Sin(Time.time * 0.23f))
                * swayAmount * (1f - blend);
            start += sway;
        }
        Vector3 control = livePosition + bodyRoot.right * (arcWidth * side) - bodyRoot.forward * arcBack;
        control.y += 0.1f;
        float u = 1f - blend;
        Vector3 position = u * u * start + 2f * u * blend * control + blend * blend * livePosition;

        cam.SetPositionAndRotation(position, Quaternion.Slerp(cameraPoint.rotation, liveRotation, blend));
        playerCamera.fieldOfView = Mathf.Lerp(cameraFov, liveFov, blend);
    }

    private void Build()
    {
        canvas = UiKit.CreateCanvas("AnaMenu_UI", 10);
        root = canvas.gameObject.AddComponent<CanvasGroup>();
        Transform rootTransform = canvas.transform;

        if (background == null)
            background = Resources.Load<Texture>("UI/Arkaplan/AnaMenu");
        UiKit.CreateFullscreen("Karartma", rootTransform, UiKit.WithAlpha(UiKit.Dim, 0.18f));

        RectTransform frontRect = UiKit.CreateRect("On", rootTransform);
        UiKit.Stretch(frontRect);
        front = frontRect.gameObject.AddComponent<CanvasGroup>();

        Image strip = UiKit.CreateImage("Sol_Serit", frontRect, UiKit.WithAlpha(UiKit.Dim, 0.5f));
        strip.rectTransform.anchorMin = Vector2.zero;
        strip.rectTransform.anchorMax = new Vector2(0f, 1f);
        strip.rectTransform.pivot = new Vector2(0f, 0.5f);
        strip.rectTransform.sizeDelta = new Vector2(690f, 0f);
        strip.rectTransform.anchoredPosition = Vector2.zero;

        menu = MenuList.Create(frontRect);
        menu.ClickSound = clickSound;
        menu.Add("Oyna", OnPlayClicked);
        menu.Add("Ayarlar", OpenSettings);
        menu.Add("Hazırlayanlar", OpenCredits);
        menu.Add("Çıkış", OnQuitClicked);
        RectTransform menuRect = (RectTransform)menu.transform;
        UiKit.Place(menuRect, Vector2.zero, new Vector2(144f, 168f), new Vector2(560f, menu.Height));

        TextMeshProUGUI logo = UiKit.CreateText("Logo", frontRect, "SAME", UiKit.FontKind.Light, 81f, 55f, UiKit.White,
            TextAlignmentOptions.BottomLeft);
        UiKit.Place(logo.rectTransform, Vector2.zero, new Vector2(144f, 168f + menu.Height + 84f), new Vector2(800f, 100f));

        TextMeshProUGUI version = UiKit.CreateText("Surum", frontRect, "v" + Application.version, UiKit.FontKind.Regular, 16.5f, 2f,
            UiKit.Secondary, TextAlignmentOptions.BottomRight);
        UiKit.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-72f, 56f), new Vector2(200f, 24f));
    }

    private void SetFrontVisible(bool visible)
    {
        front.alpha = visible ? 1f : 0f;
        front.blocksRaycasts = visible;
        menu.Interactive = visible;
    }

    public void OnPlayClicked()
    {
        if (leaving)
            return;
        leaving = true;
        menu.Interactive = false;
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator FadeOutMusic(float duration)
    {
        if (musicSource == null)
            yield break;
        GameSettings.Changed -= ApplyMusicVolume;
        float start = musicSource.volume;
        for (float t = 0f; t < duration && musicSource != null; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = start * (1f - t / duration);
            yield return null;
        }
        if (musicSource != null)
            musicSource.Stop();
    }

    private IEnumerator PlayRoutine()
    {
        // İmleç tıklama anında kilitlenir: kilitlenirken gelen ilk fare hareketi kalkış bitmeden, kontrolcü
        // donukken tüketilir (yoksa kontrol geçince kamerayı çekiyordu).
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        for (float t = 0f; t < uiFadeDuration; t += Time.unscaledDeltaTime)
        {
            root.alpha = 1f - Mathf.Clamp01(t / uiFadeDuration);
            yield return null;
        }
        Destroy(canvas.gameObject);
        StartCoroutine(FadeOutMusic(1.5f));

        // Karakter kalkar, kamera üçüncü şahıstan gözüne uçar; ikisi de kalkış ilerlemesine bağlı.
        sitter.StandUp();
        while (sitter.IsSeated)
        {
            blend = Mathf.SmoothStep(0f, 1f, sitter.StandProgress);
            yield return null;
        }

        cameraOverridden = false;
        playerCamera.transform.SetLocalPositionAndRotation(camLocalPosition, camLocalRotation);
        playerCamera.fieldOfView = liveFov;
        body.SetThirdPerson(false);
        sitter.ControlsLocked = false;
        interactor.enabled = true;
        Destroy(gameObject);
    }

    private void OpenSettings()
    {
        if (blurred == null && background != null)
            blurred = UiBlur.Blur(background);
        SetFrontVisible(false);
        SettingsView.Open(canvas.transform, blurred, clickSound, () => SetFrontVisible(true));
    }

    private void OpenCredits()
    {
        SetFrontVisible(false);
        CreditsView view = null;
        view = CreditsView.Create(canvas.transform, 0.55f, clickSound, () =>
        {
            Destroy(view.gameObject);
            SetFrontVisible(true);
        });
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
