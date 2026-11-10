using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <see cref="PlayerInteractor"/> durumunu ekrana yansıtır (UI paketi, oyun içi): ortada her zaman <c>Nokta</c>,
/// etkileşilebilir bir şeye bakınca <c>Halka</c>. 10.11'den beri (kullanıcı: "UI'da yazan tüm yazılar kalkacak, UI elementi
/// sol altta çıkacak") yazı yok: sadece tuş kutuları sol altta; altta ipucu tuşu (elde eşya: G, otururken: E...), üstünde
/// bakılan şeyin etkileşim tuşu. Yazılar (bildirim, nesne adı) kurulur ama gizlidir.
/// Sadece gösterir, oyun mantığı içermez. Arayüz Awake'te koddan kurulur.
/// </summary>
public class InteractionPromptView : MonoBehaviour
{
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private float fadeDuration = 0.15f;

    private PlayerSitController sitter;
    private Image dot;
    private CanvasGroup focusGroup;
    private KeyLine prompt;
    private CanvasGroup promptGroup;
    private KeyLine hint;
    private CanvasGroup hintGroup;

    private static readonly Vector2 CornerMargin = new Vector2(64f, 64f);
    private const float RowStep = 52f;
    private const float CornerScale = 1.35f; // sol alt tuş kutuları (10.11: "biraz büyüt")

    /// <summary>Tuş kutusu (+ gizli yazı), sol alta hizalı.</summary>
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

        RectTransform corner = UiKit.CreateRect("Sol_Alt", transform);
        UiKit.Place(corner, Vector2.zero, CornerMargin, Vector2.zero);
        corner.localScale = Vector3.one * CornerScale;

        hint = CreateKeyLine("Ipucu", corner);
        hintGroup = hint.Root.gameObject.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0f;

        prompt = CreateKeyLine("Bildirim", corner);
        promptGroup = prompt.Root.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
    }

    private static KeyLine CreateKeyLine(string name, Transform parent)
    {
        RectTransform root = UiKit.CreateRect(name, parent);
        HorizontalLayoutGroup layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UiKit.Place(root, Vector2.zero, Vector2.zero, new Vector2(10f, 36f));

        KeyLine line = new KeyLine { Root = root };
        line.Cap = UiKit.CreateKeyCap(root, "F", out line.CapText);
        line.Text = UiKit.CreateText("Yazi", root, "", UiKit.FontKind.Medium, 22.5f, 0f, UiKit.White, TextAlignmentOptions.MidlineLeft);
        line.Text.gameObject.SetActive(false);
        return line;
    }

    private void Update()
    {
        bool active = interactor.enabled && !PauseMenu.IsPaused;
        dot.enabled = active;

        bool seated = sitter != null && sitter.IsSeated;
        Interactable focused = active ? interactor.Focused : null;
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

        if (focused != null)
            prompt.Set(interactor.KeyLabelFor(focused.Key), focused.Prompt);
        promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, focused != null ? 1f : 0f, Time.unscaledDeltaTime / fadeDuration);
        // İpucu görünürken etkileşim tuşu onun üstüne çıkar.
        float promptY = hintGroup.alpha > 0.01f ? RowStep : 0f;
        prompt.Root.anchoredPosition = new Vector2(0f, Mathf.MoveTowards(prompt.Root.anchoredPosition.y, promptY, Time.unscaledDeltaTime * RowStep / fadeDuration));
    }
}
