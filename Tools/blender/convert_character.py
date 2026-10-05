"""Turns a Mixamo-rigged GLB character into a game-ready FBX for Zootopia Mobile:
- bakes its current pose (first frame) as the rest pose and normalises all transforms,
- decimates heavy meshes and keeps only base-colour textures (1K),
- retargets the game's clips (Idle, Run, Shoot_OneHanded, RecieveHit, Death) from the
  Quaternius SoldierMale rig onto the Mixamo skeleton (world-space rotation deltas with
  rest-direction alignment), and exports FBX the same way as the other character models.

blender --background --python convert_character.py -- in.glb source_rig.fbx out.fbx [decimate_ratio] [height_m] [atlas_px]
(or: python3 convert_character.py -- ... with the bpy module, e.g. `pip install bpy` in a Python 3.11 venv)

decimate_ratio >= 1 keeps the mesh as is. height_m rescales the model to that standing height first
(for sources authored in centimetres). Mixamo bone names with or without the colon ("mixamorigHips",
"mixamorig:Hips_01") are recognised; the exported skeleton uses "mixamorig:<Bone>" so CharacterRig finds the hand.
atlas_px > 0 joins all the meshes into one and packs every material's base colour into one atlas texture of that
size (one material: one draw call per character instead of one per part). When a part's texture uses alpha (hair
cards, lashes) the atlas keeps it as a PNG named "..._atlas_cutout", which the game draws alpha-tested.
"""
import bpy, sys, math, os, tempfile
from mathutils import Matrix, Vector, Quaternion

args = sys.argv[sys.argv.index("--") + 1:]
SRC_GLB, RIG_FBX, OUT = args[0], args[1], args[2]
RATIO = float(args[3]) if len(args) > 3 else 0.3
HEIGHT = float(args[4]) if len(args) > 4 else 0.0
ATLAS = int(args[5]) if len(args) > 5 else 0

MAP = {  # mixamo suffix -> Quaternius bone
    "Hips": "Body", "Spine": "Hips", "Spine1": "Abdomen", "Spine2": "Torso", "Neck": "Neck", "Head": "Head",
    "LeftShoulder": "Shoulder.L", "LeftArm": "UpperArm.L", "LeftForeArm": "LowerArm.L", "LeftHand": "Fist.L",
    "RightShoulder": "Shoulder.R", "RightArm": "UpperArm.R", "RightForeArm": "LowerArm.R", "RightHand": "Fist.R",
    "LeftUpLeg": "UpperLeg.L", "LeftLeg": "LowerLeg.L", "LeftFoot": "Foot.L",
    "RightUpLeg": "UpperLeg.R", "RightLeg": "LowerLeg.R", "RightFoot": "Foot.R",
}
CLIPS = ["Idle", "Run", "Shoot_OneHanded", "RecieveHit", "Death"]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC_GLB)
scene = bpy.context.scene
arm = [o for o in scene.objects if o.type == "ARMATURE"][0]
meshes = [o for o in scene.objects if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)]
for o in list(scene.objects):
    if o.type == "MESH" and o not in meshes:
        bpy.data.objects.remove(o, do_unlink=True)

# 1) current pose (first frame of the active action) becomes the rest pose
scene.frame_set(int(arm.animation_data.action.frame_range[0]) if arm.animation_data and arm.animation_data.action else 0)
bpy.context.view_layer.update()
for o in meshes:
    bpy.context.view_layer.objects.active = o
    if o.data.shape_keys:
        o.shape_key_clear()
    for m in list(o.modifiers):
        if m.type == "ARMATURE":
            bpy.ops.object.modifier_apply(modifier=m.name)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="POSE")
bpy.ops.pose.select_all(action="SELECT")
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode="OBJECT")
if arm.animation_data:
    arm.animation_data.action = None
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)

# 2) flatten the hierarchy and apply transforms
S = Matrix()
if HEIGHT > 0:
    zs = [(o.matrix_world @ v.co).z for o in meshes for v in o.data.vertices]
    S = Matrix.Scale(HEIGHT / (max(zs) - min(zs)), 4)
for o in meshes + [arm]:
    mw = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = S @ mw
for o in list(scene.objects):
    if o.type == "EMPTY":
        bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action="DESELECT")
for o in meshes + [arm]:
    o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in meshes:
    o.parent = arm
    o.matrix_parent_inverse = Matrix()
    mod = o.modifiers.new("Armature", "ARMATURE")
    mod.object = arm

# feet on the ground, centred
zmin = min((o.matrix_world @ v.co).z for o in meshes for v in o.data.vertices)
print("height", max((o.matrix_world @ v.co).z for o in meshes for v in o.data.vertices) - zmin)

# 3) decimate + base-colour-only materials
for o in meshes:
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris > 2500 and RATIO < 1:
        d = o.modifiers.new("Decimate", "DECIMATE")
        d.ratio = RATIO
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_move_to_index(modifier="Decimate", index=0)
        bpy.ops.object.modifier_apply(modifier="Decimate")
for m in bpy.data.materials:
    if not m.use_nodes:
        continue
    nt = m.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        continue
    for inp in bsdf.inputs:
        if inp.name != "Base Color":
            for l in list(inp.links):
                nt.links.remove(l)
    base = bsdf.inputs["Base Color"]
    if base.links:
        src = base.links[0].from_node
        # follow through colour-mix nodes to the image
        while src.type != "TEX_IMAGE" and src.inputs and any(i.links for i in src.inputs):
            src = next(i.links[0].from_node for i in src.inputs if i.links)
        if src.type == "TEX_IMAGE":
            nt.links.new(src.outputs["Color"], base)
            img = src.image
            if img and max(img.size) > 1024:
                img.scale(1024, 1024)
    for n in list(nt.nodes):
        if n.type == "TEX_IMAGE" and not any(l.to_node == bsdf and l.to_socket.name == "Base Color" for l in n.outputs["Color"].links):
            nt.nodes.remove(n)
# Re-encode the remaining base-colour textures at <= 1K (the packed originals would otherwise be embedded
# unchanged): JPEG when fully opaque, PNG when the alpha channel is used.
tmpdir = tempfile.mkdtemp()
for m in bpy.data.materials:
    if not m.use_nodes:
        continue
    for n in m.node_tree.nodes:
        if n.type != "TEX_IMAGE" or n.image is None or n.image.size[0] == 0:
            continue
        img = n.image
        if max(img.size) > 1024:
            img.scale(min(1024, img.size[0]), min(1024, img.size[1]))
        import numpy as np
        px = np.empty(len(img.pixels), dtype=np.float32)
        img.pixels.foreach_get(px)
        opaque = img.channels < 4 or float(px[3::4].min()) > 0.99
        fmt = "JPEG" if opaque else "PNG"
        # texture named "<Model>_<material>" so it is unique among the project's textures (Unity matches by name)
        tex_name = os.path.splitext(os.path.basename(OUT))[0] + "_" + "".join(c if c.isalnum() else "_" for c in m.name)
        path = os.path.join(tmpdir, tex_name + (".jpg" if opaque else ".png"))
        scene.render.image_settings.file_format = fmt
        scene.render.image_settings.quality = 90
        scene.render.image_settings.color_mode = "RGB" if opaque else "RGBA"
        img.save_render(path, scene=scene)
        n.image = bpy.data.images.load(path)
print("tris after", sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes))


def base_image(m):
    if m is None or not m.use_nodes:
        return None
    for n in m.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image is not None and n.image.size[0] > 0:
            return n.image
    return None


def base_color(m):
    if m is not None and m.use_nodes:
        bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is not None:
            return tuple(bsdf.inputs["Base Color"].default_value)
    return (0.5, 0.5, 0.5, 1.0)


def build_atlas(meshes, size):
    """Joins the meshes and packs all base-colour textures into one atlas (grid of tiles, edge-padded)."""
    import numpy as np
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    mats = [s.material for s in obj.material_slots]
    used = sorted({p.material_index for p in obj.data.polygons})
    n = max(1, math.ceil(math.sqrt(len(used))))
    tile = size // n
    pad = max(2, tile // 64)
    atlas = np.zeros((size, size, 4), dtype=np.float32)
    atlas[..., 3] = 1.0
    rect = {}
    cutout = False
    for k, mi in enumerate(used):
        tx, ty = (k % n) * tile, (k // n) * tile
        inner = tile - 2 * pad
        img = base_image(mats[mi] if mi < len(mats) else None)
        if img is not None:
            cp = img.copy()
            cp.scale(inner, inner)
            px = np.empty(inner * inner * 4, dtype=np.float32)
            cp.pixels.foreach_get(px)
            px = px.reshape(inner, inner, 4)
            bpy.data.images.remove(cp)
            if img.channels < 4 or float(px[..., 3].min()) > 0.99:
                px[..., 3] = 1.0
            else:
                cutout = True
        else:
            px = np.zeros((inner, inner, 4), dtype=np.float32)
            px[:] = base_color(mats[mi] if mi < len(mats) else None)
            px[..., 3] = 1.0
        # edge padding: repeat the border pixels into the gap so mipmaps don't bleed in other tiles
        block = np.pad(px, ((pad, pad), (pad, pad), (0, 0)), mode="edge")
        atlas[ty:ty + tile, tx:tx + tile] = block
        rect[mi] = (tx + pad, ty + pad, inner)
    uv = obj.data.uv_layers.active
    if uv is not None:
        for p in obj.data.polygons:
            r = rect.get(p.material_index)
            if r is None:
                continue
            us = [uv.data[li].uv[0] for li in p.loop_indices]
            vs = [uv.data[li].uv[1] for li in p.loop_indices]
            ou, ov = math.floor(min(us)), math.floor(min(vs))   # tiled UVs: move the face into 0..1
            for li in p.loop_indices:
                u = min(1.0, max(0.0, uv.data[li].uv[0] - ou))
                v = min(1.0, max(0.0, uv.data[li].uv[1] - ov))
                uv.data[li].uv = ((r[0] + u * r[2]) / size, (r[1] + v * r[2]) / size)
    name = os.path.splitext(os.path.basename(OUT))[0] + ("_atlas_cutout" if cutout else "_atlas")
    img = bpy.data.images.new(name, size, size, alpha=cutout)
    img.pixels.foreach_set(atlas.ravel())
    path = os.path.join(tempfile.mkdtemp(), name + (".png" if cutout else ".jpg"))
    scene.render.image_settings.file_format = "PNG" if cutout else "JPEG"
    scene.render.image_settings.quality = 90
    scene.render.image_settings.color_mode = "RGBA" if cutout else "RGB"
    img.save_render(path, scene=scene)
    if os.environ.get("ZM_ATLAS_OUT"):
        # colour variants: the same atlas layout with other textures (ModelLibrary.ApplyVariant swaps it by name)
        import shutil
        os.makedirs(os.environ["ZM_ATLAS_OUT"], exist_ok=True)
        shutil.copy(path, os.path.join(os.environ["ZM_ATLAS_OUT"], os.path.basename(path)))
    mat = bpy.data.materials.new("atlas")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.material_index = 0
    print("atlas", len(used), "materials ->", size, "px, tile", tile)
    return [obj]


def double_side_alpha_parts(meshes):
    """Hair cards and lashes (textures with alpha) get a back side: the game's lit shader culls back faces."""
    import bmesh, numpy as np
    for o in meshes:
        alpha_slots = set()
        for i, slot in enumerate(o.material_slots):
            img = base_image(slot.material)
            if img is None or img.channels < 4:
                continue
            px = np.empty(len(img.pixels), dtype=np.float32)
            img.pixels.foreach_get(px)
            if float(px[3::4].min()) < 0.99:
                alpha_slots.add(i)
        if not alpha_slots:
            continue
        bm = bmesh.new()
        bm.from_mesh(o.data)
        faces = [f for f in bm.faces if f.material_index in alpha_slots]
        res = bmesh.ops.duplicate(bm, geom=faces)
        new_faces = [g for g in res["geom"] if isinstance(g, bmesh.types.BMFace)]
        bmesh.ops.reverse_faces(bm, faces=new_faces)
        bm.to_mesh(o.data)
        bm.free()
        print("double-sided", o.name, len(new_faces), "faces")


if ATLAS > 0:
    double_side_alpha_parts(meshes)
    meshes = build_atlas(meshes, ATLAS)

# 4) retarget the game's clips from the Quaternius rig
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=RIG_FBX)
new = [o for o in bpy.data.objects if o not in before]
src = next(o for o in new if o.type == "ARMATURE")
Ws, Wt = src.matrix_world.copy(), arm.matrix_world.copy()

def mixname(suffix):
    for b in arm.data.bones:
        n = b.name.split(":")[-1]
        if n.startswith("mixamorig"):
            n = n[len("mixamorig"):]
        base = n.rsplit("_", 1)[0] if n.rsplit("_", 1)[-1].isdigit() else n
        if base == suffix:
            return b.name
    return None
tmap = {}
for suf, sname in MAP.items():
    tn = mixname(suf)
    if tn and sname in src.data.bones:
        tmap[tn] = sname
print("mapped", len(tmap), "of", len(MAP))

def rest_dir(armobj, b):
    W = armobj.matrix_world
    return ((W @ b.tail_local) - (W @ b.head_local)).normalized()

# Where each target bone "points": towards the next joint of its chain. glTF imports often leave bone tails
# along an arbitrary axis, so the head -> child-head direction is used instead of head -> tail.
NEXT = {"Spine": ["Spine1", "Spine2", "Neck"], "Spine1": ["Spine2", "Neck"], "Spine2": ["Neck", "Head"], "Neck": ["Head"],
        "Head": ["HeadTop_End"], "LeftShoulder": ["LeftArm"], "LeftArm": ["LeftForeArm"], "LeftForeArm": ["LeftHand"],
        "LeftHand": ["LeftHandMiddle1", "LeftHandIndex1"], "RightShoulder": ["RightArm"], "RightArm": ["RightForeArm"],
        "RightForeArm": ["RightHand"], "RightHand": ["RightHandMiddle1", "RightHandIndex1"], "LeftUpLeg": ["LeftLeg"],
        "LeftLeg": ["LeftFoot"], "LeftFoot": ["LeftToeBase"], "RightUpLeg": ["RightLeg"], "RightLeg": ["RightFoot"],
        "RightFoot": ["RightToeBase"]}
def chain_dir(armobj, b, suffix):
    W = armobj.matrix_world
    for nxt in NEXT.get(suffix, []):
        n = mixname(nxt)
        if n:
            d = (W @ armobj.data.bones[n].head_local) - (W @ b.head_local)
            if d.length > 1e-5:
                return d.normalized()
    if suffix == "Head":    # no head-top joint: the head points straight up like the source's
        return Vector((0, 0, 1))
    return rest_dir(armobj, b)
suffix_of = {mixname(s): s for s in MAP if mixname(s)}
corr = {}
for tn, sn in tmap.items():
    if suffix_of[tn] in ("Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftFoot", "RightFoot"):
        # upright torso/head and flat feet are the same in both rest poses: copy the world delta as is
        corr[tn] = Matrix.Identity(3)
        continue
    corr[tn] = chain_dir(arm, arm.data.bones[tn], suffix_of[tn]).rotation_difference(rest_dir(src, src.data.bones[sn])).to_matrix()

hips_t = mixname("Hips")
hips_h_t = (Wt @ arm.data.bones[hips_t].head_local).z - zmin
src_mesh = [o for o in new if o.type == "MESH"]
src_zmin = min((o.matrix_world @ v.co).z for o in src_mesh for v in o.data.vertices) if src_mesh else 0
hips_h_s = (Ws @ src.data.bones["Body"].head_local).z - src_zmin
hscale = hips_h_t / max(1e-4, hips_h_s)
print("hip heights", hips_h_t, hips_h_s, "scale", hscale)

order = []
def walk(b):
    order.append(b)
    for c in b.children:
        walk(c)
for b in arm.data.bones:
    if b.parent is None:
        walk(b)

src_actions = {}
for a in bpy.data.actions:
    for c in CLIPS:
        if a.name.endswith("|" + c) or a.name == c:
            src_actions[c] = a
print("source clips", list(src_actions))

arm.animation_data_create()
for pb in arm.pose.bones:
    pb.rotation_mode = "QUATERNION"
for clip in CLIPS:
    sa = src_actions.get(clip)
    if sa is None:
        continue
    src.animation_data.action = sa
    ta = bpy.data.actions.new(clip)
    arm.animation_data.action = ta
    f0, f1 = int(sa.frame_range[0]), int(sa.frame_range[1])
    s_rest_body = Ws @ src.data.bones["Body"].matrix_local
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        A = {}
        for b in order:
            R = b.matrix_local
            if b.parent:
                Rp = b.parent.matrix_local
                follow = A[b.parent.name] @ (Rp.inverted() @ R)
            else:
                follow = R.copy()
            if b.name in tmap:
                sn = tmap[b.name]
                Ms = Ws @ src.pose.bones[sn].matrix
                Ms0 = Ws @ src.data.bones[sn].matrix_local
                D = Ms.to_3x3() @ Ms0.to_3x3().inverted()
                rot_world = D @ corr[b.name] @ (Wt.to_3x3() @ R.to_3x3())
                rot = Wt.to_3x3().inverted() @ rot_world
                loc = follow.to_translation()
                if b.name == hips_t:
                    delta = Ms.to_translation() - s_rest_body.to_translation()
                    loc = R.to_translation() + Wt.to_3x3().inverted() @ (delta * hscale)
                A[b.name] = Matrix.Translation(loc) @ rot.normalized().to_4x4()
            else:
                A[b.name] = follow
        for b in order:
            R = b.matrix_local
            if b.parent:
                local_rest = b.parent.matrix_local.inverted() @ R
                basis = local_rest.inverted() @ A[b.parent.name].inverted() @ A[b.name]
            else:
                basis = R.inverted() @ A[b.name]
            pb = arm.pose.bones[b.name]
            loc, q, _ = basis.decompose()
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=f - f0)
            if b.name == hips_t:
                pb.location = loc
                pb.keyframe_insert("location", frame=f - f0)
    ta.use_fake_user = True
    print("baked", clip, f1 - f0 + 1, "frames")

# drop the source rig
for o in new:
    bpy.data.objects.remove(o, do_unlink=True)

# 5) keep only the animated skeleton: every bone the clips don't move (fingers, hair, face, armour helpers) is
#    folded into its nearest kept ancestor (its skin weights move there). Far less animation data, same look.
keep = set()
for tn in tmap:
    b = arm.data.bones[tn]
    while b is not None:
        keep.add(b.name)
        b = b.parent
drop = [b.name for b in arm.data.bones if b.name not in keep]
if drop:
    def kept_ancestor(name):
        b = arm.data.bones[name].parent
        while b is not None and b.name not in keep:
            b = b.parent
        return b.name if b is not None else None
    target = {n: kept_ancestor(n) for n in drop}
    for o in meshes:
        groups = {g.name: g for g in o.vertex_groups}
        for v in o.data.vertices:
            add = {}
            for ge in v.groups:
                gname = o.vertex_groups[ge.group].name
                if gname in target and target[gname] is not None and ge.weight > 0:
                    add[target[gname]] = add.get(target[gname], 0.0) + ge.weight
            for tname, w in add.items():
                g = groups.get(tname) or o.vertex_groups.new(name=tname)
                groups[tname] = g
                g.add([v.index], w, "ADD")
        for n in drop:
            if n in groups:
                o.vertex_groups.remove(groups[n])
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for n in drop:
        eb = arm.data.edit_bones.get(n)
        if eb is not None:
            arm.data.edit_bones.remove(eb)
    bpy.ops.object.mode_set(mode="OBJECT")
    # Their curves must go too: the FBX exporter leaves out every action with a curve it cannot resolve.
    def curve_sets(act):
        if hasattr(act, "fcurves"):          # Blender < 4.4
            return [act.fcurves]
        sets = []
        for layer in act.layers:             # Blender 4.4+: layered actions
            for strip in layer.strips:
                for bag in strip.channelbags:
                    sets.append(bag.fcurves)
        return sets
    for a in bpy.data.actions:
        for fcs in curve_sets(a):
            for fc in list(fcs):
                if any('pose.bones["%s"]' % n in fc.data_path for n in drop):
                    fcs.remove(fc)
    print("bones kept", len(keep), "folded", len(drop))
for a in list(bpy.data.actions):
    if a.name not in CLIPS:
        bpy.data.actions.remove(a)
arm.animation_data.action = bpy.data.actions.get("Idle")
for a in bpy.data.actions:
    track = arm.animation_data.nla_tracks.new()
    track.name = a.name
    track.strips.new(a.name, 0, a)
    track.mute = True
arm.name = "CharacterArmature"
for b in arm.data.bones:   # "mixamorigRightHand" -> "mixamorig:RightHand" (CharacterRig looks for "RightHand" after ':')
    if b.name.startswith("mixamorig") and not b.name.startswith("mixamorig:"):
        b.name = "mixamorig:" + b.name[len("mixamorig"):]
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=False, object_types={"ARMATURE", "MESH"},
                         apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=True,
                         bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         bake_anim_simplify_factor=0.5, path_mode="COPY", embed_textures=True, mesh_smooth_type="FACE")
print("exported", OUT)
