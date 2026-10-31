using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hazırlayanlar ekranı: ortada "S A M E", altında iki sütun rol/isim, sağ altta "ESC  Atla".
/// Ana menüdeki "Hazırlayanlar" ve oyun sonu (<see cref="FinalSequence"/>) aynı ekranı kullanır.
/// </summary>
public class CreditsView : MonoBehaviour
{
    private static readonly (string role, string name)[] Credits =
    {
        ("ÇEVRE MODELLEME · UI", "Sıla Açık"),
        ("KARAKTER · SES · REKLAM", "Mehmet Efe Alp"),
        ("OYNANIŞ PROGRAMLAMA", "Abdulsamed Eker"),
        ("SİSTEM PROGRAMLAMA", "Murathan Öğdür"),
    };

    private const float ColumnOffset = 245f; // sütun ortaları ekran ortasının ±245 px'i (aralık 180)
    private const float RowGap = 106f;       // iki satır arası (isim altı → rol üstü 45 px)

    private CanvasGroup group;
    private Action onSkip;
    private AudioClip clickSound;

    public CanvasGroup Group => group;

    /// <param name="dimAlpha">Arkadaki görüntünün karartması (#080A0E).</param>
    public static CreditsView Create(Transform parent, float dimAlpha, AudioClip clickSound, Action onSkip)
    {
        RectTransform root = UiKit.CreateRect("Hazirlayanlar", parent);
        UiKit.Stretch(root);
        CreditsView view = root.gameObject.AddComponent<CreditsView>();
        view.group = root.gameObject.AddComponent<CanvasGroup>();
        view.onSkip = onSkip;
        view.clickSound = clickSound;
        view.Build(dimAlpha);
        return view;
    }

    private void Build(float dimAlpha)
    {
        Image dim = UiKit.CreateFullscreen("Karartma", transform, UiKit.WithAlpha(UiKit.Dim, dimAlpha));
        dim.raycastTarget = true;

        RectTransform center = UiKit.CreateRect("Orta", transform);
        UiKit.Place(center, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI logo = UiKit.CreateText("Logo", center, "SAME", UiKit.FontKind.Light, 45f, 55f, UiKit.White,
            TextAlignmentOptions.Center);
        UiKit.Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 115f), new Vector2(600f, 60f));

        Image line = UiKit.CreateImage("Cizgi", center, UiKit.TabDivider);
        UiKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(280f, 1f));

        for (int i = 0; i < Credits.Length; i++)
        {
            float x = i % 2 == 0 ? -ColumnOffset : ColumnOffset;
            float y = 13f - (i / 2) * RowGap;
            TextMeshProUGUI role = UiKit.CreateText("Rol", center, Credits[i].role, UiKit.FontKind.Medium, 16.5f, 24f,
                UiKit.Secondary, TextAlignmentOptions.Center);
            UiKit.Place(role.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(460f, 24f));
            TextMeshProUGUI name = UiKit.CreateText("Isim", center, Credits[i].name, UiKit.FontKind.Regular, 27f, 0f,
                UiKit.White, TextAlignmentOptions.Center);
            UiKit.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y - 35f), new Vector2(460f, 36f));
        }

        RectTransform skip = UiKit.CreateFooterHint(transform, "ESC", "Atla", new Vector2(1f, 0f), new Vector2(-72f, 69f));
        Image hit = skip.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        skip.gameObject.AddComponent<MenuRowEvents>().Click = Skip;
    }

    public IEnumerator Fade(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    private void Skip()
    {
        if (onSkip == null)
            return;
        GameAudio.PlayUI(clickSound);
        Action callback = onSkip;
        onSkip = null;
        callback();
    }

    private void Update()
    {
        if (UiInput.Back() || UiInput.Submit())
            Skip();
    }
}
