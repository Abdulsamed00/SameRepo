using UnityEngine;

/// <summary>
/// NPC'yi dünya noktaları arasında sırayla yürütür (iki nokta = gidiş-dönüş). Animasyon root motion kullanmaz;
/// hareketi bu script verir. Geçici: NavMesh tabanlı NPC sistemi gelince kalkacak.
/// </summary>
public class NpcPatrol : MonoBehaviour
{
    [SerializeField] private Vector3[] points;
    [Tooltip("Yürüme animasyonuyla uyumlu hız (m/sn).")]
    [SerializeField] private float speed = 1.3f;
    [SerializeField] private float turnSpeed = 240f;

    public Vector3[] Points
    {
        get => points;
        set => points = value;
    }

    public float Speed
    {
        get => speed;
        set => speed = value;
    }

    private int next = 1;

    private void Start()
    {
        if (points == null || points.Length < 2)
        {
            enabled = false;
            return;
        }
        transform.position = points[0];
    }

    private void Update()
    {
        Vector3 target = points[next];
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.05f * 0.05f)
        {
            next = (next + 1) % points.Length;
            return;
        }

        Quaternion look = Quaternion.LookRotation(toTarget);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);

        // Dönüş tamamlanmadan ilerleme: köşede olduğu yerde dönsün, kayarak yürümesin.
        if (Quaternion.Angle(transform.rotation, look) < 25f)
        {
            Vector3 pos = transform.position;
            pos += toTarget.normalized * Mathf.Min(speed * Time.deltaTime, toTarget.magnitude);
            transform.position = pos;
        }
    }
}
