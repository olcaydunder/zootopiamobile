"""Turns MapData/<map>/ (elevation + OpenStreetMap) into compact files the game loads at runtime:

  Assets/Resources/Map/<map>/height.bytes   uint16 grid, (N x N), centimetres + 1000 offset, row = z (south→north)
  Assets/Resources/Map/<map>/ground.bytes   uint8 grid  (G x G) ground type per cell (see GROUND_* below)
  Assets/Resources/Map/<map>/features.bytes buildings, roads, trees, landmark (little-endian, see write_features)
  Assets/Resources/Map/<map>/preview.png    small picture for the map selection screen

Maps (CONFIGS below):
  eksioglu  Ekşioğlu, Çekmeköy: the clinic's neighbourhood, an island in the sea (real scale).
  senir     Senir, Keçiborlu (Isparta): the whole town between Lake Burdur and the mountain behind it.
            The town is 3.4 km long, so the frame is turned along the town and squeezed (houses keep
            their real size), and the plain down to the lake is squeezed harder.
  firat     Fırat Üniversitesi, Elazığ: the rectorate campus (real scale), hills around it.

Frame: metres, x = east (game), z = north (game).
Run:  python3 Tools/build_map.py <map> [preview.png]   (needs numpy, pillow, scipy, shapely, mapbox-earcut)
"""
import hashlib, math, os, struct, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, distance_transform_edt
from shapely.geometry import LineString, Polygon, Point, box
from shapely.ops import unary_union
from shapely.geometry.polygon import orient
from shapely.strtree import STRtree
from shapely import affinity
from shapely.prepared import prep
import mapbox_earcut as earcut

ARGS = sys.argv[1:]
MAP_ID = "eksioglu"
if ARGS and not ARGS[0].endswith(".png"):
    MAP_ID = ARGS.pop(0)
PREVIEW = ARGS[0] if ARGS else "/tmp/ground_%s.png" % MAP_ID
os.environ["MAP_ID"] = MAP_ID
sys.path.insert(0, os.path.dirname(__file__))
from maplib import osm, elev_local, way_pts, rel_outers, to_local

CONFIGS = {
    "eksioglu": dict(play=350.0, coast=372.0, map=880.0, edge="sea", enterable=70),
    "senir": dict(play=560.0, coast=600.0, map=1340.0, edge="lake", enterable=120,
                  # town axis frame (real metres from the geocoded centre): centre, direction of the town
                  town_center=(-12.0, 369.0), town_axis_deg=48.5, su=0.31, uc=160.0,
                  sv=0.45, vz=-40.0, vb=-450.0, lake_level=843.6, height_scale=0.45, shore=-470.0,
                  landmark=dict(near=(150.0, 250.0), roads=(2,), size=(14.0, 11.0), levels=2, kind=4),
                  fill="village"),
    "firat": dict(play=520.0, coast=560.0, map=1260.0, edge="hills", enterable=110,
                  origin=(1000.0, -560.0),
                  landmark=dict(near=(1286.0, -614.0), toward=(1290.0, -500.0), roads=(2, 3), size=(26.0, 17.0), levels=5, kind=2),
                  fill="campus"),
}
CFG = CONFIGS[MAP_ID]
PLAY = CFG["play"]    # half size of the playable square (land)
COAST = CFG["coast"]  # coastline distance (rounded square)
MAP = CFG["map"]      # terrain/height grid covers [-MAP/2, MAP/2]
N = 401               # height grid nodes per side
G = 1024              # ground raster per side
OUT = "Assets/Resources/Map/" + MAP_ID
os.makedirs(OUT, exist_ok=True)
EKSIOGLU = MAP_ID == "eksioglu"

GROUND_GRASS, GROUND_PARK, GROUND_DIRT, GROUND_URBAN, GROUND_INDUSTRIAL, GROUND_FOREST, \
    GROUND_CEMETERY, GROUND_PITCH, GROUND_POOL, GROUND_SIDEWALK, GROUND_PARKING, GROUND_ROAD, GROUND_BUILDING = range(13)

def h01(s):
    return int(hashlib.md5(str(s).encode()).hexdigest()[:8], 16) / 0xFFFFFFFF

def smooth01(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)

land_box = box(-PLAY - 12, -PLAY - 12, PLAY + 12, PLAY + 12)

# ---------------- real world → game frame ----------------
class Frame:
    """Real local metres (maplib.to_local) ↔ game metres."""
    def __init__(self, cfg):
        self.kind = "identity"
        self.ox, self.oz = cfg.get("origin", (0.0, 0.0))
        self.angle = 0.0          # how much a real shape turns in the game (radians, counter-clockwise)
        if "town_axis_deg" in cfg:
            self.kind = "town"
            a = math.radians(cfg["town_axis_deg"])
            self.ax = (math.cos(a), math.sin(a))          # along the town (→ game +x)
            self.nr = (-math.sin(a), math.cos(a))         # across, towards the mountain (→ game +z)
            self.angle = -a
            self.cx, self.cz = cfg["town_center"]
            self.su, self.uc = cfg["su"], cfg["uc"]
            self.sv, self.vz, self.vb = cfg["sv"], cfg["vz"], cfg["vb"]
            self.gb = self.sv * self.vb + self.vz        # game z where the squeezed plain starts
            self.shore_g = cfg["shore"]
            self.vs_fit = None
        elif self.ox or self.oz:
            self.kind = "shift"

    def uv(self, x, z):
        dx, dz = x - self.cx, z - self.cz
        return dx * self.ax[0] + dz * self.ax[1], dx * self.nr[0] + dz * self.nr[1]

    def xz(self, u, v):
        return self.cx + self.ax[0] * u + self.nr[0] * v, self.cz + self.ax[1] * u + self.nr[1] * v

    def vs(self, u):
        """Real distance (negative v) from the town axis to Lake Burdur's shore at this point along the town."""
        a, b = self.vs_fit
        return min(-1200.0, max(-2700.0, a + b * u))

    def shore(self, gx):
        """Game z of the lake shore (a gentle natural wiggle)."""
        return self.shore_g + 14.0 * math.sin(gx * 0.011 + 0.7) + 7.0 * math.sin(gx * 0.031 + 2.1)

    def fwd(self, x, z):
        if self.kind == "identity":
            return x, z
        if self.kind == "shift":
            return x - self.ox, z - self.oz
        u, v = self.uv(x, z)
        gx = self.su * (u - self.uc)
        if v >= self.vb:
            return gx, self.sv * v + self.vz
        s, vs = self.shore(gx), self.vs(u)
        if v >= vs:
            return gx, self.gb + (v - self.vb) / (vs - self.vb) * (s - self.gb)
        return gx, s + self.sv * (v - vs)

    def inv(self, gx, gz):
        if self.kind == "identity":
            return gx, gz
        if self.kind == "shift":
            return gx + self.ox, gz + self.oz
        u = gx / self.su + self.uc
        if gz >= self.gb:
            v = (gz - self.vz) / self.sv
        else:
            s, vs = self.shore(gx), self.vs(u)
            if gz >= s:
                v = self.vb + (gz - self.gb) / (s - self.gb) * (vs - self.vb)
            else:
                v = vs + (gz - s) / self.sv
        return self.xz(u, v)

    def pts(self, pts):
        return [self.fwd(x, z) for x, z in pts]

    def shape(self, poly):
        """A building: its centre moves with the frame, the shape keeps its real size and only turns."""
        if self.kind != "town":
            return Polygon(self.pts(list(poly.exterior.coords)))
        c = poly.centroid
        gx, gz = self.fwd(c.x, c.y)
        p = affinity.rotate(poly, self.angle, origin=(c.x, c.y), use_radians=True)
        return affinity.translate(p, gx - c.x, gz - c.y)

F = Frame(CFG)

if F.kind == "town":
    # Where the real lake shore is, along the town (the lake is the flat 843 m surface in the elevation data).
    found = []
    for u in range(-1600, 1001, 100):
        for d in range(0, 3200, 20):
            x, z = F.xz(u, -d)
            if elev_local(x, z) < CFG["lake_level"] - 0.1:
                found.append((u, -d))
                break
    us = np.array([f[0] for f in found], float); vs_ = np.array([f[1] for f in found], float)
    b, a = np.polyfit(us, vs_, 1)
    F.vs_fit = (a, b)
    print("lake shore fit: v = %.0f %+.3f u  (%d samples)" % (a, b, len(found)))

def coast_norm(x, z):
    p = 6.0
    return (abs(x) ** p + abs(z) ** p) ** (1.0 / p)

# ---------------- heights ----------------
xs = np.linspace(-MAP / 2, MAP / 2, N)
if EKSIOGLU:
    raw = np.array([[elev_local(x, z) for x in xs] for z in xs], np.float64)
else:
    raw = np.array([[elev_local(*F.inv(x, z)) for x in xs] for z in xs], np.float64)
raw = gaussian_filter(raw, 1.2)
inner = np.abs(xs)[None, :] <= PLAY
mask = inner & (np.abs(xs)[:, None] <= PLAY)

def rim_noise(x, z):
    ang = math.atan2(z, x)
    return 0.5 + 0.25 * math.sin(ang * 5 + 0.8) + 0.15 * math.sin(ang * 11 + 2.2) + 0.1 * math.sin(ang * 23 + 1.1)

lake = np.zeros((N, N), bool)
if CFG["edge"] == "sea":
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
else:
    if CFG["edge"] == "lake":
        H = (raw - CFG["lake_level"]) * CFG["height_scale"]
        for j, z in enumerate(xs):
            for i, x in enumerate(xs):
                lake[j, i] = z < F.shore(x)
    else:
        base = raw[mask].min() - 2.6
        H = raw - base
    # Beach: land comes down gently to the water.
    if lake.any():
        dist_in = distance_transform_edt(lake) * (MAP / (N - 1))       # metres into the lake
        dist_out = distance_transform_edt(~lake) * (MAP / (N - 1))     # metres from the water
        for j in range(N):
            for i in range(N):
                if lake[j, i]:
                    H[j, i] = -min(5.0, 0.35 + dist_in[j, i] * 0.07)
                else:
                    t = smooth01(dist_out[j, i] / 45.0)
                    H[j, i] = 0.35 + (max(H[j, i], 0.35) - 0.35) * t + 0.9 * t * (1 - t)
    # Hills (or the mountain) rise round the play area where there is no water.
    for j, z in enumerate(xs):
        for i, x in enumerate(xs):
            if lake[j, i]:
                continue
            r = coast_norm(x, z)
            if r > PLAY + 4:
                t = smooth01((r - (PLAY + 4)) / (MAP / 2 - PLAY - 10))
                H[j, i] += t ** 1.2 * (38.0 + 34.0 * rim_noise(x, z))
    H = gaussian_filter(H, 0.6)
print("height range in play area: %.1f .. %.1f" % (H[mask].min(), H[mask].max()))
q = np.clip(np.round((H + 10.0) * 100.0), 0, 65535).astype("<u2")
open(f"{OUT}/height.bytes", "wb").write(q.tobytes())

def height_at(x, z):
    fx = (x + MAP / 2) / MAP * (N - 1); fz = (z + MAP / 2) / MAP * (N - 1)
    i = min(N - 2, max(0, int(fx))); j = min(N - 2, max(0, int(fz)))
    tx, tz = fx - i, fz - j
    return (H[j, i] * (1 - tx) + H[j, i + 1] * tx) * (1 - tz) + (H[j + 1, i] * (1 - tx) + H[j + 1, i + 1] * tx) * tz

# ---------------- OSM → shapes ----------------
def poly_from(e, shape=False):
    """Polygon of a way/relation in game metres (shape=True: a building that keeps its real size)."""
    def make(ring):
        if shape and F.kind == "town":
            p = Polygon(ring)
            p = p if p.is_valid else p.buffer(0)
            if p.geom_type == "MultiPolygon":
                p = max(p.geoms, key=lambda g: g.area)
            return F.shape(p) if p.geom_type == "Polygon" and not p.is_empty else None
        return Polygon(F.pts(ring))
    if e["type"] == "way":
        pts = way_pts(e)
        if len(pts) >= 4 and pts[0] == pts[-1] or len(pts) >= 3:
            try:
                p = make(pts)
                if p is None:
                    return None
                return p.buffer(0) if not p.is_valid else p
            except Exception:
                return None
    elif e["type"] == "relation":
        polys = []
        for ring in rel_outers(e):
            if len(ring) >= 3:
                try:
                    p = make(ring)
                    if p is not None:
                        polys.append(p.buffer(0))
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
if not EKSIOGLU:
    # Campus lawns and the hospital grounds; farmland round the town.
    AREA_TYPES = [(("amenity", "university"), GROUND_PARK), (("amenity", "hospital"), GROUND_URBAN),
                  (("landuse", "farmland"), GROUND_DIRT), (("landuse", "orchard"), GROUND_GRASS)] + AREA_TYPES
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
    pts = way_pts(e)
    if len(pts) < 2:
        continue
    if F.kind == "town":
        # densify before bending the line through the squeezed frame
        ls0 = LineString(pts)
        n = max(2, int(ls0.length / 15) + 1)
        pts = [ls0.interpolate(k / (n - 1), normalized=True).coords[0] for k in range(n)]
    ls = LineString(F.pts(pts))
    clipped = ls.intersection(land_box)
    for part in getattr(clipped, "geoms", [clipped]):
        if part.length > 2 and part.geom_type == "LineString":
            pieces = [part]
            if CFG["edge"] == "lake":
                # no roads under water: keep the dry runs
                pieces, run = [], []
                for c in part.coords:
                    if height_at(*c) >= 0.3:
                        run.append(c)
                    else:
                        if len(run) >= 2:
                            pieces.append(LineString(run))
                        run = []
                if len(run) >= 2:
                    pieces.append(LineString(run))
            for piece in pieces:
                if piece.length > 2:
                    roads.append((ROAD_KIND[hw], float(w), piece.simplify(0.4)))
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
    p = poly_from(e, shape=True)
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
    if not EKSIOGLU and kind == 1 and t["building"] == "greenhouse":
        continue
    buildings.append({"id": e["id"], "poly": p, "kind": kind, "tags": t})
print("buildings (OpenStreetMap)", len(buildings))

# ---------------- generated buildings (places OpenStreetMap has no buildings for) ----------------
gen_rng = np.random.default_rng(4407)
road_tree = STRtree([ls.buffer(w / 2 + 1.2) for kind, w, ls in roads if kind <= 3]) if roads else None
road_geoms = [ls.buffer(w / 2 + 1.2) for kind, w, ls in roads if kind <= 3]
path_geoms = [ls.buffer(w / 2 + 0.3) for kind, w, ls in roads if kind == 4]
path_tree = STRtree(path_geoms) if path_geoms else None
placed = [b["poly"].buffer(1.6) for b in buildings]
cell_index = {}
def _cells(poly):
    minx, miny, maxx, maxy = poly.bounds
    for cx in range(int(minx // 40), int(maxx // 40) + 1):
        for cz in range(int(miny // 40), int(maxy // 40) + 1):
            yield (cx, cz)
for k, p in enumerate(placed):
    for c in _cells(p):
        cell_index.setdefault(c, []).append(k)

def free(poly, gap=1.6):
    """True when a footprint doesn't touch a road, a path, another building, water or a steep slope."""
    if not land_box.buffer(-14).contains(poly):
        return False
    if road_tree is not None and any(road_geoms[k].intersects(poly) for k in road_tree.query(poly)):
        return False
    if path_tree is not None and any(path_geoms[k].intersects(poly) for k in path_tree.query(poly)):
        return False
    grown = poly.buffer(gap - 1.6) if gap != 1.6 else poly
    for c in _cells(grown):
        for k in cell_index.get(c, ()):
            if placed[k].intersects(grown):
                return False
    hs = [height_at(x, z) for x, z in list(poly.exterior.coords)[:-1]]
    c = poly.centroid
    hs.append(height_at(c.x, c.y))
    return min(hs) > 0.9 and max(hs) - min(hs) < 3.2

def add_building(poly, kind, levels, bid, flags=0):
    poly = orient(poly, 1.0)
    b = {"id": bid, "poly": poly, "kind": kind, "tags": {"building": "yes"}, "gen_levels": levels}
    buildings.append(b)
    placed.append(poly.buffer(1.6))
    for c in _cells(placed[-1]):
        cell_index.setdefault(c, []).append(len(placed) - 1)
    return b

def rect(cx, cz, w, d, ang):
    """Rectangle w (along ang) x d centred at (cx, cz)."""
    L = (math.cos(ang), math.sin(ang)); S = (-L[1], L[0])
    return Polygon([(cx + L[0] * w / 2 * i + S[0] * d / 2 * j, cz + L[1] * w / 2 * i + S[1] * d / 2 * j)
                    for i, j in ((-1, -1), (1, -1), (1, 1), (-1, 1))])

def frontage(ls, rw, region, spec, gid):
    """Buildings along both sides of a street, facing it."""
    made = 0
    for side in (1, -1):
        d = gen_rng.uniform(2, 10)
        while d < ls.length - 4:
            w = gen_rng.uniform(*spec["width"])
            dep = gen_rng.uniform(*spec["depth"])
            if gen_rng.random() < spec["skip"]:
                d += w + gen_rng.uniform(*spec["gap"])
                continue
            a = ls.interpolate(d); b2 = ls.interpolate(min(ls.length, d + w))
            tx, tz = b2.x - a.x, b2.y - a.y
            L = math.hypot(tx, tz)
            if L < w * 0.8:   # tight bend
                d += 4
                continue
            tx, tz = tx / L, tz / L
            sx, sz = -tz * side, tx * side
            back = rw / 2 + gen_rng.uniform(*spec["setback"]) + dep / 2
            mx, mz = (a.x + b2.x) / 2 + sx * back, (a.y + b2.y) / 2 + sz * back
            if region is None or region.contains(Point(mx, mz)):   # region: a prepared polygon
                poly = rect(mx, mz, w, dep, math.atan2(tz, tx))
                if free(poly, spec.get("spacing", 1.6)):
                    r = gen_rng.random()
                    lv = spec["levels"][int(np.searchsorted(np.cumsum(spec["level_p"]), r * sum(spec["level_p"])))]
                    kind = spec["kind"](lv, gen_rng.random())
                    add_building(poly, kind, lv, gid[0]); gid[0] += 1
                    made += 1
            d += w + gen_rng.uniform(*spec["gap"])
    return made

landmark = None
if not EKSIOGLU:
    gid = [9_000_000_000]
    lm = CFG["landmark"]
    # The landmark building (sign + lobby) goes beside the chosen street nearest to the given real spot.
    nx, nz = F.fwd(*lm["near"])
    target = Point(nx, nz)
    cands = sorted(((ls.distance(target), kind, w, ls) for kind, w, ls in roads if kind in lm["roads"]), key=lambda c: c[0])
    def frame_at(ls, pos):
        a = ls.interpolate(max(0.0, pos - 2)); b2 = ls.interpolate(min(ls.length, pos + 2)); p0 = ls.interpolate(pos)
        tx, tz = b2.x - a.x, b2.y - a.y
        L = math.hypot(tx, tz) or 1.0
        return p0, tx / L, tz / L
    for dist0, kind, w, ls in cands[:6]:
        s0 = ls.project(target)
        p0, tx, tz = frame_at(ls, s0)
        sides = (1, -1)
        if "toward" in lm:
            # the wanted side of the street first, anywhere along it, before the other side
            tx_, tz_ = F.fwd(*lm["toward"])
            if -tz * (tx_ - p0.x) + tx * (tz_ - p0.y) < 0:
                sides = (-1, 1)
        for side in sides:
            # (a dual carriageway is two ways side by side: step back past the second one if needed)
            for extra, shift in [(e, s_) for e in (0.0, 9.0, 16.0) for s_ in (0, 6, -6, 12, -12, 20, -20, 30, -30, 42, -42)]:
                p0, tx, tz = frame_at(ls, min(max(s0 + shift, 1.0), ls.length - 1.0))
                sx, sz = -tz * side, tx * side
                back = w / 2 + 4.0 + extra + lm["size"][1] / 2
                poly = rect(p0.x + sx * back, p0.y + sz * back, lm["size"][0], lm["size"][1], math.atan2(tz, tx))
                if free(poly, 3.0):
                    landmark = add_building(poly, lm["kind"], lm["levels"], gid[0]); gid[0] += 1
                    landmark["front"] = (p0.x + sx * (w / 2 + 1.0 + extra), p0.y + sz * (w / 2 + 1.0 + extra))
                    break
            if landmark:
                break
        if landmark:
            break
    print("landmark", "placed" if landmark else "NOT placed", landmark["poly"].centroid if landmark else "")

    if CFG["fill"] == "village":
        # Senir: one- and two-storey houses with gardens along every street of the town.
        town = unary_union([poly_from(e) for e in osm if e.get("tags", {}).get("landuse") == "residential"
                            and e["type"] == "way"]).buffer(14)
        town_p = prep(town)
        spec = dict(width=(8, 13), depth=(7, 10.5), setback=(0.8, 3.5), gap=(2.5, 7.5), skip=0.14,
                    levels=(1, 2, 3), level_p=(0.5, 0.43, 0.07),
                    kind=lambda lv, r: 0 if lv == 3 else (4 if r < 0.08 else 5))
        made = 0
        order = sorted(range(len(roads)), key=lambda k: (roads[k][0], -roads[k][2].length))
        for k in order:
            kind, w, ls = roads[k]
            if kind in (2, 3) and ls.intersects(town):
                made += frontage(ls, w, town_p, spec, gid)
        # A second row behind the first where the gardens are deep enough.
        spec2 = dict(spec, setback=(13, 20), skip=0.45)
        for k in order:
            kind, w, ls = roads[k]
            if kind == 2 and ls.intersects(town):
                made += frontage(ls, w, town_p, spec2, gid)
        print("village houses", made)
    elif CFG["fill"] == "campus":
        # Fırat: faculty blocks along the campus roads where OpenStreetMap has none.
        campus = unary_union([poly_from(e) for e in osm if e.get("tags", {}).get("amenity") in ("university", "hospital")
                              and e["type"] in ("way", "relation")])
        campus_p = prep(campus)
        campus_roads = sorted([r for r in roads if r[0] in (2, 3) and r[2].intersects(campus)],
                              key=lambda r: (r[0], -r[2].length))
        made = 0
        # Long lecture halls and faculty wings first (they need the room), then smaller blocks.
        spec_big = dict(width=(36, 60), depth=(15, 21), setback=(12, 20), gap=(45, 85), skip=0.5, spacing=10.0,
                        levels=(3, 4, 5), level_p=(0.3, 0.45, 0.25), kind=lambda lv, r: 2)
        for kind, w, ls in campus_roads:
            made += frontage(ls, w, campus_p, spec_big, gid)
        spec = dict(width=(20, 26), depth=(13, 18), setback=(8, 16), gap=(30, 58), skip=0.4, spacing=10.0,
                    levels=(2, 3, 4, 5), level_p=(0.15, 0.35, 0.35, 0.15),
                    kind=lambda lv, r: 2 if r < 0.8 else 4)
        for kind, w, ls in campus_roads:
            made += frontage(ls, w, campus_p, spec, gid)
        # Housing blocks in the neighbourhoods round the campus (OpenStreetMap has the streets only).
        homes = unary_union([poly_from(e) for e in osm if e.get("tags", {}).get("landuse") in ("residential", "commercial")
                             and e["type"] in ("way", "relation")]).difference(campus)
        homes_p = prep(homes)
        spec_home = dict(width=(16, 24), depth=(11, 14), setback=(4, 8), gap=(8, 18), skip=0.2, spacing=4.0,
                         levels=(3, 4, 5, 6), level_p=(0.2, 0.35, 0.3, 0.15), kind=lambda lv, r: 0 if r < 0.85 else 4)
        for kind, w, ls in sorted(roads, key=lambda r: (r[0], -r[2].length)):
            if kind in (2, 3) and ls.intersects(homes):
                made += frontage(ls, w, homes_p, spec_home, gid)
        print("campus buildings", made)

# Landmark: the clinic in Ekşioğlu = closest building to the origin on the same side as the address
# (the geocoded point is on the street); elsewhere the building placed above.
if EKSIOGLU:
    origin = Point(0, 0)
    cands = [b for b in buildings if b["kind"] in (0, 4, 5) and b["poly"].area < 1500]
    clinic = min(cands, key=lambda b: b["poly"].distance(origin))
    print("clinic building", clinic["id"], "area %.0f" % clinic["poly"].area, "dist %.1f" % clinic["poly"].distance(origin))
else:
    clinic = landmark

# Levels and enterable selection
for b in buildings:
    p = b["poly"]; t = b["tags"]; r = h01(b["id"])
    rect_ = p.minimum_rotated_rectangle
    rc = list(rect_.exterior.coords)[:4]
    e0 = math.dist(rc[0], rc[1]); e1 = math.dist(rc[1], rc[2])
    if e0 >= e1:
        w, d = e0, e1; ang = math.atan2(rc[1][1] - rc[0][1], rc[1][0] - rc[0][0])
    else:
        w, d = e1, e0; ang = math.atan2(rc[2][1] - rc[1][1], rc[2][0] - rc[1][0])
    b["obb"] = (rect_.centroid.x, rect_.centroid.y, w, d, math.degrees(ang))
    b["rectness"] = p.area / max(1e-3, rect_.area)
    lv = b.get("gen_levels")
    if lv is None and "building:levels" in t:
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
    near = [o for o in buildings if abs(o["obb"][0] - cx) < 60 and abs(o["obb"][1] - cz) < 60] if not EKSIOGLU else buildings
    for sgn in (-1, 1):
        px, pz = cx + Sx * sgn * (d / 2 + 1.4), cz + Sz * sgn * (d / 2 + 1.4)
        pt = Point(px, pz)
        clear = all(o is b or o["poly"].distance(pt) > 1.0 for o in near)
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
    if len(chosen) >= CFG["enterable"]:
        break
if clinic not in chosen:
    chosen.append(clinic)
for b in chosen:
    b["flags"] |= 1
clinic["flags"] |= 2
for b in buildings:
    # hip tile roof for some rectangular apartments that are not enterable (enterable ones get flat roofs)
    hip_p = 0.55 if b["kind"] == 0 or EKSIOGLU else 0.85
    if b["kind"] in (0, 5) and b["rectness"] > 0.9 and not (b["flags"] & 1) and h01(b["id"] * 3) < hip_p:
        b["flags"] |= 4
print("enterable", len(chosen))

if EKSIOGLU:
    # Clinic front: the nearest road point
    best = None
    for kind, w, ls in roads:
        if kind > 1:   # the clinic's address is on Turgut Özal Caddesi (primary road)
            continue
        d = ls.distance(clinic["poly"].centroid)
        if best is None or d < best[0]:
            best = (d, ls, w)
    rp = best[1].interpolate(best[1].project(clinic["poly"].centroid))
    best_d = best[0]
else:
    rp = Point(*clinic["front"])
    best_d = rp.distance(clinic["poly"])
# Stand in front of the middle of the landmark's street-side wall (where the game puts the sign).
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
print("lobby at (%.1f, %.1f) yaw %.0f, road %.1f m away" % (lobby[0], lobby[1], lobby_yaw, best_d))

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

fields = []    # (polygon, orchard?) farmland round Senir
if CFG.get("fill") == "village":
    # Fields between the town and the lake, and on the gentle slopes: ploughed strips, meadows, orchards.
    frng = np.random.default_rng(77)
    town_g = town.buffer(4)
    forest_g = unary_union([p for g, p in areas if g == GROUND_FOREST]) if any(g == GROUND_FOREST for g, p in areas) else Polygon()
    roads_g = unary_union([ls.buffer(w / 2 + 2) for kind, w, ls in roads if kind <= 3])
    z0 = -PLAY
    while z0 < PLAY:
        dz = frng.uniform(26, 52)
        x0 = -PLAY + frng.uniform(0, 30)
        while x0 < PLAY:
            dx = frng.uniform(40, 95)
            cell = box(x0 + 1.5, z0 + 1.5, x0 + dx - 1.5, z0 + dz - 1.5)
            c = cell.centroid
            if height_at(c.x, c.y) > 1.2 and not cell.intersects(town_g):
                f = cell.difference(roads_g).difference(forest_g)
                if f.area > 400:
                    r = frng.random()
                    kind = "plough" if r < 0.42 else ("orchard" if r < 0.68 else "meadow")
                    fields.append((f, kind))
            x0 += dx
        z0 += dz

ORDER = [GROUND_URBAN, GROUND_INDUSTRIAL, GROUND_DIRT, GROUND_GRASS, GROUND_CEMETERY, GROUND_FOREST, GROUND_PARK,
         GROUND_PITCH, GROUND_PARKING, GROUND_POOL]
for f, kind in fields:
    if kind == "plough":
        fill_poly(f, GROUND_DIRT)
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
if not EKSIOGLU:
    # A paved square in front of the landmark.
    fill_poly(Point(*lobby).buffer(9.0), GROUND_SIDEWALK)
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
        if not EKSIOGLU and height_at(x, z) < 0.8:
            continue
        trees.append((x, z, kind_fn()))
        out += 1
big = not EKSIOGLU
for g, p in areas:
    if g == GROUND_FOREST:
        scatter(p, 11.0 if big else 9.5, lambda: 0 if rng.random() < 0.6 else 1, 900 if big else 500)
    elif g == GROUND_PARK:
        scatter(p, 16 if big else 13, lambda: 1 if rng.random() < 0.7 else 0, 260 if big else 80)
    elif g == GROUND_CEMETERY:
        scatter(p, 11, lambda: 2, 60)
    elif g == GROUND_URBAN:
        scatter(p, 30, lambda: 1, 400)
for e in osm:
    if e["type"] == "node" and e.get("tags", {}).get("natural") == "tree":
        x, z = F.fwd(*to_local(e["lat"], e["lon"]))
        if abs(x) < PLAY and abs(z) < PLAY:
            trees.append((x, z, 1))
# Orchards: rows of fruit trees.
for f, kind in fields:
    if kind != "orchard":
        continue
    minx, miny, maxx, maxy = f.bounds
    z = miny + 3.5
    while z < maxy - 2:
        x = minx + 3.5 + rng.uniform(0, 2)
        while x < maxx - 2:
            if f.contains(Point(x, z)) and abs(x) < PLAY - 3 and abs(z) < PLAY - 3 and height_at(x, z) > 0.8:
                trees.append((x, z, 1))
            x += 8.0
        z += 8.0
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
if not EKSIOGLU:
    # Poplars along the lake shore and pines on the hills round the play area.
    for k in range(260):
        x = rng.uniform(-PLAY + 4, PLAY - 4); z = rng.uniform(-PLAY + 4, PLAY - 4)
        h = height_at(x, z)
        if CFG["edge"] == "lake" and 0.5 < h < 3.0 and ground_at(x, z) in (GROUND_GRASS, GROUND_DIRT):
            trees.append((x, z, 2))
    trees = [t for t in trees if ground_at(t[0], t[1]) != GROUND_BUILDING]
    if len(trees) > 2600:
        keep = rng.permutation(len(trees))[:2600]
        trees = [trees[i] for i in sorted(keep)]
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
pal = {GROUND_GRASS: (110, 150, 70), GROUND_PARK: (90, 160, 70), GROUND_DIRT: (150, 120, 80), GROUND_URBAN: (170, 160, 140),
       GROUND_INDUSTRIAL: (160, 160, 165), GROUND_FOREST: (50, 100, 45), GROUND_CEMETERY: (120, 140, 100),
       GROUND_PITCH: (80, 170, 80), GROUND_POOL: (80, 160, 230), GROUND_SIDEWALK: (200, 195, 185),
       GROUND_PARKING: (120, 120, 125), GROUND_ROAD: (60, 60, 65), GROUND_BUILDING: (150, 90, 70)}
arr = np.zeros((G, G, 3), np.uint8)
for k, c in pal.items():
    arr[ground == k] = c
Image.fromarray(arr[::-1]).save(PREVIEW)

# Map selection picture: ground colours, hill shading, water, trees.
if True:
    S = 256
    hz = np.array(Image.fromarray(H.astype(np.float32)).resize((S, S), Image.BILINEAR))
    gy, gx_ = np.gradient(hz, MAP / S)
    shade = np.clip(0.78 + (-gx_ * 0.55 + gy * 0.55) * 1.6, 0.45, 1.25)
    col = np.array(Image.fromarray(arr).resize((S, S), Image.BILINEAR)).astype(np.float32)
    col *= shade[..., None]
    water = hz < 0.05
    depth = np.clip(-hz / 5.0, 0, 1)[..., None]
    col[water] = ((1 - depth) * np.array([70, 150, 175]) + depth * np.array([25, 75, 120]))[water]
    out = np.clip(col, 0, 255).astype(np.uint8)[::-1]
    Image.fromarray(out).save(f"{OUT}/preview.png", optimize=True)

for k in ("height", "ground", "features"):
    print(k, os.path.getsize(f"{OUT}/{k}.bytes"))
