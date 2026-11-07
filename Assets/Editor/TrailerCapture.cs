// Geçici: trailer çekimi için kare yakalayıcı (Play modunda, sabit 30 fps, 1920x1080).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering.Universal;

public static class TrailerCapture
{
    public const string Root = "C:/Users/Abdulsamed/AppData/Local/Temp/claude/5e72c56956a972d8/6717dbdc-bd78-4704-b985-03de177612eb/scratchpad/frames/";

    static int frame, total;
    static string dir;
    static Camera cam;
    static RenderTexture rt, rtOut;
    static Texture2D tex;
    static Func<float, Pose> path;
    static Func<float, float> fovAt;
    static Action<int> onFrame;
    public static bool Busy { get; private set; }
    public static Camera Cam => cam;

    public static float Smooth(float t) => t * t * (3 - 2 * t);

    public static Pose Look(Vector3 pos, Vector3 target) => new Pose(pos, Quaternion.LookRotation(target - pos));

    public static void Begin(string name, int frames, Func<float, Pose> camPath, Func<float, float> fov = null,
        Action<int> perFrame = null, int warmup = 5, int layersOff = 0)
    {
        if (Busy) Stop();
        total = frames; path = camPath; fovAt = fov ?? (t => 50f); onFrame = perFrame;
        dir = Root + name;
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        var go = new GameObject("Trailer_Kamera");
        cam = go.AddComponent<Camera>();
        var main = Camera.main;
        if (main) cam.CopyFrom(main);
        cam.cullingMask = (~0) & ~layersOff;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 1000f;
        cam.enabled = false;
        var d = go.AddComponent<UniversalAdditionalCameraData>();
        d.renderPostProcessing = true;
        d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        d.antialiasingQuality = AntialiasingQuality.High;
        d.renderShadows = true;

        rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = 4 };
        rtOut = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        frame = -warmup;
        Time.captureFramerate = 30;
        Insert();
        Busy = true;
    }

    static void Insert()
    {
        var loop = PlayerLoop.GetCurrentPlayerLoop();
        for (int i = 0; i < loop.subSystemList.Length; i++)
            if (loop.subSystemList[i].type == typeof(PostLateUpdate))
            {
                var list = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList);
                list.RemoveAll(s => s.type == typeof(TrailerCapture));
                list.Insert(0, new PlayerLoopSystem { type = typeof(TrailerCapture), updateDelegate = Tick });
                loop.subSystemList[i].subSystemList = list.ToArray();
            }
        PlayerLoop.SetPlayerLoop(loop);
    }

    static void Tick()
    {
        if (!Application.isPlaying || cam == null) { Stop(); return; }
        float t = Mathf.Clamp01(Mathf.Max(0, frame) / (float)Mathf.Max(1, total - 1));
        try
        {
            onFrame?.Invoke(frame);
            var pose = path(t);
            cam.transform.SetPositionAndRotation(pose.position, pose.rotation);
            cam.fieldOfView = fovAt(t);
            if (frame >= 0)
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                Graphics.Blit(rt, rtOut);
                var prev = RenderTexture.active;
                RenderTexture.active = rtOut;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                RenderTexture.active = prev;
                File.WriteAllBytes(dir + "/f" + frame.ToString("0000") + ".jpg", tex.EncodeToJPG(94));
            }
        }
        catch (Exception e) { Debug.LogException(e); Stop(); return; }
        frame++;
        if (frame >= total) Stop();
    }

    public static void Stop()
    {
        var loop = PlayerLoop.GetCurrentPlayerLoop();
        for (int i = 0; i < loop.subSystemList.Length; i++)
        {
            if (loop.subSystemList[i].subSystemList == null) continue;
            var subs = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList);
            subs.RemoveAll(s => s.type == typeof(TrailerCapture));
            loop.subSystemList[i].subSystemList = subs.ToArray();
        }
        PlayerLoop.SetPlayerLoop(loop);
        Time.captureFramerate = 0;
        if (cam) UnityEngine.Object.Destroy(cam.gameObject);
        if (rt) rt.Release();
        if (rtOut) rtOut.Release();
        if (dir != null && Directory.Exists(dir)) File.WriteAllText(dir + "/done.txt", frame.ToString());
        Busy = false;
    }
}
