using System.Collections;
using UnityEngine;

/// <summary>
/// Cüzdan (05.11; 07.11'den beri Sıla'nın modeli, koridorda giriş kapısının yanındaki ayakkabılığın üstünde): sabah F ile alınır, el uzanır, cüzdan cebe girer (görünmez olur). Alınmadan evden
/// çıkılmaz (<see cref="HouseExitLock"/>): "dışarı çıkıyorum" kararını bu verir. Gece eve dönünce yine yerindedir.
/// Alınmayı beklerken (sabah, henüz alınmadı) çevresindeki beyaz kenar (Shader Graph outline) nabız gibi parlar
/// (oyuncunun gözü masaya gitsin; kapıdan önce hatırlatma). Renkli ışık ve emission yok (07.11, kullanıcı isteği).
/// </summary>
public class Wallet : Interactable
{
    protected override float DefaultMaxDistance => 1.5f;

    [SerializeField] private string takePrompt = "Cüzdanı al";
    [SerializeField] private AudioClip takeSound;

    [Header("Parlama")]
    [SerializeField] private float pulseSpeed = 2.2f;

    [Header("Beyaz kenar (outline)")]
    [Tooltip("Kenar malzemesi (Assets/Shaders/Outline/Cuzdan_Outline_Model.mat, shader 'Same/OutlineNormal': model kutu " +
             "olmadığı için normal boyunca şişirir): gövdeye ikinci malzeme olarak eklenir, beyaz, bloom alan bir kenar çizer.")]
    [SerializeField] private Material outlineMaterial;
    [Tooltip("Kenar parlaklığı (HDR çarpanı; bloom bunun üstünü yayar).")]
    [SerializeField] private float outlineIntensity = 3f;
    [Tooltip("Kenar kalınlığı (m).")]
    [SerializeField] private float outlineWidth = 0.0035f;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    private Renderer[] renderers;
    private Collider[] colliders;
    private Material outline;
    private bool taking;
    private float glow;

    public override string Prompt => takePrompt;

    private static bool OnTable => !DayRoutine.WalletTaken || DayRoutine.NightFell;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        // Kenar: en büyük parçaya (gövde) ikinci malzeme. Toplamalı saydam; renk siyahken görünmez.
        if (outlineMaterial != null)
        {
            Renderer body = null;
            foreach (Renderer r in renderers)
            {
                if (body == null || r.bounds.size.sqrMagnitude > body.bounds.size.sqrMagnitude)
                    body = r;
            }
            if (body != null)
            {
                outline = new Material(outlineMaterial);
                outline.SetColor(OutlineColorId, Color.black);
                outline.SetFloat(OutlineWidthId, outlineWidth);
                var mats = new System.Collections.Generic.List<Material>(body.materials) { outline };
                body.materials = mats.ToArray();
            }
        }
    }

    private void Start() => SetVisible(OnTable);

    private void OnEnable() => DayRoutine.Morning += Refresh;

    private void OnDisable() => DayRoutine.Morning -= Refresh;

    private void Refresh() => SetVisible(OnTable);

    private void Update()
    {
        // Sabah, cüzdan alınmadıysa: nabız gibi parlar. Alınır ya da gece olursa söner.
        bool waiting = DayRoutine.MorningAdSeen && !DayRoutine.NightFell && !DayRoutine.WalletTaken && !taking
                       && !PauseMenu.IsPaused;
        glow = Mathf.MoveTowards(glow, waiting ? 1f : 0f, 2f * Time.unscaledDeltaTime);
        if (glow <= 0f)
        {
            if (outline != null)
                outline.SetColor(OutlineColorId, Color.black);
            return;
        }

        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * pulseSpeed);
        if (outline != null)
        {
            float v = outlineIntensity * (0.45f + 0.55f * pulse) * glow;
            outline.SetColor(OutlineColorId, new Color(v, v, v, 1f));
        }
    }

    public override bool CanInteract(PlayerInteractor interactor) =>
        !taking && DayRoutine.MorningAdSeen && !DayRoutine.NightFell && !DayRoutine.WalletTaken && !interactor.IsHolding;

    public override void Interact(PlayerInteractor interactor)
    {
        if (CanInteract(interactor))
            StartCoroutine(Take(interactor));
    }

    private IEnumerator Take(PlayerInteractor interactor)
    {
        taking = true;
        PlayerBody body = interactor.GetComponentInChildren<PlayerBody>();
        float reach = body != null ? body.PlayPickup(transform.position) : 0f;
        interactor.BusyFor(reach + 0.5f); // eli çekene kadar yürüyemez
        yield return new WaitForSeconds(reach);
        GameAudio.PlayAt(takeSound, transform.position);
        DayRoutine.NotifyWalletTaken();
        SetVisible(false);
        taking = false;
    }

    private void SetVisible(bool visible)
    {
        if (!visible)
            glow = 0f;
        foreach (Renderer r in renderers)
            r.enabled = visible;
        foreach (Collider c in colliders)
            c.enabled = visible;
    }
}
