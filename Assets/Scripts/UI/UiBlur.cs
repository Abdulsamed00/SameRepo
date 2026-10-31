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
        for (int i = 0; i < 3; i++)
        {
            w *= 2;
            h *= 2;
            RenderTexture next = Temp(w, h);
            Graphics.Blit(current, next);
            RenderTexture.ReleaseTemporary(current);
            current = next;
        }

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
