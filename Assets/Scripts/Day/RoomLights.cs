using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bir odanın ışıkları (05.11): gündüz hepsi kapalı (ev sadece güneşle aydınlanır, bake'te iç ışık yok), gece
/// <see cref="onByDefault"/> kadar açık başlar, <see cref="LightSwitch"/> ile açılıp kapanır. Işıklar Realtime: bake
/// edilmediği için gündüz/gece lightmap'lerinde yoktur ve istenildiği an kapatılabilir. Lamba/avize parlaması
/// (emission) ışıkla birlikte söner. Seçimler (<see cref="choices"/>) sadece o gece geçerlidir, sabah sıfırlanır.
/// </summary>
public class RoomLights : MonoBehaviour
{
    [SerializeField] private string roomId = "Oda";
    [SerializeField] private Light[] lights;
    [Tooltip("Işıkla birlikte sönen lamba/avize renderer'ları (emission kapanır).")]
    [SerializeField] private Renderer[] glowRenderers;
    [Tooltip("Işık kapalıyken Unlit (kendinden parlayan) ampullerin yerine geçen ışık alan cam malzemesi (08.11: kapalıyken " +
             "simsiyah görünüyordu). Boşsa koddan kırık beyaz, parlak bir URP Lit malzeme kurulur.")]
    [SerializeField] private Material offBulbMaterial;
    [Tooltip("Gece, oyuncu dokunmadan önce açık mı.")]
    [SerializeField] private bool onByDefault = true;

    private static readonly Dictionary<string, bool> choices = new Dictionary<string, bool>();
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private static Material defaultOffBulb;
    private readonly Dictionary<Renderer, Material> litMaterials = new Dictionary<Renderer, Material>();
    private bool applied;
    private bool appliedOn;

    public string RoomId => roomId;

    /// <summary>Şu an açık (sadece gece olabilir).</summary>
    public bool IsOn => DayRoutine.NightFell && (choices.TryGetValue(roomId, out bool chosen) ? chosen : onByDefault);

    public void Toggle()
    {
        if (!DayRoutine.NightFell)
            return;
        choices[roomId] = !IsOn;
        Refresh(true);
    }

    private void OnEnable() => Refresh(true);

    private void Update()
    {
        if (!DayRoutine.NightFell && choices.Count > 0)
            choices.Clear();
        Refresh(false);
    }

    private Material OffBulb
    {
        get
        {
            if (offBulbMaterial != null)
                return offBulbMaterial;
            if (defaultOffBulb == null)
            {
                defaultOffBulb = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Ampul_Sonuk (kod)" };
                defaultOffBulb.SetColor(BaseColorId, new Color(0.86f, 0.84f, 0.78f));
                defaultOffBulb.SetFloat("_Smoothness", 0.85f);
                defaultOffBulb.SetFloat("_Metallic", 0f);
            }
            return defaultOffBulb;
        }
    }

    private void Refresh(bool force)
    {
        bool on = IsOn;
        if (!force && applied && on == appliedOn)
            return;
        applied = true;
        appliedOn = on;

        foreach (Light l in lights)
            if (l != null)
                l.enabled = on;

        foreach (Renderer r in glowRenderers)
        {
            if (r == null)
                continue;
            // Unlit ampul (renk = parlama): kapalıyken siyaha boyamak yerine ışık alan cam malzemesine geçer.
            if (!litMaterials.ContainsKey(r) && r.sharedMaterial != null && r.sharedMaterial.shader.name.Contains("Unlit"))
                litMaterials[r] = r.sharedMaterial;
            if (litMaterials.TryGetValue(r, out Material glowMat))
            {
                r.sharedMaterial = on ? glowMat : OffBulb;
                r.SetPropertyBlock(null);
                continue;
            }
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            if (!on)
            {
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, Color.black);
            }
            r.SetPropertyBlock(block);
        }
    }
}
