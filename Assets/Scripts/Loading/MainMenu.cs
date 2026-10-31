using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ana menü (MainMenu sahnesi). Arayüz koddan kurulur: arka planda evin görüntüsü (Resources/UI/Arkaplan/AnaMenu),
/// sol altta "S A M E" ve Oyna / Ayarlar / Hazırlayanlar / Çıkış. Ayarlar ve Hazırlayanlar aynı ekranın üstünde açılır.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameScene = "HouseScene";
    [SerializeField] private AudioClip clickSound;
    [Tooltip("Boşsa Resources/UI/Arkaplan/AnaMenu kullanılır.")]
    [SerializeField] private Texture background;

    private Canvas canvas;
    private CanvasGroup front;
    private MenuList menu;
    private RenderTexture blurred;
    private bool leaving;

    private void Start()
    {
        // Yeni oyun: önceki oyundan (final dahil) kalan gün, alışveriş ve kıyafet durumu sıfırlanır.
        DayRoutine.ResetAll();
        // Finalde jenerik görüntüyü tamamen soldurur; menüye renkli dönülsün.
        Shader.SetGlobalFloat("_Solma", 0f);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UiKit.EnsureEventSystem();
        Build();
    }

    private void OnDestroy()
    {
        if (blurred != null)
            blurred.Release();
    }

    private void Build()
    {
        canvas = UiKit.CreateCanvas("AnaMenu_UI", 10);
        Transform root = canvas.transform;

        if (background == null)
            background = Resources.Load<Texture>("UI/Arkaplan/AnaMenu");
        RawImage bg = UiKit.CreateRect("Arkaplan", root).gameObject.AddComponent<RawImage>();
        bg.texture = background;
        bg.color = background != null ? Color.white : UiKit.Dim;
        bg.raycastTarget = false;
        UiKit.Stretch(bg.rectTransform);
        UiKit.CreateFullscreen("Karartma", root, UiKit.WithAlpha(UiKit.Dim, 0.42f));

        RectTransform frontRect = UiKit.CreateRect("On", root);
        UiKit.Stretch(frontRect);
        front = frontRect.gameObject.AddComponent<CanvasGroup>();

        Image strip = UiKit.CreateImage("Sol_Serit", frontRect, UiKit.WithAlpha(UiKit.Dim, 0.28f));
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
        SceneLoader.Load(gameScene);
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
