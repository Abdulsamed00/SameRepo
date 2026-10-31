using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Duraklatma ekranındaki satış fişi: <c>Fis_Kagit_Bos</c> üstüne IBM Plex Mono satırlar, satın alınan her ürün
/// için bir satır. Sağdan 225, üstten 144, genişlik 435, -2.5° eğik.
/// </summary>
public static class ReceiptView
{
    private const float Width = 435f;
    private const float Padding = 42f;
    private const float LineHeight = 38f;
    private static readonly CultureInfo Turkish = new CultureInfo("tr-TR");

    /// <returns>Hiç ürün alınmadıysa null (fiş gösterilmez).</returns>
    public static RectTransform Create(Transform parent)
    {
        List<ProductType> bought = new List<ProductType>();
        foreach (ProductType type in System.Enum.GetValues(typeof(ProductType)))
            if (PlayerPurchases.Owns(type))
                bought.Add(type);
        if (bought.Count == 0)
            return null;

        Image paper = UiKit.CreateImage("Fis", parent, Color.white, UiKit.Sprite("Fis_Kagit_Bos"));
        RectTransform rect = paper.rectTransform;
        rect.localEulerAngles = new Vector3(0f, 0f, -2.5f);

        float y = Padding;
        Line(rect, "SATIŞ FİŞİ", ref y, 22.5f, 6f, UiKit.PaperText, TextAlignmentOptions.Center, 30f, bold: true);
        Line(rect, "Mağaza · Kasa 02", ref y, 17f, 0f, UiKit.PaperSecondary, TextAlignmentOptions.Center, 34f);
        Dashes(rect, ref y);
        Pair(rect, $"GÜN {GameDay.Index + 1}", $"NO {bought.Count:0000}", ref y, UiKit.PaperSecondary, 17f, 44f);
        foreach (ProductType type in bought)
            Pair(rect, type.DisplayName().ToUpper(Turkish), "1 AD", ref y, UiKit.PaperText, 19.5f, LineHeight);
        y = Mathf.Max(y + 40f, 300f);
        Dashes(rect, ref y);
        Pair(rect, "TOPLAM ADET", bought.Count.ToString(), ref y, UiKit.PaperText, 19.5f, 56f, bold: true);
        Line(rect, "Yine bekleriz.", ref y, 17f, 0f, UiKit.PaperSecondary, TextAlignmentOptions.Center, 30f);

        rect.sizeDelta = new Vector2(Width, Mathf.Max(y + Padding, 495f));
        return rect;
    }

    private static TextMeshProUGUI Text(RectTransform paper, string content, float size, float spacing, Color color,
        TextAlignmentOptions alignment, bool bold)
    {
        TextMeshProUGUI text = UiKit.CreateText("Satir", paper, content, UiKit.FontKind.Mono, size, spacing, color, alignment, shadow: false);
        if (bold)
            text.fontStyle = FontStyles.Bold;
        return text;
    }

    private static void Line(RectTransform paper, string content, ref float y, float size, float spacing, Color color,
        TextAlignmentOptions alignment, float height, bool bold = false)
    {
        TextMeshProUGUI text = Text(paper, content, size, spacing, color, alignment, bold);
        PlaceRow(text.rectTransform, y, height);
        y += height;
    }

    private static void Pair(RectTransform paper, string left, string right, ref float y, Color color, float size, float height,
        bool bold = false)
    {
        PlaceRow(Text(paper, left, size, 0f, color, TextAlignmentOptions.MidlineLeft, bold).rectTransform, y, height);
        PlaceRow(Text(paper, right, size, 0f, color, TextAlignmentOptions.MidlineRight, bold).rectTransform, y, height);
        y += height;
    }

    // Kesik çizgi: monospace tire dizisi.
    private static void Dashes(RectTransform paper, ref float y)
    {
        TextMeshProUGUI text = Text(paper, new string('-', 44), 13f, 0f, UiKit.PaperDash, TextAlignmentOptions.Center, false);
        text.overflowMode = TextOverflowModes.Truncate;
        PlaceRow(text.rectTransform, y, 24f);
        y += 24f;
    }

    private static void PlaceRow(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(Padding, -top - height);
        rect.offsetMax = new Vector2(-Padding, -top);
    }
}
