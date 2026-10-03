"""Downloads real-world data for the game map (runs on GitHub Actions, which has internet).
Elevation: AWS Terrain Tiles (terrarium PNG). Map features: OpenStreetMap via Overpass."""
import json, math, os, sys, time, urllib.parse, urllib.request

UA = {"User-Agent": "ZootopiaMobile-mapfetch/1.0 (github.com/olcaydunder/zootopiamobile)"}
OUT = "MapData"
HALF_KM = float(os.environ.get("HALF_KM", "1.3"))
os.makedirs(OUT, exist_ok=True)

def get(url, data=None, tries=4):
    for i in range(tries):
        try:
            req = urllib.request.Request(url, data=data, headers=UA)
            with urllib.request.urlopen(req, timeout=180) as r:
                return r.read()
        except Exception as e:
            print("retry", url[:90], e); time.sleep(5 * (i + 1))
    raise SystemExit("failed: " + url)

# 1) Geocode the clinic (fallbacks: street, neighbourhood)
center = None
for q in ["Turgut Özal Caddesi 179, Ekşioğlu, Çekmeköy, İstanbul",
          "Turgut Özal Caddesi, Ekşioğlu, Çekmeköy, İstanbul",
          "Ekşioğlu Mahallesi, Çekmeköy, İstanbul"]:
    res = json.loads(get("https://nominatim.openstreetmap.org/search?format=json&limit=1&q=" + urllib.parse.quote(q)))
    print(q, "->", res[:1])
    if res:
        center = (float(res[0]["lat"]), float(res[0]["lon"])); query = q; break
    time.sleep(1.5)
if center is None:
    raise SystemExit("geocode failed")
lat, lon = center
dlat = HALF_KM / 111.32
dlon = HALF_KM / (111.32 * math.cos(math.radians(lat)))
bbox = (lat - dlat, lon - dlon, lat + dlat, lon + dlon)

# 2) Elevation tiles
Z = 15
def tile(la, lo):
    n = 2 ** Z
    x = (lo + 180) / 360 * n
    y = (1 - math.asinh(math.tan(math.radians(la))) / math.pi) / 2 * n
    return x, y
x0, y1 = tile(bbox[0], bbox[1]); x1, y0 = tile(bbox[2], bbox[3])
tiles = []
os.makedirs(OUT + "/elev", exist_ok=True)
for tx in range(int(x0), int(x1) + 1):
    for ty in range(int(y0), int(y1) + 1):
        png = get(f"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{Z}/{tx}/{ty}.png")
        open(f"{OUT}/elev/{Z}_{tx}_{ty}.png", "wb").write(png)
        tiles.append([tx, ty])
print("elevation tiles", len(tiles))

# 3) OpenStreetMap features
b = f"{bbox[0]},{bbox[1]},{bbox[2]},{bbox[3]}"
q = f"""[out:json][timeout:170];
(
  way["building"]({b});
  relation["building"]({b});
  way["highway"]({b});
  way["landuse"]({b});
  relation["landuse"]({b});
  way["natural"]({b});
  relation["natural"]({b});
  way["leisure"]({b});
  way["amenity"]({b});
  node["amenity"]({b});
  node["natural"="tree"]({b});
  way["waterway"]({b});
  way["barrier"]({b});
  way["railway"]({b});
  node["name"]({b});
);
out body geom;"""
osm = None
for ep in ["https://overpass-api.de/api/interpreter", "https://overpass.kumi.systems/api/interpreter"]:
    try:
        osm = get(ep, data=urllib.parse.urlencode({"data": q}).encode(), tries=2); break
    except SystemExit as e:
        print(e)
if osm is None:
    raise SystemExit("overpass failed")
open(f"{OUT}/osm.json", "wb").write(osm)
print("osm bytes", len(osm), "elements", len(json.loads(osm)["elements"]))

json.dump({"query": query, "center": [lat, lon], "bbox": bbox, "half_km": HALF_KM, "zoom": Z, "tiles": tiles},
          open(f"{OUT}/meta.json", "w"), indent=1)
