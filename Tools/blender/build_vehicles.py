"""Procedural low-poly vehicles for Zootopia Mobile -> Assets/Resources/Models/Vehicles/<Name>.fbx

    python3 build_vehicles.py                       # rebuild all 7 FBX files
    python3 build_vehicles.py --only Tank,Heli      # rebuild some
    python3 build_vehicles.py --preview DIR         # also render 800x500 previews + vehicles_sheet.png
    python3 build_vehicles.py --verify              # re-import the FBX files and check names / pivots / axes

Same axes as the other game models (see convert_prop.py): in Blender FRONT = -Y, UP = +Z, metres, exported
with FBX_SCALE_ALL so the front ends up on Unity +Z. Origin of every vehicle = centre of its ground contact
(boat: waterline centre). Hierarchy (empty VehicleRoot at the origin):

    VehicleRoot -> Body, Wheel_0..n (FL, FR, then rearwards; L = +X here = -X in Unity)
                -> Handlebar (moto)          -> Rotor, TailRotor (heli)     -> Prop (boat)
                -> Turret -> Gun -> Muzzle (empty at the barrel tip) (tank)

Moving parts keep their own pivot (wheel centre, rotor hub, turret ring, gun trunnion) with an identity
rotation, so their local axes are the vehicle axes: wheels / tail rotor / gun spin around local X, turret and
rotor around local "up" (Blender Z; in Unity the root carries the usual -90 deg X axis conversion, so this is
the parts' local Z there), the boat prop around the fore-aft axis. Exception: the Muzzle empty is turned +90 deg
about X so that its Unity transform.forward points out of the barrel.
Materials (base colour only): Paint, Dark, Tire, Metal, Glass, Light, Canvas, Rotor. Paint is the panel colour
the game swaps for camouflage. Colours are stored as display (sRGB) values, i.e. what Unity shows.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector as V, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Models", "Vehicles"))

PAINT, DARK, TIRE, METAL, GLASS, LIGHT, CANVAS, ROTOR = range(8)
MAT_NAMES = ["Paint", "Dark", "Tire", "Metal", "Glass", "Light", "Canvas", "Rotor"]
COMMON = {DARK: (0.13, 0.13, 0.14), TIRE: (0.07, 0.07, 0.075), METAL: (0.62, 0.63, 0.65), GLASS: (0.40, 0.50, 0.58),
          LIGHT: (1.0, 0.93, 0.76), CANVAS: (0.45, 0.43, 0.30), ROTOR: (0.17, 0.17, 0.18)}
TRI_BUDGET = {"Offroad": 4000, "ATV": 4000, "Moto": 4000, "Boat": 4000, "Truck": 7000, "Heli": 7000, "Tank": 7000}

rad = math.radians


def lin(a, b, n):
    return [a + (b - a) * i / (n - 1) for i in range(n)]


def arc(cy, cz, r, a0, a1, n):
    """Points (y, z) on a circle, angles in degrees (0 = +Y/rear, 90 = up)."""
    return [(cy + r * math.cos(rad(a)), cz + r * math.sin(rad(a))) for a in lin(a0, a1, n)]


def basis(axis):
    a = V(axis).normalized()
    h = V((0, 0, 1)) if abs(a.z) < 0.9 else V((1, 0, 0))
    u = a.cross(h).normalized()
    return a, u, a.cross(u).normalized()


def round_path(pts, r, steps=3):
    """Replace the inner corners of a polyline with small arcs (quadratic Bezier)."""
    pts = [V(p) for p in pts]
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, b, c = pts[i - 1], pts[i], pts[i + 1]
        d0, d1 = (a - b), (c - b)
        rr = min(r, d0.length * 0.45, d1.length * 0.45)
        p0, p2 = b + d0.normalized() * rr, b + d1.normalized() * rr
        for k in range(steps + 1):
            t = k / steps
            out.append(p0 * (1 - t) ** 2 + b * 2 * t * (1 - t) + p2 * t * t)
    out.append(pts[-1])
    return out


def rot(x=0.0, y=0.0, z=0.0):
    return (Matrix.Rotation(rad(z), 3, "Z") @ Matrix.Rotation(rad(y), 3, "Y") @ Matrix.Rotation(rad(x), 3, "X"))


class Part:
    """Geometry of one exported mesh object, built in vehicle space (metres, front -Y, up +Z)."""

    def __init__(self):
        self.bm = bmesh.new()

    def _face(self, vs, mat):
        f = self.bm.faces.new(vs)
        f.material_index = mat
        return f

    def skin(self, rings, mat, cap0=True, cap1=True, wrap=False, cap_mat=None):
        """Connect consecutive rings of points (a 1-point ring is a pole). mat: int or f(ring_i, seg_j)."""
        mf = mat if callable(mat) else (lambda i, j: mat)
        vs = [[self.bm.verts.new(V(p)) for p in r] for r in rings]
        out = []
        pairs = list(zip(vs[:-1], vs[1:])) + ([(vs[-1], vs[0])] if wrap else [])
        for i, (a, b) in enumerate(pairs):
            if len(a) == 1 or len(b) == 1:
                ring, apex = (b, a[0]) if len(a) == 1 else (a, b[0])
                n = len(ring)
                for j in range(n):
                    out.append(self._face([apex, ring[j], ring[(j + 1) % n]], mf(i, j)))
            else:
                n = len(a)
                for j in range(n):
                    k = (j + 1) % n
                    out.append(self._face([a[j], a[k], b[k], b[j]], mf(i, j)))
        cm = cap_mat if cap_mat is not None else mf(0, 0)
        if not wrap:
            if cap0 and len(vs[0]) > 2:
                out.append(self._face(list(reversed(vs[0])), cm))
            if cap1 and len(vs[-1]) > 2:
                out.append(self._face(vs[-1], cap_mat if cap_mat is not None else mf(len(vs) - 2, 0)))
        return out

    def finish(self, faces, bevel=0.0, mat=-1):
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        if bevel > 0:
            edges = list({e for f in faces for e in f.edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, offset_type="OFFSET", segments=1, profile=0.5,
                            affect="EDGES", clamp_overlap=True, material=mat)
        return faces

    # ---- primitives -------------------------------------------------------------------------------------
    def obox(self, c, size, mat, R=None, bevel=0.0):
        c = V(c)
        sx, sy, sz = (s / 2 for s in size)
        R = R or Matrix.Identity(3)
        P = lambda x, y, z: c + R @ V((x, y, z))
        r0 = [P(-sx, -sy, -sz), P(sx, -sy, -sz), P(sx, -sy, sz), P(-sx, -sy, sz)]
        r1 = [P(-sx, sy, -sz), P(sx, sy, -sz), P(sx, sy, sz), P(-sx, sy, sz)]
        return self.finish(self.skin([r0, r1], mat), bevel, mat)

    def box(self, a, b, mat, bevel=0.0):
        lo = [min(a[i], b[i]) for i in range(3)]
        hi = [max(a[i], b[i]) for i in range(3)]
        return self.obox([(lo[i] + hi[i]) / 2 for i in range(3)], [hi[i] - lo[i] for i in range(3)], mat, None, bevel)

    def prism(self, prof, axis, a0, a1, mat, bevel=0.0, scale1=1.0, pivot=None, rings=None):
        """Extrude a 2D profile along an axis ('x': prof=(y,z), 'y': (x,z), 'z': (x,y)).
        rings: optional list of (a, scale) for a multi-step loft (sloped sides)."""
        def P(u, v, w):
            return V((w, u, v)) if axis == "x" else V((u, w, v)) if axis == "y" else V((u, v, w))
        cu, cv = pivot or (sum(p[0] for p in prof) / len(prof), sum(p[1] for p in prof) / len(prof))
        steps = rings or [(a0, 1.0), (a1, scale1)]
        rs = [[P(cu + (u - cu) * s, cv + (v - cv) * s, a) for u, v in prof] for a, s in steps]
        return self.finish(self.skin(rs, mat), bevel, mat)

    def cyl(self, p0, p1, r, mat, r1=None, seg=12, phase=0.0, cap_mat=None, bevel=0.0):
        p0, p1 = V(p0), V(p1)
        _, u, w = basis(p1 - p0)
        ang = [phase + 2 * math.pi * k / seg for k in range(seg)]
        ring = lambda c, rr: [c + (u * math.cos(t) + w * math.sin(t)) * rr for t in ang]
        return self.finish(self.skin([ring(p0, r), ring(p1, r if r1 is None else r1)], mat, cap_mat=cap_mat), bevel, mat)

    def lathe(self, c, axis, prof, seg, mats, phase=0.0):
        """Surface of revolution; prof = [(along_axis, radius)], radius 0 -> pole; mats per band (or int)."""
        c = V(c)
        a, u, w = basis(axis)
        ang = [phase + 2 * math.pi * k / seg for k in range(seg)]
        rings = [[c + a * t] if r <= 1e-6 else [c + a * t + (u * math.cos(q) + w * math.sin(q)) * r for q in ang]
                 for t, r in prof]
        mf = mats if isinstance(mats, int) else (lambda i, j: mats[i])
        return self.finish(self.skin(rings, mf if not isinstance(mats, int) else mats))

    def sweep(self, path, r, mat, seg=8, closed=False, radii=None, cap=True):
        """Tube along a 3D path (parallel-transport frames)."""
        pts = [V(p) for p in path]
        n = len(pts)
        tans = []
        for i in range(n):
            t = (pts[(i + 1) % n] - pts[i - 1]) if closed else (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)])
            tans.append(t.normalized())
        _, u, _ = basis(tans[0])
        ang = [2 * math.pi * k / seg for k in range(seg)]
        rings = []
        for i in range(n):
            t = tans[i]
            u = (u - t * u.dot(t)).normalized()
            w = t.cross(u)
            rr = radii[i] if radii else r
            rings.append([pts[i] + (u * math.cos(q) + w * math.sin(q)) * rr for q in ang])
        return self.finish(self.skin(rings, mat, cap0=cap, cap1=cap, wrap=closed))

    def band(self, path_yz, x0, x1, t, mat, closed=False, tlist=None):
        """Strip of rectangular section x0..x1 by t (offset to the right of the walking direction,
        i.e. outwards for counter-clockwise (y, z) paths) along a planar path in the YZ plane."""
        pts = [V((y, z)) for y, z in path_yz]
        n = len(pts)

        def seg_n(i):  # normal of segment i -> i+1
            d = (pts[(i + 1) % n] - pts[i]).normalized()
            return V((d.y, -d.x))
        rings = []
        for i in range(n):
            if closed or 0 < i < n - 1:
                na, nb = seg_n(i - 1), seg_n(i)
                nv = (na + nb).normalized()
                nv = nv / max(0.35, nv.dot(nb))
            else:
                nv = seg_n(0) if i == 0 else seg_n(n - 2)
            tt = tlist[i] if tlist else t
            p, q = pts[i], pts[i] + nv * tt
            rings.append([(x0, p.x, p.y), (x1, p.x, p.y), (x1, q.x, q.y), (x0, q.x, q.y)])
        return self.finish(self.skin(rings, mat, cap0=not closed, cap1=not closed, wrap=closed))

    def ring_path(self, c, normal, r, tube, mat, n=10, seg=5):
        a, u, w = basis(normal)
        pts = [V(c) + (u * math.cos(2 * math.pi * k / n) + w * math.sin(2 * math.pi * k / n)) * r for k in range(n)]
        return self.sweep(pts, tube, mat, seg=seg, closed=True)

    def recolor(self, faces, fn):
        for f in faces:
            if f.is_valid:
                m = fn(f.calc_center_median(), f.normal)
                if m is not None:
                    f.material_index = m

    def tris(self):
        return sum(len(f.verts) - 2 for f in self.bm.faces)


# ---------------------------------------------------------------------------------------------------------
# shared bits

def wheel(R, W, rim_r, seg=18, lugs=True, rim=METAL, hub=DARK, lug_h=None):
    """Tyre + rim around the local X axis, centred on the origin. R = outer radius (incl. lugs)."""
    p = Part()
    hw = W / 2
    lug_h = (lug_h if lug_h is not None else R * 0.06) if lugs else 0.0
    Rt = R - lug_h
    side = [(0.55 * hw, 0.0), (0.55 * hw, 0.36 * rim_r), (0.82 * hw, rim_r), (hw, 0.84 * Rt), (0.80 * hw, Rt)]
    mats = [hub, rim, TIRE, TIRE]
    tread = [(0.0, Rt)] if lugs else []
    prof = side + tread + [(-x, r) for x, r in reversed(side)]
    mats = mats + [TIRE] * (len(tread) + 1) + [TIRE, TIRE, rim, hub]
    p.lathe((0, 0, 0), (1, 0, 0), prof, seg, mats)
    if lugs:
        step = 2 * math.pi / seg
        pick = []
        for f in p.bm.faces:
            if all(abs(V((0, v.co.y, v.co.z)).length - Rt) < 1e-4 for v in f.verts):
                c = f.calc_center_median()
                j = int(((math.atan2(c.z, c.y)) % (2 * math.pi)) / step)
                if (j + (c.x > 0)) % 2 == 0:
                    pick.append(f)
        res = bmesh.ops.extrude_discrete_faces(p.bm, faces=pick)
        for f in res["faces"]:
            c = f.calc_center_median()
            for v in f.verts:
                d = v.co - c
                d.y *= 0.8
                d.z *= 0.8
                co = c + d
                radial = V((0, co.y, co.z)).normalized()
                v.co = co + radial * lug_h
    return p


def at(part, pos):
    """Move a locally built part (e.g. a wheel) to its place in vehicle space."""
    bmesh.ops.translate(part.bm, vec=V(pos), verts=part.bm.verts[:])
    return part


def seat(p, x, y, ztop, w=0.48, d=0.46, back=0.55, mat=DARK, tilt=12.0, base=None, base_mat=DARK, back_bevel=0.03):
    p.box((x - w / 2, y - d / 2, ztop - 0.13), (x + w / 2, y + d / 2, ztop), mat, bevel=0.03)
    if base is not None:
        p.box((x - w * 0.35, y - d * 0.35, base), (x + w * 0.35, y + d * 0.35, ztop - 0.13), base_mat)
    if back > 0:
        R = rot(x=-tilt)
        c = V((x, y + d / 2 - 0.05, ztop - 0.04)) + R @ V((0, 0, back / 2))
        p.obox(c, (w * 0.94, 0.11, back), mat, R, bevel=back_bevel)


def steering_wheel(p, c, toward, r=0.17):
    a = V(toward).normalized()
    p.ring_path(c, a, r, 0.022, DARK, n=9, seg=4)
    p.cyl(V(c), V(c) - a * 0.32, 0.025, DARK, seg=6)
    for k in (0, 1):  # two spokes
        _, u, w = basis(a)
        d = u if k == 0 else -u
        p.cyl(V(c), V(c) + d * r, 0.015, DARK, seg=5)


def lamp(p, c, axis, r, depth=0.06, ring=DARK, seg=12):
    """Round lamp facing `axis`: dark housing + light lens."""
    a = V(axis).normalized()
    c = V(c)
    p.lathe(c, a, [(-depth, 0), (-depth, r * 1.15), (0.0, r * 1.15), (0.005, r * 0.9), (0.02, 0)], seg,
            [ring, ring, ring, LIGHT])


# ---------------------------------------------------------------------------------------------------------
# vehicles. Each returns dict(paint=, canvas=, body=Part, parts=[(name, Part, pivot, parent_name)], empties=[...])

def build_offroad():
    b = Part()
    R, W, wx, wf, wr = 0.42, 0.32, 0.80, -1.30, 1.30
    zb = 0.60

    def arch(cy, r=0.55):
        a0 = math.degrees(math.asin((zb - R) / r))
        return arch_pts(cy, R, r, a0, n=6)
    bottom = [(-1.95, zb)] + arch(wf) + arch(wr) + [(1.80, zb)]
    hood = [(-0.55, 1.16), (-0.62, 1.14), (-1.85, 1.07), (-1.95, 1.00)]
    side = bottom + [(1.80, 1.12), (1.76, 1.16)] + hood
    centre = bottom + [(1.80, 1.12), (1.76, 1.16), (1.66, 1.16), (1.66, 0.80), (-0.46, 0.80), (-0.46, 1.16)] + hood
    for s in (1, -1):
        b.prism(side, "x", s * 0.74, s * 0.88, PAINT, bevel=0.03)
    b.prism(centre, "x", -0.74, 0.74, PAINT)
    # chassis, axles, bumpers, steps
    b.box((-0.50, -1.90, 0.30), (0.50, 1.75, 0.61), DARK)
    for y in (wf, wr):
        b.cyl((-0.66, y, R), (0.66, y, R), 0.07, DARK, seg=8)
        b.box((-0.18, y - 0.16, R - 0.14), (0.18, y + 0.16, R + 0.12), DARK)
    b.box((-0.96, -2.08, 0.42), (0.96, -1.93, 0.64), DARK, bevel=0.025)
    b.box((-0.30, -2.12, 0.46), (0.30, -2.07, 0.60), METAL)  # winch plate
    b.box((-0.92, 1.78, 0.44), (0.92, 1.90, 0.64), DARK)
    for s in (1, -1):
        b.box((s * 0.86, -0.70, 0.46), (s * 0.97, 0.70, 0.56), DARK)
        # fender flares
        for cy in (wf, wr):
            b.band(arc(cy, R, 0.55, 8, 172, 7), s * 0.84, s * 0.98, 0.07, DARK)
    # grille + lamps
    b.box((-0.46, -1.975, 0.66), (0.46, -1.94, 0.98), DARK)
    for i in range(5):
        x = -0.32 + i * 0.16
        b.box((x - 0.03, -1.995, 0.69), (x + 0.03, -1.96, 0.95), PAINT)
    for s in (1, -1):
        lamp(b, (s * 0.64, -1.955, 0.86), (0, -1, 0), 0.10, seg=10)
        b.box((s * 0.80, 1.80, 0.86), (s * 0.66, 1.83, 1.02), DARK)
    # windshield (leans back), dash, steering wheel
    R_ws = rot(x=-14)
    wc = V((0, -0.46, 1.38))
    for s in (1, -1):
        b.obox(wc + V((s * 0.81, 0, 0)), (0.07, 0.06, 0.52), PAINT, R_ws)
        b.obox(wc + V((s * 0.90, 0.02, 0.12)), (0.10, 0.03, 0.08), DARK)  # mirrors
    b.obox(wc + R_ws @ V((0, 0, 0.24)), (1.69, 0.07, 0.07), PAINT)
    b.obox(wc + R_ws @ V((0, 0, -0.01)), (1.56, 0.025, 0.44), GLASS)
    b.box((-0.74, -0.48, 0.92), (0.74, -0.30, 1.12), DARK, bevel=0.02)
    steering_wheel(b, (0.40, -0.18, 1.18), (0, 0.7, 0.55))
    # seats (driver on the left = +X)
    for x in (0.40, -0.40):
        seat(b, x, 0.08, 1.00, back_bevel=0.0)
        seat(b, x, 1.10, 1.00, back_bevel=0.0)
    # roll bar
    hoop = [(0.80, 0.42, 1.12), (0.80, 0.42, 1.80), (-0.80, 0.42, 1.80), (-0.80, 0.42, 1.12)]
    b.sweep(round_path(hoop, 0.16, 3), 0.045, DARK, seg=6)
    for s in (1, -1):
        b.cyl((s * 0.80, 0.50, 1.74), (s * 0.80, 1.66, 1.17), 0.04, DARK, seg=6)
        b.cyl((s * 0.80, 0.36, 1.76), (s * 0.80, -0.36, 1.60), 0.035, DARK, seg=6)
    # spare tyre on the back (part of Body)
    sp = wheel(0.40, 0.28, 0.24, seg=16)
    add_part(b, sp, Matrix.Translation((0, 2.06, 1.04)) @ Matrix.Rotation(rad(90), 4, "Z"))
    b.box((-0.12, 1.86, 0.92), (0.12, 1.93, 1.16), DARK)
    wheels = [wheel(R, W, 0.25, seg=16) for _ in range(4)]
    pos = [(wx, wf, R), (-wx, wf, R), (wx, wr, R), (-wx, wr, R)]
    return dict(body=b, paint=(0.40, 0.42, 0.22),
                parts=[("Wheel_%d" % i, at(wheels[i], pos[i]), pos[i], None) for i in range(4)])


def arch_pts(cy, cz, r, a0, n=7):
    """Wheel-arch points from the front side over the top to the rear side (front = -Y)."""
    return [(cy + r * math.cos(rad(a)), cz + r * math.sin(rad(a))) for a in lin(180 - a0, a0, n)]


def add_part(dst, src, M):
    """Copy a Part's geometry into another Part with a 4x4 transform."""
    me = bpy.data.meshes.new("tmp")
    src.bm.to_mesh(me)
    me.transform(M)
    dst.bm.from_mesh(me)
    bpy.data.meshes.remove(me)


def build_atv():
    b = Part()
    Rf, Rr = 0.27, 0.29
    yf, yr = -0.62, 0.62
    # front body: fenders + nose, one slab with the wheel arch cut out
    zb = 0.50
    a0 = math.degrees(math.asin((zb - Rf) / 0.36))
    front = [(-0.98, zb)] + arch_pts(yf, Rf, 0.36, a0) + [(-0.20, zb), (-0.18, 0.60), (-0.28, 0.70),
                                                          (-0.88, 0.72), (-1.00, 0.64), (-1.03, 0.52)]
    for s in (1, -1):
        b.prism(front, "x", s * 0.24, s * 0.62, PAINT, bevel=0.025)
    nose = [(-1.00, 0.36), (-0.20, 0.36), (-0.20, 0.62), (-0.40, 0.66), (-0.86, 0.63), (-1.03, 0.50)]
    b.prism(nose, "x", -0.24, 0.24, PAINT, bevel=0.025)
    a1 = math.degrees(math.asin((0.52 - Rr) / 0.38))
    rear = [(0.20, 0.52)] + arch_pts(yr, Rr, 0.38, a1) + [(1.00, 0.52), (1.02, 0.66), (0.95, 0.74), (0.30, 0.74),
                                                         (0.18, 0.66)]
    b.prism(rear, "x", -0.63, 0.63, PAINT, bevel=0.025)
    # frame / engine / tank / side panels / seat
    b.box((-0.16, -0.95, 0.24), (0.16, 0.95, 0.40), DARK)
    b.box((-0.24, -0.32, 0.26), (0.24, 0.30, 0.62), DARK, bevel=0.03)
    b.box((-0.20, -0.30, 0.55), (0.20, 0.05, 0.68), METAL, bevel=0.02)
    b.box((-0.19, -0.46, 0.62), (0.19, -0.12, 0.86), PAINT, bevel=0.05)
    for s in (1, -1):
        b.box((s * 0.16, -0.14, 0.48), (s * 0.27, 0.50, 0.78), PAINT, bevel=0.03)
    b.box((-0.19, -0.16, 0.74), (0.19, 0.42, 0.88), DARK, bevel=0.04)
    b.box((-0.19, 0.38, 0.78), (0.19, 0.86, 0.98), DARK, bevel=0.04)
    b.box((-0.07, 0.36, 0.86), (0.07, 0.46, 0.93), METAL)  # grab handle
    # footrests
    for s in (1, -1):
        b.box((s * 0.20, -0.22, 0.30), (s * 0.52, 0.22, 0.36), DARK, bevel=0.01)
    # handlebar
    b.cyl((0, -0.42, 0.62), (0, -0.52, 0.98), 0.035, DARK, seg=8)
    bar = [(0.40, -0.44, 1.04), (0.24, -0.48, 1.02), (0.10, -0.52, 0.99), (-0.10, -0.52, 0.99), (-0.24, -0.48, 1.02),
           (-0.40, -0.44, 1.04)]
    b.sweep(bar, 0.022, METAL, seg=8)
    for s in (1, -1):
        b.cyl((s * 0.30, -0.455, 1.033), (s * 0.43, -0.435, 1.045), 0.032, DARK, seg=8)
    b.box((-0.13, -0.62, 0.92), (0.13, -0.48, 1.06), PAINT, bevel=0.03)  # headlight pod
    b.box((-0.10, -0.625, 0.95), (0.10, -0.61, 1.02), LIGHT)
    for s in (1, -1):
        b.box((s * 0.32, -1.045, 0.565), (s * 0.52, -1.02, 0.625), LIGHT)
    # racks
    for (y0, y1, z) in ((-0.95, -0.36, 0.76), (0.34, 0.98, 0.78)):
        loop = [(0.48, y0, z), (0.48, y1, z), (-0.48, y1, z), (-0.48, y0, z)]
        b.sweep(round_path(loop + [loop[0]], 0.06, 2), 0.018, METAL, seg=6, cap=True)
        for k in range(1, 4):
            yy = y0 + (y1 - y0) * k / 4
            b.cyl((0.48, yy, z), (-0.48, yy, z), 0.012, METAL, seg=5)
    # front bumper
    bump = [(0.30, -0.98, 0.32), (0.30, -1.10, 0.42), (0.20, -1.12, 0.56), (-0.20, -1.12, 0.56), (-0.30, -1.10, 0.42),
            (-0.30, -0.98, 0.32)]
    b.sweep(bump, 0.025, DARK, seg=8)
    # suspension
    for s in (1, -1):
        b.cyl((s * 0.14, yf, 0.32), (s * 0.34, yf, Rf), 0.03, DARK, seg=6)
        b.cyl((s * 0.14, yf + 0.12, 0.30), (s * 0.34, yf, Rf - 0.03), 0.025, DARK, seg=6)
        b.cyl((s * 0.10, yf - 0.02, 0.52), (s * 0.30, yf, Rf + 0.04), 0.04, METAL, seg=8)  # shock
    b.cyl((-0.36, yr, Rr), (0.36, yr, Rr), 0.05, DARK, seg=8)
    b.cyl((-0.12, 0.62, 0.62), (-0.14, 1.04, 0.68), 0.05, METAL, seg=10)  # exhaust
    wf_ = [wheel(Rf, 0.20, 0.15, seg=16) for _ in range(2)]
    wr_ = [wheel(Rr, 0.25, 0.16, seg=16) for _ in range(2)]
    pos = [(0.43, yf, Rf), (-0.43, yf, Rf), (0.44, yr, Rr), (-0.44, yr, Rr)]
    ws = wf_ + wr_
    return dict(body=b, paint=(0.75, 0.12, 0.10),
                parts=[("Wheel_%d" % i, at(ws[i], pos[i]), pos[i], None) for i in range(4)])


def build_moto():
    b = Part()
    h = Part()  # Handlebar group (fork, clamps, bar, fender, number plate)
    R = 0.34
    yf, yr = -0.71, 0.72
    head = V((0, -0.42, 0.95))
    axle = V((0, yf, R))
    ax = (head - axle).normalized()   # steering axis (raked)
    # fork legs
    for s in (1, -1):
        off = V((s * 0.085, 0, 0))
        b_low = axle + off + ax * 0.02
        h.cyl(b_low, b_low + ax * 0.36, 0.035, DARK, seg=8)
        h.cyl(b_low + ax * 0.34, head + off + ax * 0.08, 0.026, METAL, seg=8)
    for t in (-0.06, 0.08):
        c = head + ax * t
        h.obox(c, (0.26, 0.09, 0.05), DARK, rot(x=-25), bevel=0.01)
    top = head + ax * 0.12
    h.cyl(top, top + V((0, 0.02, 0.06)), 0.02, DARK, seg=6)
    bar = [(0.40, -0.30, 1.16), (0.22, -0.36, 1.13), (0.08, -0.40, 1.11), (-0.08, -0.40, 1.11), (-0.22, -0.36, 1.13),
           (-0.40, -0.30, 1.16)]
    h.sweep(bar, 0.016, METAL, seg=8)
    for s in (1, -1):
        h.cyl((s * 0.29, -0.333, 1.143), (s * 0.42, -0.295, 1.163), 0.026, DARK, seg=8)
        h.box((s * 0.22, -0.39, 1.12), (s * 0.30, -0.32, 1.15), DARK)  # levers / switches
    # number plate + headlight
    pc = head + ax * 0.02 + V((0, -0.12, 0))
    h.obox(pc, (0.26, 0.04, 0.30), PAINT, rot(x=-22), bevel=0.02)
    h.obox(pc + V((0, -0.03, 0.02)), (0.14, 0.03, 0.10), LIGHT, rot(x=-22))
    # front fender (high, dirt style) following the wheel
    fend = arc(yf, R, R + 0.06, 40, 150, 8)
    h.band(fend, -0.075, 0.075, 0.03, PAINT)
    for s in (1, -1):  # fender stays from the fork lowers up to the fender
        h.cyl(axle + V((s * 0.085, 0, 0)) + ax * 0.30, V((s * 0.07, yf - 0.035, 2 * R + 0.06)), 0.012, DARK, seg=5)
    # --- body ---
    # frame tubes
    fr = [(0, -0.40, 0.88), (0, -0.22, 0.48), (0, 0.00, 0.30), (0, 0.18, 0.36), (0, 0.22, 0.62), (0, 0.15, 0.85)]
    for s in (1, -1):
        b.sweep([V(p) + V((s * 0.07, 0, 0)) for p in fr], 0.022, DARK, seg=6)
        b.cyl((s * 0.06, -0.38, 0.92), (s * 0.08, 0.20, 0.88), 0.022, DARK, seg=6)
        b.cyl((s * 0.08, 0.20, 0.88), (s * 0.07, 0.86, 0.96), 0.018, DARK, seg=6)
    b.cyl((0, head.y, head.z - 0.10), (0, head.y + 0.05, head.z + 0.10), 0.045, DARK, seg=8)  # steering head tube
    # tank (loft)
    secs = [(-0.40, 0.10, 0.86, 1.00), (-0.30, 0.17, 0.82, 1.08), (-0.12, 0.18, 0.80, 1.10), (0.04, 0.15, 0.84, 1.04),
            (0.10, 0.10, 0.88, 0.99)]
    rings = []
    for y, hw, z0, z1 in secs:
        rings.append([(hw * 0.6, y, z0), (hw, y, z0 + (z1 - z0) * 0.45), (hw * 0.75, y, z1), (-hw * 0.75, y, z1),
                      (-hw, y, z0 + (z1 - z0) * 0.45), (-hw * 0.6, y, z0)])
    b.finish(b.skin(rings, PAINT))
    # radiator shrouds
    for s in (1, -1):
        b.obox((s * 0.17, -0.30, 0.80), (0.04, 0.30, 0.30), PAINT, rot(x=-18, z=s * 8), bevel=0.012)
        b.obox((s * 0.12, -0.28, 0.66), (0.05, 0.20, 0.24), DARK, rot(x=-18))  # radiators
    # seat
    seat_r = []
    for y, hw, z in ((-0.02, 0.11, 0.99), (0.20, 0.13, 1.02), (0.55, 0.12, 1.00), (0.80, 0.09, 0.98)):
        seat_r.append([(hw, y, z - 0.10), (hw, y, z - 0.02), (hw * 0.6, y, z), (-hw * 0.6, y, z), (-hw, y, z - 0.02),
                       (-hw, y, z - 0.10)])
    b.finish(b.skin(seat_r, DARK))
    # side panels + tail
    for s in (1, -1):
        b.obox((s * 0.13, 0.48, 0.84), (0.04, 0.42, 0.16), PAINT, rot(x=8), bevel=0.012)
    tail = [(0.40, 0.88), (0.80, 0.94), (1.08, 1.02), (1.10, 0.99), (0.80, 0.89), (0.42, 0.82)]
    b.prism(tail, "x", -0.10, 0.10, PAINT, bevel=0.01)
    b.box((-0.05, 1.04, 0.95), (0.05, 1.10, 0.99), DARK)  # tail light
    # engine
    b.box((-0.14, -0.22, 0.28), (0.14, 0.16, 0.60), DARK, bevel=0.03)
    b.obox((0, -0.16, 0.70), (0.20, 0.20, 0.24), METAL, rot(x=-20), bevel=0.02)
    for k in range(3):
        b.obox(V((0, -0.16, 0.66)) + rot(x=-20) @ V((0, 0, 0.05 * k)), (0.24, 0.24, 0.02), METAL, rot(x=-20))
    for s in (1, -1):
        b.cyl((s * 0.14, -0.05, 0.42), (s * 0.17, -0.05, 0.42), 0.11, METAL, seg=12)  # side covers
    # exhaust (right side = -X)
    ex = [(-0.02, -0.26, 0.72), (-0.06, -0.36, 0.58), (-0.12, -0.30, 0.40), (-0.16, -0.05, 0.36), (-0.17, 0.25, 0.55),
          (-0.17, 0.42, 0.74)]
    b.sweep(ex, 0.03, METAL, seg=8)
    b.cyl((-0.17, 0.40, 0.73), (-0.17, 0.88, 0.86), 0.06, METAL, r1=0.05, seg=10, cap_mat=DARK)
    # swingarm + shock
    for s in (1, -1):
        p0, p1 = V((s * 0.08, 0.10, 0.42)), V((s * 0.08, yr, R))
        d = p1 - p0
        R_sw = Matrix.Rotation(math.atan2(d.z, d.y), 3, "X")
        b.obox((p0 + p1) / 2, (0.04, d.length, 0.07), METAL, R_sw)
    b.cyl((0, 0.20, 0.72), (0, 0.40, 0.46), 0.04, METAL, seg=8)
    b.cyl((0, 0.24, 0.67), (0, 0.36, 0.51), 0.055, PAINT, seg=8)  # spring
    b.cyl((-0.11, yr, R), (0.11, yr, R), 0.025, METAL, seg=6)
    # footpegs
    for s in (1, -1):
        b.cyl((s * 0.10, 0.12, 0.40), (s * 0.24, 0.12, 0.40), 0.02, DARK, seg=6)
    w0 = wheel(R, 0.10, 0.27, seg=20, lug_h=0.018)
    w1 = wheel(R, 0.13, 0.25, seg=20, lug_h=0.02)
    return dict(body=b, paint=(0.12, 0.30, 0.70),
                parts=[("Wheel_0", at(w0, (0, yf, R)), (0, yf, R), None), ("Wheel_1", at(w1, (0, yr, R)), (0, yr, R), None),
                       ("Handlebar", h, tuple(head), None)])


def build_truck():
    b = Part()
    R, W, wx = 0.55, 0.40, 1.00
    ys = [-2.50, 1.10, 2.50]
    # chassis + axles
    for s in (1, -1):
        b.box((s * 0.38, -3.25, 0.72), (s * 0.54, 3.45, 0.98), DARK)
    for y in ys:
        b.cyl((-0.86, y, R), (0.86, y, R), 0.08, DARK, seg=8)
        b.box((-0.22, y - 0.18, R - 0.18), (0.22, y + 0.18, R + 0.15), DARK, bevel=0.04)
    b.box((-1.18, -3.48, 0.72), (1.18, -3.28, 1.02), DARK, bevel=0.03)  # bumper
    for s in (1, -1):
        b.box((s * 0.55, -3.56, 0.80), (s * 0.70, -3.47, 0.94), DARK)  # tow hooks
    # hood
    hood = [(-3.28, 0.95), (-3.28, 1.84), (-3.18, 1.92), (-1.85, 1.98), (-1.85, 0.95)]
    b.prism(hood, "x", -0.64, 0.64, PAINT, bevel=0.03)
    b.box((-0.50, -3.31, 1.05), (0.50, -3.27, 1.78), DARK)
    for i in range(6):
        x = -0.40 + i * 0.16
        b.box((x - 0.035, -3.34, 1.08), (x + 0.035, -3.29, 1.75), PAINT)
    # front fenders (flat, military)
    fend = [(-3.25, 1.32), (-1.85, 1.32), (-1.85, 0.90), (-1.93, 0.90), (-1.93, 1.25), (-3.17, 1.25), (-3.17, 0.98),
            (-3.25, 0.98)]
    for s in (1, -1):
        b.prism(fend, "x", s * 0.64, s * 1.24, PAINT, bevel=0.015)
        lamp(b, (s * 0.94, -3.27, 1.12), (0, -1, 0), 0.11, depth=0.08)
        b.box((s * 0.70, -3.27, 1.33), (s * 0.78, -3.15, 1.40), DARK)  # marker lamp mounts
    # cab
    cab = [(-1.86, 0.92), (-1.86, 1.96), (-1.68, 2.82), (-1.62, 2.86), (-0.56, 2.86), (-0.54, 0.92)]
    b.prism(cab, "x", -1.14, 1.14, PAINT, bevel=0.04)
    # windshield (2 panes on the slope)
    p0, p1 = V((0, -1.86, 1.98)), V((0, -1.69, 2.78))
    d = (p1 - p0)
    Rw = Matrix.Rotation(-math.atan2(d.y, d.z), 3, "X")
    mid = (p0 + p1) / 2 + V((0, -0.02, 0))
    for s in (1, -1):
        b.obox(mid + V((s * 0.53, 0, 0)), (0.96, 0.03, d.length * 0.82), GLASS, Rw)
        b.box((s * 1.15, -1.62, 2.02), (s * 1.16, -0.86, 2.62), GLASS)
        b.box((s * 1.15, -1.72, 1.00), (s * 1.16, -1.68, 2.70), DARK)  # door seam
        b.box((s * 1.15, -0.78, 1.00), (s * 1.16, -0.74, 2.70), DARK)
        b.box((s * 1.16, -1.00, 1.86), (s * 1.18, -0.86, 1.90), METAL)  # handle
        # mirrors
        b.cyl((s * 1.14, -1.70, 2.40), (s * 1.38, -1.72, 2.48), 0.02, DARK, seg=6)
        b.box((s * 1.33, -1.76, 2.18), (s * 1.43, -1.70, 2.56), DARK, bevel=0.01)
        # steps + fuel tank / toolbox
        b.box((s * 0.54, -1.62, 0.62), (s * 1.22, -0.90, 0.68), DARK)
        b.box((s * 0.90, -0.40, 0.70), (s * 1.18, 0.62, 1.12), PAINT if s < 0 else DARK, bevel=0.03)
    b.cyl((-0.96, -0.62, 1.15), (-0.96, -0.62, 3.05), 0.07, METAL, seg=10, cap_mat=DARK)  # exhaust stack
    b.box((-0.30, -1.30, 2.86), (0.30, -0.90, 2.93), DARK, bevel=0.01)  # roof hatch
    # cargo bed
    b.box((-1.24, -0.42, 1.16), (1.24, 3.56, 1.36), DARK)
    b.box((-1.24, -0.42, 1.34), (1.24, 3.56, 1.90), PAINT, bevel=0.03)
    for i in range(7):
        y = -0.2 + i * 0.6
        for s in (1, -1):
            b.box((s * 1.24, y - 0.04, 1.30), (s * 1.27, y + 0.04, 1.92), DARK)  # stakes
    for s in (1, -1):
        b.box((s * 1.25, -0.30, 1.52), (s * 1.27, 3.44, 1.58), DARK)  # rope rail
    # canvas cover with hoops, rear opening
    ring = lambda y, k: [(-1.22 * k, y, 1.86), (-1.22 * k, y, 2.80), (-1.06 * k, y, 3.06 + (k - 1) * 0.4),
                         (-0.62 * k, y, 3.17 + (k - 1) * 0.4), (0.62 * k, y, 3.17 + (k - 1) * 0.4),
                         (1.06 * k, y, 3.06 + (k - 1) * 0.4), (1.22 * k, y, 2.80), (1.22 * k, y, 1.86)]
    rings = [ring(-0.38, 1.0)]
    hoops = [0.30, 1.10, 1.90, 2.70]
    for y in hoops:
        rings += [ring(y - 0.08, 0.985), ring(y, 1.025), ring(y + 0.08, 0.985)]
    rings += [ring(3.48, 1.0), ring(3.52, 1.012), ring(3.52, 0.90), ring(3.20, 0.90)]
    nb = len(rings) - 1

    def cmat(i, j):
        return DARK if i >= nb - 1 else CANVAS
    b.finish(b.skin(rings, cmat))
    # rear: lights, mud flaps
    for s in (1, -1):
        b.box((s * 0.95, 3.56, 1.20), (s * 1.10, 3.60, 1.30), DARK)
        b.box((s * 0.80, 3.05, 0.35), (s * 1.18, 3.10, 1.16), DARK)
    wl = [wheel(R, W, 0.32, seg=18, lug_h=0.035) for _ in range(6)]
    pos = []
    for y in ys:
        pos += [(wx, y, R), (-wx, y, R)]
    return dict(body=b, paint=(0.29, 0.34, 0.22), canvas=(0.52, 0.48, 0.34),
                parts=[("Wheel_%d" % i, at(wl[i], pos[i]), pos[i], None) for i in range(6)])


def smooth_path(pts, sub=3, closed=False):
    """Catmull-Rom subdivision of a polyline."""
    P = [V(p) for p in pts]
    n = len(P)
    out = []
    rng = range(n) if closed else range(n - 1)
    for i in rng:
        p0 = P[(i - 1) % n] if closed else P[max(i - 1, 0)]
        p1, p2 = P[i], P[(i + 1) % n]
        p3 = P[(i + 2) % n] if closed else P[min(i + 2, n - 1)]
        for k in range(sub):
            t = k / sub
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                              (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    if not closed:
        out.append(P[-1])
    return out


def build_boat():
    b = Part()
    # hull stations: y, deck half-width, deck z, chine half-width, chine z, keel z
    st = [(-2.62, 0.10, 0.66, 0.06, 0.40, 0.30), (-2.35, 0.48, 0.60, 0.28, 0.12, 0.02),
          (-1.85, 0.78, 0.54, 0.50, -0.10, -0.20), (-1.05, 0.92, 0.50, 0.62, -0.18, -0.30),
          (0.00, 0.95, 0.48, 0.66, -0.20, -0.32), (1.40, 0.95, 0.48, 0.66, -0.19, -0.30),
          (2.35, 0.92, 0.48, 0.64, -0.16, -0.26)]
    rings = [[(0, y, zk), (-hc, y, zc), (-hw, y, zd), (hw, y, zd), (hc, y, zc)] for y, hw, zd, hc, zc, zk in st]
    b.finish(b.skin(rings, lambda i, j: DARK if j == 2 else PAINT, cap_mat=PAINT))
    # inflatable tubes (Dark), along a U around the bow
    ctrl = [(0.94, 2.70, 0.56), (0.95, 1.40, 0.56), (0.95, 0.0, 0.57), (0.92, -1.05, 0.60), (0.78, -1.85, 0.66),
            (0.48, -2.38, 0.72), (0.0, -2.68, 0.76)]
    ctrl = ctrl + [(-x, y, z) for x, y, z in reversed(ctrl[:-1])]
    path = smooth_path(ctrl, sub=3)
    n = len(path)
    radii = [0.25 - 0.04 * (1 - abs(2 * i / (n - 1) - 1)) for i in range(n)]
    # cone ends behind the transom
    path = [path[0] + V((0, 0.22, 0))] + path + [path[-1] + V((0, 0.22, 0))]
    radii = [0.12] + radii + [0.12]
    b.sweep(path, 0.25, DARK, seg=10, radii=radii)
    # rub strake / handles on the tubes
    for s in (1, -1):
        for y in (-0.6, 0.6, 1.6):
            b.box((s * 1.18, y - 0.12, 0.62), (s * 1.21, y + 0.12, 0.66), METAL)
    # console
    con = [(-0.60, 0.48), (-0.52, 1.02), (-0.24, 1.22), (0.12, 1.22), (0.12, 0.48)]
    b.prism(con, "x", -0.38, 0.38, PAINT, bevel=0.03)
    Rw = rot(x=-25)
    b.obox((0, -0.28, 1.42), (0.70, 0.03, 0.40), GLASS, Rw)
    for s in (1, -1):
        b.obox((s * 0.36, -0.16, 1.38), (0.03, 0.26, 0.30), GLASS, rot(x=-12))
    b.box((-0.30, -0.12, 1.06), (0.30, 0.11, 1.10), DARK)
    steering_wheel(b, (0.14, 0.16, 1.14), (0, 1, 0.5), r=0.15)
    b.box((-0.05, -0.58, 1.02), (0.05, -0.52, 1.07), LIGHT)
    b.cyl((0, 0.0, 1.22), (0, 0.0, 1.70), 0.015, METAL, seg=6)  # nav light mast
    b.cyl((0, 0.0, 1.70), (0, 0.0, 1.76), 0.035, LIGHT, seg=8)
    # seats (Canvas): two jockey seats, rear bench for two, bow cushion
    for x in (0.30, -0.30):
        seat(b, x, 0.60, 0.98, w=0.44, d=0.42, back=0.20, mat=CANVAS, tilt=5, base=0.48)
    b.box((-0.75, 1.62, 0.48), (0.75, 2.20, 0.66), PAINT, bevel=0.02)
    b.box((-0.72, 1.66, 0.66), (0.72, 2.18, 0.80), CANVAS, bevel=0.03)
    b.obox((0, 2.20, 0.98), (1.40, 0.12, 0.40), CANVAS, rot(x=-10), bevel=0.03)
    b.prism([(-0.55, -2.15), (0.55, -2.15), (0.68, -1.30), (-0.68, -1.30)], "z", 0.48, 0.62, CANVAS, bevel=0.03)
    # bow cleat + transom
    b.box((-0.06, -2.55, 0.66), (0.06, -2.40, 0.72), METAL)
    # outboard motor
    b.box((-0.14, 2.33, 0.20), (0.14, 2.48, 0.56), METAL)
    cw = []
    for y, hw, z0, z1 in ((2.42, 0.18, 0.56, 1.10), (2.52, 0.24, 0.55, 1.20), (2.72, 0.25, 0.55, 1.22),
                          (2.86, 0.20, 0.58, 1.14), (2.92, 0.12, 0.64, 1.02)):
        cw.append([(hw, y, z0), (hw, y, z1 - 0.10), (hw * 0.65, y, z1), (-hw * 0.65, y, z1), (-hw, y, z1 - 0.10),
                   (-hw, y, z0)])
    b.finish(b.skin(cw, DARK))
    b.box((-0.255, 2.50, 0.85), (0.255, 2.80, 0.88), METAL)  # cowl band
    b.obox((0, 2.66, 0.36), (0.20, 0.30, 0.40), DARK, bevel=0.02)  # midsection
    b.box((-0.07, 2.58, -0.42), (0.07, 2.80, 0.18), DARK, bevel=0.02)  # shaft leg
    b.box((-0.16, 2.48, -0.28), (0.16, 2.90, -0.25), DARK)  # cavitation plate
    b.cyl((0, 2.50, -0.44), (0, 2.88, -0.44), 0.075, DARK, seg=10)
    b.lathe((0, 2.50, -0.44), (0, -1, 0), [(0, 0.075), (0.08, 0)], 10, [DARK])
    b.box((-0.015, 2.62, -0.62), (0.015, 2.86, -0.50), DARK)  # skeg
    # propeller (separate, spins around Y)
    pr = Part()
    pc = V((0, 2.93, -0.44))
    pr.lathe(pc, (0, 1, 0), [(-0.05, 0), (-0.05, 0.05), (0.06, 0.04), (0.09, 0)], 10, [METAL, METAL, METAL])
    for k in range(3):
        a = 120 * k
        Rb = Matrix.Rotation(rad(a), 3, "Y") @ Matrix.Rotation(rad(28), 3, "Z")
        pr.obox(pc + Matrix.Rotation(rad(a), 3, "Y") @ V((0.0, 0.0, 0.13)), (0.10, 0.012, 0.17), METAL, Rb)
    return dict(body=b, paint=(0.92, 0.92, 0.90), canvas=(0.70, 0.64, 0.52),
                parts=[("Prop", pr, tuple(pc), None)])


def superellipse_ring(y, hw, z0, z1, n=12, e=2.6, zc_bias=0.0):
    zc, hh = (z0 + z1) / 2, (z1 - z0) / 2
    out = []
    for k in range(n):
        t = 2 * math.pi * (k + 0.5) / n
        c, s = math.cos(t), math.sin(t)
        out.append((hw * math.copysign(abs(c) ** (2 / e), c), y, zc + hh * math.copysign(abs(s) ** (2 / e), s)))
    return out


def build_heli():
    b = Part()
    # fuselage loft
    st = [(-2.44, 0.14, 0.98, 1.20), (-2.36, 0.36, 0.84, 1.38), (-2.18, 0.54, 0.72, 1.58), (-1.90, 0.68, 0.64, 1.76),
          (-1.50, 0.76, 0.60, 1.90), (-1.00, 0.80, 0.59, 1.98), (-0.30, 0.81, 0.60, 2.00), (-0.18, 0.81, 0.60, 2.00),
          (0.55, 0.79, 0.63, 2.00),
          (1.10, 0.64, 0.80, 1.97), (1.50, 0.44, 1.10, 1.94), (1.80, 0.30, 1.38, 1.90)]
    rings = [superellipse_ring(y, hw, z0, z1, n=16) for y, hw, z0, z1 in st]
    fus = b.finish(b.skin(rings, PAINT))

    def canopy(c, n):
        if c.y < -1.15 and c.z > 0.98 and n.z > -0.3:
            return GLASS
        if (-1.0 < c.y < -0.3 or -0.18 < c.y < 0.55) and 1.05 < c.z < 1.86 and abs(n.x) > 0.6:
            return GLASS  # door windows (frame column between them)
        return None
    b.recolor(fus, canopy)
    # engine fairing + intakes + exhaust
    eg = [superellipse_ring(y, hw, z0, z1, n=10, e=3.0) for y, hw, z0, z1 in
          ((-0.85, 0.30, 1.85, 2.12), (-0.55, 0.50, 1.85, 2.36), (0.70, 0.50, 1.85, 2.36), (1.40, 0.36, 1.80, 2.22),
           (1.80, 0.20, 1.80, 2.06))]
    b.finish(b.skin(eg, PAINT))
    for s in (1, -1):
        b.box((s * 0.44, -0.40, 2.02), (s * 0.52, 0.10, 2.26), DARK)
    b.cyl((0, 1.70, 2.06), (0, 2.05, 2.12), 0.11, DARK, seg=10, cap_mat=DARK)
    b.cyl((0, -0.20, 2.30), (0, -0.20, 2.62), 0.08, METAL, seg=10)  # mast
    b.cyl((0, -0.20, 2.32), (0, -0.20, 2.42), 0.20, DARK, r1=0.12, seg=10)
    # tail boom
    tb = [superellipse_ring(y, r, zc - r, zc + r, n=10, e=2.0) for y, r, zc in
          ((1.40, 0.34, 1.62), (2.40, 0.25, 1.68), (3.60, 0.18, 1.76), (4.70, 0.13, 1.84), (5.05, 0.11, 1.86))]
    b.finish(b.skin(tb, PAINT))
    # fins
    fin = [(4.55, 1.86), (4.95, 2.62), (5.25, 2.66), (5.12, 1.90), (5.05, 1.50), (4.88, 1.42), (4.82, 1.80)]
    b.prism(fin, "x", -0.04, 0.04, PAINT, bevel=0.012)
    b.box((-0.80, 3.95, 1.76), (0.80, 4.30, 1.82), PAINT, bevel=0.012)
    for s in (1, -1):
        b.box((s * 0.78, 3.98, 1.62), (s * 0.82, 4.32, 1.98), PAINT, bevel=0.01)
    b.cyl((0.04, 4.95, 2.30), (0.14, 4.95, 2.30), 0.06, DARK, seg=8)  # tail rotor gearbox
    b.cyl((0, 4.92, 1.45), (0, 5.10, 1.30), 0.02, METAL, seg=6)  # tail skid
    # skids
    for s in (1, -1):
        sk = round_path([(s * 1.0, 1.35, 0.05), (s * 1.0, -1.45, 0.05), (s * 1.0, -1.85, 0.32)], 0.25, 3)
        b.sweep(sk, 0.05, METAL, seg=8)
    for y in (-0.95, 0.75):
        st_ = round_path([(1.0, y, 0.06), (0.92, y, 0.45), (0.55, y, 0.66), (-0.55, y, 0.66), (-0.92, y, 0.45),
                          (-1.0, y, 0.06)], 0.15, 2)
        b.sweep(st_, 0.045, METAL, seg=8)
    lamp(b, (0, -2.05, 0.70), (0, -0.5, -0.8), 0.07)
    b.cyl((0.25, -2.0, 0.8), (0.25, -2.0, 0.55), 0.01, DARK, seg=5)  # wire cutter / antenna
    # rotor (hub + 4 blades, spins around Z)
    hub = V((0, -0.20, 2.68))
    r = Part()
    r.lathe(hub, (0, 0, 1), [(-0.07, 0), (-0.07, 0.20), (0.07, 0.20), (0.12, 0.08), (0.14, 0)], 12,
            [ROTOR, ROTOR, ROTOR, ROTOR])
    for k in range(4):
        Rz = Matrix.Rotation(rad(90 * k), 3, "Z")
        d = Rz @ V((0, -1, 0))
        r.obox(hub + d * 0.30, (0.16, 0.30, 0.08), METAL, Rz)  # grips
        for (r0, r1, ch) in ((0.42, 3.9, 0.30), (3.9, 4.1, 0.22)):
            Rb = Rz @ Matrix.Rotation(rad(-4), 3, "Y")
            r.obox(hub + d * ((r0 + r1) / 2), (ch, r1 - r0, 0.045), ROTOR, Rb)
    # tail rotor (2 blades in the YZ plane, spins around X)
    th = V((0.22, 4.95, 2.30))
    tr = Part()
    tr.cyl(th - V((0.08, 0, 0)), th + V((0.06, 0, 0)), 0.07, ROTOR, seg=8)
    for k in (0, 1):
        Rx = Matrix.Rotation(rad(180 * k + 20), 3, "X")
        tr.obox(th + Rx @ V((0, 0, 0.40)), (0.03, 0.13, 0.70), ROTOR, Rx @ Matrix.Rotation(rad(8), 3, "Z"))
    return dict(body=b, paint=(0.25, 0.29, 0.25),
                parts=[("Rotor", r, tuple(hub), None), ("TailRotor", tr, tuple(th), None)])


def build_tank():
    b = Part()
    # hull: lower (between tracks) + upper (full width)
    low = [(-2.45, 0.42), (2.55, 0.42), (2.95, 0.80), (2.95, 1.10), (-3.05, 1.10), (-3.05, 0.92)]
    b.prism(low, "x", -1.04, 1.04, PAINT, bevel=0.03)
    up = [(-3.05, 1.08), (2.95, 1.08), (2.98, 1.52), (2.90, 1.56), (-1.55, 1.56), (-3.05, 1.18)]
    b.prism(up, "x", -1.72, 1.72, PAINT, bevel=0.03)
    # tracks, wheels, skirts
    sp, idl = (2.70, 0.58, 0.33), (-2.78, 0.60, 0.31)
    outline = ([(-2.30, 0.10), (2.30, 0.10)] + arc(sp[0], sp[1], sp[2], -55, 90, 6) +
               arc(idl[0], idl[1], idl[2], 90, 235, 6))
    pts = []
    for i in range(len(outline)):  # resample every ~0.09 m for track links
        a, c = V(outline[i]), V(outline[(i + 1) % len(outline)])
        k = max(1, int((c - a).length / 0.09))
        pts += [a.lerp(c, t / k) for t in range(k)]
    tl = [0.10 if (i // 2) % 2 == 0 else 0.065 for i in range(len(pts))]  # links/teeth on the outside
    for s in (1, -1):
        b.band([tuple(p) for p in pts], s * 1.06, s * 1.66, 0.08, TIRE, closed=True, tlist=tl)
        for y in lin(-1.95, 1.95, 6):
            b.cyl((s * 1.10, y, 0.42), (s * 1.62, y, 0.42), 0.32, DARK, seg=12)
            b.cyl((s * 1.62, y, 0.42), (s * 1.65, y, 0.42), 0.12, METAL, seg=8)
        for y in (-1.2, 0.0, 1.2):
            b.cyl((s * 1.15, y, 0.80), (s * 1.55, y, 0.80), 0.08, DARK, seg=8)  # return rollers
        b.cyl((s * 1.08, sp[0], sp[1]), (s * 1.64, sp[0], sp[1]), 0.28, DARK, seg=12)
        b.cyl((s * 1.64, sp[0], sp[1]), (s * 1.68, sp[0], sp[1]), 0.16, METAL, seg=10)
        b.cyl((s * 1.08, idl[0], idl[1]), (s * 1.64, idl[0], idl[1]), 0.25, DARK, seg=12)
        # side skirts
        sk = [(-2.95, 1.12), (2.65, 1.12), (2.65, 0.56), (-2.40, 0.56), (-2.95, 0.90)]
        b.prism(sk, "x", s * 1.67, s * 1.73, PAINT, bevel=0.012)
        for y in (-1.6, -0.5, 0.6, 1.7):
            b.box((s * 1.72, y - 0.02, 0.58), (s * 1.74, y + 0.02, 1.10), DARK)
        # headlights, tool boxes, rear lights
        b.box((s * 1.30, -3.00, 1.18), (s * 1.52, -2.85, 1.32), DARK, bevel=0.01)
        b.box((s * 1.33, -3.01, 1.21), (s * 1.49, -2.99, 1.29), LIGHT)
        b.box((s * 1.25, 1.70, 1.56), (s * 1.68, 2.70, 1.78), PAINT, bevel=0.02)
        b.box((s * 1.30, 2.95, 1.30), (s * 1.50, 2.99, 1.40), DARK)
        b.box((s * 0.60, -3.12, 0.88), (s * 0.80, -3.02, 1.00), DARK)  # tow hooks
    # engine deck grilles, driver hatch, exhaust
    for y in (1.60, 2.15):
        b.box((-0.95, y - 0.22, 1.555), (0.95, y + 0.22, 1.58), DARK)
    b.box((-0.30, -2.10, 1.40), (0.30, -1.70, 1.56), PAINT, bevel=0.02)
    b.box((-0.22, -2.13, 1.46), (0.22, -2.11, 1.52), GLASS)
    b.box((-0.80, 2.96, 0.95), (0.80, 3.00, 1.25), DARK)
    # ---- turret (pivot = turret ring centre on the hull roof) ----
    tp = V((0, 0.30, 1.56))
    t = Part()
    poly = [(-0.45, -1.20), (0.45, -1.20), (1.25, -0.80), (1.45, -0.10), (1.42, 0.95), (1.10, 1.75), (-1.10, 1.75),
            (-1.42, 0.95), (-1.45, -0.10), (-1.25, -0.80)]
    poly = [(x + tp.x, y + tp.y) for x, y in poly]
    t.prism(poly, "z", 0, 0, PAINT, pivot=(tp.x, tp.y + 0.2),
            rings=[(tp.z - 0.02, 0.90), (tp.z + 0.28, 1.0), (tp.z + 0.66, 0.88)])
    t.cyl(tp - V((0, 0, 0.04)), tp + V((0, 0, 0.05)), 0.92, DARK, seg=16)  # turret ring
    ct = tp + V((0.55, 0.45, 0.66))
    t.lathe(ct, (0, 0, 1), [(0, 0), (0, 0.34), (0.16, 0.32), (0.22, 0.26), (0.26, 0)], 12,
            [PAINT, PAINT, PAINT, PAINT])  # commander cupola
    for k in range(6):
        a = rad(60 * k + 30)
        t.box(ct + V((0.30 * math.cos(a) - 0.04, 0.30 * math.sin(a) - 0.04, 0.08)),
              ct + V((0.30 * math.cos(a) + 0.04, 0.30 * math.sin(a) + 0.04, 0.14)), GLASS)
    mg = ct + V((-0.05, -0.30, 0.30))
    t.box(mg - V((0.06, 0.15, 0.06)), mg + V((0.06, 0.15, 0.06)), DARK)
    t.cyl(mg - V((0, 0.15, 0)), mg - V((0, 0.75, 0)), 0.02, DARK, seg=6)
    t.cyl(ct + V((-0.05, -0.15, 0.20)), mg - V((0, 0, 0.05)), 0.02, DARK, seg=6)
    gs = tp + V((-0.62, -0.62, 0.66))
    t.box(gs - V((0.18, 0.20, 0.0)), gs + V((0.18, 0.20, 0.28)), PAINT, bevel=0.02)  # gunner sight
    t.box(gs + V((-0.14, -0.21, 0.06)), gs + V((0.14, -0.19, 0.22)), GLASS)
    t.box(tp + V((-0.30, 0.20, 0.66)), tp + V((0.10, 0.70, 0.71)), PAINT, bevel=0.015)  # loader hatch
    for s in (1, -1):  # smoke dischargers
        for k in range(3):
            c0 = tp + V((s * (1.18 + 0.06 * k), -0.62 + 0.13 * k, 0.40))
            d = V((s * 0.35, -1, 0.35)).normalized()
            t.cyl(c0, c0 + d * 0.22, 0.045, DARK, seg=8)
        # antennas
        t.cyl(tp + V((s * 0.90, 1.45, 0.66)), tp + V((s * 0.90, 1.45, 1.90)), 0.012, DARK, seg=5)
        t.cyl(tp + V((s * 0.90, 1.45, 0.62)), tp + V((s * 0.90, 1.45, 0.76)), 0.04, DARK, seg=6)
    # bustle stowage basket
    t.box(tp + V((-1.05, 1.70, 0.12)), tp + V((1.05, 2.12, 0.52)), DARK, bevel=0.02)
    t.box(tp + V((-1.00, 1.74, 0.50)), tp + V((1.00, 2.08, 0.58)), CANVAS, bevel=0.03)
    # ---- gun (pivot = trunnion) ----
    gp = tp + V((0, -1.05, 0.34))
    g = Part()
    g.obox(gp + V((0, -0.08, 0)), (0.82, 0.42, 0.46), PAINT, bevel=0.04)  # mantlet
    g.obox(gp + V((0, -0.32, 0)), (0.50, 0.16, 0.34), PAINT, bevel=0.03)
    L = 3.75
    tip = gp + V((0, -L, 0))
    g.cyl(gp + V((0, -0.38, 0)), gp + V((0, -1.2, 0)), 0.115, PAINT, r1=0.10, seg=12)
    g.cyl(gp + V((0, -1.2, 0)), gp + V((0, -2.15, 0)), 0.10, PAINT, seg=12)
    g.cyl(gp + V((0, -2.15, 0)), gp + V((0, -2.75, 0)), 0.15, PAINT, seg=12, bevel=0.03)  # bore evacuator
    g.cyl(gp + V((0, -2.75, 0)), gp + V((0, -L + 0.18, 0)), 0.095, PAINT, seg=12)
    g.lathe(tip + V((0, 0.18, 0)), (0, -1, 0), [(0, 0), (0, 0.12), (0.18, 0.12), (0.18, 0.06), (0.10, 0)], 12,
            [DARK, DARK, DARK, DARK])
    return dict(body=b, paint=(0.76, 0.66, 0.47),
                parts=[("Turret", t, tuple(tp), None), ("Gun", g, tuple(gp), "Turret")],
                empties=[("Muzzle", tuple(tip), "Gun", (90.0, 0.0, 0.0))])  # Unity forward = out of the barrel


VEHICLES = {"Offroad": build_offroad, "ATV": build_atv, "Moto": build_moto, "Truck": build_truck,
            "Boat": build_boat, "Heli": build_heli, "Tank": build_tank}


# ---------------------------------------------------------------------------------------------------------
# scene assembly / export

def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def make_materials(paint, canvas):
    cols = dict(COMMON)
    cols[PAINT] = paint
    if canvas:
        cols[CANVAS] = canvas
    mats = []
    for i, name in enumerate(MAT_NAMES):
        m = bpy.data.materials.new(name)
        if m.node_tree is None:  # Blender < 5 creates materials without nodes
            m.use_nodes = True
        bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        rgb = cols[i]
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.7
        bsdf.inputs["Metallic"].default_value = 0.0
        m.diffuse_color = (*(srgb_to_lin(c) for c in rgb), 1.0)  # viewport/preview shows what Unity shows
        mats.append(m)
    return mats


def make_object(name, part, mats, pivot, parent):
    bm = part.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.translate(bm, vec=-V(pivot), verts=bm.verts[:])
    used = sorted({f.material_index for f in bm.faces})
    remap = {m: i for i, m in enumerate(used)}
    for f in bm.faces:
        f.material_index = remap[f.material_index]
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in used:
        me.materials.append(mats[m])
    me.validate()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def build(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    spec = VEHICLES[name]()
    mats = make_materials(spec["paint"], spec.get("canvas"))
    root = bpy.data.objects.new("VehicleRoot", None)
    root.empty_display_size = 0.5
    bpy.context.scene.collection.objects.link(root)
    objs = {"VehicleRoot": (root, V((0, 0, 0)))}
    body = make_object("Body", spec["body"], mats, (0, 0, 0), root)
    body.parent = root
    objs["Body"] = (body, V((0, 0, 0)))
    for pname, part, pivot, parent in spec.get("parts", []):
        ob = make_object(pname, part, mats, pivot, None)
        par, ppiv = objs[parent or "VehicleRoot"]
        ob.parent = par
        ob.location = V(pivot) - ppiv
        objs[pname] = (ob, V(pivot))
    for ename, pos, parent, erot in spec.get("empties", []):
        e = bpy.data.objects.new(ename, None)
        e.rotation_euler = [rad(a) for a in erot]
        e.empty_display_size = 0.2
        bpy.context.scene.collection.objects.link(e)
        par, ppiv = objs[parent]
        e.parent = par
        e.location = V(pos) - ppiv
        objs[ename] = (e, V(pos))
    bpy.context.view_layer.update()
    return root, [o for o, _ in objs.values()]


def stats(objs):
    tris = 0
    mn, mx = V((1e9,) * 3), V((-1e9,) * 3)
    for o in objs:
        if o.type != "MESH":
            continue
        tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            mn = V([min(a, c) for a, c in zip(mn, w)])
            mx = V([max(a, c) for a, c in zip(mx, w)])
    return tris, mn, mx


def export(path, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"EMPTY", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", path_mode="COPY", embed_textures=True,
                             mesh_smooth_type="FACE", bake_anim=False)


def render_preview(path, objs, mn, mx, view=(1.0, -1.35, 0.58)):
    sc = bpy.context.scene
    # ground
    size = (mx - mn).length
    bpy.ops.mesh.primitive_plane_add(size=size * 6, location=((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
    gnd = bpy.context.active_object
    gm = bpy.data.materials.new("PreviewGround")
    gm.diffuse_color = (1.0, 1.0, 1.0, 1)
    gnd.data.materials.append(gm)
    cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
    sc.collection.objects.link(cam)
    cam.data.lens = 50
    ctr = (mn + mx) / 2
    d = V(view).normalized()
    f = -d
    r = f.cross(V((0, 0, 1))).normalized()
    u = r.cross(f)
    tx = cam.data.sensor_width / 2 / cam.data.lens * 0.90
    ty = tx * 500 / 800
    dist = 0.0
    for o in objs:  # fit the actual vertices into the frame
        if o.type == "MESH":
            for v in o.data.vertices:
                p = o.matrix_world @ v.co - ctr
                dist = max(dist, abs(p.dot(r)) / tx - p.dot(f), abs(p.dot(u)) / ty - p.dot(f))
    cam.location = ctr + d * dist
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x, sc.render.resolution_y = 800, 500
    sc.render.resolution_percentage = 100
    sc.display.render_aa = "16"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_shadows = True
    sh.shadow_intensity = 0.35
    sh.show_cavity = True
    sh.cavity_type = "WORLD"
    sh.show_object_outline = True
    sh.object_outline_color = (0.05, 0.05, 0.05)
    sc.display.light_direction = (0.35, -0.4, 0.85)
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("PreviewWorld")
    w.color = (0.72, 0.75, 0.78)
    sc.world = w
    sc.render.film_transparent = False
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def make_sheet(paths, out):
    from PIL import Image, ImageDraw, ImageFont
    try:
        font = ImageFont.load_default(size=30)
    except TypeError:  # Pillow < 10.1
        font = ImageFont.load_default()
    ims = [Image.open(p) for p in paths]
    cols = 3
    rows = (len(ims) + cols - 1) // cols
    w, h = ims[0].size
    sheet = Image.new("RGB", (cols * w, rows * h), (190, 196, 202))
    dr = ImageDraw.Draw(sheet)
    for i, (im, p) in enumerate(zip(ims, paths)):
        x, y = (i % cols) * w, (i // cols) * h
        sheet.paste(im.convert("RGB"), (x, y))
        dr.text((x + 14, y + 10), os.path.splitext(os.path.basename(p))[0], fill=(20, 20, 20), font=font)
    sheet.save(out)


def verify(names):
    """Re-import each FBX and check names, hierarchy, pivots and the forward axis."""
    ok = True
    for name in names:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=os.path.join(OUT_DIR, name + ".fbx"))
        obs = {o.name: o for o in bpy.context.scene.objects}
        print("\n== %s ==" % name)
        for o in sorted(obs.values(), key=lambda o: (len(o.children_recursive) == 0, o.name)):
            w = o.matrix_world.translation
            print("  %-12s %-6s parent=%-12s world=(%.3f, %.3f, %.3f) rot=%s" % (
                o.name, o.type, o.parent.name if o.parent else "-", w.x, w.y, w.z,
                tuple(round(math.degrees(a), 1) for a in o.matrix_world.to_euler())))
        expect = {"VehicleRoot", "Body"}
        expect |= {"Offroad": {"Wheel_%d" % i for i in range(4)}, "ATV": {"Wheel_%d" % i for i in range(4)},
                   "Moto": {"Wheel_0", "Wheel_1", "Handlebar"}, "Truck": {"Wheel_%d" % i for i in range(6)},
                   "Boat": {"Prop"}, "Heli": {"Rotor", "TailRotor"}, "Tank": {"Turret", "Gun", "Muzzle"}}[name]
        missing = expect - set(obs)
        if missing:
            ok = False
            print("  MISSING", missing)
        for n in expect - {"VehicleRoot"} - missing:
            want = {"Gun": "Turret", "Muzzle": "Gun"}.get(n, "VehicleRoot")
            if obs[n].parent is None or obs[n].parent.name != want:
                ok = False
                print("  BAD PARENT", n)
        for n, o in obs.items():
            if n.startswith("Wheel_") or n in ("Rotor", "Prop"):
                vs = [v.co for v in o.data.vertices]
                c = V([(min(v[i] for v in vs) + max(v[i] for v in vs)) / 2 for i in range(3)])
                if n.startswith("Wheel_") and c.length > 0.01:
                    ok = False
                    print("  PIVOT OFF", n, c)
                print("  %s local bbox centre (%.3f, %.3f, %.3f)" % (n, c.x, c.y, c.z))
        # forward: lamps / glass / muzzle must be on the -Y side
        fr = []
        for o in obs.values():
            if o.type == "MESH":
                for p in o.data.polygons:
                    if o.data.materials[p.material_index].name.split(".")[0] in ("Light", "Glass"):
                        fr.append((o.matrix_world @ p.center).y)
        if "Muzzle" in obs:
            fr = [obs["Muzzle"].matrix_world.translation.y]
        fy = sum(fr) / len(fr)
        print("  front features mean Y = %.2f (%s)" % (fy, "front is -Y: OK" if fy < 0 else "WRONG"))
        ok &= fy < 0
    print("\nVERIFY", "OK" if ok else "FAILED")
    return ok


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    only = list(VEHICLES)
    preview = None
    if "--only" in argv:
        only = argv[argv.index("--only") + 1].split(",")
    if "--preview" in argv:
        preview = argv[argv.index("--preview") + 1]
    if "--verify" in argv:
        sys.exit(0 if verify(only) else 1)
    os.makedirs(OUT_DIR, exist_ok=True)
    pngs = []
    rows = []
    for name in only:
        root, objs = build(name)
        tris, mn, mx = stats(objs)
        export(os.path.join(OUT_DIR, name + ".fbx"), objs)
        size = mx - mn
        rows.append((name, tris, size, mn, mx))
        print("%-8s tris %5d (budget %d)  L %.2f  W %.2f  H %.2f m   z %.2f..%.2f" % (
            name, tris, TRI_BUDGET[name], size.y, size.x, size.z, mn.z, mx.z))
        if preview:
            os.makedirs(preview, exist_ok=True)
            p = os.path.join(preview, name + ".png")
            render_preview(p, objs, mn, mx)
            pngs.append(p)
    if preview and len(pngs) > 1:
        make_sheet(pngs, os.path.join(preview, "vehicles_sheet.png"))
    print("\n%-8s %6s %7s %7s %7s" % ("vehicle", "tris", "L", "W", "H"))
    for name, tris, size, mn, mx in rows:
        flag = "" if tris <= TRI_BUDGET[name] else "  OVER BUDGET"
        print("%-8s %6d %7.2f %7.2f %7.2f%s" % (name, tris, size.y, size.x, size.z, flag))


if __name__ == "__main__":
    main()
