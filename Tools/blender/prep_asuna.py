"""Prepares MSGDI's "Free Test Character Asuna" (CC-BY 4.0) for convert_character.py:
- renames its own skeleton to Mixamo bone names (so the game's clips can be retargeted and CharacterRig finds the
  hips, spine, legs, arms and hands),
- puts the base-colour textures on the materials (the FBX only links the PBR maps): one of the four body colours,
  the head, the hair (with its alpha, for alpha-tested hair cards), lashes/brows (with alpha), eyes and teeth,
- drops the cornea (a transparent shell over the eyes),
and writes a GLB.

python3 prep_asuna.py -- <pack folder>/FreeTestCharacterAsunaFBX out.glb [Blue|Black|Green|Red] [Brown|Black|Blonde|Red|White]
(with the bpy module; then: convert_character.py -- out.glb SoldierMale.fbx Asuna.fbx 0.3 0 2048)
"""
import bpy, sys, os
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
PACK, OUT = args[0], args[1]
BODY = args[2] if len(args) > 2 else "Blue"
HAIR = args[3] if len(args) > 3 else "Brown"
TEX = os.path.join(PACK, "Textures")

# Asuna bone -> Mixamo bone (CharacterRig and convert_character.py know the Mixamo names)
RENAME = {
    "Spine0": "mixamorig:Hips",
    "Spine1": "Pelvis",                 # zero-length helper above the hips that carries the legs and the spine
    "Spine2": "mixamorig:Spine",
    "Spine3": "mixamorig:Spine2",
    "Neck": "mixamorig:Neck",
    "Head": "mixamorig:Head",
    "cShrugger_L": "mixamorig:LeftShoulder", "Bicep_L": "mixamorig:LeftArm", "4arm_L": "mixamorig:LeftForeArm",
    "Hand_L": "mixamorig:LeftHand", "Pointer1_L": "mixamorig:LeftHandIndex1",
    "cShrugger_R": "mixamorig:RightShoulder", "Bicep_R": "mixamorig:RightArm", "4arm_R": "mixamorig:RightForeArm",
    "Hand_R": "mixamorig:RightHand", "Pointer1_R": "mixamorig:RightHandIndex1",
    "Hip_L": "mixamorig:LeftUpLeg", "Shin_L": "mixamorig:LeftLeg", "Foot_L": "mixamorig:LeftFoot", "Toe1_L": "mixamorig:LeftToeBase",
    "Hip_R": "mixamorig:RightUpLeg", "Shin_R": "mixamorig:RightLeg", "Foot_R": "mixamorig:RightFoot", "Toe1_R": "mixamorig:RightToeBase",
}

# material -> (albedo, alpha or None)
TEXTURES = {
    "AsunaBody": ("AsunaBody/Albedo/AsunaBody%sAlbedo.png" % BODY, None),
    "AsunaHead": ("AsunaHead/AsunaHeadAlbedo.png", None),
    "DefaultFemaleHair": ("DefaultFemaleHair/DefaultFemaleHairAlbedo%s.png" % HAIR, "DefaultFemaleHair/DefaultFemaleHairAlpha.png"),
    "DefaultLashesBrows": ("DefaultLashesBrows/LashesBrowsAlbedo.png", "DefaultLashesBrows/LashesBrowsAlpha.png"),
    "DefaultTeeth": ("DefaultTeeth/DefaultTeethAlbedo.png", None),
    "Eyeball": ("DefaultEyes/Eyeball_AlbedoGreen.png", None),
}
DROP = {"Cornea"}
SIZE = 1024   # the converter keeps base colour at <= 1K anyway

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(PACK, "Character", "FreeTestCharacterAsuna.fbx"))
scene = bpy.context.scene
arm = next(o for o in scene.objects if o.type == "ARMATURE")
for b in arm.data.bones:
    if b.name in RENAME:
        b.name = RENAME[b.name]


def load_rgba(albedo, alpha):
    img = bpy.data.images.load(os.path.join(TEX, albedo))
    img.scale(SIZE, SIZE)
    px = np.empty(SIZE * SIZE * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(SIZE, SIZE, 4)
    if alpha:
        a = bpy.data.images.load(os.path.join(TEX, alpha))
        a.scale(SIZE, SIZE)
        ap = np.empty(SIZE * SIZE * 4, dtype=np.float32)
        a.pixels.foreach_get(ap)
        px[..., 3] = ap.reshape(SIZE, SIZE, 4)[..., 0]
        bpy.data.images.remove(a)
    else:
        px[..., 3] = 1.0
    out = bpy.data.images.new(os.path.splitext(os.path.basename(albedo))[0], SIZE, SIZE, alpha=alpha is not None)
    out.pixels.foreach_set(px.ravel())
    out.pack()
    bpy.data.images.remove(img)
    return out


for m in bpy.data.materials:
    name = m.name.split(".")[0]
    if name not in TEXTURES:
        continue
    if m.node_tree is None:
        m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type == "TEX_IMAGE":
            nt.nodes.remove(n)
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = load_rgba(*TEXTURES[name])
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if TEXTURES[name][1]:
        nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    print("textured", m.name, TEXTURES[name])

# Drop the cornea faces.
import bmesh
for o in [o for o in scene.objects if o.type == "MESH"]:
    drop = {i for i, s in enumerate(o.material_slots) if s.material and s.material.name.split(".")[0] in DROP}
    if not drop:
        continue
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in drop], context="FACES")
    bm.to_mesh(o.data)
    bm.free()
    if len(o.data.polygons) == 0:
        bpy.data.objects.remove(o, do_unlink=True)

bpy.ops.export_scene.gltf(filepath=os.path.abspath(OUT), export_format="GLB", export_animations=False, export_skins=True,
                          export_morph=False, export_image_format="AUTO")
print("written", OUT)
