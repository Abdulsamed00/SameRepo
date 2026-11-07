using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Sahneler arasında yaşayan tam ekran siyah perde. Sahne geçişinde kararır; yeni sahne yüklenince kendiliğinden aydınlanır.
/// Sahneye elle eklenmez, ilk kullanımda kodla oluşturulur.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public const float FadeOutDuration = 0.8f;
    public const float FadeInDuration = 0.8f;

    private static ScreenFader instance;

    private Image curtain;
    private float alpha;
    private CanvasGroup caption;
    private TextMeshProUGUI captionTop;
    private TextMeshProUGUI captionMain;

    /// <summary>Sahne değişse de yaşayan coroutine sahibi.</summary>
    public static MonoBehaviour Runner => Instance;

    private static ScreenFader Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject root = new GameObject("ScreenFader");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<ScreenFader>();
                instance.Build();
            }
            return instance;
        }
    }

    /// <summary>Ekranı karartır; bitince ekran siyahtır.</summary>
    public static IEnumerator FadeOut(float duration = FadeOutDuration) => Instance.Fade(1f, duration);

    /// <summary>Ekranı aydınlatır (aynı sahnede ışınlanma gibi, sahne yüklenmeden karartılan durumlar için).</summary>
    public static IEnumerator FadeIn(float duration = FadeInDuration) => Instance.Fade(0f, duration);

    /// <summary>
    /// Kararmış perdenin üstünde ortalı iki satır (gün geçişi: "ERTESİ SABAH" / "2. GÜN"). Yazı belirir,
    /// <paramref name="duration"/> boyunca kalır (geçişler dahil), söner.
    /// </summary>
    public static IEnumerator Caption(string top, string main, float duration, float fade = 0.6f) =>
        Instance.ShowCaption(top, main, duration, fade);

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        GameObject imageObject = new GameObject("Curtain");
        imageObject.transform.SetParent(transform, false);
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = UiKit.ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        curtain = imageObject.AddComponent<Image>();
        curtain.color = new Color(0f, 0f, 0f, 0f);
        curtain.raycastTarget = false;
        RectTransform rect = curtain.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        RectTransform captionRect = UiKit.CreateRect("Yazi", transform);
        UiKit.Place(captionRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        caption = captionRect.gameObject.AddComponent<CanvasGroup>();
        caption.alpha = 0f;
        captionTop = UiKit.CreateText("Ust", captionRect, "", UiKit.FontKind.Medium, 18f, 40f, UiKit.Secondary,
            TextAlignmentOptions.Center, shadow: false);
        UiKit.Place(captionTop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 43f), new Vector2(1200f, 30f));
        captionMain = UiKit.CreateText("Ana", captionRect, "", UiKit.FontKind.Light, 51f, 30f, UiKit.White,
            TextAlignmentOptions.Center, shadow: false);
        UiKit.Place(captionMain.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(1200f, 70f));

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private IEnumerator ShowCaption(string top, string main, float duration, float fade)
    {
        captionTop.text = Loc.T(top);
        captionMain.text = Loc.T(main);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            caption.alpha = Mathf.Min(Mathf.Clamp01(t / fade), Mathf.Clamp01((duration - t) / fade));
            yield return null;
        }
        caption.alpha = 0f;
    }

    private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneLoader.LoadingSceneName)
        {
            StopAllCoroutines();
            StartCoroutine(Fade(0f, 0.4f));
            return;
        }

        SceneLoader.NotifyArrived();
        StopAllCoroutines();
        StartCoroutine(Fade(0f, FadeInDuration));
    }

    private IEnumerator Fade(float target, float duration)
    {
        float start = alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        SetAlpha(target);
    }

    private void SetAlpha(float value)
    {
        alpha = value;
        curtain.color = new Color(0f, 0f, 0f, value);
    }
}
