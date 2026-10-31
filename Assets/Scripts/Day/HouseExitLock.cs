using UnityEngine;

/// <summary>
/// Günün ürünü alındıktan sonra evin dış kapısını kilitler: oyuncu o gün tekrar dışarı çıkamaz, uyuması gerekir.
/// </summary>
public class HouseExitLock : MonoBehaviour
{
    [SerializeField] private SceneDoor door;

    private void Update() => door.Locked = DayRoutine.BoughtToday;
}
