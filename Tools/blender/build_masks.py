"""Procedural low-poly animal masks for Zootopia Mobile -> Assets/Resources/Models/Masks/<id>.fbx
plus their inventory icons -> Assets/Resources/UI/Icons/gear_<id>.png

    python3 build_masks.py                  # all masks + icons
    python3 build_masks.py --only k_cat     # some

Same axes as the other game models (see build_vehicles.py): in Blender FRONT = -Y, UP = +Z, metres, exported with
FBX_SCALE_ALL so the face ends up on Unity +Z. Origin = centre of the wearer's head. The shell is sized for a
head about 0.21 m wide; the game scales it to the character. One mesh ("Mask") with base-colour materials
(Main, Accent, Dark, White, Pink, Extra) that the game turns into its shared lit materials. Flat shaded.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector as V, Matrix, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Models", "Masks")
ICON_DIR = os.path.join(ROOT, "Assets", "Resources", "UI", "Icons")

MAIN, ACCENT, DARK, WHITE, PINK, EXTRA = range(6)
MAT_NAMES = ["Main", "Accent", "Dark", "White", "Pink", "Extra"]
RX, RY, RZ = 0.105, 0.12, 0.125      # the head shell
rad = math.radians


def hexc(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def front(x, z, rx=RX, ry=RY, rz=RZ):
    """Y of the head shell's front surface at (x, z)."""
    k = 1 - (x / rx) ** 2 - (z / rz) ** 2
    return -ry * math.sqrt(max(k, 0.0))


class Mask:
    def __init__(self):
        self.bm = bmesh.new()

    def _place(self, verts, scale, rot, loc, mat):
        m = Matrix.Translation(V(loc)) @ rot.to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            f.material_index = mat

    def ell(self, c, r, mat, seg=12, ring=8, rot=(0, 0, 0)):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=ring, radius=1.0)
        self._place(res["verts"], r, Euler([rad(a) for a in rot]), c, mat)

    def cone(self, base, tip, r1, r2, mat, seg=8, flat=1.0):
        """Cone/frustum from base to tip; flat < 1 squashes it sideways (a blade, a fin)."""
        base, tip = V(base), V(tip)
        d = tip - base
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2,
                                    depth=d.length)
        q = V((0, 0, 1)).rotation_difference(d.normalized())
        self._place(res["verts"], (1.0, flat, 1.0), q.to_euler(), (base + tip) / 2, mat)

    def box(self, c, size, mat, rot=(0, 0, 0)):
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        self._place(res["verts"], size, Euler([rad(a) for a in rot]), c, mat)

    def decal(self, x, z, rw, rh, th, mat, lift=0.0, roll=0.0, seg=12):
        """A flattened ellipsoid lying on the head shell at (x, z) (front side), turned to the shell's normal:
        rw across, rh up along the surface, th thick; roll turns it in the surface plane (degrees)."""
        y = front(x, z)
        nrm = V((x / RX ** 2, y / RY ** 2, z / RZ ** 2)).normalized()
        q = V((0, -1, 0)).rotation_difference(nrm)
        q = q @ Euler((0, rad(roll), 0)).to_quaternion()
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=6, radius=1.0)
        self._place(res["verts"], (rw, th, rh), q.to_euler(), V((x, y, z)) + nrm * lift, mat)

    def mirror(self, fn, *args, **kw):
        """Calls fn for the right side (x > 0 values as given) and again mirrored to the left."""
        fn(*args, **kw)
        flip = lambda p: (-p[0], p[1], p[2])
        a = list(args)
        for i, v in enumerate(a):
            if isinstance(v, (tuple, list)) and len(v) == 3 and i < 2:
                a[i] = flip(v)
        kw = dict(kw)
        if "rot" in kw:
            r = kw["rot"]
            kw["rot"] = (r[0], -r[1], -r[2])
        fn(*a, **kw)

    def head(self, mat=MAIN, rx=RX, ry=RY, rz=RZ):
        self.ell((0, 0, 0), (rx, ry, rz), mat, seg=14, ring=10)

    def eyes(self, x=0.045, z=0.025, r=(0.022, 0.012, 0.02), mat=DARK, shine=True, iris=None):
        y = front(x, z) + 0.004
        if iris is not None:
            self.mirror(self.ell, (x, y, z), (r[0] * 1.35, r[1], r[2] * 1.35), iris, seg=10, ring=6)
            y -= 0.006
        self.mirror(self.ell, (x, y, z), r, mat, seg=10, ring=6)
        if shine:
            self.mirror(self.ell, (x + r[0] * 0.35, y - r[1] * 0.8, z + r[2] * 0.4), (0.006, 0.004, 0.006), WHITE, seg=6, ring=4)


# ----- the animals -----

def cat(m):
    m.head()
    for s in (1, -1):
        m.cone((s * 0.06, 0.0, 0.085), (s * 0.095, 0.015, 0.2), 0.05, 0.004, MAIN, seg=4)
        m.cone((s * 0.06, -0.012, 0.095), (s * 0.088, 0.003, 0.18), 0.028, 0.003, PINK, seg=4)
    for i, x in enumerate((-0.025, 0.0, 0.025)):
        m.box((x, front(x, 0.09) + 0.01, 0.09 + (0.008 if i == 1 else 0)), (0.012, 0.02, 0.05), EXTRA, rot=(-35, 0, 0))
    m.eyes(iris=EXTRA if False else None)
    m.ell((0.024, front(0, -0.04) - 0.01, -0.045), (0.034, 0.028, 0.026), ACCENT)
    m.ell((-0.024, front(0, -0.04) - 0.01, -0.045), (0.034, 0.028, 0.026), ACCENT)
    m.ell((0, front(0, -0.02) - 0.025, -0.022), (0.016, 0.011, 0.011), PINK, seg=8, ring=6)
    for s in (1, -1):
        for k, a in enumerate((-8, 6)):
            m.box((s * 0.08, front(0.05, -0.04) - 0.006, -0.042 + k * 0.012), (0.075, 0.003, 0.003), DARK, rot=(0, s * a, s * 8))


def dog(m):
    m.head()
    for s in (1, -1):
        m.ell((s * 0.112, 0.01, 0.0), (0.028, 0.05, 0.095), ACCENT if False else EXTRA, rot=(0, s * 18, 0))
    m.ell((0.045, front(0.045, 0.03) + 0.006, 0.035), (0.04, 0.012, 0.038), EXTRA)   # eye patch
    m.eyes()
    m.ell((0, -0.13, -0.035), (0.058, 0.075, 0.046), ACCENT)
    m.ell((0, -0.2, -0.012), (0.026, 0.018, 0.019), DARK, seg=10, ring=6)
    m.ell((0.0, -0.165, -0.078), (0.022, 0.03, 0.01), PINK, seg=8, ring=6)


def rabbit(m):
    m.head()
    for s in (1, -1):
        m.ell((s * 0.045, 0.02, 0.21), (0.032, 0.02, 0.115), MAIN, rot=(-6, s * 10, 0))
        m.ell((s * 0.046, 0.003, 0.21), (0.019, 0.008, 0.088), PINK, rot=(-6, s * 10, 0))
    m.eyes(x=0.05, z=0.03)
    m.ell((0.022, front(0, -0.04) - 0.006, -0.045), (0.03, 0.025, 0.026), WHITE)
    m.ell((-0.022, front(0, -0.04) - 0.006, -0.045), (0.03, 0.025, 0.026), WHITE)
    m.ell((0, front(0, -0.02) - 0.02, -0.024), (0.014, 0.01, 0.01), PINK, seg=8, ring=6)
    m.box((0.0065, front(0, -0.07) - 0.012, -0.075), (0.012, 0.006, 0.02), WHITE)
    m.box((-0.0065, front(0, -0.07) - 0.012, -0.075), (0.012, 0.006, 0.02), WHITE)
    for s in (1, -1):
        m.ell((s * 0.07, front(0.07, -0.02) + 0.01, -0.02), (0.022, 0.006, 0.014), PINK, seg=8, ring=6)


def bear(m):
    m.head(rx=0.11, rz=0.122)
    for s in (1, -1):
        m.ell((s * 0.085, 0.01, 0.1), (0.042, 0.022, 0.042), MAIN)
        m.ell((s * 0.085, -0.004, 0.1), (0.024, 0.01, 0.024), ACCENT)
    m.eyes(x=0.042, z=0.03, r=(0.018, 0.01, 0.018))
    m.ell((0, -0.112, -0.04), (0.058, 0.055, 0.045), ACCENT)
    m.ell((0, -0.165, -0.022), (0.026, 0.016, 0.017), DARK, seg=10, ring=6)
    m.box((0, -0.162, -0.05), (0.004, 0.006, 0.03), DARK)


def raccoon(m):
    m.head()
    for s in (1, -1):
        m.cone((s * 0.065, 0.005, 0.085), (s * 0.09, 0.015, 0.17), 0.045, 0.006, MAIN, seg=5)
        m.cone((s * 0.065, -0.008, 0.095), (s * 0.085, 0.004, 0.155), 0.025, 0.004, DARK, seg=5)
        m.ell((s * 0.045, front(0.045, 0.06) + 0.004, 0.065), (0.035, 0.01, 0.012), WHITE, seg=8, ring=6)
    for s in (1, -1):   # the bandit mask
        m.decal(s * 0.05, 0.02, 0.05, 0.03, 0.006, ACCENT, lift=0.0, roll=s * 12)
    m.decal(0.0, 0.03, 0.022, 0.012, 0.008, ACCENT, lift=0.001)
    m.eyes(x=0.045, z=0.022, r=(0.016, 0.01, 0.016), mat=DARK, iris=EXTRA)
    m.ell((0, -0.122, -0.04), (0.045, 0.05, 0.035), WHITE)
    m.ell((0, -0.168, -0.028), (0.018, 0.012, 0.013), DARK, seg=8, ring=6)
    for k in range(3):
        m.box((0, 0.02 + k * 0.04, front(0, 0) * 0 + 0.118 - k * 0.012), (0.05, 0.016, 0.02), ACCENT, rot=(-10 - k * 15, 0, 0))


def owl(m):
    m.head(rx=0.112, rz=0.12)
    m.ell((0, -0.085, 0.0), (0.098, 0.05, 0.088), WHITE, seg=16, ring=8)   # facial disc
    for s in (1, -1):
        m.ell((s * 0.044, front(0.044, 0.02) - 0.006, 0.02), (0.042, 0.014, 0.042), ACCENT, seg=14, ring=6)
        m.ell((s * 0.044, front(0.044, 0.02) - 0.016, 0.02), (0.02, 0.008, 0.02), DARK, seg=10, ring=6)
        m.ell((s * 0.044 + 0.007, front(0.044, 0.02) - 0.022, 0.028), (0.006, 0.003, 0.006), WHITE, seg=6, ring=4)
        m.cone((s * 0.07, 0.0, 0.09), (s * 0.115, 0.03, 0.175), 0.035, 0.003, MAIN, seg=4, flat=0.5)
    m.cone((0, front(0, -0.02) - 0.03, -0.005), (0, front(0, -0.02) - 0.04, -0.065), 0.02, 0.002, EXTRA, seg=5)
    for k in range(4):   # feather tips on the forehead
        x = -0.045 + k * 0.03
        m.cone((x, front(x, 0.08) + 0.008, 0.09), (x, front(x, 0.08) - 0.002, 0.06), 0.014, 0.002, ACCENT, seg=4, flat=0.4)


def penguin(m):
    m.head()
    m.ell((0, -0.06, -0.04), (0.092, 0.07, 0.075), WHITE, seg=16, ring=8)
    for s in (1, -1):
        m.decal(s * 0.042, 0.035, 0.026, 0.026, 0.004, WHITE, lift=0.0)
    m.eyes(x=0.042, z=0.035, r=(0.015, 0.012, 0.016))
    m.cone((0, front(0, -0.02) - 0.03, -0.015), (0, front(0, -0.02) - 0.085, -0.03), 0.028, 0.004, EXTRA, seg=6, flat=0.55)
    for s in (1, -1):
        m.decal(s * 0.062, -0.035, 0.016, 0.01, 0.005, PINK, lift=0.02)


def wolf(m):
    m.head()
    for s in (1, -1):
        m.cone((s * 0.06, 0.01, 0.085), (s * 0.085, 0.03, 0.215), 0.05, 0.004, MAIN, seg=4)
        m.cone((s * 0.06, -0.008, 0.095), (s * 0.08, 0.016, 0.19), 0.028, 0.003, ACCENT, seg=4)
        m.cone((s * 0.1, 0.0, -0.04), (s * 0.14, 0.02, -0.07), 0.03, 0.004, ACCENT, seg=4, flat=0.5)   # cheek fur
        m.box((s * 0.04, front(0.04, 0.05) - 0.002, 0.052), (0.05, 0.012, 0.01), DARK, rot=(0, 0, s * -14))   # brow
    m.eyes(x=0.042, z=0.028, r=(0.016, 0.01, 0.013), iris=EXTRA)
    m.ell((0, -0.15, -0.045), (0.05, 0.09, 0.04), ACCENT)
    m.ell((0, -0.125, -0.022), (0.04, 0.07, 0.028), MAIN)
    m.ell((0, -0.235, -0.032), (0.022, 0.016, 0.015), DARK, seg=10, ring=6)
    for s in (1, -1):
        m.cone((s * 0.022, -0.2, -0.07), (s * 0.022, -0.2, -0.09), 0.006, 0.0, WHITE, seg=4)


def lion(m):
    n = 16
    for i in range(n):   # mane: two rings of tufts around the face
        a = 2 * math.pi * i / n
        d = V((math.cos(a), 0, math.sin(a)))
        m.cone(V((0, 0.02, 0)) + d * 0.08, V((0, 0.05, 0)) + d * 0.2, 0.06, 0.008, ACCENT, seg=5)
        a2 = a + math.pi / n
        d2 = V((math.cos(a2), 0, math.sin(a2)))
        m.cone(V((0, 0.06, 0)) + d2 * 0.07, V((0, 0.1, 0)) + d2 * 0.17, 0.055, 0.008, EXTRA, seg=5)
    m.head()
    for s in (1, -1):
        m.ell((s * 0.08, -0.01, 0.1), (0.032, 0.018, 0.03), MAIN)
        m.ell((s * 0.08, -0.022, 0.1), (0.018, 0.008, 0.016), DARK)
    m.eyes(x=0.042, z=0.03, r=(0.016, 0.01, 0.015), iris=WHITE)
    m.ell((0, -0.112, -0.045), (0.062, 0.05, 0.045), WHITE)
    m.ell((0, -0.135, -0.005), (0.03, 0.04, 0.03), MAIN)
    m.cone((0, -0.165, -0.008), (0, -0.168, -0.035), 0.022, 0.006, DARK, seg=3)


def dragon(m):
    m.head(rz=0.12)
    m.ell((0, -0.15, -0.035), (0.062, 0.1, 0.045), MAIN, seg=10, ring=6)   # snout
    m.ell((0, -0.14, -0.07), (0.05, 0.085, 0.022), ACCENT, seg=10, ring=6)  # jaw plate
    for s in (1, -1):
        m.ell((s * 0.022, -0.242, -0.02), (0.009, 0.006, 0.006), DARK, seg=6, ring=4)   # nostrils
        m.box((s * 0.045, front(0.045, 0.06) - 0.006, 0.06), (0.06, 0.02, 0.014), MAIN, rot=(0, 0, s * -18))   # brow
        # horns: two segments sweeping back
        m.cone((s * 0.06, 0.02, 0.095), (s * 0.1, 0.1, 0.18), 0.03, 0.016, EXTRA, seg=6)
        m.cone((s * 0.1, 0.1, 0.18), (s * 0.11, 0.2, 0.2), 0.016, 0.001, EXTRA, seg=6)
        m.cone((s * 0.1, -0.02, -0.03), (s * 0.16, 0.03, -0.01), 0.022, 0.002, ACCENT, seg=4, flat=0.4)   # cheek fins
        for k in range(3):
            m.cone((s * 0.03, -0.13 - k * 0.035, -0.078), (s * 0.03, -0.13 - k * 0.035, -0.098), 0.006, 0.0, WHITE, seg=4)
    m.eyes(x=0.045, z=0.03, r=(0.016, 0.01, 0.012), iris=EXTRA)
    for k in range(5):   # crest
        t = k / 4
        y = -0.02 + t * 0.15
        z = 0.125 - t * t * 0.12
        h = 0.07 - t * 0.03
        m.cone((0, y, z - 0.01), (0, y + 0.03, z + h), 0.03, 0.002, ACCENT, seg=4, flat=0.25)


MASKS = {
    #           maker    main       accent     dark       white      pink       extra
    "k_cat": (cat, "#f29a40", "#fbecd8", "#1c1a1f", "#ffffff", "#ff9aa8", "#c86a1e"),
    "k_dog": (dog, "#8c6138", "#ecd9b8", "#1a1614", "#ffffff", "#ff7a8a", "#5a3a20"),
    "k_rabbit": (rabbit, "#eceef3", "#ffb3c6", "#1c1c22", "#ffffff", "#ffa6ba", "#d0d4dc"),
    "k_bear": (bear, "#5c3d28", "#b48c66", "#16110e", "#ffffff", "#ff8a8a", "#3a2618"),
    "k_raccoon": (raccoon, "#8c8f95", "#222327", "#141416", "#f2f2f2", "#ff9aa8", "#f0c040"),
    "k_owl": (owl, "#8c6a46", "#ffcc33", "#141210", "#f4ead8", "#ff9aa8", "#f08a20"),
    "k_penguin": (penguin, "#1c2029", "#f8f8f8", "#0c0d10", "#f8f8f8", "#ff9aa8", "#ffa21f"),
    "k_wolf": (wolf, "#737a8c", "#dadde5", "#15161a", "#ffffff", "#ff9aa8", "#ffc61a"),
    "k_lion": (lion, "#e8ad4c", "#9e521f", "#1e1410", "#fbeed2", "#ff9aa8", "#c2702a"),
    "k_dragon": (dragon, "#c21f1f", "#7a0e12", "#120808", "#fff6e0", "#ff9aa8", "#ffc83a"),
}


def make_materials(cols):
    mats = []
    for name, hx in zip(MAT_NAMES, cols):
        rgb = hexc(hx)
        m = bpy.data.materials.new(name)
        if m.node_tree is None:
            m.use_nodes = True
        bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.75
        m.diffuse_color = (*(srgb_to_lin(c) for c in rgb), 1.0)
        mats.append(m)
    return mats


def build(mid):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    maker, *cols = MASKS[mid]
    mats = make_materials(cols)
    m = Mask()
    maker(m)
    bm = m.bm
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    used = sorted({f.material_index for f in bm.faces})
    remap = {k: i for i, k in enumerate(used)}
    for f in bm.faces:
        f.material_index = remap[f.material_index]
    me = bpy.data.meshes.new("Mask")
    bm.to_mesh(me)
    bm.free()
    for k in used:
        me.materials.append(mats[k])
    me.validate()
    ob = bpy.data.objects.new("Mask", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def export(path, ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE", bake_anim=False)


def render_icon(path, ob, size=256):
    sc = bpy.context.scene
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    view = V((-0.55, -1.0, 0.28)).normalized()   # from the front, a little to the wearer's right, above
    f = -view
    r = f.cross(V((0, 0, 1))).normalized()
    u = r.cross(f)
    pts = [ob.matrix_world @ v.co for v in ob.data.vertices]
    xs = [p.dot(r) for p in pts]
    ys = [p.dot(u) for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    span = max(max(xs) - min(xs), max(ys) - min(ys))
    cam.data.ortho_scale = span * 1.12
    cam.location = r * cx + u * cy + view * 2.0
    cam.rotation_euler = f.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x = sc.render.resolution_y = size
    sc.render.resolution_percentage = 100
    sc.display.render_aa = "16"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_shadows = False
    sh.show_cavity = True
    sh.cavity_type = "BOTH"
    sh.cavity_ridge_factor = 1.2
    sh.cavity_valley_factor = 1.0
    sh.show_object_outline = True
    sh.object_outline_color = (0.03, 0.035, 0.05)
    sc.view_settings.view_transform = "Standard"
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    only = None
    if "--only" in argv:
        only = argv[argv.index("--only") + 1].split(",")
    os.makedirs(OUT_DIR, exist_ok=True)
    done = []
    for mid in MASKS:
        if only and mid not in only:
            continue
        ob = build(mid)
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        export(os.path.join(OUT_DIR, mid + ".fbx"), ob)
        icon = os.path.join(ICON_DIR, "gear_" + mid + ".png")
        render_icon(icon, ob)
        done.append(icon)
        print("mask", mid, "tris", tris)
    return done


if __name__ == "__main__":
    main()
