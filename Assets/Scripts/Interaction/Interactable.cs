using UnityEngine;

/// <summary>Bir etkileşimin hangi tuşla tetiklendiği.</summary>
public enum InteractionKey
{
    /// <summary>F (Interact action'ı).</summary>
    Interact,
    /// <summary>E (Sit action'ı): oturma/kalkma, mağazada ürünü eline alma.</summary>
    Secondary
}

/// <summary>
/// Oyuncunun etkileşime girebileceği her objenin tabanı.
/// Yeni etkileşim (oturma, TV açma, kıyafet alma...) bu sınıfı türetip Interact'i doldurur;
/// algılama ve bildirim <see cref="PlayerInteractor"/> tarafında ortaktır.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private string prompt = "Etkileşime gir";

    /// <summary>Ekranda tuş etiketinin yanında görünen metin.</summary>
    public virtual string Prompt => prompt;

    /// <summary>Bildirimin üstünde küçük harflerle görünen nesne adı ("PALTO"). Boşsa gösterilmez.</summary>
    public virtual string Title => null;

    /// <summary>Etkileşimi tetikleyen tuş. Varsayılan F.</summary>
    public virtual InteractionKey Key => InteractionKey.Interact;

    /// <summary>False dönerse obje algılanmaz ve bildirim gösterilmez.</summary>
    public virtual bool CanInteract(PlayerInteractor interactor) => true;

    public abstract void Interact(PlayerInteractor interactor);
}
