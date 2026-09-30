"""
Export the M1911 and .45 ACP ammo to Unity. Materials collapse into two small palette textures:
albedo (sRGB) and metallic/smoothness (R = metallic, A = smoothness, URP Lit convention), so the
whole gun is one material while steel still reads as metal and wood as wood.
Works on temporary duplicates; the .blend keeps per-part materials.
"""
import bpy
import os

PROJECT = r"C:\Users\Cobos\Escape from Polykov"
OUT_DIR = os.path.join(PROJECT, r"Assets\_Project\Art\Weapons\M1911")
CELL, COLUMNS = 4, 8
os.makedirs(OUT_DIR, exist_ok=True)


def _srgb(c):
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def build_palettes(objects):
    materials = []
    for o in objects:
        for m in o.data.materials:
            if m and m not in materials:
                materials.append(m)
    rows = (len(materials) + COLUMNS - 1) // COLUMNS
    w, h = COLUMNS * CELL, max(rows, 1) * CELL
    albedo = [0.0] * (w * h * 4)
    mask = [0.0] * (w * h * 4)
    uv_of = {}
    for i, m in enumerate(materials):
        cx, cy = i % COLUMNS, i // COLUMNS
        col = [_srgb(c) for c in m.diffuse_color[:3]]
        metal, smooth = m.metallic, 1.0 - m.roughness
        for y in range(cy * CELL, cy * CELL + CELL):
            for x in range(cx * CELL, cx * CELL + CELL):
                k = (y * w + x) * 4
                albedo[k:k + 4] = [col[0], col[1], col[2], 1.0]
                mask[k:k + 4] = [metal, 0.0, 0.0, smooth]
        uv_of[m.name] = ((cx + 0.5) * CELL / w, (cy + 0.5) * CELL / h)
    for name, px, alpha in (("T_M1911_Albedo", albedo, False), ("T_M1911_MetalSmooth", mask, True)):
        img = bpy.data.images.get(name)
        if img:
            bpy.data.images.remove(img)
        img = bpy.data.images.new(name, width=w, height=h, alpha=alpha)
        if alpha:
            img.colorspace_settings.name = "Non-Color"
        img.pixels = px
        img.filepath_raw = os.path.join(OUT_DIR, name + ".png")
        img.file_format = "PNG"
        img.save()
    return uv_of, len(materials)


def palette_duplicate(src, uv_of, mat, coll):
    name = src.name
    src.name = name + "__src"
    dup = src.copy()
    dup.data = src.data.copy()
    dup.name = name
    dup.data.name = name
    coll.objects.link(dup)
    me = dup.data
    uv = me.uv_layers.new(name="Palette")
    for poly in me.polygons:
        u, v = uv_of[me.materials[poly.material_index].name]
        for li in poly.loop_indices:
            uv.data[li].uv = (u, v)
    for layer in [l for l in me.uv_layers if l.name != "Palette"]:
        me.uv_layers.remove(layer)
    me.materials.clear()
    me.materials.append(mat)
    return src, dup


def export(root_name, fbx_name, uv_of, mat):
    root = bpy.data.objects[root_name]
    coll = root.users_collection[0]
    meshes = [o for o in root.children if o.type == "MESH"]
    pairs = [palette_duplicate(o, uv_of, mat, coll) for o in meshes]
    for src, dup in pairs:
        dup.parent = root
        dup.matrix_parent_inverse = src.matrix_parent_inverse.copy()
        src.parent = None
    saved_loc = root.location.copy()
    root.location = (0, 0, 0)
    for o in bpy.data.objects:
        try:
            o.select_set(False)
        except RuntimeError:
            pass
    root.select_set(True)
    for o in root.children:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, fbx_name),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        bake_anim=False,
        path_mode="STRIP",
    )
    root.location = saved_loc
    for src, dup in pairs:
        me = dup.data
        bpy.data.objects.remove(dup, do_unlink=True)
        bpy.data.meshes.remove(me)
        src.name = src.name.replace("__src", "")
        src.parent = root
    return os.path.join(OUT_DIR, fbx_name)


all_meshes = [o for o in bpy.data.collections["M1911"].objects if o.type == "MESH"]
uv_of, count = build_palettes(all_meshes)
mat = bpy.data.materials.get("M_M1911") or bpy.data.materials.new("M_M1911")
gun = export("M1911", "M1911.fbx", uv_of, mat)
ammo = export("Ammo_45ACP", "Ammo_45ACP.fbx", uv_of, mat)
result = {"gun": gun, "ammo": ammo, "materials": count}
