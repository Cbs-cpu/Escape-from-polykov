"""
Zona A props generated with Tripo H3.1 from crops of the concept sheets (ArtSource/Reference/ZoneA/crops) and sent
through the Blender bridge into ArtSource/Maps/ZoneA_Props_Source.blend as "SRC_<name>" roots with tripo parts.
Per prop: keep the parts that belong to it (the crops also contain crates / barrels), join, decimate to a budget,
flat low-poly palette (large parts = body colour, small parts = detail colour), black inverted-hull outline, scale to
real size with the origin at the base centre, and export FBX to Assets/_Project/Art/Environment/ZoneA/Props.
Leftovers that make good props on their own (the lathe crop's barrel cluster) are exported too.
"""
import bpy
import bmesh
import os
from mathutils import Vector, Matrix

ROOT = os.environ.get("POLYKOV_ROOT", r"C:\Users\Cobos\Escape from Polykov")
OUT = os.path.join(ROOT, r"Assets\_Project\Art\Environment\ZoneA\Props")
OUTLINE = 0.02

PALETTE = {"machine": "#56654a", "machine_dark": "#3f4a37", "steel": "#4a4f53", "rust": "#7b4a2e", "barrel_green": "#4c5a3c"}


def part_bounds(o):
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return Vector([min(v[i] for v in bb) for i in range(3)]), Vector([max(v[i] for v in bb) for i in range(3)])


# prop -> (source root, keep(min, max) predicate, real height or length, measure axis, body colour, detail colour, tris)
PROPS = {
    "Press": ("SRC_Press", lambda mn, mx: not (mx.y <= -0.13 or (mn.x >= 0.14 and mx.z < 0.2) or mx.z < 0.06),
              6.6, "z", "machine", "steel", 7000),
    "Lathe": ("SRC_Lathe", lambda mn, mx: mx.y > 0.0, 7.0, "x", "machine", "steel", 6000),
    "BarrelCluster": ("SRC_Lathe", lambda mn, mx: mx.y <= 0.0, 0.95, "z", "barrel_green", "rust", 2500),
}


def mat(key):
    name = "M_ZoneProp_" + key
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        h = PALETTE[key].lstrip("#")
        c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        m.diffuse_color = (*[x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c], 1)
        m.roughness = 0.85
    return m


def outline_mat():
    m = bpy.data.materials.get("M_Outline") or bpy.data.materials.new("M_Outline")
    m.diffuse_color = (0, 0, 0, 1)
    return m


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


def build(name, spec):
    src, keep, size, axis, body, detail, budget = spec
    root = bpy.data.objects[src]
    parts = [o for o in root.children if o.type == "MESH" and keep(*part_bounds(o))]
    vols = {o: (lambda b: (b[1] - b[0]).x * (b[1] - b[0]).y * (b[1] - b[0]).z)(part_bounds(o)) for o in parts}
    big = max(vols.values())
    bm = bmesh.new()
    mats = [mat(body), mat(detail), outline_mat()]
    for o in parts:
        me = o.data.copy()
        me.transform(o.matrix_world)
        start = len(bm.faces)
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        bm.faces.ensure_lookup_table()
        idx = 0 if vols[o] > big * 0.02 else 1
        for f in bm.faces[start:]:
            f.material_index = idx
    me = bpy.data.meshes.new("ZA_" + name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    old = bpy.data.objects.get("ZA_" + name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    ob = bpy.data.objects.new("ZA_" + name, me)
    bpy.context.scene.collection.objects.link(ob)
    # decimate
    bpy.context.view_layer.objects.active = ob
    mod = ob.modifiers.new("D", "DECIMATE")
    mod.ratio = min(1.0, budget / max(1, sum(len(p.vertices) - 2 for p in me.polygons)))
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="D")
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    orient_islands_outward(bm)
    # scale to real size, origin at base centre, Tripo +Y forward kept
    lo = Vector([min(v.co[i] for v in bm.verts) for i in range(3)])
    hi = Vector([max(v.co[i] for v in bm.verts) for i in range(3)])
    k = size / max(1e-6, (hi - lo)["xyz".index(axis)])
    base = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for v in bm.verts:
        v.co = (v.co - base) * k
    # inverted hull outline
    bm.normal_update()
    ret = bmesh.ops.duplicate(bm, geom=list(bm.faces))
    faces = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMFace)]
    verts = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
    bm.normal_update()
    for v in verts:
        v.co += v.normal * OUTLINE
    bmesh.ops.reverse_faces(bm, faces=faces)
    for f in faces:
        f.material_index = 2
    bm.to_mesh(ob.data)
    bm.free()
    for p in ob.data.polygons:
        p.use_smooth = False
    ob.location = (0, 0, 0)
    return ob, len(ob.data.polygons) // 2, [round(x * k, 2) for x in (hi - lo)]


def export(ob):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, ob.name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", bake_anim=False, path_mode="STRIP")


def run():
    report = {}
    x = 0.0
    for name, spec in PROPS.items():
        ob, tris, dims = build(name, spec)
        export(ob)
        ob.location = (x, 12, 0)
        x += dims[0] + 2
        report[name] = {"tris": tris, "size_m": dims}
    for r in ("SRC_Press", "SRC_Lathe"):
        o = bpy.data.objects.get(r)
        if o:
            for c in o.children:
                c.hide_viewport = c.hide_render = True
    bpy.ops.wm.save_mainfile()
    return report


result = run()
