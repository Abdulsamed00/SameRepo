using System.Collections;
using UnityEngine;

/// <summary>
/// Açık dolaptan kıyafet değiştirme (11.11, kullanıcı isteği): yatmadan önce ilk günkü kıyafetler giyilir
/// (<see cref="DayRoutine.HomeChangePending"/>), dışarı çıkmadan önce alınan kıyafetlerin hepsi tek seferde giyilir
/// (<see cref="DayRoutine.DressPending"/>). Dolabın iç duvarlarındaki (Govde_*) görünmez collider'lara eklenir; dolap
/// açıkken oraya bakınca çıkar. Değişim kısa bir karartmada olur.
/// </summary>
public class OutfitChange : Interactable
{
    protected override float DefaultMaxDistance => 1.7f;

    [SerializeField] private Openable wardrobe;
    public Openable Wardrobe { set => wardrobe = value; }
    [SerializeField] private string homePrompt = "Ev kıyafetlerini giy";
    [SerializeField] private string dressPrompt = "Kıyafetlerimi giy";
    [SerializeField] private AudioClip changeSound;
    [SerializeField] private float fadeTime = 0.35f;
    [SerializeField] private float holdTime = 0.5f;

    private bool busy;

    // Sahne dosyasına dokunmadan: evde dolabın iç duvarlarına (Govde_Arka/Sol/Sag/Ust/Alt) çalışma anında eklenir.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Attach();

    private static void Attach()
    {
        foreach (Openable openable in FindObjectsByType<Openable>(FindObjectsSortMode.None))
        {
            if (openable.name != "Dolap")
                continue;
            // Dolabın tüm gövde collider'larına (iç duvarlar, raf, gövde modeli...): ışın ortada önce kıyafet/raf gibi
            // etkileşimsiz bir collider'a çarpınca arkadaki duvardaki bileşene hiç ulaşmıyordu, sadece sağa sola bakınca
            // çalışıyordu (11.11). Kapaklar (Openable'ın parçaları) ve dolaptaki kıyafetler hariç.
            foreach (Collider c in openable.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger || c.gameObject == openable.gameObject || c.GetComponent<OutfitChange>() != null || c.GetComponentInParent<WardrobeClothing>() != null)
                    continue;
                if (IsUnderDoor(c.transform, openable.transform))
                    continue;
                OutfitChange change = c.gameObject.AddComponent<OutfitChange>();
                change.Wardrobe = openable;
            }
        }
    }

    private static bool IsUnderDoor(Transform t, Transform root)
    {
        for (; t != null && t != root; t = t.parent)
            if (t.name.StartsWith("Kapak"))
                return true;
        return false;
    }

    private static bool Needed => DayRoutine.HomeChangePending || DayRoutine.DressPending;

    public override string Prompt => busy || !Needed ? "" : DayRoutine.HomeChangePending ? homePrompt : dressPrompt;

    private void Awake()
    {
        if (wardrobe == null)
            wardrobe = GetComponentInParent<Openable>();
    }

    public override bool CanInteract(PlayerInteractor interactor)
    {
        if (busy || !Needed || (wardrobe != null && !wardrobe.IsOpen) || interactor.IsHolding)
            return false;
        PlayerSitController sitter = interactor.GetComponent<PlayerSitController>();
        return sitter == null || !sitter.IsSeated;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (!busy)
            StartCoroutine(Change(interactor));
    }

    private IEnumerator Change(PlayerInteractor interactor)
    {
        busy = true;
        FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
        if (controller != null)
            controller.Frozen = true;
        bool toHome = DayRoutine.HomeChangePending;

        yield return ScreenFader.FadeOut(fadeTime);
        GameAudio.PlayAt(changeSound != null ? changeSound : WardrobeClothing.SharedHandleSound, transform.position);
        if (toHome)
            PlayerOutfit.ChangeToHome();
        else
            PlayerOutfit.WearAllOwned();
        yield return new WaitForSeconds(holdTime);
        yield return ScreenFader.FadeIn(fadeTime);

        if (controller != null)
            controller.Frozen = false;
        busy = false;
        if (toHome)
            DayRoutine.NotifyChangedToHome();
    }
}
