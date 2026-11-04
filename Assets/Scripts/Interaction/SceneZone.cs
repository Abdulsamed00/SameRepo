using UnityEngine;

/// <summary>
/// Bölgenin içindeyken F ile yükleme ekranı üzerinden başka sahneye geçiren etkileşim
/// ("Eve dön", "Mağazaya gir", "Sokağa çık"). Işın gerekmez, bölgede olmak yeter.
/// <see cref="spawnId"/> doluysa hedef sahnede o kimlikli <see cref="SpawnOverride"/> noktasında doğulur.
/// <see cref="requireLeaveFirst"/> açıksa oyuncu bölgenin içinde doğduysa bir kez dışarı çıkmadan etkinleşmez
/// (doğduğu yerde hemen geri dönmesin); dışarıda doğduysa ilk girişte etkindir.
/// İçeride olma trigger olayıyla değil collider sınırına göre konumdan bakılır: trigger'la oyuncu dışarıda
/// doğduğunda çıkış olayı hiç gelmiyor, bölge ancak çıkıp tekrar girince açılıyordu ("Eve dön" zor çıkıyordu);
/// ışınlanma ve CharacterController kapatma da trigger olaylarını kaçırabiliyor.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneZone : Interactable
{
    [SerializeField] private string targetScene = "HouseScene";
    [SerializeField] private string spawnId;
    [SerializeField] private bool requireLeaveFirst;
    [Tooltip("Kilitliyken gösterilen bildirim (ör. mağazadan ödeme yapmadan çıkılamaz).")]
    [SerializeField] private string lockedPrompt = "Kapı kilitli";

    private Collider area;
    private PlayerInteractor interactor;
    private bool armed;
    private bool triggered;

    /// <summary>Kilitliyken bölge bildirimi gösterir ama sahne geçişi yapmaz (ör. <see cref="ShopExitGate"/>).</summary>
    public bool Locked { get; set; }

    public string LockedPrompt
    {
        get => lockedPrompt;
        set => lockedPrompt = value;
    }

    public override string Prompt => Locked ? lockedPrompt : base.Prompt;

    private void Awake()
    {
        area = GetComponent<Collider>();
        armed = !requireLeaveFirst;
    }

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    public override bool CanInteract(PlayerInteractor who) => armed && !triggered;

    public override void Interact(PlayerInteractor who)
    {
        if (triggered || Locked)
            return;

        triggered = true;
        SceneLoader.Load(targetScene, spawnId);
    }

    // Update, tüm Start'lardan (SpawnOverride oyuncuyu taşır) sonra çalışır; konum o anda kesindir.
    private void Update()
    {
        if (interactor == null)
            interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor == null || !interactor.isActiveAndEnabled)
            return;

        // Oyuncu pivotu ayakta; bazı bölgelerin tabanı tam zemin hizasında, ayak biraz aşağıda kalırsa kaçırmasın.
        bool inside = area.bounds.Contains(interactor.transform.position + Vector3.up * 0.5f);
        if (!inside)
            armed = true;

        if (inside && interactor.Zone == null)
            interactor.Zone = this;
        else if (!inside && interactor.Zone == this)
            interactor.Zone = null;
    }
}
