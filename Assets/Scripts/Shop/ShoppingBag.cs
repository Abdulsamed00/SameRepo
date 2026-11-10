using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mağazada ürün alınca oyuncunun eline gelen alışveriş poşeti (11.11). Durum statik, sahne geçişlerinde korunur:
/// kasada <see cref="Give"/>, dolapta kıyafet çıkarılırken ya da yeni güne geçince <see cref="Put"/>. Görünüm
/// <see cref="ShoppingBagView"/> ile oyuncunun kamerasına bağlanır (sahne/prefab düzenlemesi gerekmez, çalışma anında eklenir).
/// </summary>
public static class ShoppingBag
{
    public static bool Carrying { get; private set; }

    public static event Action Changed;

    public static void Give()
    {
        if (Carrying)
            return;
        Carrying = true;
        Refresh();
        Changed?.Invoke();
    }

    public static void Put()
    {
        if (!Carrying)
            return;
        Carrying = false;
        Refresh();
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Carrying = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Refresh();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

    // Sahnedeki oyuncuya görünümü ekler / açar / kapatır.
    private static void Refresh()
    {
        PlayerBody body = UnityEngine.Object.FindFirstObjectByType<PlayerBody>();
        if (body == null)
            return;
        ShoppingBagView view = body.GetComponent<ShoppingBagView>();
        if (view == null && Carrying)
            view = body.gameObject.AddComponent<ShoppingBagView>();
        if (view != null)
            view.enabled = Carrying;
    }
}
