"""GLB gun / gear → game FBX (same axes as the other models: in Blender the muzzle points -Y, up +Z,
origin at the top of the pistol grip, GunRoot empty parent).

python3 convert_prop.py -- in.glb out.fbx rotZdeg length gripFromRear gripFromBottom maxTris [drop,names]
For gear (not guns) pass gripFromRear < 0: origin goes to the bottom centre.
"""
import bpy, sys, math
from mathutils import Matrix, Vector

a = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = a[0], a[1]
ROT, LENGTH, GRIP_R, GRIP_B, MAXTRIS = float(a[2]), float(a[3]), float(a[4]), float(a[5]), int(a[6])
DROP = set(a[7].split(",")) if len(a) > 7 and a[7] else set()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
sc = bpy.context.scene
for o in list(sc.objects):
    if o.type == "MESH" and o.name in DROP:
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

root = bpy.data.objects.new("GunRoot", None)
sc.collection.objects.link(root)
g.parent = root
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=OUT, object_types={"EMPTY", "MESH"}, apply_scale_options="FBX_SCALE_ALL",
                         path_mode="COPY", embed_textures=True, mesh_smooth_type="FACE", bake_anim=False)
vs = [v.co for v in g.data.vertices]
print("final bounds", [round(min(v[i] for v in vs), 3) for i in range(3)], [round(max(v[i] for v in vs), 3) for i in range(3)])
