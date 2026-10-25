using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sahne geçişlerini yükleme ekranı üzerinden yapar. Önce oyuncu dondurulur ve ekran kararır,
/// sonra Loading sahnesi açılır; yeni sahne gelince <see cref="ScreenFader"/> ekranı aydınlatır.
/// Kullanım: SceneLoader.Load("GameScene");
/// </summary>
public static class SceneLoader
{
    public const string LoadingSceneName = "Loading";

    public static string TargetScene { get; private set; }

    /// <summary>Hedef sahnede oyuncunun doğacağı <see cref="SpawnOverride"/> kimliği; boşsa sahnenin varsayılan noktası.</summary>
    public static string SpawnId { get; private set; }

    private static bool transitioning;

    public static void Load(string targetScene, string spawnId = null)
    {
        if (transitioning)
            return;

        transitioning = true;
        TargetScene = targetScene;
        SpawnId = spawnId;

        FreezePlayer();
        ScreenFader.Runner.StartCoroutine(FadeAndLoad());
    }

    /// <summary>Bekleyen doğma kimliğini döndürür ve temizler.</summary>
    public static string ConsumeSpawnId()
    {
        string id = SpawnId;
        SpawnId = null;
        return id;
    }

    /// <summary>Hedef sahne açıldı; yeni geçişe izin ver.</summary>
    public static void NotifyArrived() => transitioning = false;

    private static IEnumerator FadeAndLoad()
    {
        yield return ScreenFader.FadeOut();
        SceneManager.LoadScene(LoadingSceneName);
    }

    private static void FreezePlayer()
    {
        foreach (FirstPersonController controller in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            controller.enabled = false;
        foreach (PlayerInteractor interactor in Object.FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
            interactor.enabled = false;
    }
}
