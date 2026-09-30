"""
Tripo H3.1 AK-74N (multiview from the reference sheet, generated in 51 parts) -> modular low-poly weapon.
Source space (as imported by the Tripo bridge): muzzle +X, gun's right side -Y, Z up, magazine base at z = 0.
Each Tripo part is assigned to a slot of the sheet's modular system (PART_SLOTS), every slot is decimated to its own
triangle budget, then rotated so the muzzle points -Y (Unity +Z) and scaled to the real 943 mm length.
Flat palette colors from the sheet + black inverted-hull outline. Export collapses materials into two palette
textures (albedo, metallic/smoothness) like the M1911.
Run in AK74N_H31_Source.blend: exec(open(r"<path>", encoding="utf-8").read())
Set EXPORT = True to also write Assets/_Project/Art/Weapons/AK74N/AK74N.fbx.
"""
import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix

PROJECT = os.environ.get("POLYKOV_ROOT", r"C:\Users\Cobos\Escape from Polykov")
OUT_DIR = os.path.join(PROJECT, r"Assets\_Project\Art\Weapons\AK74N")
EXPORT = globals().get("EXPORT", False)
SOURCE_LENGTH = 0.986
REAL_LENGTH = 0.943
OUTLINE = 0.0011
PREFIX = "AK74N_"

# slot -> (hex color, metallic, roughness, triangle budget)
SLOTS = {
    "Receiver": ("#3b3d40", 0.6, 0.45, 2600), "TopCover": ("#43464a", 0.6, 0.45, 1400),
    "ChargingHandle": ("#4a4d51", 0.7, 0.4, 300), "Safety": ("#4a4d51", 0.7, 0.4, 250),
    "Trigger": ("#4a4d51", 0.7, 0.4, 120), "RearSight": ("#2f3134", 0.7, 0.4, 500),
    "Grip": ("#2a2b2d", 0.0, 0.8, 900), "Stock": ("#2d2e30", 0.0, 0.8, 1800),
    "Magazine": ("#7a4a2c", 0.0, 0.6, 1300), "HandguardLower": ("#2b2c2e", 0.0, 0.8, 1300),
    "HandguardUpper": ("#2b2c2e", 0.0, 0.8, 700), "GasBlock": ("#35373a", 0.7, 0.4, 600),
    "Barrel": ("#2e3033", 0.8, 0.35, 700), "FrontSight": ("#2f3134", 0.7, 0.4, 400),
    "Muzzle": ("#2a2b2d", 0.8, 0.4, 500),
}

# Tripo part index -> slot (from per-part bounds + side renders; see docs/screenshots/ak_slots.png).
PART_SLOTS = {
    0: "Stock", 6: "Stock", 49: "Stock", 28: "Stock",
    3: "Grip", 34: "Grip", 41: "Grip", 42: "Grip",
    20: "Trigger",
    25: "Safety", 18: "ChargingHandle",
    4: "Receiver", 9: "Receiver", 10: "Receiver",
    1: "TopCover",
    13: "RearSight", 16: "RearSight", 39: "RearSight", 40: "RearSight", 45: "RearSight", 46: "RearSight",
    17: "Magazine", 27: "Magazine", 30: "Magazine", 47: "Magazine", 44: "Magazine", 48: "Magazine",
    31: "Magazine", 23: "Magazine", 21: "Magazine", 8: "Magazine", 15: "Magazine", 33: "Magazine",
    2: "HandguardLower", 29: "HandguardLower", 32: "HandguardLower",
    5: "HandguardUpper",
    24: "GasBlock", 35: "GasBlock",
    50: "Barrel", 37: "Barrel", 12: "Barrel", 19: "Barrel", 38: "Barrel", 11: "Barrel", 14: "Barrel",
    7: "FrontSight",
    22: "Muzzle", 43: "Muzzle", 36: "Muzzle", 26: "Muzzle",
}

# Pivots / sockets in source space (before orientation fix). Moving parts pivot where they hinge or slide.
GRIP = Vector((-0.19, 0.005, 0.15))
PIVOTS = {"Magazine": (-0.01, 0.005, 0.19), "Trigger": (-0.125, 0.005, 0.18), "Safety": (-0.18, -0.03, 0.225),
          "TopCover": (-0.235, 0.005, 0.245), "ChargingHandle": (-0.03, -0.03, 0.235)}
SOCKETS = {"Socket_Muzzle": (0.493, 0.005, 0.22), "Socket_RearSight": (0.075, 0.005, 0.285),
           "Socket_FrontSight": (0.39, 0.005, 0.281), "Socket_RightHand": tuple(GRIP),
           "Socket_LeftHand": (0.14, 0.005, 0.2), "Socket_MagWell": (-0.01, 0.005, 0.18),
           "Socket_EjectionPort": (-0.05, -0.03, 0.235)}


def _lin(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]


def material(slot):
    name = "M_AK_" + slot
    hexc, metal, rough, _ = SLOTS[slot]
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*_lin(hexc), 1)
    m.metallic, m.roughness = metal, rough
    return m


def tripo_parts():
    parts = {}
    for o in bpy.data.objects:
        if o.type == "MESH" and o.name.startswith("tripo_part_"):
            parts[int(o.name.rsplit("_", 1)[1])] = o
    missing = sorted(set(parts) - set(PART_SLOTS))
    if missing:
        raise RuntimeError("Unassigned Tripo parts: %s" % missing)
    return parts


def slot_mesh(slot, parts):
    bm = bmesh.new()
    for idx, o in parts.items():
        if PART_SLOTS[idx] != slot:
            continue
        me = o.data.copy()
        me.transform(o.matrix_world)
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(PREFIX + slot + "_hi")
    bm.to_mesh(me)
    bm.free()
    return me


def decimate(ob, budget):
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    if tris > budget:
        bpy.context.view_layer.objects.active = ob
        mod = ob.modifiers.new("D", "DECIMATE")
        mod.ratio = budget / tris
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier="D")
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00005)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=0.00002)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    orient_islands_outward(bm)
    bm.to_mesh(ob.data)
    bm.free()


def orient_islands_outward(bm):
    """recalc_face_normals picks an arbitrary side on open Tripo shells; flip any island whose area-weighted
    normals point toward its own centroid (works for closed pieces and for half-shells alike)."""
    bm.faces.ensure_lookup_table()
    total = sum(f.calc_area() for f in bm.faces) or 1.0
    mesh_centroid = sum((f.calc_center_median() * f.calc_area() for f in bm.faces), Vector()) / total
    seen = set()
    for start in bm.faces:
        if start.index in seen:
            continue
        island, stack = [], [start]
        seen.add(start.index)
        while stack:
            f = stack.pop()
            island.append(f)
            for e in f.edges:
                for g in e.link_faces:
                    if g.index not in seen:
                        seen.add(g.index)
                        stack.append(g)
        area = sum(f.calc_area() for f in island) or 1.0
        centroid = sum((f.calc_center_median() * f.calc_area() for f in island), Vector()) / area
        outward = sum(f.normal.dot(f.calc_center_median() - centroid) * f.calc_area() for f in island)
        if abs(outward) / area < 0.0005:  # flat sheet (e.g. grip panel): its own centroid can't tell sides
            outward = sum(f.normal.dot(f.calc_center_median() - mesh_centroid) * f.calc_area() for f in island)
        if outward < 0:
            bmesh.ops.reverse_faces(bm, faces=island)


def add_outline(me):
    om = bpy.data.materials.get("M_Outline") or bpy.data.materials.new("M_Outline")
    om.diffuse_color = (0, 0, 0, 1)
    om.metallic, om.roughness = 0.0, 1.0
    me.materials.append(om)
    idx = len(me.materials) - 1
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    ret = bmesh.ops.duplicate(bm, geom=list(bm.faces))
    faces = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMFace)]
    verts = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
    bm.normal_update()
    for v in verts:
        v.co += v.normal * OUTLINE
    bmesh.ops.reverse_faces(bm, faces=faces)
    for f in faces:
        f.material_index = idx
    bm.to_mesh(me)
    bm.free()


def clean_previous():
    for o in list(bpy.data.objects):
        if o.name.startswith(PREFIX) or o.name == "AK74N" or o.name.startswith("Socket_"):
            bpy.data.objects.remove(o, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)


def build():
    clean_previous()
    parts = tripo_parts()
    coll = bpy.context.scene.collection
    # Source -> project space: muzzle +X -> -Y, right side -Y -> -X (rotate -90 deg about Z), scale to real size.
    fix = Matrix.Rotation(-math.pi / 2, 4, "Z") @ Matrix.Scale(REAL_LENGTH / SOURCE_LENGTH, 4)
    grip_w = fix @ GRIP
    root = bpy.data.objects.new("AK74N", None)
    coll.objects.link(root)
    root.location = grip_w
    counts = {}
    for slot, (_, _, _, budget) in SLOTS.items():
        me = slot_mesh(slot, parts)
        if not me.polygons:
            bpy.data.meshes.remove(me)
            continue
        ob = bpy.data.objects.new(PREFIX + slot, me)
        coll.objects.link(ob)
        decimate(ob, budget)
        pivot = fix @ Vector(PIVOTS.get(slot, tuple(GRIP)))
        ob.data.transform(Matrix.Translation(-pivot) @ fix)
        me = ob.data
        me.name = PREFIX + slot
        for p in me.polygons:
            p.use_smooth = False
        me.materials.clear()
        me.materials.append(material(slot))
        add_outline(me)
        ob.parent = root
        ob.location = pivot - grip_w
        counts[slot] = len(me.polygons) // 2
    for name, p in SOCKETS.items():
        e = bpy.data.objects.new(name, None)
        e.empty_display_size = 0.01
        coll.objects.link(e)
        e.parent = root
        e.location = (fix @ Vector(p)) - grip_w
    for o in parts.values():
        o.hide_viewport = o.hide_render = True
    return counts


# ---------------------------------------------------------------- export (palette textures, one material)
CELL, COLUMNS = 4, 8


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
    albedo, mask, uv_of = [0.0] * (w * h * 4), [0.0] * (w * h * 4), {}
    for i, m in enumerate(materials):
        cx, cy = i % COLUMNS, i // COLUMNS
        col = [_srgb(c) for c in m.diffuse_color[:3]]
        for y in range(cy * CELL, cy * CELL + CELL):
            for x in range(cx * CELL, cx * CELL + CELL):
                k = (y * w + x) * 4
                albedo[k:k + 4] = [col[0], col[1], col[2], 1.0]
                mask[k:k + 4] = [m.metallic, 0.0, 0.0, 1.0 - m.roughness]
        uv_of[m.name] = ((cx + 0.5) * CELL / w, (cy + 0.5) * CELL / h)
    for name, px, alpha in (("T_AK74N_Albedo", albedo, False), ("T_AK74N_MetalSmooth", mask, True)):
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
    return uv_of


def export():
    os.makedirs(OUT_DIR, exist_ok=True)
    root = bpy.data.objects["AK74N"]
    meshes = [o for o in root.children if o.type == "MESH"]
    uv_of = build_palettes(meshes)
    mat = bpy.data.materials.get("M_AK74N") or bpy.data.materials.new("M_AK74N")
    coll = bpy.context.scene.collection
    pairs = []
    for src in meshes:
        name = src.name
        src.name = name + "__src"
        dup = src.copy()
        dup.data = src.data.copy()
        dup.name = dup.data.name = name
        coll.objects.link(dup)
        me = dup.data
        uv = me.uv_layers.new(name="Palette")
        for poly in me.polygons:
            u, v = uv_of[me.materials[poly.material_index].name]
            for li in poly.loop_indices:
                uv.data[li].uv = (u, v)
        me.materials.clear()
        me.materials.append(mat)
        pairs.append((src, dup))
        for child in list(src.children):  # tip sockets travel with the exported copy
            child.parent = dup
        src.parent = None
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for o in root.children_recursive:
        o.select_set(True)
    saved_loc = root.location.copy()
    root.location = (0, 0, 0)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_DIR, "AK74N.fbx"), use_selection=True,
                             object_types={"EMPTY", "MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", bake_anim=False,
                             path_mode="STRIP")
    root.location = saved_loc
    for src, dup in pairs:
        name, me = dup.name, dup.data
        for child in list(dup.children):
            child.parent = src
        bpy.data.objects.remove(dup, do_unlink=True)
        bpy.data.meshes.remove(me)
        src.name = name
        src.parent = root
    return os.path.join(OUT_DIR, "AK74N.fbx")


result = {"tris": build()}
if EXPORT:
    result["fbx"] = export()
