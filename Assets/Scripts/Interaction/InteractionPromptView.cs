using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <see cref="PlayerInteractor"/> durumunu ekrana yansıtır: bakılan objenin "F  Al" / "E  Otur" bildirimi,
/// elde eşya varken bırakma ipucu, otururken kalkma ipucu. Sadece gösterir, oyun mantığı içermez.
/// </summary>
public class InteractionPromptView : MonoBehaviour
{
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Text promptText;
    [SerializeField] private Text heldText;

    private PlayerSitController sitter;

    private void Awake()
    {
        sitter = interactor.GetComponent<PlayerSitController>();
    }

    private void Update()
    {
        Interactable focused = interactor.Focused;
        promptText.enabled = focused != null;
        if (focused != null)
            promptText.text = $"<b>{interactor.KeyLabelFor(focused.Key)}</b>  {focused.Prompt}";

        bool seated = sitter != null && sitter.IsSeated;
        heldText.enabled = interactor.IsHolding || seated;
        if (seated)
            heldText.text = $"<b>{interactor.SitKeyLabel}</b>  Kalk";
        else if (interactor.IsHolding)
            heldText.text = $"<b>{interactor.DropKeyLabel}</b>  Bırak";
    }
}
