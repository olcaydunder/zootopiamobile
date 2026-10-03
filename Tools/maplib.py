"""Shared helpers: OSM + elevation → local metres around the clinic (x east, z north)."""
import json, math
import numpy as np
from PIL import Image

ROOT = "MapData"
meta = json.load(open(f"{ROOT}/meta.json"))
LAT0, LON0 = meta["center"]
Z = meta["zoom"]
MX = 111320.0 * math.cos(math.radians(LAT0))
MZ = 110574.0

def to_local(lat, lon):
    return ((lon - LON0) * MX, (lat - LAT0) * MZ)

# ---- elevation mosaic ----
tiles = meta["tiles"]
txs = sorted({t[0] for t in tiles}); tys = sorted({t[1] for t in tiles})
mos = np.zeros((256 * len(tys), 256 * len(txs)), np.float32)
for tx, ty in tiles:
    a = np.asarray(Image.open(f"{ROOT}/elev/{Z}_{tx}_{ty}.png").convert("RGB")).astype(np.float32)
    h = a[..., 0] * 256 + a[..., 1] + a[..., 2] / 256 - 32768
    i, j = tys.index(ty), txs.index(tx)
    mos[i * 256:(i + 1) * 256, j * 256:(j + 1) * 256] = h

def elev(lat, lon):
    n = 2 ** Z
    fx = ((lon + 180) / 360 * n - txs[0]) * 256 - 0.5
    fy = ((1 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2 * n - tys[0]) * 256 - 0.5
    x0, y0 = int(math.floor(fx)), int(math.floor(fy)); tx, ty = fx - x0, fy - y0
    x0 = max(0, min(mos.shape[1] - 2, x0)); y0 = max(0, min(mos.shape[0] - 2, y0))
    a, b, c, d = mos[y0, x0], mos[y0, x0 + 1], mos[y0 + 1, x0], mos[y0 + 1, x0 + 1]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty

def elev_local(x, z):
    return elev(LAT0 + z / MZ, LON0 + x / MX)

osm = json.load(open(f"{ROOT}/osm.json"))["elements"]

def way_pts(e):
    return [to_local(p["lat"], p["lon"]) for p in e.get("geometry", []) if p]

def rel_outers(e):
    out = []
    for m in e.get("members", []):
        if m.get("role") == "outer" and m.get("geometry"):
            out.append([to_local(p["lat"], p["lon"]) for p in m["geometry"] if p])
    return out
