using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Geçici test aracı: Play'e basıldığı andan itibaren oyunun ne kadar sürdüğünü sağ üstte gösterir.
/// Toplam süre ve her günün süresi yazılır, gün bitince Console'a da düşer. Duraklatma menüsünde sayaç durur,
/// final başlayınca (<see cref="FinalSequence"/>) donar. Kodla, sahneden bağımsız oluşur (DontDestroyOnLoad).
/// Kaldırmak için bu dosyayı ve FinalSequence.Run'daki <c>PlaytimeTimer.Stop()</c> satırını sil.
/// </summary>
public class PlaytimeTimer : MonoBehaviour
{
    private static PlaytimeTimer instance;

    private readonly List<string> laps = new List<string>();
    private readonly StringBuilder builder = new StringBuilder();
    private TextMeshProUGUI label;
    private float total;
    private float dayStart;
    private int day;
    private bool stopped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null)
            return;
        GameObject go = new GameObject("Oyun_Suresi_Sayaci");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PlaytimeTimer>();
    }

    /// <summary>Finalde çağrılır: sayaç son değerinde kalır.</summary>
    public static void Stop()
    {
        if (instance == null || instance.stopped)
            return;
        instance.CloseDay();
        instance.stopped = true;
        Debug.Log($"[Sayaç] Oyun bitti: {Format(instance.total)}");
    }

    private void Awake()
    {
        Canvas canvas = UiKit.CreateCanvas("Sayac_Canvas", 1000, transform);
        label = UiKit.CreateText("Sure", canvas.transform, "", UiKit.FontKind.Mono, 26f, alignment: TextAlignmentOptions.TopRight);
        UiKit.Place(label.rectTransform, new Vector2(1f, 1f), new Vector2(-32f, -24f), new Vector2(500f, 400f));
        day = DayRoutine.DayNumber;
    }

    private void Update()
    {
        if (!stopped)
        {
            if (!PauseMenu.IsPaused)
                total += Time.unscaledDeltaTime;
            if (DayRoutine.DayNumber != day)
            {
                CloseDay();
                day = DayRoutine.DayNumber;
            }
        }

        builder.Clear();
        builder.Append(Format(total));
        if (stopped)
            builder.Append("  BİTTİ");
        else
            builder.Append('\n').Append(day).Append(". gün  ").Append(Format(total - dayStart));
        foreach (string lap in laps)
            builder.Append('\n').Append(lap);
        label.SetText(builder);
    }

    private void CloseDay()
    {
        string lap = $"{day}. gün  {Format(total - dayStart)}";
        laps.Add(lap);
        Debug.Log($"[Sayaç] {lap} (toplam {Format(total)})");
        dayStart = total;
    }

    private static string Format(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return $"{s / 60:00}:{s % 60:00}";
    }
}
