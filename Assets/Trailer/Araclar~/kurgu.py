"""
SAME fragmanı kaba kurgu: Recordings/Trailer altındaki faz kayıtlarını KESIMLER listesine göre keser,
uç uca ekler, başa siyahtan açılma ekler ve tek MP4 yazar (H.264, 1080p30, yuv420p).

Kullanım:
    python kurgu.py <ffmpeg.exe> [cikti.mp4]

Kesim değiştirmek için KESIMLER listesini düzenleyip yeniden çalıştır. Müzik eklenince MUZIK yolunu doldur.
"""
import os
import subprocess
import sys

KOK = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "Recordings", "Trailer"))

# (dosya, başlangıç sn, bitiş sn, açıklama)
KESIMLER = [
    ("F1_Ev.mp4",        0.5, 12.5, "F1 eksiklik: kanepe, kalkış, dolap, ayna omuz üstü"),
    ("AI/Seedance_F1_Ayna.mp4", 0.0, 5.0, "F1 eksiklik: aynada burna dokunma (Seedance 2 Mini, 864x496)"),
    ("F2_Sokak.mp4",     0.0,  7.9, "F2 tüketim: cadde, simitçi, BUYNER"),
    ("F2_Magaza.mp4",    0.2,  6.0, "F2 tüketim: palto, kasa"),
    ("F3_Sokak.mp4",     0.0,  6.1, "F3 dönüşüm: sarışın, klinik, kuaför"),
    ("F3_Ayna.mp4",      0.0,  1.8, "F3 dönüşüm: aynada yeni burun ve saç"),
    ("Faz3a_Magaza.mp4", 0.0,  5.0, "F4 kaos: ürünler, kasa"),
    ("Faz3b_Sokak.mp4",  0.0,  7.5, "F4 kaos: panolar, tek tip kalabalık"),
    ("F5_Kalabalik.mp4", 0.0, 10.5, "F5 asimilasyon: tepeden kalabalık, SAME"),
]

ACILIS = 0.6     # baştaki siyahtan açılma (sn)

# Müzik (KOK'a göre yol; None = sessiz). Pixabay, Tomas_Herudek, "Trailer Rising Tension - Piano, Heartbeat & Clocks"
# (Pixabay lisansı: ticari kullanım serbest, atıf gerekmez). Parça MUZIK_SUSTUR kesiminin başladığı anda, kendi
# MUZIK_DORUK saniyesindeyken aniden susar (F5 kalabalık + daktilo sessizlikte gelir); başlangıç buna göre hesaplanır.
MUZIK = "Muzik/Piano_Heartbeat_Clocks_400972.mp3"
MUZIK_SUSTUR = "F5_Kalabalik.mp4"   # bu kesimin başında müzik kesilir (None = videonun sonuna kadar, sonda söner)
MUZIK_DORUK = 36.0                  # kesildiği anda parçanın kaçıncı saniyesinde olsun
MUZIK_GIRIS = 1.0                   # başta yumuşak giriş (sn)
MUZIK_SEVIYE = 0.9


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    ffmpeg = sys.argv[1]
    cikti = sys.argv[2] if len(sys.argv) > 2 else os.path.join(KOK, "SAME_Fragman_Kaba.mp4")

    girdiler, filtre, etiketler = [], [], []
    toplam = 0.0
    sustur = None
    for i, (dosya, a, b, _) in enumerate(KESIMLER):
        if MUZIK_SUSTUR and dosya == MUZIK_SUSTUR and sustur is None:
            sustur = toplam
        yol = os.path.join(KOK, dosya)
        if not os.path.exists(yol):
            print("Eksik kayit, atlandi:", dosya)
            continue
        n = len(girdiler) // 2   # her girdi iki argüman: "-i", yol
        girdiler += ["-i", yol]
        f = f"[{n}:v]trim=start={a}:end={b},setpts=PTS-STARTPTS,fps=30,"
        # Farklı oranlı klipler (yapay zekâ çıktısı) esnemesin: doldur ve taşanı kırp.
        f += "scale=1920:1080:force_original_aspect_ratio=increase:flags=lanczos,crop=1920:1080,setsar=1"
        if not etiketler and ACILIS > 0:
            f += f",fade=t=in:st=0:d={ACILIS}"
        filtre.append(f + f"[v{n}]")
        etiketler.append(f"[v{n}]")
        toplam += b - a

    filtre.append("".join(etiketler) + f"concat=n={len(etiketler)}:v=1:a=0[v]")
    komut = [ffmpeg, "-y", "-loglevel", "error"] + girdiler
    harita = ["-map", "[v]"]
    if MUZIK:
        komut += ["-i", os.path.join(KOK, MUZIK)]
        m = len(girdiler) // 2
        if sustur is not None:
            # Parça, sustur anında MUZIK_DORUK saniyesinde olacak şekilde yerleşir; orada 80 ms'de kesilir.
            basla = sustur - MUZIK_DORUK          # müziğin videodaki başlangıcı (negatifse parçanın başı kırpılır)
            p0 = max(0.0, -basla)
            sure = MUZIK_DORUK - p0
            gecikme = int(max(0.0, basla) * 1000)
            filtre.append(
                f"[{m}:a]atrim=start={p0}:end={MUZIK_DORUK},asetpts=PTS-STARTPTS,volume={MUZIK_SEVIYE},"
                f"afade=t=in:d={MUZIK_GIRIS},afade=t=out:st={sure - 0.08:.3f}:d=0.08,"
                f"adelay={gecikme}|{gecikme},apad=whole_dur={toplam}[a]")
            print(f"Muzik {max(0.0, basla):.2f} sn'de girer, {sustur:.2f} sn'de kesilir")
        else:
            filtre.append(f"[{m}:a]atrim=end={toplam},volume={MUZIK_SEVIYE},afade=t=out:st={toplam - 1.5}:d=1.5[a]")
        harita += ["-map", "[a]", "-c:a", "aac", "-b:a", "192k"]
    komut += ["-filter_complex", ";".join(filtre)] + harita
    komut += ["-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart", cikti]
    subprocess.run(komut, check=True)
    print(f"Yazildi: {cikti} ({toplam:.1f} sn)")


if __name__ == "__main__":
    main()
