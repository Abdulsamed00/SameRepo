using UnityEngine;

/// <summary>
/// Açılınca başka sahneye geçen kapı. F'ye basılınca oyuncu donar, kapı açılırken ekran kararır ve yükleme ekranı gelir.
/// Bir kez açıldıktan sonra tekrar etkileşilmez.
/// </summary>
public class SceneDoor : Openable
{
    [SerializeField] private string targetScene = "StreetScene";

    private bool triggered;

    public override bool CanInteract(PlayerInteractor interactor) => !triggered && base.CanInteract(interactor);

    public override void Interact(PlayerInteractor interactor)
    {
        if (triggered)
            return;

        triggered = true;
        base.Interact(interactor);
        SceneLoader.Load(targetScene);
    }
}
