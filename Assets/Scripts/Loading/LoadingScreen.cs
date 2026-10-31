using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Loading sahnesi: hedef sahneyi arka planda yükler. Arayüz koddan kurulur (UI paketi, yükleme ekranı):
/// arkada hedef sahnenin görüntüsü (Resources/UI/Arkaplan/&lt;sahne adı&gt;), sol altta "N. GÜN" ve "S A M E",
/// sağ altta "YÜKLENİYOR" ve 300x2 px çubuk.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [Header("Ayarlar")]
    [SerializeField] private string fallbackScene = "HouseScene";
    [SerializeField] private float minimumDuration = 1.5f;
    [SerializeField] private float fillSmoothing = 6f;

    private RectTransform progressFill;
    private float displayedProgress;

    private IEnumerator Start()
    {
        string target = string.IsNullOrEmpty(SceneLoader.TargetScene) ? fallbackScene : SceneLoader.TargetScene;

        Build(target);
        SetProgress(0f);
        yield return null;

        // Menü sahnesinden kalan kullanılmayan asset'leri boşalt. Bu, sahne yüklemesi başlamadan
        // yapılmalı: allowSceneActivation=false iken başlatılırsa sahne yüklemesinin arkasında
        // kuyruğa girip sonsuza kadar bekler.
        AsyncOperation unload = Resources.UnloadUnusedAssets();
        while (!unload.isDone)
            yield return null;

        AsyncOperation load = SceneManager.LoadSceneAsync(target);
        load.allowSceneActivation = false;

        float elapsed = 0f;
        while (load.progress < 0.9f || elapsed < minimumDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            // Async progress 0.9'da durur; çubuğu 0..1 aralığına çevir, minimum süreye de bağla.
            float real = Mathf.Clamp01(load.progress / 0.9f);
            float timed = Mathf.Clamp01(elapsed / minimumDuration);
            float goal = Mathf.Min(real, timed) * 0.9f;

            displayedProgress = Mathf.Lerp(displayedProgress, goal, fillSmoothing * Time.unscaledDeltaTime);
            SetProgress(displayedProgress);
            yield return null;
        }

        SetProgress(1f);
        yield return null;
        yield return ScreenFader.FadeOut(0.4f);
        load.allowSceneActivation = true;
    }

    private void Build(string target)
    {
        Canvas canvas = UiKit.CreateCanvas("Yukleme_UI", 10);
        Transform root = canvas.transform;

        Texture background = Resources.Load<Texture>("UI/Arkaplan/" + target);
        RawImage bg = UiKit.CreateRect("Arkaplan", root).gameObject.AddComponent<RawImage>();
        bg.texture = background;
        bg.color = background != null ? Color.white : UiKit.Dim;
        bg.raycastTarget = false;
        UiKit.Stretch(bg.rectTransform);
        UiKit.CreateFullscreen("Karartma", root, UiKit.WithAlpha(UiKit.Dim, 0.5f));

        TextMeshProUGUI logo = UiKit.CreateText("Logo", root, "SAME", UiKit.FontKind.Light, 39f, 55f, UiKit.White,
            TextAlignmentOptions.BottomLeft);
        UiKit.Place(logo.rectTransform, Vector2.zero, new Vector2(144f, 108f), new Vector2(600f, 50f));

        TextMeshProUGUI day = UiKit.CreateText("Gun", root, $"{GameDay.Index + 1}. GÜN", UiKit.FontKind.Medium, 16.5f, 40f,
            UiKit.Unselected, TextAlignmentOptions.BottomLeft);
        UiKit.Place(day.rectTransform, Vector2.zero, new Vector2(144f, 170f), new Vector2(600f, 24f));

        Image track = UiKit.CreateImage("Cubuk_Yol", root, Color.white, UiKit.Sprite("Yukleme_Yol"));
        UiKit.Place(track.rectTransform, new Vector2(1f, 0f), new Vector2(-144f, 120f), new Vector2(300f, 2f));

        Image fill = UiKit.CreateImage("Cubuk_Dolgu", track.rectTransform, Color.white, UiKit.Sprite("Yukleme_Dolgu"));
        progressFill = fill.rectTransform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.pivot = new Vector2(0f, 0.5f);
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;

        TextMeshProUGUI status = UiKit.CreateText("Durum", root, "YÜKLENİYOR", UiKit.FontKind.Medium, 16.5f, 24f,
            UiKit.Unselected, TextAlignmentOptions.BottomRight);
        UiKit.Place(status.rectTransform, new Vector2(1f, 0f), new Vector2(-144f, 140f), new Vector2(400f, 24f));
    }

    private void SetProgress(float value)
    {
        Vector2 max = progressFill.anchorMax;
        max.x = Mathf.Clamp01(value);
        progressFill.anchorMax = max;
    }
}
