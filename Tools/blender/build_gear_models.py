"""Grenades, helmets and vests from the assets-v3 release (CC-BY, see ASSETS.md) -> game models and inventory icons.

    python3 build_gear_models.py -- <folder with the .glb files>      (bpy module; Blender 4.2+ / 5)

Writes Assets/Resources/Models/Props/<name>.fbx (via convert_prop.py: origin at the bottom centre, one atlas texture)
and, for the items that stand for a piece of gear, Assets/Resources/UI/Icons/gear_<id>.png rendered from the model
(make_gear_icons.py leaves those icons alone).
"""
import bpy, sys, os, subprocess, math
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SRC = ARGS[0]
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..", "..")
PROPS = os.path.join(ROOT, "Assets", "Resources", "Models", "Props")
ICONS = os.path.join(ROOT, "Assets", "Resources", "UI", "Icons")

PACK = "vest_armor_holster_lowpoly_gameready_pack.glb"
PACK_ITEMS = ["Holster_Holster_0", "Vest_Vest_0", "Vest_001_VestC75_0", "Vest_1_Prem_Vest_1_Prem_Material_0",
              "Vest_1_T1_Vest_1_T1_Material_0", "Vest_1_T2_Vest_1_T2_Material_0", "Vest_T0_EquipAtlas_0_0"]


def only(keep):
    return "drop=Icosphere*," + ",".join(n for n in PACK_ITEMS if n != keep)


# name: (glb, size in metres (largest side), max triangles, extra options, gear icon id or None); glb None: the prop
# is already in the game, only its icon is rendered
ITEMS = {
    "Grenade_M67":   ("m67_fragmentation_grenade.glb", 0.10, 1500, ["drop=Icosphere*"], "x_frag"),
    "Grenade_Smoke": ("smoke_grenade.glb", 0.145, 1500, ["drop=Icosphere*"], "t_smoke"),
    "Grenade_Gas":   ("m18_smoke_grenade_green.glb", 0.145, 1500, ["drop=Icosphere*"], "t_gas"),
    "Grenade_Flash": ("zarya_2_stun_grenade.glb", 0.15, 2500, ["drop=Icosphere*"], "t_flash"),
    "Helmet_MICH":   ("mich_2001_military_helmet.glb", 0.28, 2500, ["drop=Icosphere*"], "h_tactical"),
    "Helmet_K6":     ("combat_helmet_k6-3.glb", 0.32, 3000, ["drop=Icosphere*"], "h_heavy"),
    "ArmorVest":     (None, 0, 0, [], "b_plate"),   # this upload is the vest the game already had: icon only
    "Vest_Tactical": ("tactical_armor_vest.glb", 0.65, 4000, ["drop=Icosphere*"], "b_heavy"),
    "Vest_Rig":      (PACK, 0.55, 2500, [only("Vest_T0_EquipAtlas_0_0")], "b_light"),
    "Vest_Olive":    (PACK, 0.6, 3000, [only("Vest_1_T2_Vest_1_T2_Material_0")], "b_commando"),
    "Vest_Black":    (PACK, 0.6, 3000, [only("Vest_1_T1_Vest_1_T1_Material_0")], None),
}


def convert(name, glb, size, tris, opts):
    out = os.path.join(PROPS, name + ".fbx")
    cmd = [sys.executable, os.path.join(HERE, "convert_prop.py"), "--", os.path.join(SRC, glb), out,
           "0", str(size), "-1", "0", str(tris)] + opts + ["atlas=512"]
    print(" ".join(cmd))
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL)
    return out


def render_icon(fbx, path, size=256):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    sc = bpy.context.scene
    obs = [o for o in sc.objects if o.type == "MESH"]
    view = Vector((-0.55, -1.0, 0.32)).normalized()   # from the front, a little to the right, above
    f = -view
    r = f.cross(Vector((0, 0, 1))).normalized()
    u = r.cross(f)
    pts = [o.matrix_world @ v.co for o in obs for v in o.data.vertices]
    xs = [p.dot(r) for p in pts]
    ys = [p.dot(u) for p in pts]
    zs = [p.dot(view) for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    span = max(max(xs) - min(xs), max(ys) - min(ys))
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = span * 1.15
    cam.location = r * cx + u * cy + view * (max(zs) + 1.0)
    cam.rotation_euler = f.to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = (max(zs) - min(zs)) + 10
    sc.camera = cam
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x = sc.render.resolution_y = size
    sc.display.render_aa = "16"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "TEXTURE"
    sh.show_shadows = False
    sh.show_cavity = True
    sh.cavity_type = "BOTH"
    sh.show_object_outline = True
    sh.object_outline_color = (0.03, 0.035, 0.05)
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.exposure = 0.9          # the models are dark; the icons sit on dark panels
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    names = [a for a in ARGS[1:] if a != "--icons-only"] or list(ITEMS)
    for n in names:
        glb, size, tris, opts, icon = ITEMS[n]
        fbx = os.path.join(PROPS, n + ".fbx") if "--icons-only" in ARGS or glb is None else convert(n, glb, size, tris, opts)
        if icon:
            render_icon(fbx, os.path.join(ICONS, "gear_" + icon + ".png"))
