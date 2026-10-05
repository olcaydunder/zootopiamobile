"""Prepares DJMaesen's "female specops" (CC-BY 4.0) for convert_character.py:
- renames its skeleton to Mixamo bone names (CharacterRig and convert_character.py know those),
- the holstered pistol, the knife and the goggles hang from bones (not skinned): they are bound to their bone
  (weight 1) so the converter keeps them; loose cartridges inside the pistol are dropped,
- writes a GLB in the rest pose.

python3 prep_specops.py -- female_specops.glb out.glb
(then: convert_character.py -- out.glb SoldierMale.fbx FemaleSpecops.fbx 0.75 1.7 2048)
"""
import bpy, sys, os
from mathutils import Matrix

args = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = args[0], args[1]

RENAME = {
    "hip_01": "Hips", "spine_012": "Spine", "chest_013": "Spine2", "neck_067": "Neck", "head_068": "Head",
    "L_shoulder_020": "LeftShoulder", "L_arm_021": "LeftArm", "L_elbow_022": "LeftForeArm", "L_wrist_023": "LeftHand",
    "L_point1_028": "LeftHandIndex1",
    "R_shoulder_043": "RightShoulder", "R_arm_044": "RightArm", "R_elbow_045": "RightForeArm", "R_wrist_046": "RightHand",
    "R_point1_051": "RightHandIndex1",
    "L_leg_02": "LeftUpLeg", "L_knee_03": "LeftLeg", "L_ankle_04": "LeftFoot", "L_foot_05": "LeftToeBase",
    "R_leg_07": "RightUpLeg", "R_knee_08": "RightLeg", "R_ankle_09": "RightFoot", "R_foot_010": "RightToeBase",
}
DROP = ("shell", "bullet", "Icosphere")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
sc = bpy.context.scene
arm = next(o for o in sc.objects if o.type == "ARMATURE")
for b in arm.data.bones:
    if b.name in RENAME:
        b.name = "mixamorig:" + RENAME[b.name]

for o in [o for o in sc.objects if o.type == "MESH" and o.name.startswith(DROP)]:
    bpy.data.objects.remove(o, do_unlink=True)

# rigid parts: bind to the bone they hang from, in the rest pose
arm.data.pose_position = "REST"
bpy.context.view_layer.update()
for o in [o for o in sc.objects if o.type == "MESH" and not any(m.type == "ARMATURE" for m in o.modifiers)]:
    p, bone = o.parent, None
    while p is not None:
        if p.parent_type == "BONE" and p.parent_bone:
            bone = p.parent_bone
            break
        p = p.parent
    if o.parent_type == "BONE" and o.parent_bone:
        bone = o.parent_bone
    if bone is None:
        print("no bone for", o.name, "- dropped")
        bpy.data.objects.remove(o, do_unlink=True)
        continue
    mw = o.matrix_world.copy()
    o.data = o.data.copy()
    o.data.transform(mw)
    o.parent = arm
    o.parent_type = "OBJECT"
    o.matrix_world = arm.matrix_world.copy()
    o.data.transform(arm.matrix_world.inverted())
    vg = o.vertex_groups.new(name=bone)
    vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    mod = o.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    print("bound", o.name, "->", bone)
arm.data.pose_position = "POSE"
if arm.animation_data:
    arm.animation_data.action = None
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
for pb in arm.pose.bones:
    pb.matrix_basis = Matrix()

bpy.ops.export_scene.gltf(filepath=os.path.abspath(OUT), export_format="GLB", export_animations=False, export_skins=True,
                          export_morph=False, export_image_format="AUTO")
print("written", OUT)
