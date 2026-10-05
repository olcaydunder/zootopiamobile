"""Renders a side-view icon of every gun model (Assets/Resources/Models/Guns/*.fbx) into
Assets/Resources/UI/Guns/<name>.png (512x256, transparent), muzzle pointing right, for the gunsmith's model cards.

<blender python> Tools/blender/render_gun_icons.py [-- names...]
"""
import bpy, sys, os, glob
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..", "..")
GUNS = os.path.join(ROOT, "Assets", "Resources", "Models", "Guns")
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "Guns")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def render(fbx, path, w=512, h=256):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    sc = bpy.context.scene
    obs = [o for o in sc.objects if o.type == "MESH"]
    pts = [o.matrix_world @ v.co for o in obs for v in o.data.vertices]
    # Unity's +Z (the muzzle) is Blender's -Y after the FBX round trip: look from the gun's right side (-X in Blender),
    # so -Y runs to the right of the picture.
    view = Vector((-1.0, 0.0, 0.08)).normalized()
    f = -view
    r = f.cross(Vector((0, 0, 1))).normalized()
    u = r.cross(f)
    xs = [p.dot(r) for p in pts]
    ys = [p.dot(u) for p in pts]
    zs = [p.dot(view) for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    spanx, spany = max(xs) - min(xs), max(ys) - min(ys)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(spanx, spany * w / h) * 1.08
    cam.location = r * cx + u * cy + view * (max(zs) + 1.0)
    cam.rotation_euler = f.to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_end = (max(zs) - min(zs)) + 10
    sc.camera = cam
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.display.render_aa = "16"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "TEXTURE"
    sh.show_shadows = False
    sh.show_cavity = True
    sh.cavity_type = "BOTH"
    sh.show_object_outline = True
    sh.object_outline_color = (0.02, 0.025, 0.035)
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.exposure = 1.1
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    files = sorted(glob.glob(os.path.join(GUNS, "*.fbx")))
    for fbx in files:
        name = os.path.splitext(os.path.basename(fbx))[0]
        if ARGS and name not in ARGS:
            continue
        render(fbx, os.path.join(OUT, name + ".png"))
        print("icon", name)
