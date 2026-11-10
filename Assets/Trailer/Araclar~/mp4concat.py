# Aynı ayarla kodlanmış (H.264, tek video izi, B-kare yok) MP4'leri yeniden kodlamadan uç uca ekler.
import struct, sys
def boxes(b, s, e):
    while s + 8 <= e:
        n, t = struct.unpack(">I4s", b[s:s+8]); h = 8
        if n == 1: n = struct.unpack(">Q", b[s+8:s+16])[0]; h = 16
        if n == 0: n = e - s
        yield t.decode("latin1"), s, h, n; s += n
def find(b, s, e, path):
    for t, o, h, n in boxes(b, s, e):
        if t == path[0]:
            return (o, h, n) if len(path) == 1 else find(b, o + h, o + n, path[1:])
    return None
def box(t, payload): return struct.pack(">I4s", len(payload) + 8, t.encode("latin1")) + payload
def read(f):
    b = open(f, "rb").read(); L = len(b)
    def get(*p):
        r = find(b, 0, L, list(p)); assert r, (f, p); o, h, n = r; return b[o+h:o+n]
    stbl = ["moov", "trak", "mdia", "minf", "stbl"]
    stsz = get(*stbl, "stsz"); uniform, count = struct.unpack(">II", stsz[4:12])
    sizes = [uniform] * count if uniform else list(struct.unpack(">%dI" % count, stsz[12:12+4*count]))
    stco = get(*stbl, "stco"); nco = struct.unpack(">I", stco[4:8])[0]; assert nco == 1, (f, "chunks", nco)
    off = struct.unpack(">I", stco[8:12])[0]
    stts = get(*stbl, "stts"); ne = struct.unpack(">I", stts[4:8])[0]; assert ne == 1, (f, "stts", ne)
    n, delta = struct.unpack(">II", stts[8:16]); assert n == count
    ss = find(b, 0, L, stbl + ["stss"]); sync = None
    if ss:
        d = b[ss[0]+ss[1]:ss[0]+ss[2]]; k = struct.unpack(">I", d[4:8])[0]; sync = list(struct.unpack(">%dI" % k, d[8:8+4*k]))
    return dict(b=b, get=get, sizes=sizes, data=b[off:off+sum(sizes)], delta=delta, sync=sync, stsd=get(*stbl, "stsd"),
                ts=struct.unpack(">I", get("moov", "trak", "mdia", "mdhd")[12:16])[0])
out, files = sys.argv[1], sys.argv[2:]
parts = [read(f) for f in files]; p0 = parts[0]
for f, p in zip(files, parts):
    assert p["delta"] == p0["delta"] and p["ts"] == p0["ts"], f
    if p["stsd"] != p0["stsd"]: print("UYARI: kodlayici basligi farkli:", f)
sizes = []; sync = []; data = []
for p in parts:
    base = len(sizes)
    sync += [base + s for s in (p["sync"] if p["sync"] is not None else range(1, len(p["sizes"]) + 1))]
    sizes += p["sizes"]; data.append(p["data"])
N = len(sizes); dur_media = N * p0["delta"]
g = p0["get"]; b = p0["b"]
ftyp = find(b, 0, len(b), ["ftyp"]); ftyp = b[ftyp[0]:ftyp[0]+ftyp[2]]
mvhd = bytearray(g("moov", "mvhd")); mts = struct.unpack(">I", mvhd[12:16])[0]; dur_movie = dur_media * mts // p0["ts"]
mvhd[16:20] = struct.pack(">I", dur_movie)
tkhd = bytearray(g("moov", "trak", "tkhd")); tkhd[20:24] = struct.pack(">I", dur_movie)
mdhd = bytearray(g("moov", "trak", "mdia", "mdhd")); mdhd[16:20] = struct.pack(">I", dur_media)
mdat_off = len(ftyp) + 8
stbl = (box("stsd", p0["stsd"]) + box("stts", struct.pack(">III", 0, 1, N) + struct.pack(">I", p0["delta"]))
        + box("stsc", struct.pack(">IIIII", 0, 1, 1, N, 1)) + box("stsz", struct.pack(">III", 0, 0, N) + struct.pack(">%dI" % N, *sizes))
        + box("stco", struct.pack(">III", 0, 1, mdat_off)) + box("stss", struct.pack(">II", 0, len(sync)) + struct.pack(">%dI" % len(sync), *sync)))
minf = box("vmhd", g("moov", "trak", "mdia", "minf", "vmhd")) + box("dinf", g("moov", "trak", "mdia", "minf", "dinf")) + box("stbl", stbl)
mdia = box("mdhd", bytes(mdhd)) + box("hdlr", g("moov", "trak", "mdia", "hdlr")) + box("minf", minf)
moov = box("moov", box("mvhd", bytes(mvhd)) + box("trak", box("tkhd", bytes(tkhd)) + box("mdia", mdia)))
payload = b"".join(data)
open(out, "wb").write(ftyp + struct.pack(">I4s", len(payload) + 8, b"mdat") + payload + moov)
print("yazildi", out, "kare", N, "sure", dur_media / p0["ts"], "sn", "anahtar kare", len(sync))
