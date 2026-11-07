using System.Collections;
using UnityEngine;

/// <summary>
/// Masadaki cüzdan (05.11): sabah F ile alınır, el uzanır, cüzdan cebe girer (görünmez olur). Alınmadan evden
/// çıkılmaz (<see cref="HouseExitLock"/>): "dışarı çıkıyorum" kararını bu verir. Gece eve dönünce yine masadadır.
/// Alınmayı beklerken (sabah, henüz alınmadı) yavaşça parlar: gövde sıcak bir tonla yanıp söner ve çevresine hafif
/// ışık verir (oyuncunun gözü masaya gitsin; kapıdan önce hatırlatma).
/// </summary>
public class Wallet : Interactable
{
    protected override float DefaultMaxDistance => 1.5f;

    [SerializeField] private string takePrompt = "Cüzdanı al";
    [SerializeField] private AudioClip takeSound;

    [Header("Parıltı")]
    [SerializeField] private Color glowColor = new Color(1f, 0.78f, 0.35f);
    [SerializeField] private float glowStrength = 0.5f;
    [SerializeField] private float pulseSpeed = 2.2f;
    [SerializeField] private float lightIntensity = 0.6f;
    [SerializeField] private float lightRange = 1.1f;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    private Renderer[] renderers;
    private Collider[] colliders;
    private Material[] glowMaterials;
    private Light glowLight;
    private bool taking;
    private float glow;

    public override string Prompt => takePrompt;

    private static bool OnTable => !DayRoutine.WalletTaken || DayRoutine.NightFell;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        // Malzeme kopyaları (asset'e dokunmadan emission açılır).
        var list = new System.Collections.Generic.List<Material>();
        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor(EmissionId, Color.black);
                list.Add(m);
            }
        }
        glowMaterials = list.ToArray();

        GameObject lightObject = new GameObject("Parilti");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        glowLight = lightObject.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.range = lightRange;
        glowLight.intensity = 0f;
        glowLight.shadows = LightShadows.None;
        glowLight.lightmapBakeType = LightmapBakeType.Realtime;
        glowLight.enabled = false;
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
            if (glowLight.enabled)
            {
                glowLight.enabled = false;
                foreach (Material m in glowMaterials)
                    m.SetColor(EmissionId, Color.black);
            }
            return;
        }

        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * pulseSpeed);
        Color emission = glowColor * (glowStrength * pulse * glow);
        foreach (Material m in glowMaterials)
            m.SetColor(EmissionId, emission);
        glowLight.enabled = true;
        glowLight.intensity = lightIntensity * pulse * glow;
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
