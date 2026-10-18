using UnityEngine;

/// <summary>
/// Oyuncunun F ile etkileşime girebileceği her objenin tabanı.
/// Yeni etkileşim (oturma, TV açma, kıyafet alma...) bu sınıfı türetip Interact'i doldurur;
/// algılama ve bildirim <see cref="PlayerInteractor"/> tarafında ortaktır.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private string prompt = "Etkileşime gir";

    /// <summary>Ekranda tuş etiketinin yanında görünen metin.</summary>
    public virtual string Prompt => prompt;

    /// <summary>False dönerse obje algılanmaz ve bildirim gösterilmez.</summary>
    public virtual bool CanInteract(PlayerInteractor interactor) => true;

    public abstract void Interact(PlayerInteractor interactor);
}
