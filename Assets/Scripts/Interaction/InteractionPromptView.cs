using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <see cref="PlayerInteractor"/> durumunu ekrana yansıtır: bakılan objenin "F  Al" bildirimi
/// ve elde eşya varken bırakma ipucu. Sadece gösterir, oyun mantığı içermez.
/// </summary>
public class InteractionPromptView : MonoBehaviour
{
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Text promptText;
    [SerializeField] private Text heldText;

    private void Update()
    {
        Interactable focused = interactor.Focused;
        promptText.enabled = focused != null;
        if (focused != null)
            promptText.text = $"<b>{interactor.InteractKeyLabel}</b>  {focused.Prompt}";

        heldText.enabled = interactor.IsHolding;
        if (interactor.IsHolding)
            heldText.text = $"<b>{interactor.DropKeyLabel}</b>  Bırak";
    }
}
