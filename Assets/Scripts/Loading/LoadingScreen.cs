using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private Text statusText;

    [Header("Ayarlar")]
    [SerializeField] private string fallbackScene = "HouseScene";
    [SerializeField] private float minimumDuration = 1.5f;
    [SerializeField] private float fillSmoothing = 6f;

    private float displayedProgress;

    private IEnumerator Start()
    {
        string target = string.IsNullOrEmpty(SceneLoader.TargetScene) ? fallbackScene : SceneLoader.TargetScene;

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

        statusText.text = "Hazırlanıyor...";
        SetProgress(1f);
        yield return null;
        load.allowSceneActivation = true;
    }

    private void SetProgress(float value)
    {
        Vector2 max = progressFill.anchorMax;
        max.x = Mathf.Clamp01(value);
        progressFill.anchorMax = max;

        if (statusText != null && value < 0.95f)
            statusText.text = "Yükleniyor... %" + Mathf.RoundToInt(value / 0.9f * 100f);
    }
}
