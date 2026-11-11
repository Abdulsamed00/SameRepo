using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fragman ön izlemesi: Play'e girmeden <see cref="Arm"/> çağrılır; Timeline zamanı (fragman sahnesindeki PlayableDirector) verilen
/// anlara gelince fragman kamerası 960x540 PNG'ye çizilir (overlay arayüz görünmez). Kayıt almadan çekimleri
/// kontrol etmek için.
/// </summary>
public static class TrailerPreviewTool
{
    private static readonly List<float> times = new List<float>();
    private static string folder, prefix;
    private static int next;

    public static string Status => times.Count == 0 ? "bos" : next + "/" + times.Count;

    public static void Arm(string outFolder, string namePrefix, params float[] at)
    {
        folder = outFolder;
        prefix = namePrefix;
        times.Clear();
        times.AddRange(at);
        times.Sort();
        next = 0;
        // Play'e girişte domain yeniden yüklenir, statikler sıfırlanır: istek SessionState'te saklanır.
        SessionState.SetString(Key, folder + "|" + prefix + "|" + string.Join(";", times.ConvertAll(t => t.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private const string Key = "Trailer_Preview";

    [InitializeOnLoadMethod]
    private static void Restore()
    {
        string saved = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(saved))
            return;
        string[] parts = saved.Split('|');
        if (parts.Length < 3)
            return;
        folder = parts[0];
        prefix = parts[1];
        times.Clear();
        foreach (string t in parts[2].Split(';'))
        {
            if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                times.Add(v);
        }
        next = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!Application.isPlaying || next >= times.Count || !TrailerController.Rolling)
            return;
        UnityEngine.Playables.PlayableDirector director = Object.FindFirstObjectByType<UnityEngine.Playables.PlayableDirector>();
        if (director == null || director.time < times[next])
            return;
        GameObject go = GameObject.Find("Trailer_Kamera");
        Camera cam = go != null ? go.GetComponent<Camera>() : null;
        if (cam != null)
        {
            RenderTexture rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = previous;
            rt.Release();
            string label = times[next].ToString("F1", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_');
            File.WriteAllBytes(Path.Combine(folder, prefix + "_" + label + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        next++;
        if (next >= times.Count)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseString(Key);
            times.Clear();
        }
    }
}
