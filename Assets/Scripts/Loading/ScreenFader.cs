using System.Collections;
using UnityEngine;
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

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        GameObject imageObject = new GameObject("Curtain");
        imageObject.transform.SetParent(transform, false);
        curtain = imageObject.AddComponent<Image>();
        curtain.color = new Color(0f, 0f, 0f, 0f);
        curtain.raycastTarget = false;
        RectTransform rect = curtain.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        SceneManager.sceneLoaded += OnSceneLoaded;
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
