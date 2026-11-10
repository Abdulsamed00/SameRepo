using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// Fragman çekimini MP4'e kaydeder (Unity Recorder). Play modunda <see cref="TrailerController"/> Timeline'ı
/// başlattığı kare kayıt başlar, Timeline süresi kadar kare alınır ve durur. Kare hızı sabit (oyun zamanı kayda
/// kilitlenir), bu yüzden kayıt sırasında editörün yavaşlaması görüntüyü etkilemez.
/// Çıktı: proje klasörünün yanında Recordings/Trailer/&lt;ad&gt;.mp4
/// </summary>
public static class TrailerRecorderTool
{
    public const int FrameRate = 30;
    public const int Width = 1920;
    public const int Height = 1080;

    private static RecorderController controller;
    private static string pendingName;
    private static string lastFile;

    public static string OutputFolder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Recordings", "Trailer");

    public static string Status =>
        controller != null && controller.IsRecording() ? "kayit suruyor: " + lastFile
        : pendingName != null ? "bekliyor: " + pendingName
        : "bos" + (lastFile != null ? " (son: " + lastFile + ")" : "");

    /// <summary>Play'e girmeden önce çağrılır: Timeline başlayınca bu adla kayıt alınır.</summary>
    public static void Arm(string name)
    {
        pendingName = name;
        SessionState.SetString("Trailer_PendingName", name);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [InitializeOnLoadMethod]
    private static void Restore()
    {
        // Play'e girerken domain yenilenir; bekleyen kayıt adı SessionState'ten geri alınır.
        string name = SessionState.GetString("Trailer_PendingName", "");
        if (!string.IsNullOrEmpty(name))
        {
            pendingName = name;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
            return;

        if (controller == null && pendingName != null && TrailerController.Rolling)
        {
            Begin(pendingName, Mathf.RoundToInt(TrailerController.Duration * FrameRate));
            pendingName = null;
            SessionState.EraseString("Trailer_PendingName");
        }
        else if (controller != null && !controller.IsRecording())
        {
            controller = null;
            EditorApplication.update -= Tick;
            Debug.Log("Trailer kaydı bitti: " + lastFile);
        }
    }

    private static void Begin(string name, int frames)
    {
        Directory.CreateDirectory(OutputFolder);
        RecorderControllerSettings settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        MovieRecorderSettings movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "Trailer";
        movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
        };
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = Width, OutputHeight = Height };
        movie.CaptureAudio = false;
        lastFile = Path.Combine(OutputFolder, name);
        movie.OutputFile = lastFile;

        settings.AddRecorderSettings(movie);
        settings.SetRecordModeToFrameInterval(0, Mathf.Max(1, frames) - 1);
        settings.FrameRate = FrameRate;
        settings.FrameRatePlayback = FrameRatePlayback.Constant;
        settings.CapFrameRate = false;

        controller = new RecorderController(settings);
        controller.PrepareRecording();
        controller.StartRecording();
        lastFile += ".mp4";
    }
}
