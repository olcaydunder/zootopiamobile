"""Unity does not use the textures embedded in our FBX files (only image files it happens to find by name), so models
that carry their textures inside the FBX showed up white. For each FBX this writes the embedded base-colour textures
to Resources/Models/<category>/Tex/<model>/<image>.<png|jpg> with a map.txt ("material=file=image name in the FBX" per
line); ModelLibrary.Spawn puts them on the model's materials at run time. Generic names ("Image_0", used by several
models) become <model>_<material>.

<blender python> Tools/blender/extract_fbx_textures.py -- <fbx files...>
"""
import bpy, sys, os, re

files = sys.argv[sys.argv.index("--") + 1:]
for f in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=f)
    model = os.path.splitext(os.path.basename(f))[0]
    out_dir = os.path.join(os.path.dirname(os.path.abspath(f)), "Tex", model)
    lines, written = [], {}
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        img = None
        for n in m.node_tree.nodes:
            if n.type == "BSDF_PRINCIPLED":
                for l in n.inputs["Base Color"].links:
                    if l.from_node.type == "TEX_IMAGE" and l.from_node.image is not None:
                        img = l.from_node.image
        if img is None or not img.packed_file:
            continue
        data = img.packed_file.data
        base = os.path.basename(img.filepath) or img.name
        base = re.sub(r"\.\d{3}$", "", base)
        base = os.path.splitext(base)[0] if base.lower().endswith((".png", ".jpg", ".jpeg", ".tga")) else base
        ext = ".png" if data[:4] == b"\x89PNG" else ".jpg" if data[:2] == b"\xff\xd8" else ".png"
        fname = base
        if re.match(r"^Image_\d+$", base):
            fname = model + "_" + re.sub(r"[^A-Za-z0-9]+", "", m.name)
        if fname not in written:
            os.makedirs(out_dir, exist_ok=True)
            with open(os.path.join(out_dir, fname + ext), "wb") as fh:
                fh.write(data)
            written[fname] = len(data)
        lines.append("%s=%s=%s" % (m.name, fname, base))
    if lines:
        with open(os.path.join(out_dir, "map.txt"), "w") as fh:
            fh.write("\n".join(lines) + "\n")
        print("TEX", model, lines, written)
