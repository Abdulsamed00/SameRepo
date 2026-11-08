using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Ayarlar ve duraklatma ekranlarının bulanık arka planı. Shader yerine küçülterek bulanıklaştırır: görüntü
/// yarıya yarıya 1/32'ye indirilip 1/2'ye geri büyütülür (her adım bilineer ortalama; az adımda kareli görünüyor).
/// </summary>
public static class UiBlur
{
    public static RenderTexture Blur(Texture source)
    {
        int w = Mathf.Max(1, source.width / 2);
        int h = Mathf.Max(1, source.height / 2);
        RenderTexture current = Temp(w, h);
        Graphics.Blit(source, current);

        for (int i = 0; i < 4 && w > 8; i++)
        {
            w /= 2;
            h /= 2;
            RenderTexture next = Temp(w, h);
            Graphics.Blit(current, next);
            RenderTexture.ReleaseTemporary(current);
            current = next;
        }
        // Ekran görüntüsünün alfa'sı URP'de her zaman 1 değil (gece, parlak/karanlık bölgelerde düşebiliyor):
        // bulanık görüntü yarı saydam kalıp arkadaki canlı sahneyi ("parlama") gösteriyordu. En küçük boyutta
        // (birkaç bin piksel) alfa 1'e sabitlenir, sonra büyütülür.
        Texture2D small = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = current;
        small.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        RenderTexture.active = previous;
        Color32[] pixels = small.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i].a = 255;
        small.SetPixels32(pixels);
        small.Apply(false);
        RenderTexture.ReleaseTemporary(current);

        Texture upSource = small;
        current = null;
        for (int i = 0; i < 3; i++)
        {
            w *= 2;
            h *= 2;
            RenderTexture next = Temp(w, h);
            Graphics.Blit(upSource, next);
            if (current != null)
                RenderTexture.ReleaseTemporary(current);
            current = next;
            upSource = current;
        }
        UnityEngine.Object.Destroy(small);

        RenderTexture result = new RenderTexture(w * 2, h * 2, 0) { name = "Bulanik_Arkaplan", filterMode = FilterMode.Bilinear };
        Graphics.Blit(current, result);
        RenderTexture.ReleaseTemporary(current);
        return result;
    }

    /// <summary>Karenin sonunda ekranın görüntüsünü alıp bulanıklaştırır (zaman durmuşken de çalışır).</summary>
    public static IEnumerator CaptureScreen(Action<RenderTexture> done)
    {
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        RenderTexture blurred = Blur(shot);
        UnityEngine.Object.Destroy(shot);
        done(blurred);
    }

    private static RenderTexture Temp(int w, int h)
    {
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0);
        rt.filterMode = FilterMode.Bilinear;
        return rt;
    }
}
