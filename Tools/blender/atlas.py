"""Shared by convert_character.py and convert_prop.py: packs every material's base colour into one atlas texture
(one material, one draw call per model) and gives alpha-tested parts (hair cards, lashes) a back side."""
import bpy, math, os, tempfile, shutil
import numpy as np


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


def build_atlas(meshes, size, out_path, scene):
    """Joins the meshes and packs all base-colour textures into one atlas (grid of tiles, edge-padded).
    The atlas is named after out_path ("<Model>_atlas", or "<Model>_atlas_cutout" when a texture uses alpha,
    which the game draws alpha-tested). Returns [joined mesh]."""
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
    name = os.path.splitext(os.path.basename(out_path))[0] + ("_atlas_cutout" if cutout else "_atlas")
    img = bpy.data.images.new(name, size, size, alpha=cutout)
    img.pixels.foreach_set(atlas.ravel())
    path = os.path.join(tempfile.mkdtemp(), name + (".png" if cutout else ".jpg"))
    scene.render.image_settings.file_format = "PNG" if cutout else "JPEG"
    scene.render.image_settings.quality = 90
    scene.render.image_settings.color_mode = "RGBA" if cutout else "RGB"
    img.save_render(path, scene=scene)
    if os.environ.get("ZM_ATLAS_OUT"):
        # colour variants: the same atlas layout with other textures (ModelLibrary.ApplyVariant swaps it by name)
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
    import bmesh
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
