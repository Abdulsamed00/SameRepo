using UnityEngine;

/// <summary>
/// Trigger bölgesinin içindeyken F ile yükleme ekranı üzerinden başka sahneye geçiren etkileşim
/// ("Eve dön", "Mağazaya gir", "Sokağa çık"). Işın gerekmez, bölgede olmak yeter.
/// <see cref="spawnId"/> doluysa hedef sahnede o kimlikli <see cref="SpawnOverride"/> noktasında doğulur.
/// <see cref="requireLeaveFirst"/> açıksa oyuncu bölgeden bir kez çıkmadan etkinleşmez (doğduğu yerde hemen çıkmasın).
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneZone : Interactable
{
    [SerializeField] private string targetScene = "HouseScene";
    [SerializeField] private string spawnId;
    [SerializeField] private bool requireLeaveFirst;

    private bool armed;
    private bool triggered;

    private void Awake() => armed = !requireLeaveFirst;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    public override bool CanInteract(PlayerInteractor interactor) => armed && !triggered;

    public override void Interact(PlayerInteractor interactor)
    {
        if (triggered)
            return;

        triggered = true;
        SceneLoader.Load(targetScene, spawnId);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerInteractor interactor = other.GetComponentInParent<PlayerInteractor>();
        if (interactor != null)
            interactor.Zone = this;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerInteractor interactor = other.GetComponentInParent<PlayerInteractor>();
        if (interactor == null)
            return;

        armed = true;
        if (interactor.Zone == this)
            interactor.Zone = null;
    }
}
