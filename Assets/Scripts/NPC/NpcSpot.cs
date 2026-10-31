using UnityEngine;

public enum NpcActivity
{
    Stand,
    Sit,
    Talk
}

/// <summary>
/// NPC'lerin uğrayıp bir süre vakit geçirdiği nokta: bank (Sit), vitrin/tezgâh önü (Stand), sohbet yeri (Talk).
/// Her slot bir NPC alır; slot transform'u NPC kökünün oradaki pozudur (oturmada y dahil), ileri yönü NPC'nin bakışıdır.
/// Slot yoksa objenin kendisi tek slottur. Talk noktasında yanında biri varken sohbet, yalnızken bekleme animasyonu oynar.
/// </summary>
public class NpcSpot : MonoBehaviour
{
    [SerializeField] private NpcActivity activity = NpcActivity.Stand;
    [SerializeField] private Transform[] slots;
    [Tooltip("Noktada kalma süresi (sn, min-max).")]
    [SerializeField] private Vector2 duration = new Vector2(10f, 25f);
    [Tooltip("NPC'nin yürüyerek geldiği nokta slotun bu kadar önündedir (m). Oturmada bankın önü, ayakta 0.")]
    [SerializeField] private float approachDistance;

    private NpcAgent[] occupants;

    public NpcActivity Activity => activity;
    public int SlotCount => slots != null && slots.Length > 0 ? slots.Length : 1;
    public float RandomDuration => Random.Range(duration.x, duration.y);
    public int FreeSlots { get; private set; }
    public int OccupantCount => SlotCount - FreeSlots;

    private void Awake()
    {
        occupants = new NpcAgent[SlotCount];
        FreeSlots = SlotCount;
    }

    public Transform Slot(int index) => slots != null && slots.Length > 0 ? slots[index] : transform;

    public Vector3 ApproachPoint(int index)
    {
        Transform s = Slot(index);
        Vector3 p = s.position + s.forward * approachDistance;
        p.y = transform.position.y;
        return p;
    }

    /// <summary>Boş bir slotu NPC'ye ayırır, slot indeksini döner (yoksa -1). Doluluk varsa yanına oturur.</summary>
    public int Reserve(NpcAgent agent)
    {
        if (FreeSlots == 0)
            return -1;

        int pick = -1;
        int start = Random.Range(0, occupants.Length);
        for (int i = 0; i < occupants.Length; i++)
        {
            int k = (start + i) % occupants.Length;
            if (occupants[k] == null)
            {
                pick = k;
                break;
            }
        }
        occupants[pick] = agent;
        FreeSlots--;
        return pick;
    }

    public void Release(NpcAgent agent)
    {
        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == agent)
            {
                occupants[i] = null;
                FreeSlots++;
            }
        }
    }

    /// <summary>Noktaya varmış başka bir NPC var mı (sohbet için).</summary>
    public bool HasCompany(NpcAgent agent)
    {
        foreach (NpcAgent o in occupants)
        {
            if (o != null && o != agent && o.AtSpot)
                return true;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = activity == NpcActivity.Sit ? Color.cyan : activity == NpcActivity.Talk ? Color.yellow : Color.green;
        for (int i = 0; i < SlotCount; i++)
        {
            Transform s = Slot(i);
            if (s == null)
                continue;
            Gizmos.DrawWireSphere(s.position + Vector3.up * 0.9f, 0.25f);
            Gizmos.DrawLine(s.position + Vector3.up * 0.9f, s.position + Vector3.up * 0.9f + s.forward * 0.5f);
        }
    }
}
