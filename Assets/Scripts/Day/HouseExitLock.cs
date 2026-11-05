using UnityEngine;

/// <summary>
/// Evin dış kapısının kilidi, günlük döngünün sırasına göre:
/// reklam izlenmeden çıkılamaz; günün ürünü alındıktan sonra o gün tekrar çıkılamaz (uyumak gerekir);
/// son günde giyip poz verip uyuyunca, sabah kapı açılır (final).
/// </summary>
public class HouseExitLock : MonoBehaviour
{
    [SerializeField] private SceneDoor door;
    [SerializeField] private string watchAdPrompt = "Önce televizyona bakayım";
    [SerializeField] private string morningMirrorPrompt = "Önce aynaya bir bakayım";
    [SerializeField] private string enoughPrompt = "Bugünlük dışarısı yeter";
    [SerializeField] private string wearFirstPrompt = "Önce yeni kıyafetimi giyeyim";
    [SerializeField] private string poseFirstPrompt = "Önce aynada kendime bakayım";
    [SerializeField] private string sleepFirstPrompt = "Önce bir uyuyayım";

    private void Update()
    {
        if (DayRoutine.FinalReady)
        {
            door.Locked = false;
            return;
        }

        bool needAd = !DayRoutine.AdRequirementMet;
        bool bought = DayRoutine.BoughtToday;
        bool needMirror = DayRoutine.MorningMirrorPending;
        door.Locked = needAd || needMirror || bought;
        if (needAd)
            door.LockedPrompt = watchAdPrompt;
        else if (needMirror)
            door.LockedPrompt = morningMirrorPrompt;
        else if (!GameDay.IsLastDay)
            door.LockedPrompt = enoughPrompt;
        else
            door.LockedPrompt = !DayRoutine.WornToday ? wearFirstPrompt
                : !DayRoutine.PosedToday ? poseFirstPrompt
                : sleepFirstPrompt;
    }
}
