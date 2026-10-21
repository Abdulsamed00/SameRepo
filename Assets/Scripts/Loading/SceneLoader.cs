using UnityEngine.SceneManagement;

/// <summary>
/// Sahne geçişlerini yükleme ekranı üzerinden yapar.
/// Kullanım: SceneLoader.Load("GameScene");
/// </summary>
public static class SceneLoader
{
    public const string LoadingSceneName = "Loading";

    public static string TargetScene { get; private set; }

    public static void Load(string targetScene)
    {
        TargetScene = targetScene;
        SceneManager.LoadScene(LoadingSceneName);
    }
}
