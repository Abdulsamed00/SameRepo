using UnityEngine;

/// <summary>
/// E ile oturulan yer (koltuk, kanepe, sandalye). Oturma/kalkma akışı oyuncudaki
/// <see cref="PlayerSitController"/>'dadır; bu bileşen sadece nereye ve hangi yöne oturulacağını söyler.
/// Kanepe gibi birden fazla yeri olan mobilyada oyuncuya en yakın oturma noktası seçilir.
/// </summary>
public class Seat : Interactable
{
    // Koltuğun önüne gelince.
    protected override float DefaultMaxDistance => 1.6f;

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

    /// <summary>
    /// Oyuncu en yakın oturma noktasının önünde mi (oturma yönü tarafında). Kanepenin arkasından ya da
    /// sırtlık tarafından oturulmasın diye.
    /// </summary>
    public bool IsInFront(Vector3 playerPosition)
    {
        Transform point = ClosestSitPoint(playerPosition);
        Vector3 toPlayer = playerPosition - point.position;
        toPlayer.y = 0f;
        return Vector3.Dot(toPlayer, point.forward) > 0f;
    }

    public Vector3 StandPosition(Transform sitPoint) => sitPoint.position + sitPoint.forward * standDistance;

    public override bool CanInteract(PlayerInteractor interactor)
    {
        if (Occupied)
            return false;
        PlayerSitController sitter = interactor.GetComponent<PlayerSitController>();
        return sitter != null && sitter.CanSit && IsInFront(interactor.transform.position);
    }

    public override void Interact(PlayerInteractor interactor)
    {
        PlayerSitController sitter = interactor.GetComponent<PlayerSitController>();
        if (sitter != null)
            sitter.Sit(this);
    }
}
