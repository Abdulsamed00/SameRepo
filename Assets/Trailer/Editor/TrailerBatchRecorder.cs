using UnityEditor;
using UnityEngine;

/// <summary>
/// Fragman sahnelerini sırayla kaydeder: her biri için oyun + fragman sahnesini açar, kaydı kurar, Play'e girer,
/// kayıt bitince Play'den çıkar ve sıradakine geçer. Sıra SessionState'te durur (Play'e girişteki domain
/// yenilemesinden sağ çıkar). Kayıt sürerken script düzenleme: Unity derler ve kayıt yarıda kalır.
/// </summary>
public static class TrailerBatchRecorder
{
    private const string QueueKey = "Trailer_BatchQueue";
    private const string ActiveKey = "Trailer_BatchActive";

    /// <param name="items">"ad|oyunSahnesiYolu" çiftleri.</param>
    public static void Start(params string[] items)
    {
        SessionState.SetString(QueueKey, string.Join(";", items));
        SessionState.EraseString(ActiveKey);
        Hook();
    }

    public static string Status => "sira: " + SessionState.GetString(QueueKey, "") + " | aktif: " + SessionState.GetString(ActiveKey, "")
        + " | kayit: " + TrailerRecorderTool.Status;

    [InitializeOnLoadMethod]
    private static void Restore()
    {
        if (!string.IsNullOrEmpty(SessionState.GetString(QueueKey, "")) || !string.IsNullOrEmpty(SessionState.GetString(ActiveKey, "")))
            Hook();
    }

    private static void Hook()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        string active = SessionState.GetString(ActiveKey, "");

        if (!string.IsNullOrEmpty(active))
        {
            // Kayıt bitti mi: kaydedici boşta ve son dosya bu kaydın dosyası.
            if (EditorApplication.isPlaying && TrailerRecorderTool.Status.StartsWith("bos") && TrailerRecorderTool.Status.Contains(active + ".mp4"))
            {
                Debug.Log("Trailer toplu kayit: bitti " + active);
                SessionState.EraseString(ActiveKey);
                EditorApplication.isPlaying = false;
            }
            return;
        }

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string queue = SessionState.GetString(QueueKey, "");
        if (string.IsNullOrEmpty(queue))
        {
            EditorApplication.update -= Tick;
            Debug.Log("Trailer toplu kayit: hepsi bitti");
            return;
        }
        string[] items = queue.Split(';');
        SessionState.SetString(QueueKey, string.Join(";", items, 1, items.Length - 1));
        string[] pair = items[0].Split('|');
        SessionState.SetString(ActiveKey, pair[0]);
        Application.runInBackground = true;
        TrailerBuilder.Open(pair[0], pair[1]);
        TrailerRecorderTool.Arm(pair[0]);
        EditorApplication.isPlaying = true;
        Debug.Log("Trailer toplu kayit: basladi " + pair[0]);
    }
}
