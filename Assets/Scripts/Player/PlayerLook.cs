using System;

/// <summary>
/// Oyuncunun kuaför ve klinikte değişen görünümü (sarı saç; burun ve dudak). Statik: sahne geçişlerinde korunur.
/// Karaktere <see cref="PlayerOutfitView"/> yansıtır.
/// </summary>
public static class PlayerLook
{
    public static bool HairDyed { get; private set; }
    public static bool SurgeryDone { get; private set; }

    public static event Action Changed;

    public static void DyeHair()
    {
        HairDyed = true;
        Changed?.Invoke();
    }

    public static void HaveSurgery()
    {
        SurgeryDone = true;
        Changed?.Invoke();
    }

    public static void Reset()
    {
        HairDyed = false;
        SurgeryDone = false;
    }
}
