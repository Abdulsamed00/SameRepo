using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <see cref="PlayerInteractor"/> durumunu ekrana yansıtır (UI paketi, oyun içi): ortada her zaman <c>Nokta</c>,
/// etkileşilebilir bir şeye bakınca <c>Halka</c> ve altında tuş kutusu + bildirim ("E  Dolabı aç"), ürünlerde üstte
/// nesne adı. Elde eşya varken / otururken / incelerken ekranın altında ikinci satır ("G  Bırak").
/// Sadece gösterir, oyun mantığı içermez. Arayüz Awake'te koddan kurulur.
/// </summary>
public class InteractionPromptView : MonoBehaviour
{
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private float fadeDuration = 0.15f;

    private PlayerSitController sitter;
    private Image dot;
    private CanvasGroup focusGroup;
    private TextMeshProUGUI title;
    private KeyLine prompt;
    private KeyLine hint;
    private CanvasGroup hintGroup;
    private KeyLine seatedPrompt;
    private CanvasGroup seatedPromptGroup;
    private string lastTitle;
    private int lastTitleVersion = -1;

    /// <summary>Tuş kutusu + yazı, yatayda ortalı.</summary>
    private class KeyLine
    {
        public RectTransform Root;
        public RectTransform Cap;
        public TextMeshProUGUI CapText;
        public TextMeshProUGUI Text;
        private string key;
        private string label;
        private int version = -1;

        // newLabel Türkçe kaynak metin (sahnede serialize edilmiş olabilir); seçili dile burada çevrilir.
        public void Set(string newKey, string newLabel)
        {
            if (newKey == key && newLabel == label && version == Loc.Version)
                return;
            key = newKey;
            label = newLabel;
            version = Loc.Version;
            UiKit.SetKeyLabel(Cap, CapText, key);
            Text.text = Loc.T(label);
            Text.rectTransform.sizeDelta = new Vector2(Text.GetPreferredValues(Text.text).x, 36f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(Root);
        }
    }

    private void Awake()
    {
        sitter = interactor.GetComponent<PlayerSitController>();
        Build();
    }

    private void Build()
    {
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiKit.ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
        }

        RectTransform center = UiKit.CreateRect("Orta", transform);
        UiKit.Place(center, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        dot = UiKit.CreateImage("Nokta", center, Color.white, UiKit.Sprite("Nokta"));
        UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));

        RectTransform focus = UiKit.CreateRect("Odak", center);
        UiKit.Place(focus, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        focusGroup = focus.gameObject.AddComponent<CanvasGroup>();
        focusGroup.alpha = 0f;

        Image ring = UiKit.CreateImage("Halka", focus, Color.white, UiKit.Sprite("Halka"));
        UiKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));

        title = UiKit.CreateText("Nesne_Adi", focus, "", UiKit.FontKind.SemiBold, 16.5f, 22f, UiKit.Secondary,
            TextAlignmentOptions.Center);
        UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(600f, 24f));

        prompt = CreateKeyLine("Bildirim", focus, UiKit.FontKind.Medium, UiKit.White);

        RectTransform bottom = UiKit.CreateRect("Alt", transform);
        UiKit.Place(bottom, new Vector2(0.5f, 0f), new Vector2(0f, 108f), Vector2.zero);
        hintGroup = bottom.gameObject.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0f;
        hint = CreateKeyLine("Ipucu", bottom, UiKit.FontKind.Regular, UiKit.Unselected);

        // Otururken etkileşim bildirimi (TV'yi aç/kapat) ekranın ortasında değil, ipucunun üstünde çıkar.
        seatedPrompt = CreateKeyLine("Oturma_Bildirimi", bottom, UiKit.FontKind.Medium, UiKit.White);
        seatedPrompt.Root.anchoredPosition = new Vector2(0f, 46f);
        seatedPromptGroup = seatedPrompt.Root.gameObject.AddComponent<CanvasGroup>();
        seatedPromptGroup.alpha = 0f;
    }

    private static KeyLine CreateKeyLine(string name, Transform parent, UiKit.FontKind kind, Color color)
    {
        RectTransform root = UiKit.CreateRect(name, parent);
        HorizontalLayoutGroup layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UiKit.Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 36f));

        KeyLine line = new KeyLine { Root = root };
        line.Cap = UiKit.CreateKeyCap(root, "F", out line.CapText);
        line.Text = UiKit.CreateText("Yazi", root, "", kind, 22.5f, 0f, color, TextAlignmentOptions.MidlineLeft);
        return line;
    }

    private void Update()
    {
        bool active = interactor.enabled && !PauseMenu.IsPaused;
        dot.enabled = active;

        bool seated = sitter != null && sitter.IsSeated;
        Interactable focused = active ? interactor.Focused : null;
        if (focused != null && seated)
            seatedPrompt.Set(interactor.KeyLabelFor(focused.Key), focused.Prompt);
        seatedPromptGroup.alpha = Mathf.MoveTowards(seatedPromptGroup.alpha, focused != null && seated ? 1f : 0f,
            Time.unscaledDeltaTime / fadeDuration);
        if (focused != null && !seated)
        {
            string newTitle = focused.Title;
            if (newTitle != lastTitle || lastTitleVersion != Loc.Version)
            {
                lastTitle = newTitle;
                lastTitleVersion = Loc.Version;
                title.text = Loc.T(newTitle) ?? "";
            }
            // Etkileşim satırı ortanın 42 px altında; nesne adı varsa onun altında.
            bool hasTitle = !string.IsNullOrEmpty(newTitle);
            prompt.Root.anchoredPosition = new Vector2(0f, hasTitle ? -88f : -61f);
            prompt.Set(interactor.KeyLabelFor(focused.Key), focused.Prompt);
        }
        focusGroup.alpha = Mathf.MoveTowards(focusGroup.alpha, focused != null && !seated ? 1f : 0f, Time.unscaledDeltaTime / fadeDuration);

        bool showHint = active && (!string.IsNullOrEmpty(interactor.Hint) || seated || interactor.CanDropHeld);
        if (showHint)
        {
            if (!string.IsNullOrEmpty(interactor.Hint))
                hint.Set(interactor.HintKey ?? interactor.DropKeyLabel, interactor.Hint);
            else if (seated)
                hint.Set(interactor.SitKeyLabel, "Kalk");
            else
                hint.Set(interactor.DropKeyLabel, "Bırak");
        }
        hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, showHint ? 1f : 0f, Time.unscaledDeltaTime / fadeDuration);
    }
}
