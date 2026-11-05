using UnityEngine;

/// <summary>
/// Duvardaki ışık düğmesi (05.11): F ile bağlı odanın ışıklarını (<see cref="RoomLights"/>) açar/kapatır.
/// Sadece gece çalışır (gündüz iç ışık yok, ev güneşle aydınlanır).
/// </summary>
public class LightSwitch : Interactable
{
    protected override float DefaultMaxDistance => 1.6f;

    [SerializeField] private RoomLights room;
    [SerializeField] private string turnOnPrompt = "Işığı aç";
    [SerializeField] private string turnOffPrompt = "Işığı kapat";
    [SerializeField] private AudioClip clickSound;

    public override string Prompt => room != null && room.IsOn ? turnOffPrompt : turnOnPrompt;

    public override bool CanInteract(PlayerInteractor who) => room != null && DayRoutine.NightFell;

    public override void Interact(PlayerInteractor who)
    {
        if (!CanInteract(who))
            return;
        room.Toggle();
        GameAudio.PlayAt(clickSound, transform.position);
    }
}
