using UnityEngine;

/// <summary>
/// Açılınca başka sahneye geçen kapı. F'ye basılınca oyuncu donar, kapı açılırken ekran kararır ve yükleme ekranı gelir.
/// Bir kez açıldıktan sonra tekrar etkileşilmez. Kilitliyken (<see cref="Locked"/>) bildirim görünür, kapı açılmaz.
/// </summary>
public class SceneDoor : Openable
{
    [SerializeField] private string targetScene = "StreetScene";
    [Tooltip("Kilitliyken gösterilen bildirim (ör. günün ürünü alındıktan sonra evden çıkılamaz).")]
    [SerializeField] private string lockedPrompt = "Kapı kilitli";

    private bool triggered;

    /// <summary>Kilitliyken bildirim gösterilir ama kapı açılmaz (ör. <see cref="HouseExitLock"/>).</summary>
    public bool Locked { get; set; }

    public override string Prompt => Locked ? lockedPrompt : base.Prompt;

    public override bool CanInteract(PlayerInteractor interactor) => !triggered && base.CanInteract(interactor);

    public override void Interact(PlayerInteractor interactor)
    {
        if (triggered || Locked)
            return;

        triggered = true;
        base.Interact(interactor);
        SceneLoader.Load(targetScene);
    }
}
