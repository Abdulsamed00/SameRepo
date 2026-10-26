using UnityEngine;

/// <summary>
/// E ile oturulan yer (koltuk, kanepe, sandalye). Oturma/kalkma akışı oyuncudaki
/// <see cref="PlayerSitController"/>'dadır; bu bileşen sadece nereye ve hangi yöne oturulacağını söyler.
/// Kanepe gibi birden fazla yeri olan mobilyada oyuncuya en yakın oturma noktası seçilir.
/// </summary>
public class Seat : Interactable
{
    [Tooltip("Oturan oyuncunun kök noktaları: zeminde, yüzü oturma yönüne (+z) bakar. Boşsa bu obje.")]
    [SerializeField] private Transform[] sitPoints;
    [Tooltip("Kalkınca oyuncunun duracağı yer: oturma noktasının bu kadar önü (m).")]
    [SerializeField] private float standDistance = 0.45f;

    public override InteractionKey Key => InteractionKey.Secondary;
    public bool Occupied { get; set; }

    public Transform ClosestSitPoint(Vector3 from)
    {
        if (sitPoints == null || sitPoints.Length == 0)
            return transform;

        Transform best = sitPoints[0];
        float bestDist = float.MaxValue;
        foreach (Transform p in sitPoints)
        {
            float d = (p.position - from).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = p;
            }
        }
        return best;
    }

    public Vector3 StandPosition(Transform sitPoint) => sitPoint.position + sitPoint.forward * standDistance;

    public override bool CanInteract(PlayerInteractor interactor)
    {
        if (Occupied)
            return false;
        PlayerSitController sitter = interactor.GetComponent<PlayerSitController>();
        return sitter != null && sitter.CanSit;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        PlayerSitController sitter = interactor.GetComponent<PlayerSitController>();
        if (sitter != null)
            sitter.Sit(this);
    }
}
