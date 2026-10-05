"""GLB gun / gear → game FBX (same axes as the other models: in Blender the muzzle points -Y, up +Z,
origin at the top of the pistol grip, GunRoot empty parent).

python3 convert_prop.py -- in.glb out.fbx rotZdeg length gripFromRear gripFromBottom maxTris [options]
For gear (not guns) pass gripFromRear < 0: origin goes to the bottom centre.
Options (key=value; a bare first option is the drop list, for older calls):
  drop=a,b*     meshes to leave out (names or wildcards: arms, muzzle flashes, loose cartridges, helper spheres)
  atlas=px      pack all textured materials into one atlas of that size (one draw call per gun)
  pose=1        keep the pose at the frame the glTF importer leaves (animated guns whose rest pose is taken apart)
  pitch=deg     tilt the muzzle down by this much after rotZ (models that were saved at an angle)
  color=pat:rrggbb,...  base colour of untextured materials by name pattern, first match wins (white models)
"""
import bpy, sys, math, os, fnmatch
from mathutils import Matrix, Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from atlas import build_atlas, base_image

a = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = a[0], a[1]
ROT, LENGTH, GRIP_R, GRIP_B, MAXTRIS = float(a[2]), float(a[3]), float(a[4]), float(a[5]), int(a[6])
OPT = {}
for i, kv in enumerate(a[7:]):
    if "=" in kv:
        k, v = kv.split("=", 1)
        OPT[k] = v
    elif i == 0:
        OPT["drop"] = kv
    elif i == 1:
        OPT["atlas"] = kv
DROP = [d for d in OPT.get("drop", "").split(",") if d]
ATLAS = int(OPT.get("atlas", 0))
POSE = OPT.get("pose", "0") == "1"
PITCH = float(OPT.get("pitch", 0))
COLORS = [c.rsplit(":", 1) for c in OPT.get("color", "").split(",") if ":" in c]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
sc = bpy.context.scene
for o in list(sc.objects):
    if o.type == "MESH" and any(fnmatch.fnmatchcase(o.name, d) for d in DROP):
        bpy.data.objects.remove(o, do_unlink=True)
if POSE:
    # bake every mesh as it looks at the current frame (armature deformation and bone parents included)
    dg = bpy.context.evaluated_depsgraph_get()
    baked = []
    for o in [o for o in sc.objects if o.type == "MESH"]:
        e = o.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(e, preserve_all_data_layers=True, depsgraph=dg)
        n = bpy.data.objects.new(o.name + "_posed", me)
        n.matrix_world = e.matrix_world.copy()
        baked.append(n)
    for o in list(sc.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for n in baked:
        sc.collection.objects.link(n)
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
    for m in list(o.modifiers):
        o.modifiers.remove(m)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.make_single_user(object=True, obdata=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.join()
g = bpy.context.view_layer.objects.active
g.name = "GunMesh"

# orient: muzzle → -Y
g.data.transform(Matrix.Rotation(math.radians(ROT), 4, "Z"))
if PITCH:
    g.data.transform(Matrix.Rotation(math.radians(PITCH), 4, "X"))   # muzzle (-Y) tips down by PITCH
vs = [v.co for v in g.data.vertices]
mn = Vector([min(v[i] for v in vs) for i in range(3)]); mx = Vector([max(v[i] for v in vs) for i in range(3)])
if GRIP_R >= 0:
    length = mx.y - mn.y
    s = LENGTH / length
    origin = Vector(((mn.x + mx.x) / 2, mx.y - GRIP_R * length, mn.z + GRIP_B * (mx.z - mn.z)))
else:
    s = LENGTH / max(mx - mn)
    origin = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
g.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation(-origin))

# decimate
tris = sum(len(p.vertices) - 2 for p in g.data.polygons)
if tris > MAXTRIS:
    d = g.modifiers.new("Decimate", "DECIMATE")
    d.ratio = MAXTRIS / tris
    bpy.ops.object.modifier_apply(modifier="Decimate")
print("tris", tris, "->", sum(len(p.vertices) - 2 for p in g.data.polygons))

# base colour only, max 1024
for m in g.data.materials:
    if not m or not m.use_nodes:
        continue
    nt = m.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        continue
    base = bsdf.inputs["Base Color"]
    for inp in bsdf.inputs:
        if inp.name != "Base Color":
            for l in list(inp.links):
                nt.links.remove(l)
    if base.links:
        src = base.links[0].from_node
        while src.type != "TEX_IMAGE" and src.inputs and any(i.links for i in src.inputs):
            src = next(i.links[0].from_node for i in src.inputs if i.links)
        if src.type == "TEX_IMAGE":
            nt.links.new(src.outputs["Color"], base)
            if src.image and max(src.image.size) > 1024:
                src.image.scale(1024, 1024)
    for n in list(nt.nodes):
        if n.type == "TEX_IMAGE" and not any(l.to_node == bsdf and l.to_socket.name == "Base Color" for l in n.outputs["Color"].links):
            nt.nodes.remove(n)

for m in g.data.materials:
    if m is None or base_image(m) is not None:
        continue
    hexc = next((h for pat, h in COLORS if fnmatch.fnmatchcase(m.name, pat)), None)
    if hexc and m.use_nodes:
        bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is not None:
            # Unity reads FBX material colours as sRGB: the value goes in as written, not linearised
            srgb = [int(hexc[i:i + 2], 16) / 255 for i in (0, 2, 4)]
            bsdf.inputs["Base Color"].default_value = (*srgb, 1.0)

if ATLAS > 0 and any(base_image(m) for m in g.data.materials):
    g = build_atlas([g], ATLAS, OUT, sc)[0]
    g.name = "GunMesh"

root = bpy.data.objects.new("GunRoot", None)
sc.collection.objects.link(root)
g.parent = root
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=OUT, object_types={"EMPTY", "MESH"}, apply_scale_options="FBX_SCALE_ALL",
                         path_mode="COPY", embed_textures=True, mesh_smooth_type="FACE", bake_anim=False)
vs = [v.co for v in g.data.vertices]
print("final bounds", [round(min(v[i] for v in vs), 3) for i in range(3)], [round(max(v[i] for v in vs), 3) for i in range(3)])
