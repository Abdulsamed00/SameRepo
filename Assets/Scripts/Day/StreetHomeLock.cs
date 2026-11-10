using UnityEngine;

/// <summary>
/// Sokaktaki "Eve dön" bölgesinin kilidi: günün görevleri bitmeden (ürün alınmadan; kuaför/klinik gününde o iş de
/// yapılmadan) eve dönülemez. Kilitliyken bildirim boştur, etkileşim hiç seçilmez.
/// </summary>
public class StreetHomeLock : MonoBehaviour
{
    [SerializeField] private SceneZone homeZone;

    private BeautyVisit[] visits;

    private void Awake()
    {
        if (homeZone == null)
            homeZone = GetComponent<SceneZone>();
        visits = FindObjectsByType<BeautyVisit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void Update()
    {
        if (homeZone == null)
            return;

        bool done = DayRoutine.BoughtToday;
        if (done)
        {
            foreach (BeautyVisit visit in visits)
            {
                if (visit != null && visit.Pending)
                {
                    done = false;
                    break;
                }
            }
        }

        homeZone.Locked = !done;
        homeZone.LockedPrompt = "";
    }
}
