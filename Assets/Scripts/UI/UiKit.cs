using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Arayüz paketinin (Assets/UI/Referans/OKU_BENI.md) ortak parçaları: fontlar, sprite'lar, renkler ve koddan
/// UI kurma yardımcıları. Tüm ekranlar 1920x1080 referansına göre kurulur (Scale With Screen Size, match 0.5),
/// böylece pakete yazılan piksel değerleri doğrudan kullanılır.
/// </summary>
public static class UiKit
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    public static readonly Color White = Color.white;
    public static readonly Color Unselected = new Color(1f, 1f, 1f, 0.78f);
    public static readonly Color Secondary = new Color(1f, 1f, 1f, 0.6f);
    public static readonly Color Divider = new Color(1f, 1f, 1f, 0.08f);
    public static readonly Color TabDivider = new Color(1f, 1f, 1f, 0.14f);
    public static readonly Color Dim = new Color(8f / 255f, 10f / 255f, 14f / 255f, 1f);
    public static readonly Color PaperText = Hex(0x2B2A28);
    public static readonly Color PaperSecondary = Hex(0x5E5B55);
    public static readonly Color PaperDash = Hex(0x8F8A80);

    public enum FontKind { Light, Regular, Medium, SemiBold, Mono }

    private static readonly Dictionary<FontKind, TMP_FontAsset> fonts = new Dictionary<FontKind, TMP_FontAsset>();
    private static readonly Dictionary<FontKind, Material> shadowMaterials = new Dictionary<FontKind, Material>();
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public static TMP_FontAsset Font(FontKind kind)
    {
        if (!fonts.TryGetValue(kind, out TMP_FontAsset font) || font == null)
        {
            string file = kind switch
            {
                FontKind.Light => "Inter-Light",
                FontKind.Medium => "Inter-Medium",
                FontKind.SemiBold => "Inter-SemiBold",
                FontKind.Mono => "IBMPlexMono-Regular",
                _ => "Inter-Regular",
            };
            font = Resources.Load<TMP_FontAsset>("UI/Fontlar/" + file + " SDF");
            fonts[kind] = font;
        }
        return font;
    }

    /// <summary>Paketteki yazı gölgesi: Underlay siyah, alfa 0.5, softness 0.5, offset Y -0.5.</summary>
    private static Material ShadowMaterial(FontKind kind)
    {
        if (!shadowMaterials.TryGetValue(kind, out Material material) || material == null)
        {
            material = new Material(Font(kind).material) { name = kind + " Golge" };
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.5f));
            material.SetFloat("_UnderlaySoftness", 0.5f);
            material.SetFloat("_UnderlayOffsetY", -0.5f);
            shadowMaterials[kind] = material;
        }
        return material;
    }

    public static Sprite Sprite(string name)
    {
        if (!sprites.TryGetValue(name, out Sprite sprite) || sprite == null)
        {
            sprite = Resources.Load<Sprite>("UI/Sprites/" + name);
            sprites[name] = sprite;
        }
        return sprite;
    }

    public static Color Hex(int rgb, float alpha = 1f) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    /// <summary>Ekranı kaplayan, 1920x1080 referanslı yeni bir overlay canvas.</summary>
    public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Menüler fareyle tıklanabilsin diye sahnede EventSystem yoksa oluşturur.</summary>
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null)
            return;
        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Object.DontDestroyOnLoad(go);
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>Tek noktaya anchor'lar: anchor ve pivot aynı (ör. sol alt = (0,0)), konum o köşeden piksel.</summary>
    public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null && sprite.border != Vector4.zero)
            image.type = Image.Type.Sliced;
        if (sprite != null)
            rect.sizeDelta = sprite.rect.size;
        return image;
    }

    public static Image CreateFullscreen(string name, Transform parent, Color color)
    {
        Image image = CreateImage(name, parent, color);
        Stretch(image.rectTransform);
        return image;
    }

    /// <summary>
    /// TMP yazısı. <paramref name="spacing"/> paketteki "Character Spacing" değeri (TMP birimi).
    /// Gölge, koyu kağıt üstündeki fiş dışında her yazıda açıktır.
    /// </summary>
    public static TextMeshProUGUI CreateText(string name, Transform parent, string content, FontKind kind, float size,
        float spacing = 0f, Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.Left, bool shadow = true)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font(kind);
        if (shadow)
            text.fontSharedMaterial = ShadowMaterial(kind);
        // Metin seçili dile çevrilir; dil değişince LocText yeniden çevirir.
        if (string.IsNullOrEmpty(content))
            text.text = content;
        else
            rect.gameObject.AddComponent<LocText>().Init(text, content);
        text.fontSize = size;
        text.characterSpacing = spacing;
        text.color = color ?? White;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>Yazının ağırlığını değiştirir (seçili menü satırı Medium, diğerleri Regular); gölge korunur.</summary>
    public static void SetFont(TextMeshProUGUI text, FontKind kind, bool shadow = true)
    {
        text.font = Font(kind);
        text.fontSharedMaterial = shadow ? ShadowMaterial(kind) : Font(kind).material;
    }

    /// <summary>Tuş kutusu ("E", "ESC"): çerçeve sprite'ı yazının genişliğine göre uzar.</summary>
    public static RectTransform CreateKeyCap(Transform parent, string label, out TextMeshProUGUI text, float height = 36f)
    {
        Image frame = CreateImage("Tus", parent, White, Sprite("Tus_Cerceve_Bos"));
        RectTransform rect = frame.rectTransform;
        rect.sizeDelta = new Vector2(height, height);
        text = CreateText("Harf", rect, label, FontKind.SemiBold, height * 0.47f, 0f, White, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        SetKeyLabel(rect, text, label, height);
        return rect;
    }

    public static void SetKeyLabel(RectTransform cap, TextMeshProUGUI text, string label, float height = 36f)
    {
        text.text = label;
        float width = Mathf.Max(height, text.GetPreferredValues(label).x + height * 0.5f);
        cap.sizeDelta = new Vector2(width, height);
    }

    /// <summary>Alt bilgi satırı: tuş kutusu + açıklama ("ESC  Geri").</summary>
    public static RectTransform CreateFooterHint(Transform parent, string key, string label, Vector2 anchor, Vector2 position)
    {
        RectTransform row = CreateRect("Ipucu_" + key, parent);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Place(row, anchor, position, new Vector2(10f, 36f));

        CreateKeyCap(row, key, out _, 33f);
        TextMeshProUGUI text = CreateText("Yazi", row, label, FontKind.Regular, 19.5f, 0f, Unselected);
        // Genişlik çevrilmiş yazıya göre; dil değişirse yazı taşar (yerleşim sola dayalı), ekranı kuran yeniden kurar.
        text.rectTransform.sizeDelta = new Vector2(text.GetPreferredValues(text.text).x, 33f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return row;
    }
}
