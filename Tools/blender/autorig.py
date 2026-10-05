"""Rigs a static (unrigged) humanoid GLB in T- or A-pose with a Mixamo-named skeleton, so convert_character.py can
retarget the game's clips onto it.

python3 autorig.py -- in.glb out.glb [drop,names*]

The character must stand on the ground facing -Y (glTF front), arms out to the sides. Joints are placed from the
mesh (fingertips, leg and foot sections) and body proportions; skin weights come from Blender's bone heat, and any
part it leaves unweighted (loose straps, pouches) follows its nearest bone.
"""
import bpy, sys, os, math, fnmatch
import numpy as np
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = args[0], args[1]
DROP = [d for d in (args[2].split(",") if len(args) > 2 else []) if d] + ["Icosphere*"]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
sc = bpy.context.scene
for o in list(sc.objects):
    if o.type == "MESH" and any(fnmatch.fnmatchcase(o.name, d) for d in DROP):
        bpy.data.objects.remove(o, do_unlink=True)
meshes = [o for o in sc.objects if o.type == "MESH"]
for o in meshes:
    mw = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = mw
for o in list(sc.objects):
    if o.type != "MESH":
        bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.make_single_user(object=True, obdata=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "Body"

# normalise: 1.8 m tall, feet at z = 0, centred on x / y
V = np.array([v.co[:] for v in body.data.vertices])
zmin, zmax = V[:, 2].min(), V[:, 2].max()
k = 1.8 / (zmax - zmin)
cx, cy = (V[:, 0].min() + V[:, 0].max()) / 2, np.median(V[:, 1])
body.data.transform(Matrix.Scale(k, 4) @ Matrix.Translation((-cx, -cy, -zmin)))
V = np.array([v.co[:] for v in body.data.vertices])
H = 1.8


def band(z0, z1, side=0, xmin=None):
    m = (V[:, 2] >= z0) & (V[:, 2] <= z1)
    if side:
        m &= (V[:, 0] * side > (xmin if xmin is not None else 0.0))
    return V[m]


def centre(pts):
    return Vector(((pts[:, 0].min() + pts[:, 0].max()) / 2, np.median(pts[:, 1]), (pts[:, 2].min() + pts[:, 2].max()) / 2))


torso_y = np.median(band(0.5 * H, 0.75 * H)[:, 1]) if len(band(0.5 * H, 0.75 * H)) else 0.0
J = {}
J["Hips"] = Vector((0, torso_y, 0.53 * H))
J["Spine"] = Vector((0, torso_y, 0.60 * H))
J["Spine1"] = Vector((0, torso_y, 0.67 * H))
J["Spine2"] = Vector((0, torso_y, 0.74 * H))
head_pts = band(0.86 * H, H)
hy = np.median(head_pts[:, 1]) if len(head_pts) else torso_y
J["Neck"] = Vector((0, (torso_y + hy) / 2, 0.835 * H))
J["Head"] = Vector((0, hy, 0.875 * H))
J["HeadTop_End"] = Vector((0, hy, H))

for side, S in ((1, "Left"), (-1, "Right")):           # the character's left is +X (it faces -Y)
    # arm: from the shoulder joint to the fingertip (furthest point to that side)
    tip_i = np.argmax(V[:, 0] * side)
    tip = Vector(V[tip_i])
    sh = Vector((side * 0.1 * H, torso_y, 0.815 * H))
    hand_len = 0.105 * H
    d = (tip - sh).normalized()
    wrist = tip - d * hand_len
    # the wrist sits in the middle of the arm's cross-section there
    near = V[np.linalg.norm(V - np.array(wrist[:]), axis=1) < 0.05 * H]
    if len(near):
        wrist = Vector(near.mean(axis=0))
    elbow = sh.lerp(wrist, 0.5) + Vector((0, 0.012 * H, 0))
    J[S + "Shoulder"] = Vector((side * 0.025 * H, torso_y, 0.80 * H))
    J[S + "Arm"] = sh
    J[S + "ForeArm"] = elbow
    J[S + "Hand"] = wrist
    J[S + "HandIndex1"] = wrist + d * (hand_len * 0.55)
    J[S + "HandIndex1_End"] = tip
    # leg: section centres at the knee and the ankle on that side
    kp = band(0.26 * H, 0.30 * H, side, 0.01)
    ap = band(0.04 * H, 0.07 * H, side, 0.01)
    knee = centre(kp) if len(kp) else Vector((side * 0.1 * H, torso_y, 0.28 * H))
    ankle = centre(ap) if len(ap) else Vector((side * 0.1 * H, torso_y, 0.05 * H))
    knee.z, ankle.z = 0.28 * H, 0.05 * H
    knee.y -= 0.01 * H                                   # knees bend forward
    fp = band(0.0, 0.03 * H, side, 0.01)
    toe_y = fp[:, 1].min() if len(fp) else ankle.y - 0.12 * H
    J[S + "UpLeg"] = Vector((side * 0.052 * H, torso_y, 0.50 * H))
    J[S + "Leg"] = knee
    J[S + "Foot"] = ankle
    J[S + "ToeBase"] = Vector((ankle.x, ankle.y + 0.62 * (toe_y - ankle.y), 0.015 * H))
    J[S + "Toe_End"] = Vector((ankle.x, toe_y, 0.015 * H))

CHAIN = [("Hips", None, "Spine"), ("Spine", "Hips", "Spine1"), ("Spine1", "Spine", "Spine2"), ("Spine2", "Spine1", "Neck"),
         ("Neck", "Spine2", "Head"), ("Head", "Neck", "HeadTop_End")]
for S in ("Left", "Right"):
    CHAIN += [(S + "Shoulder", "Spine2", S + "Arm"), (S + "Arm", S + "Shoulder", S + "ForeArm"),
              (S + "ForeArm", S + "Arm", S + "Hand"), (S + "Hand", S + "ForeArm", S + "HandIndex1"),
              (S + "HandIndex1", S + "Hand", S + "HandIndex1_End"),
              (S + "UpLeg", "Hips", S + "Leg"), (S + "Leg", S + "UpLeg", S + "Foot"), (S + "Foot", S + "Leg", S + "ToeBase"),
              (S + "ToeBase", S + "Foot", S + "Toe_End")]

ad = bpy.data.armatures.new("Armature")
arm = bpy.data.objects.new("Armature", ad)
sc.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
eb = {}
for name, parent, tail in CHAIN:
    b = ad.edit_bones.new("mixamorig:" + name)
    b.head = J[name]
    b.tail = J[tail]
    if (b.tail - b.head).length < 1e-4:
        b.tail = b.head + Vector((0, 0, 0.02))
    if parent:
        b.parent = eb[parent]
        b.use_connect = False
    eb[name] = b
# a sensible roll for the limbs (Z of each bone towards the character's front)
for b in ad.edit_bones:
    b.align_roll(Vector((0, -1, 0)) if abs((b.tail - b.head).normalized().y) < 0.9 else Vector((0, 0, 1)))
bpy.ops.object.mode_set(mode="OBJECT")

# skin: bone heat on a watertight voxel copy of the body (clothes, pouches and straps are separate shells, which bone
# heat cannot solve directly), weights copied over to the real mesh, then the nearest bone for anything left
proxy = body.copy()
proxy.data = body.data.copy()
sc.collection.objects.link(proxy)
rm = proxy.modifiers.new("Remesh", "REMESH")
rm.mode = "VOXEL"
rm.voxel_size = 0.012
bpy.ops.object.select_all(action="DESELECT")
proxy.select_set(True)
bpy.context.view_layer.objects.active = proxy
bpy.ops.object.modifier_apply(modifier="Remesh")
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
print("proxy", len(proxy.data.vertices), "vertices, weighted", sum(1 for v in proxy.data.vertices if v.groups))
for g in proxy.vertex_groups:
    body.vertex_groups.new(name=g.name)
dt = body.modifiers.new("Weights", "DATA_TRANSFER")
dt.object = proxy
dt.use_vert_data = True
dt.data_types_verts = {"VGROUP_WEIGHTS"}
dt.vert_mapping = "POLYINTERP_NEAREST"
dt.layers_vgroup_select_src = "ALL"
dt.layers_vgroup_select_dst = "NAME"
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.modifier_apply(modifier="Weights")
bpy.data.objects.remove(proxy, do_unlink=True)
body.parent = arm
mod = body.modifiers.new("Armature", "ARMATURE")
mod.object = arm
groups = {g.index: g.name for g in body.vertex_groups}
segs = [(b.name, Vector(b.head_local), Vector(b.tail_local)) for b in ad.bones if not b.name.endswith("_End")]


def nearest_bone(p):
    best, bd = None, 1e9
    for n, a, b in segs:
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
        dd = (a + ab * t - p).length
        if dd < bd:
            best, bd = n, dd
    return best


fixed = 0
for v in body.data.vertices:
    w = sum(g.weight for g in v.groups)
    if w < 1e-3:
        n = nearest_bone(v.co)
        vg = body.vertex_groups.get(n) or body.vertex_groups.new(name=n)
        vg.add([v.index], 1.0, "REPLACE")
        fixed += 1
print("autorig: %d vertices, %d weighted by nearest bone" % (len(body.data.vertices), fixed))

bpy.ops.export_scene.gltf(filepath=os.path.abspath(OUT), export_format="GLB", export_animations=False, export_skins=True,
                          export_morph=False, export_image_format="AUTO")
print("written", OUT)
