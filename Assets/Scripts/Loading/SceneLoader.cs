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

    /// <summary>Ana menü. Ayrı bir sahne değil: evde (<see cref="HouseSceneName"/>) oyuncu kanepede otururken açılır (<see cref="IntroMenu"/>).</summary>
    public const string MenuSceneName = "MainMenu";
    public const string HouseSceneName = "HouseScene";

    public static string TargetScene { get; private set; }

    /// <summary>Hedef ev sahnesi ana menü olarak açılacak (<see cref="IntroMenu"/> bunu tüketir).</summary>
    public static bool MenuPending { get; private set; }

    private static bool firstLoadDone;

    /// <summary>
    /// Oyun açıldıktan sonraki ilk yükleme (açılış): yükleme ekranı <c>Resources/UI/Arkaplan/Acilis</c> görselini ve
    /// sağ üstte "SAME" yazısını gösterir (09.11, kullanıcı isteği). Menüye sonradan dönüşlerde normal ekran.
    /// </summary>
    public static bool OpeningLoad { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        firstLoadDone = false;
        OpeningLoad = false;
    }

    /// <summary>Menü bekleniyorsa true döner ve bayrağı temizler.</summary>
    public static bool ConsumeMenuPending()
    {
        bool pending = MenuPending;
        MenuPending = false;
        return pending;
    }

    /// <summary>Hedef sahnede oyuncunun doğacağı <see cref="SpawnOverride"/> kimliği; boşsa sahnenin varsayılan noktası.</summary>
    public static string SpawnId { get; private set; }

    private static bool transitioning;

    /// <summary>Geçiş sürüyor (kararma başladı, hedef sahne henüz açılmadı).</summary>
    public static bool IsTransitioning => transitioning;

    public static void Load(string targetScene, string spawnId = null)
    {
        if (transitioning)
            return;

        transitioning = true;
        // Evden çıkış: dönünce hava kararmıştır.
        if (SceneManager.GetActiveScene().name == HouseSceneName && targetScene != MenuSceneName)
            DayRoutine.NotifyLeftHouse();
        MenuPending = targetScene == MenuSceneName;
        OpeningLoad = !firstLoadDone && MenuPending;
        firstLoadDone = true;
        TargetScene = MenuPending ? HouseSceneName : targetScene;
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
        // Menüye dönüş yeni oyundur: ekran kararmışken gün, alışveriş ve kıyafet durumu sıfırlanır (ev sahnesi açılmadan önce).
        if (MenuPending)
        {
            DayRoutine.ResetAll();
            Shader.SetGlobalFloat("_Solma", 0f);
        }
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
