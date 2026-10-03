"""Turns MapData/ (elevation + OpenStreetMap) into compact files the game loads at runtime:

  Assets/Resources/Map/height.bytes   uint16 grid, (N x N), centimetres + 1000 offset, row = z (south→north)
  Assets/Resources/Map/ground.bytes   uint8 grid  (G x G) ground type per cell (see GROUND_* below)
  Assets/Resources/Map/features.bytes buildings, roads, trees, clinic (little-endian, see write_features)

Frame: metres, x = east, z = north, origin = the clinic (Turgut Özal Cd. 179, Ekşioğlu, Çekmeköy).
Run:  python3 Tools/build_map.py   (needs numpy, pillow, scipy, shapely, mapbox-earcut)
"""
import hashlib, math, os, struct, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter
from shapely.geometry import LineString, Polygon, Point, box
from shapely.ops import unary_union
from shapely.geometry.polygon import orient
import mapbox_earcut as earcut

sys.path.insert(0, os.path.dirname(__file__))
from maplib import osm, elev_local, way_pts, rel_outers

PLAY = 350.0          # half size of the playable square (land)
COAST = 372.0         # coastline distance (rounded square)
MAP = 880.0           # terrain/height grid covers [-MAP/2, MAP/2]
N = 401               # height grid nodes per side (2.2 m)
G = 1024              # ground raster per side (0.86 m)
OUT = "Assets/Resources/Map"
os.makedirs(OUT, exist_ok=True)

GROUND_GRASS, GROUND_PARK, GROUND_DIRT, GROUND_URBAN, GROUND_INDUSTRIAL, GROUND_FOREST, \
    GROUND_CEMETERY, GROUND_PITCH, GROUND_POOL, GROUND_SIDEWALK, GROUND_PARKING, GROUND_ROAD, GROUND_BUILDING = range(13)

def h01(s):
    return int(hashlib.md5(str(s).encode()).hexdigest()[:8], 16) / 0xFFFFFFFF

land_box = box(-PLAY - 12, -PLAY - 12, PLAY + 12, PLAY + 12)

# ---------------- heights ----------------
def coast_norm(x, z):
    p = 6.0
    return (abs(x) ** p + abs(z) ** p) ** (1.0 / p)

xs = np.linspace(-MAP / 2, MAP / 2, N)
raw = np.array([[elev_local(x, z) for x in xs] for z in xs], np.float64)
raw = gaussian_filter(raw, 1.2)
inner = np.abs(xs)[None, :] <= PLAY
mask = inner & (np.abs(xs)[:, None] <= PLAY)
base = raw[mask].min() - 2.6
H = raw - base
for j, z in enumerate(xs):
    for i, x in enumerate(xs):
        r = coast_norm(x, z)
        ang = math.atan2(z, x)
        r += (math.sin(ang * 7 + 1.3) * 5 + math.sin(ang * 13 + 0.4) * 3)
        if r > PLAY + 4:
            t = min(1.0, (r - (PLAY + 4)) / (COAST - PLAY + 18))
            t = t * t * (3 - 2 * t)
            H[j, i] = H[j, i] * (1 - t) + (-6.5) * t
            if r > COAST + 6:
                H[j, i] = min(H[j, i], -6.5 + 0 * t)
print("height range in play area: %.1f .. %.1f" % (H[mask].min(), H[mask].max()))
q = np.clip(np.round((H + 10.0) * 100.0), 0, 65535).astype("<u2")
open(f"{OUT}/height.bytes", "wb").write(q.tobytes())

def height_at(x, z):
    fx = (x + MAP / 2) / MAP * (N - 1); fz = (z + MAP / 2) / MAP * (N - 1)
    i = min(N - 2, max(0, int(fx))); j = min(N - 2, max(0, int(fz)))
    tx, tz = fx - i, fz - j
    return (H[j, i] * (1 - tx) + H[j, i + 1] * tx) * (1 - tz) + (H[j + 1, i] * (1 - tx) + H[j + 1, i + 1] * tx) * tz

# ---------------- OSM → shapes ----------------
def poly_from(e):
    if e["type"] == "way":
        pts = way_pts(e)
        if len(pts) >= 4 and pts[0] == pts[-1] or len(pts) >= 3:
            try:
                p = Polygon(pts)
                return p.buffer(0) if not p.is_valid else p
            except Exception:
                return None
    elif e["type"] == "relation":
        polys = []
        for ring in rel_outers(e):
            if len(ring) >= 3:
                try:
                    polys.append(Polygon(ring).buffer(0))
                except Exception:
                    pass
        return unary_union(polys) if polys else None
    return None

AREA_TYPES = [
    (("landuse", "residential"), GROUND_URBAN),
    (("landuse", "commercial"), GROUND_URBAN),
    (("landuse", "retail"), GROUND_URBAN),
    (("landuse", "industrial"), GROUND_INDUSTRIAL),
    (("landuse", "construction"), GROUND_DIRT),
    (("landuse", "brownfield"), GROUND_DIRT),
    (("landuse", "grass"), GROUND_PARK),
    (("landuse", "meadow"), GROUND_GRASS),
    (("natural", "grassland"), GROUND_GRASS),
    (("leisure", "park"), GROUND_PARK),
    (("leisure", "playground"), GROUND_PARK),
    (("leisure", "garden"), GROUND_PARK),
    (("leisure", "pitch"), GROUND_PITCH),
    (("landuse", "forest"), GROUND_FOREST),
    (("natural", "wood"), GROUND_FOREST),
    (("natural", "scrub"), GROUND_FOREST),
    (("landuse", "cemetery"), GROUND_CEMETERY),
    (("amenity", "parking"), GROUND_PARKING),
    (("amenity", "school"), GROUND_URBAN),
    (("leisure", "swimming_pool"), GROUND_POOL),
]
areas = []   # (priority order) list of (ground, polygon)
for (k, v), g in AREA_TYPES:
    for e in osm:
        t = e.get("tags", {})
        if t.get(k) == v and e["type"] in ("way", "relation"):
            p = poly_from(e)
            if p is not None and not p.is_empty and p.intersects(land_box):
                areas.append((g, p.intersection(land_box)))

ROAD_W = {"motorway": 14, "trunk": 12, "primary": 11, "secondary": 9, "tertiary": 8, "unclassified": 6,
          "residential": 6, "living_street": 5, "service": 4, "primary_link": 7, "secondary_link": 7,
          "trunk_link": 7, "motorway_link": 7, "tertiary_link": 6, "pedestrian": 4, "track": 3,
          "footway": 2, "path": 1.6, "steps": 2, "cycleway": 2}
ROAD_KIND = {"motorway": 0, "trunk": 0, "primary": 1, "secondary": 1, "trunk_link": 1, "motorway_link": 1,
             "primary_link": 1, "secondary_link": 1, "tertiary": 2, "tertiary_link": 2, "unclassified": 2,
             "residential": 2, "living_street": 2, "service": 3, "pedestrian": 4, "track": 4, "footway": 4,
             "path": 4, "steps": 4, "cycleway": 4}
roads = []   # (kind, width, LineString)
for e in osm:
    t = e.get("tags", {})
    hw = t.get("highway")
    if e["type"] != "way" or hw not in ROAD_W or t.get("tunnel") in ("yes", "building_passage") or t.get("area") == "yes":
        continue
    w = ROAD_W[hw]
    if hw in ("primary", "secondary", "trunk") and t.get("oneway") == "yes":
        w = 8.0
    if "lanes" in t:
        try:
            w = max(w * 0.6, float(t["lanes"].split(";")[0]) * 3.3)
        except ValueError:
            pass
    ls = LineString(way_pts(e))
    clipped = ls.intersection(land_box)
    for part in getattr(clipped, "geoms", [clipped]):
        if part.length > 2 and part.geom_type == "LineString":
            roads.append((ROAD_KIND[hw], float(w), part.simplify(0.4)))
print("roads", len(roads))

# Buildings
KIND = {"apartments": 0, "residential": 0, "yes": 0, "house": 5, "detached": 5, "industrial": 1, "warehouse": 1,
        "school": 2, "public": 2, "hospital": 2, "university": 2, "mosque": 3, "commercial": 4, "retail": 4,
        "supermarket": 4, "office": 4, "roof": 6, "construction": 0, "garage": 6, "garages": 6}
buildings = []
for e in osm:
    t = e.get("tags", {})
    if "building" not in t or e["type"] not in ("way", "relation"):
        continue
    p = poly_from(e)
    if p is None or p.is_empty:
        continue
    if p.geom_type == "MultiPolygon":
        p = max(p.geoms, key=lambda g: g.area)
    if not land_box.buffer(-14).contains(p) or p.area < 12:
        continue
    p = orient(p.simplify(0.35, preserve_topology=True), 1.0)
    if p.area < 12 or len(p.exterior.coords) < 4:
        continue
    kind = KIND.get(t["building"], 0)
    if t.get("amenity") == "place_of_worship" or t.get("religion") == "muslim":
        kind = 3
    buildings.append({"id": e["id"], "poly": p, "kind": kind, "tags": t})
print("buildings", len(buildings))

# Clinic = closest building to the origin on the same side as the address (the geocoded point is on the street).
origin = Point(0, 0)
cands = [b for b in buildings if b["kind"] in (0, 4, 5) and b["poly"].area < 1500]
clinic = min(cands, key=lambda b: b["poly"].distance(origin))
print("clinic building", clinic["id"], "area %.0f" % clinic["poly"].area, "dist %.1f" % clinic["poly"].distance(origin))

# Levels and enterable selection
for b in buildings:
    p = b["poly"]; t = b["tags"]; r = h01(b["id"])
    rect = p.minimum_rotated_rectangle
    rc = list(rect.exterior.coords)[:4]
    e0 = math.dist(rc[0], rc[1]); e1 = math.dist(rc[1], rc[2])
    if e0 >= e1:
        w, d = e0, e1; ang = math.atan2(rc[1][1] - rc[0][1], rc[1][0] - rc[0][0])
    else:
        w, d = e1, e0; ang = math.atan2(rc[2][1] - rc[1][1], rc[2][0] - rc[1][0])
    b["obb"] = (rect.centroid.x, rect.centroid.y, w, d, math.degrees(ang))
    b["rectness"] = p.area / max(1e-3, rect.area)
    lv = None
    if "building:levels" in t:
        try:
            lv = int(float(t["building:levels"].split(";")[0]))
        except ValueError:
            lv = None
    k = b["kind"]
    if lv is None:
        a = p.area
        if k == 1: lv = 2 if a > 800 else 1
        elif k == 2: lv = 3 + int(r * 2)
        elif k == 3: lv = 2
        elif k == 4: lv = 1 + int(r * 2)
        elif k == 5: lv = 1 + int(r * 2)
        elif k == 6: lv = 1
        elif a < 90: lv = 1 + int(r * 2)
        elif a < 220: lv = 2 + int(r * 3)
        else: lv = 4 + int(r * 3)
    b["levels"] = max(1, min(lv, 12))
    b["flags"] = 0

clinic["levels"] = max(clinic["levels"], 2)
enterable = [b for b in buildings
             if b["kind"] in (0, 2, 4, 5) and b["rectness"] > 0.86 and 7.5 <= b["obb"][3] and b["obb"][2] <= 26]
def door_ok(b):
    # At least one long-side door must open onto free ground (not against another building) with a sane step.
    cx, cz, w, d, ang = b["obb"]
    la = math.radians(ang)
    Lx, Lz = math.cos(la), math.sin(la)
    Sx, Sz = -Lz, Lx
    corners = [(cx + Lx * w / 2 * i + Sx * d / 2 * j, cz + Lz * w / 2 * i + Sz * d / 2 * j) for i in (-1, 1) for j in (-1, 1)]
    y0 = max(height_at(x, z) for x, z in corners) + 0.15
    for sgn in (-1, 1):
        px, pz = cx + Sx * sgn * (d / 2 + 1.4), cz + Sz * sgn * (d / 2 + 1.4)
        pt = Point(px, pz)
        clear = all(o is b or o["poly"].distance(pt) > 1.0 for o in buildings)
        if clear and y0 - height_at(px, pz) < 2.2:
            return True
    return False
enterable = [b for b in enterable if b is clinic or door_ok(b)]
enterable.sort(key=lambda b: h01(b["id"] * 7))
chosen = []
for b in enterable:
    c = b["poly"].centroid
    if all(c.distance(o["poly"].centroid) > 26 for o in chosen):
        chosen.append(b)
    if len(chosen) >= 70:
        break
if clinic not in chosen:
    chosen.append(clinic)
for b in chosen:
    b["flags"] |= 1
clinic["flags"] |= 2
for b in buildings:
    # hip tile roof for some rectangular apartments that are not enterable (enterable ones get flat roofs)
    if b["kind"] in (0, 5) and b["rectness"] > 0.9 and not (b["flags"] & 1) and h01(b["id"] * 3) < 0.55:
        b["flags"] |= 4
print("enterable", len(chosen))

# Clinic front: the nearest road point
best = None
for kind, w, ls in roads:
    if kind > 1:   # the clinic's address is on Turgut Özal Caddesi (primary road)
        continue
    d = ls.distance(clinic["poly"].centroid)
    if best is None or d < best[0]:
        best = (d, ls, w)
rp = best[1].interpolate(best[1].project(clinic["poly"].centroid))
# Stand in front of the middle of the clinic's street-side wall (where the game puts the sign).
ocx, ocz, ow, od, oang = clinic["obb"]
la = math.radians(oang)
Lx, Lz = math.cos(la), math.sin(la)
Sx, Sz = -Lz, Lx
dx, dz = rp.x - ocx, rp.y - ocz
dl, ds = dx * Lx + dz * Lz, dx * Sx + dz * Sz
if abs(dl) * (od / 2) > abs(ds) * (ow / 2):
    sgn = 1 if dl > 0 else -1
    dirx, dirz, dist = Lx * sgn, Lz * sgn, ow / 2
else:
    sgn = 1 if ds > 0 else -1
    dirx, dirz, dist = Sx * sgn, Sz * sgn, od / 2
lobby = (ocx + dirx * (dist + 3.4), ocz + dirz * (dist + 3.4))
lobby_yaw = math.degrees(math.atan2(dirx, dirz))   # Unity yaw: 0 = +z (north), 90 = +x (east)
print("lobby at (%.1f, %.1f) yaw %.0f, road %.1f m away" % (lobby[0], lobby[1], lobby_yaw, best[0]))

# ---------------- ground raster ----------------
img = Image.new("L", (G, G), GROUND_GRASS)
dr = ImageDraw.Draw(img)
def gp(x, z):
    return ((x + MAP / 2) / MAP * G, (z + MAP / 2) / MAP * G)   # row = z (flipped when saved)
def fill_poly(poly, val):
    for g in getattr(poly, "geoms", [poly]):
        if g.geom_type != "Polygon" or g.is_empty:
            continue
        dr.polygon([gp(x, z) for x, z in g.exterior.coords], fill=val)
        for hole in g.interiors:
            dr.polygon([gp(x, z) for x, z in hole.coords], fill=GROUND_GRASS)
ORDER = [GROUND_URBAN, GROUND_INDUSTRIAL, GROUND_DIRT, GROUND_GRASS, GROUND_CEMETERY, GROUND_FOREST, GROUND_PARK,
         GROUND_PITCH, GROUND_PARKING, GROUND_POOL]
for g in ORDER:
    for gg, p in areas:
        if gg == g:
            fill_poly(p, g)
px_per_m = G / MAP
for kind, w, ls in sorted(roads, key=lambda r: -r[0]):
    pts = [gp(x, z) for x, z in ls.coords]
    if kind <= 2:
        dr.line(pts, fill=GROUND_SIDEWALK, width=max(1, int(round((w + 4.0) * px_per_m))), joint="curve")
for kind, w, ls in sorted(roads, key=lambda r: -r[0]):
    pts = [gp(x, z) for x, z in ls.coords]
    dr.line(pts, fill=GROUND_ROAD if kind <= 3 else GROUND_SIDEWALK, width=max(1, int(round(w * px_per_m))), joint="curve")
for b in buildings:
    fill_poly(b["poly"].buffer(0.6), GROUND_BUILDING)
ground = np.asarray(img, np.uint8)          # row 0 = z min (we drew with z increasing downward → same as grid)
open(f"{OUT}/ground.bytes", "wb").write(ground.tobytes())

def ground_at(x, z):
    i = int((x + MAP / 2) / MAP * G); j = int((z + MAP / 2) / MAP * G)
    if 0 <= i < G and 0 <= j < G:
        return ground[j, i]
    return GROUND_GRASS

# ---------------- trees ----------------
rng = np.random.default_rng(1923)
trees = []
def scatter(poly, spacing, kind_fn, cap):
    minx, miny, maxx, maxy = poly.bounds
    n = int(poly.area / (spacing * spacing))
    out = 0
    for _ in range(n * 2):
        if out >= min(n, cap):
            break
        x = rng.uniform(minx, maxx); z = rng.uniform(miny, maxy)
        if abs(x) > PLAY - 3 or abs(z) > PLAY - 3 or not poly.contains(Point(x, z)):
            continue
        if ground_at(x, z) in (GROUND_ROAD, GROUND_SIDEWALK, GROUND_BUILDING, GROUND_POOL, GROUND_PITCH, GROUND_PARKING):
            continue
        trees.append((x, z, kind_fn()))
        out += 1
for g, p in areas:
    if g == GROUND_FOREST:
        scatter(p, 9.5, lambda: 0 if rng.random() < 0.6 else 1, 500)
    elif g == GROUND_PARK:
        scatter(p, 13, lambda: 1 if rng.random() < 0.7 else 0, 80)
    elif g == GROUND_CEMETERY:
        scatter(p, 11, lambda: 2, 60)
    elif g == GROUND_URBAN:
        scatter(p, 30, lambda: 1, 400)
for e in osm:
    if e["type"] == "node" and e.get("tags", {}).get("natural") == "tree":
        from maplib import to_local
        x, z = to_local(e["lat"], e["lon"])
        if abs(x) < PLAY and abs(z) < PLAY:
            trees.append((x, z, 1))
# Street trees along residential roads
for kind, w, ls in roads:
    if kind != 2:
        continue
    d = rng.uniform(5, 20)
    while d < ls.length:
        p = ls.interpolate(d)
        p2 = ls.interpolate(min(ls.length, d + 1))
        tx, tz = p2.x - p.x, p2.y - p.y
        L = math.hypot(tx, tz) or 1
        side = 1 if rng.random() < 0.5 else -1
        x = p.x - tz / L * side * (w / 2 + 1.6); z = p.y + tx / L * side * (w / 2 + 1.6)
        if rng.random() < 0.35 and ground_at(x, z) in (GROUND_SIDEWALK, GROUND_URBAN, GROUND_GRASS, GROUND_PARK) and abs(x) < PLAY and abs(z) < PLAY:
            trees.append((x, z, 1))
        d += rng.uniform(16, 30)
print("trees", len(trees))

# ---------------- features.bytes ----------------
def tri(poly):
    ring = list(poly.exterior.coords)[:-1]
    verts = np.array(ring, dtype=np.float64).reshape(-1, 2)
    idx = earcut.triangulate_float64(verts, np.array([len(ring)], dtype=np.uint32))
    return ring, list(idx)

f = open(f"{OUT}/features.bytes", "wb")
W = lambda fmt, *a: f.write(struct.pack("<" + fmt, *a))
W("4si", b"ZMAP", 1)
W("fffii", MAP, PLAY, COAST, N, G)
W("ffff", lobby[0], lobby[1], lobby_yaw, height_at(*lobby))
W("i", len(buildings))
for b in buildings:
    ring, idx = tri(b["poly"])
    cx, cz, w, d, yaw = b["obb"]
    W("BBBB", b["kind"], b["levels"], b["flags"], 0)
    W("fffff", cx, cz, w, d, yaw)
    W("i", len(ring))
    for x, z in ring:
        W("ff", x, z)
    W("i", len(idx))
    for i in idx:
        W("H", i)
W("i", len(roads))
for kind, w, ls in roads:
    W("Bf", kind, w)
    c = list(ls.coords)
    W("i", len(c))
    for x, z in c:
        W("ff", x, z)
W("i", len(trees))
for x, z, k in trees:
    W("ffB", x, z, k)
f.close()

# quick look image for checking
prev = Image.new("RGB", (G, G))
pal = {GROUND_GRASS: (110, 150, 70), GROUND_PARK: (90, 160, 70), GROUND_DIRT: (150, 120, 80), GROUND_URBAN: (170, 160, 140),
       GROUND_INDUSTRIAL: (160, 160, 165), GROUND_FOREST: (50, 100, 45), GROUND_CEMETERY: (120, 140, 100),
       GROUND_PITCH: (80, 170, 80), GROUND_POOL: (80, 160, 230), GROUND_SIDEWALK: (200, 195, 185),
       GROUND_PARKING: (120, 120, 125), GROUND_ROAD: (60, 60, 65), GROUND_BUILDING: (150, 90, 70)}
arr = np.zeros((G, G, 3), np.uint8)
for k, c in pal.items():
    arr[ground == k] = c
Image.fromarray(arr[::-1]).save(sys.argv[1] if len(sys.argv) > 1 else "/tmp/ground.png")
for k in ("height", "ground", "features"):
    print(k, os.path.getsize(f"{OUT}/{k}.bytes"))
