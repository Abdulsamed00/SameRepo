using UnityEngine;

/// <summary>
/// Evin dış kapısının kilidi, günlük döngünün sırasına göre:
/// ilk gece çıkılmaz; sabah cüzdan alınmadan (<see cref="DayRoutine.WalletTaken"/>) çıkılmaz; 4. gün sabah aynaya
/// bakmadan çıkılmaz; günün ürünü alındıktan sonra o gün tekrar çıkılamaz; son günde uyuyunca sabah kapı açılır (final).
/// 05.11: oyuncuya ne yapması gerektiği yazılmaz. Kilitliyken bildirim boştur (etkileşim hiç seçilmez), sadece
/// durum bildiren iki cümle kaldı (gece, alışverişten sonra).
/// </summary>
public class HouseExitLock : MonoBehaviour
{
    [SerializeField] private SceneDoor door;
    [SerializeField] private string nightPrompt = "Bu saatte dışarı çıkılmaz";
    [SerializeField] private string enoughPrompt = "Bugünlük dışarısı yeter";

    private void Update()
    {
        if (DayRoutine.FinalReady)
        {
            door.Locked = DayRoutine.DressPending;
            door.LockedPrompt = "";
            return;
        }

        if (DayRoutine.BoughtToday)
        {
            door.Locked = true;
            door.LockedPrompt = GameDay.IsLastDay ? "" : enoughPrompt;
            return;
        }

        if (DayRoutine.Prologue)
        {
            door.Locked = true;
            door.LockedPrompt = nightPrompt;
            return;
        }

        door.Locked = DayRoutine.MorningMirrorPending || !DayRoutine.WalletTaken || DayRoutine.DressPending;
        door.LockedPrompt = "";
    }
}
