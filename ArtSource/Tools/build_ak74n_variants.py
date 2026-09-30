"""
AK-74N modular variants from the reference sheet (VARIANTES DE COMPONENTES) + suppressor, built procedurally around
the Tripo base parts so every piece sits in place on the same rifle. Run AFTER process_tripo_ak74n.py (same .blend).

Result: children of the AK74N root named "AK74N_<attachmentId>" (ids = the game's AttachmentRules ids), plus the fixed
parts (Receiver, GasBlock, RearSight, Trigger, Safety, ChargingHandle). Muzzle devices and barrels carry a child empty
"Socket_Tip_<id>" at their front face. Space: root-local (grip at the origin), muzzle toward -Y, gun's right side -X,
Z up, bore axis at x = 0, z = BORE_Z. Same style: flat shading, sheet palette, black inverted-hull outline.
"""
import bpy
import bmesh
import math
from mathutils import Vector, Matrix

ROOT = "AK74N"
OUTLINE = 0.0011
BORE_Z = 0.0669
TIP_Y = -0.593           # front face of the standard barrel (where muzzle devices start)
GB_FRONT, FS_REAR = -0.48, -0.5289   # gas block front / front sight rear (exposed barrel tube between them)

PALETTE = {
    "steel": ("#2e3033", 0.8, 0.35), "dark": ("#2a2b2d", 0.8, 0.4), "polymer": ("#2b2c2e", 0.0, 0.8),
    "cover": ("#43464a", 0.6, 0.45), "wood": ("#8e5a2e", 0.0, 0.7), "wood_dark": ("#6e4323", 0.0, 0.7),
    "fde": ("#8b7a5a", 0.0, 0.75), "olive": ("#353a31", 0.0, 0.85), "smoke": ("#8f9a9c", 0.1, 0.25),
    "brass": ("#b08d3c", 0.8, 0.35), "mag_steel": ("#4a4d51", 0.7, 0.4), "tritium": ("#7cff4f", 0.0, 0.3),
    "slot": ("#151617", 0.0, 0.9), "can": ("#232426", 0.3, 0.6), "rail": ("#3b3d40", 0.6, 0.45),
}


def _lin(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]


def mat(key):
    name = "M_AK_" + key
    hexc, metal, rough = PALETTE[key]
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*_lin(hexc), 1)
    m.metallic, m.roughness = metal, rough
    return m


class Part:
    """Accumulates geometry (root-local space) with per-face materials, then becomes one outlined object."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def _mi(self, material):
        if isinstance(material, str):
            material = mat(material)
        if material not in self.mats:
            self.mats.append(material)
        return self.mats.index(material)

    def _tag(self, faces, material):
        i = self._mi(material)
        for f in faces:
            f.material_index = i
        return faces

    # --- primitives -------------------------------------------------------------------------------------------
    def box(self, center, size, material, bevel=0.0):
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = r["verts"]
        sx, sy, sz = size
        for v in verts:
            v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz)) + Vector(center)
        faces = list({f for v in verts for f in v.link_faces})
        if bevel > 0:
            edges = list({e for f in faces for e in f.edges})
            res = bmesh.ops.bevel(self.bm, geom=edges + verts, offset=bevel, segments=1, affect="EDGES")
            faces = list({f for v in verts if v.is_valid for f in v.link_faces} | set(res.get("faces", [])))
        return self._tag(faces, material)

    def lathe(self, profile, material, sides=10, center=(0.0, BORE_Z), cap=True, phase=0.0):
        """profile: [(y, radius), ...] revolved around an axis parallel to Y through (x, z) = center."""
        cx, cz = center
        rings = []
        for y, r in profile:
            rings.append([self.bm.verts.new((cx + math.cos(phase + 2 * math.pi * i / sides) * r, y,
                                             cz + math.sin(phase + 2 * math.pi * i / sides) * r)) for i in range(sides)])
        faces = []
        for a, b in zip(rings, rings[1:]):
            for i in range(sides):
                j = (i + 1) % sides
                faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        if cap:
            faces.append(self.bm.faces.new(list(reversed(rings[0]))))
            faces.append(self.bm.faces.new(rings[-1]))
        return self._tag(faces, material)

    def loft(self, sections, material, cap=True):
        """sections: [(y, [(x, z), ...]), ...] closed polygons with equal vertex counts."""
        rings = [[self.bm.verts.new((x, y, z)) for x, z in poly] for y, poly in sections]
        n = len(rings[0])
        faces = []
        for a, b in zip(rings, rings[1:]):
            for i in range(n):
                j = (i + 1) % n
                faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        if cap:
            faces.append(self.bm.faces.new(list(reversed(rings[0]))))
            faces.append(self.bm.faces.new(rings[-1]))
        return self._tag(faces, material)

    def tube(self, p0, p1, radius, material, sides=6):
        p0, p1 = Vector(p0), Vector(p1)
        d = (p1 - p0)
        q = d.to_track_quat("Z", "Y")
        rings = []
        for p in (p0, p1):
            rings.append([self.bm.verts.new(p + q @ Vector((math.cos(2 * math.pi * i / sides) * radius,
                                                             math.sin(2 * math.pi * i / sides) * radius, 0)))
                          for i in range(sides)])
        faces = [self.bm.faces.new((rings[0][i], rings[0][(i + 1) % sides], rings[1][(i + 1) % sides], rings[1][i]))
                 for i in range(sides)]
        faces.append(self.bm.faces.new(list(reversed(rings[0]))))
        faces.append(self.bm.faces.new(rings[1]))
        return self._tag(faces, material)

    def copy(self, obj_name, deform=None, recolor=None):
        """Copies the body (not the outline hull) of a base part, in root-local space."""
        src = bpy.data.objects[obj_name]
        me = src.data.copy()
        me.transform(src.matrix_basis)  # matrix_local is stale for objects created this run
        tmp = bmesh.new()
        tmp.from_mesh(me)
        outline_idx = [i for i, m in enumerate(me.materials) if m and m.name == "M_Outline"]
        bmesh.ops.delete(tmp, geom=[f for f in tmp.faces if f.material_index in outline_idx], context="FACES")
        remap = {}
        for i, m in enumerate(me.materials):
            if i in outline_idx:
                continue
            target = recolor(m) if recolor else m
            remap[i] = self._mi(target)
        for f in tmp.faces:
            f.material_index = remap.get(f.material_index, 0)
        if deform:
            for v in tmp.verts:
                v.co = deform(v.co.copy())
        tmp_me = bpy.data.meshes.new("_tmp")
        tmp.to_mesh(tmp_me)
        tmp.free()
        self.bm.from_mesh(tmp_me)
        bpy.data.meshes.remove(tmp_me)
        bpy.data.meshes.remove(me)

    # --- finish -----------------------------------------------------------------------------------------------
    def finish(self, root, pivot=(0, 0, 0), sockets=None):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=0.00002)
        bmesh.ops.dissolve_degenerate(self.bm, edges=self.bm.edges, dist=0.00002)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        orient_islands_outward(self.bm)
        pivot = Vector(pivot)
        for v in self.bm.verts:
            v.co -= pivot
        name = "AK74N_" + self.name
        old = bpy.data.objects.get(name)
        if old:
            bpy.data.objects.remove(old, do_unlink=True)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = False
        add_outline(me)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.parent = root
        ob.location = pivot
        for sname, pos in (sockets or {}).items():
            e = bpy.data.objects.new(sname, None)
            e.empty_display_size = 0.01
            bpy.context.scene.collection.objects.link(e)
            e.parent = ob
            e.location = Vector(pos) - pivot
        return ob


def orient_islands_outward(bm):
    bm.faces.ensure_lookup_table()
    total = sum(f.calc_area() for f in bm.faces) or 1.0
    mesh_c = sum((f.calc_center_median() * f.calc_area() for f in bm.faces), Vector()) / total
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
        c = sum((f.calc_center_median() * f.calc_area() for f in island), Vector()) / area
        out = sum(f.normal.dot(f.calc_center_median() - c) * f.calc_area() for f in island)
        if abs(out) / area < 0.0005:
            out = sum(f.normal.dot(f.calc_center_median() - mesh_c) * f.calc_area() for f in island)
        if out < 0:
            bmesh.ops.reverse_faces(bm, faces=island)


def add_outline(me):
    om = bpy.data.materials.get("M_Outline") or bpy.data.materials.new("M_Outline")
    om.diffuse_color = (0, 0, 0, 1)
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


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def slice_stats(obj_name, z, tol=0.004):
    """(min y, max y, max |x|) of a base part's body vertices near height z, in root-local space."""
    o = bpy.data.objects[obj_name]
    n = len(o.data.vertices) // 2  # body first, outline hull duplicated after it
    vs = [o.matrix_basis @ o.data.vertices[i].co for i in range(n)]
    sl = [v for v in vs if abs(v.z - z) < tol] or vs
    return min(v.y for v in sl), max(v.y for v in sl), max(abs(v.x) for v in sl)


# ================================================================================================ base renames
def rename_base(root):
    """Turns the Tripo slot objects into the factory attachments (joining barrel + front sight, both handguards)."""
    def join(names, new):
        objs = [bpy.data.objects[n] for n in names if n in bpy.data.objects]
        if not objs:
            return
        p = Part(new)
        for o in objs:
            p.copy(o.name)
        pivot = objs[0].location.copy()
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
        p.finish(root, pivot)

    join(["AK74N_Barrel", "AK74N_FrontSight"], "ak_barrel_standard")
    join(["AK74N_HandguardLower", "AK74N_HandguardUpper"], "ak_hg_polymer")
    for old, new in (("AK74N_TopCover", "ak_cover_rail"), ("AK74N_Grip", "ak_grip_polymer"),
                     ("AK74N_Stock", "ak_stock_polymer"), ("AK74N_Magazine", "ak_mag_30"),
                     ("AK74N_Muzzle", "ak_muzzle_brake")):
        o = bpy.data.objects.get(old)
        if o:
            o.name = o.data.name = "AK74N_" + new
    for obj, y in (("AK74N_ak_barrel_standard", TIP_Y), ("AK74N_ak_muzzle_brake", -0.6532)):
        o = bpy.data.objects[obj]
        name = "Socket_Tip_" + obj[6:]
        if name not in bpy.data.objects:
            e = bpy.data.objects.new(name, None)
            e.empty_display_size = 0.01
            bpy.context.scene.collection.objects.link(e)
            e.parent = o
            e.location = Vector((0, y, BORE_Z)) - o.location


# ================================================================================================ muzzle devices
def muzzles(root):
    y0 = TIP_Y
    # Flash hider: birdcage with four dark slots.
    p = Part("ak_muzzle_flash_hider")
    p.lathe([(y0, 0.0105), (y0 - 0.008, 0.0105), (y0 - 0.010, 0.0115), (y0 - 0.056, 0.0115), (y0 - 0.060, 0.0100)],
            "dark", sides=10)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        p.box((math.cos(a) * 0.0112, y0 - 0.036, BORE_Z + math.sin(a) * 0.0112), (0.004, 0.034, 0.004), "slot")
    p.finish(root, sockets={"Socket_Tip_ak_muzzle_flash_hider": (0, y0 - 0.060, BORE_Z)})

    # Compensator: squared block with top and side ports.
    p = Part("ak_muzzle_compensator")
    p.lathe([(y0, 0.0105), (y0 - 0.010, 0.0105)], "dark", sides=10)
    p.box((0, y0 - 0.036, BORE_Z + 0.001), (0.026, 0.052, 0.024), "dark", bevel=0.002)
    for k in range(3):
        y = y0 - 0.020 - k * 0.013
        p.box((0, y, BORE_Z + 0.0125), (0.014, 0.006, 0.002), "slot")
        for s in (-1, 1):
            p.box((s * 0.0132, y, BORE_Z + 0.002), (0.002, 0.006, 0.012), "slot")
    p.finish(root, sockets={"Socket_Tip_ak_muzzle_compensator": (0, y0 - 0.062, BORE_Z)})

    # Thread protector: short knurled cap.
    p = Part("ak_muzzle_thread_protector")
    p.lathe([(y0, 0.0092), (y0 - 0.004, 0.0098), (y0 - 0.006, 0.0092), (y0 - 0.010, 0.0098), (y0 - 0.012, 0.0092),
             (y0 - 0.016, 0.0098), (y0 - 0.019, 0.0085)], "dark", sides=12)
    p.finish(root, sockets={"Socket_Tip_ak_muzzle_thread_protector": (0, y0 - 0.019, BORE_Z)})

    # Suppressor (PBS-4 / modern 5.45 can): stepped mount, 38 mm can, grooves, chamfered cap. ~205 mm.
    p = Part("ak_suppressor")
    L = 0.205
    prof = [(y0, 0.0115), (y0 - 0.018, 0.0125), (y0 - 0.020, 0.0175), (y0 - 0.026, 0.019)]
    y = y0 - 0.026
    while y > y0 - L + 0.02:
        prof += [(y - 0.028, 0.019), (y - 0.030, 0.0182), (y - 0.033, 0.0182), (y - 0.035, 0.019)]
        y -= 0.035
    prof += [(y0 - L + 0.006, 0.019), (y0 - L, 0.015), (y0 - L - 0.001, 0.006)]
    p.lathe(prof, "can", sides=14)
    p.lathe([(y0 - L - 0.0005, 0.005), (y0 - L - 0.0015, 0.005)], "slot", sides=8)
    p.finish(root, sockets={"Socket_Tip_ak_suppressor": (0, y0 - L - 0.001, BORE_Z)})


# ================================================================================================ barrels
def barrels(root):
    base = "AK74N_ak_barrel_standard"
    # Long (RPK-74 style, +105 mm): stretch the exposed tube between gas block and front sight.
    ext = 0.105

    def long_def(v):
        if v.y < FS_REAR:
            v.y -= ext
        elif v.y < GB_FRONT:
            v.y = GB_FRONT + (v.y - GB_FRONT) * (FS_REAR - ext - GB_FRONT) / (FS_REAR - GB_FRONT)
        return v
    p = Part("ak_barrel_long")
    p.copy(base, deform=long_def)
    p.finish(root, sockets={"Socket_Tip_ak_barrel_long": (0, TIP_Y - ext, BORE_Z)})

    # Short threaded (AK-105 style): front sight moves back onto the gas block, exposed thread rings at the tip.
    cut = FS_REAR - GB_FRONT  # negative

    def short_def(v):
        if v.y < FS_REAR:
            v.y -= cut
        elif v.y < GB_FRONT:
            v.y = GB_FRONT
        return v
    p = Part("ak_barrel_threaded")
    p.copy(base, deform=short_def)
    t0 = TIP_Y - cut
    p.lathe([(t0, 0.0090), (t0 - 0.003, 0.0090), (t0 - 0.004, 0.0084), (t0 - 0.006, 0.0090), (t0 - 0.007, 0.0084),
             (t0 - 0.009, 0.0090), (t0 - 0.010, 0.0084), (t0 - 0.012, 0.0088)], "steel", sides=10)
    p.finish(root, sockets={"Socket_Tip_ak_barrel_threaded": (0, t0, BORE_Z)})

    # Competition: fluted heavy sleeve over the exposed tube, bronze-tone front sight band.
    p = Part("ak_barrel_competition")
    p.copy(base)
    p.lathe([(GB_FRONT + 0.001, 0.0118), (FS_REAR - 0.001, 0.0118)], "steel", sides=12)
    for k in range(6):
        a = k * math.pi / 3
        p.box((math.cos(a) * 0.0118, (GB_FRONT + FS_REAR) / 2, BORE_Z + math.sin(a) * 0.0118),
              (0.0025, (GB_FRONT - FS_REAR) - 0.008, 0.0025), "slot")
    p.finish(root, sockets={"Socket_Tip_ak_barrel_competition": (0, TIP_Y, BORE_Z)})

    # Night: standard barrel with tritium inserts on the front sight post and its ears.
    p = Part("ak_barrel_night")
    p.copy(base)
    p.box((0, -0.5485, 0.1235), (0.0035, 0.004, 0.005), "tritium")
    for s in (-1, 1):
        p.box((s * 0.0105, -0.5485, 0.1165), (0.003, 0.004, 0.004), "tritium")
    p.finish(root, sockets={"Socket_Tip_ak_barrel_night": (0, TIP_Y, BORE_Z)})


# ================================================================================================ handguards
HG_Y0, HG_Y1 = -0.2025, -0.388  # rear / front of the lower handguard


def handguards(root):
    p = Part("ak_hg_wood")
    p.copy("AK74N_ak_hg_polymer", recolor=lambda m: mat("wood"))
    p.finish(root)

    # Tactical M-LOK tube with full-length top Picatinny rail.
    p = Part("ak_hg_tactical")
    oct_ = [(0.034, 0.045), (0.034, 0.095), (0.024, 0.112), (-0.024, 0.112), (-0.034, 0.095), (-0.034, 0.045),
            (-0.022, 0.028), (0.022, 0.028)]
    p.loft([(HG_Y0 - 0.004, oct_), (HG_Y1, oct_)], "polymer")
    rail(p, HG_Y0 - 0.006, HG_Y1 + 0.002, 0.112)
    for s in (-1, 1):
        for k in range(4):
            y = HG_Y0 - 0.025 - k * 0.042
            p.box((s * 0.0345, y, 0.070), (0.002, 0.030, 0.008), "slot")
    for k in range(3):
        p.box((0, HG_Y0 - 0.035 - k * 0.05, 0.0275), (0.008, 0.032, 0.002), "slot")
    p.finish(root)

    # Polymer lower + railed gas-tube cover (UltiMAK style) replacing the upper handguard.
    p = Part("ak_hg_optic")
    hg = bpy.data.objects["AK74N_ak_hg_polymer"]
    p.copy(hg.name, deform=None)
    # drop the original upper handguard faces: anything above the gas-tube line inside the upper span
    bm = p.bm
    kill = [f for f in bm.faces if f.calc_center_median().z > 0.092 and f.calc_center_median().y < -0.276]
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    up = [(0.016, 0.086), (0.018, 0.100), (0.016, 0.113), (-0.016, 0.113), (-0.018, 0.100), (-0.016, 0.086)]
    p.loft([(-0.279, up), (-0.386, up)], "rail")
    rail(p, -0.281, -0.385, 0.113, width=0.021)
    p.finish(root)


def rail(p, y0, y1, z, width=0.021, material="rail"):
    """Picatinny: base strip + transverse teeth every 10 mm."""
    p.box((0, (y0 + y1) / 2, z + 0.002), (width, abs(y1 - y0), 0.004), material)
    y = y0 - 0.004
    while y > y1 + 0.004:
        p.box((0, y, z + 0.0055), (width, 0.0052, 0.003), material)
        y -= 0.010


# ================================================================================================ dust covers
CV_REAR, CV_FRONT = 0.045, -0.205
COVER_PIVOT = None


def cover_section(top=0.1085, shoulder=0.100, half=0.0322, bottom=0.068, faceted=False):
    if faceted:
        return [(half, bottom), (half, shoulder - 0.004), (half * 0.55, top), (-half * 0.55, top),
                (-half, shoulder - 0.004), (-half, bottom)]
    return [(half, bottom), (half, shoulder - 0.006), (half * 0.9, shoulder), (half * 0.68, top - 0.003),
            (half * 0.37, top), (-half * 0.37, top), (-half * 0.68, top - 0.003), (-half * 0.9, shoulder),
            (-half, shoulder - 0.006), (-half, bottom)]


def cover_body(p, material="cover", faceted=False):
    rear_lo = cover_section(top=0.1, shoulder=0.094, faceted=faceted)
    mid = cover_section(faceted=faceted)
    front = cover_section(top=0.104, shoulder=0.097, faceted=faceted)
    p.loft([(CV_REAR, rear_lo), (CV_REAR - 0.02, mid), (CV_FRONT + 0.012, mid), (CV_FRONT, front)], material)
    # recoil-spring button poking through the rear
    p.lathe([(CV_REAR + 0.004, 0.006), (CV_REAR - 0.002, 0.006)], "steel", sides=8, center=(0, 0.086))


def covers(root):
    pivot = bpy.data.objects["AK74N_ak_cover_rail"].location.copy()
    # Standard: stamped cover with three lateral stiffening ribs.
    p = Part("ak_cover_standard")
    cover_body(p)
    for s in (-1, 1):
        for z in (0.077, 0.087):
            p.box((s * 0.0327, (CV_REAR + CV_FRONT) / 2 - 0.01, z), (0.0016, 0.19, 0.0035), "cover")
    p.finish(root, pivot)

    # Serrated tactical: anti-glare serrations along the top.
    p = Part("ak_cover_serrated")
    cover_body(p)
    y = CV_REAR - 0.03
    while y > CV_FRONT + 0.02:
        p.box((0, y, 0.1088), (0.024, 0.003, 0.0022), "slot")
        y -= 0.008
    p.finish(root, pivot)

    # Light: lightening windows (dark insets) and a thinner rear.
    p = Part("ak_cover_light")
    cover_body(p)
    for s in (-1, 1):
        for k in range(3):
            p.box((s * 0.0325, 0.0 - k * 0.06, 0.084), (0.002, 0.040, 0.016), "slot")
    for k in range(4):
        p.box((0, -0.01 - k * 0.045, 0.1087), (0.018, 0.028, 0.002), "slot")
    p.finish(root, pivot)

    # Modern: faceted cover with short rear rail and hex bolt heads.
    p = Part("ak_cover_modern")
    cover_body(p, faceted=True)
    rail(p, CV_REAR - 0.02, CV_REAR - 0.13, 0.1085, width=0.02)
    for s in (-1, 1):
        for y in (0.02, -0.18):
            p.lathe([(y, 0.0035), (y - 0.001, 0.0035)], "steel", sides=6, center=(s * 0.0335, 0.08))
    p.finish(root, pivot)


# ================================================================================================ grips
def grips(root):
    base = "AK74N_ak_grip_polymer"
    p = Part("ak_grip_wood")
    p.copy(base, recolor=lambda m: mat("wood"))
    p.finish(root)

    # Textured: olive polymer with stippled side panels.
    p = Part("ak_grip_textured")
    p.copy(base, recolor=lambda m: mat("olive"))
    for i in range(4):
        z = -0.062 + i * 0.017
        ymin, ymax, xmax = slice_stats(base, z)
        for j in range(3):
            y = ymin + (ymax - ymin) * (0.3 + 0.2 * j)
            for s in (-1, 1):
                p.box((s * (xmax - 0.0005), y, z), (0.003, 0.0045, 0.0045), "olive")
    p.finish(root)

    # Ergonomic: palm swell, flared base, FDE.
    def ergo(v):
        t = smooth01(1 - abs(v.z + 0.03) / 0.05)
        v.x *= 1.0 + 0.22 * t
        if v.z < -0.07:
            v.y += (v.z + 0.07) * 0.5
        return v
    p = Part("ak_grip_ergo")
    p.copy(base, deform=ergo, recolor=lambda m: mat("fde"))
    p.finish(root)


# ================================================================================================ stocks
def stocks(root):
    base = "AK74N_ak_stock_polymer"
    p = Part("ak_stock_wood")
    p.copy(base, recolor=lambda m: mat("wood"))
    p.finish(root)

    # Side-folding wire stock (AKS-74 style): hinge block, two struts, curved butt plate.
    p = Part("ak_stock_wire")
    p.box((0, 0.052, 0.035), (0.05, 0.028, 0.07), "steel", bevel=0.002)          # rear trunnion / hinge block
    p.lathe([(0.04, 0.007), (0.066, 0.007)], "steel", sides=8, center=(-0.028, 0.045))  # hinge knuckle
    top0, top1 = Vector((0, 0.066, 0.064)), Vector((0, 0.262, 0.045))
    bot0, bot1 = Vector((0, 0.066, 0.006)), Vector((0, 0.262, -0.028))
    for s in (-1, 1):
        o = Vector((s * 0.011, 0, 0))
        p.tube(top0 + o, top1 + o, 0.0045, "steel")
        p.tube(bot0 + o, top1 + o * 0.6 + Vector((0, 0, -0.04)), 0.0045, "steel")
    p.tube(Vector((0, 0.19, 0.049)), Vector((0, 0.19, -0.01)), 0.004, "steel")     # cross brace
    plate = [(0.016, -0.045), (0.018, 0.0), (0.016, 0.055), (-0.016, 0.055), (-0.018, 0.0), (-0.016, -0.045)]
    p.loft([(0.262, plate), (0.276, [(x, z - 0.004) for x, z in plate])], "steel")
    p.finish(root)

    # Telescopic (AR buffer tube adapter + collapsible stock).
    p = Part("ak_stock_tele")
    p.box((0, 0.058, 0.04), (0.046, 0.036, 0.062), "polymer", bevel=0.003)          # adapter
    p.lathe([(0.07, 0.0155), (0.24, 0.0155), (0.245, 0.013)], "steel", sides=10, center=(0, 0.062))
    body = [(0.022, -0.035), (0.024, 0.055), (0.018, 0.088), (-0.018, 0.088), (-0.024, 0.055), (-0.022, -0.035)]
    nose = [(0.02, 0.03), (0.022, 0.058), (0.017, 0.082), (-0.017, 0.082), (-0.022, 0.058), (-0.02, 0.03)]
    p.loft([(0.165, nose), (0.215, body), (0.278, body)], "polymer")
    p.box((0, 0.281, 0.026), (0.042, 0.008, 0.12), "slot")                        # rubber butt pad
    p.box((0, 0.2, -0.004), (0.026, 0.05, 0.006), "slot")                         # adjustment lever
    p.finish(root)


# ================================================================================================ magazines
def magazines(root):
    base = bpy.data.objects["AK74N_ak_mag_30"]
    pivot = base.location.copy()
    d = Vector((0, -0.42, -0.91)).normalized()

    def extend(v):
        t = smooth01((-0.02 - v.z) / 0.06)
        return v + d * 0.055 * t
    p = Part("ak_mag_45")
    p.copy(base.name, deform=extend)
    p.finish(root, pivot)

    p = Part("ak_mag_clear")
    p.copy(base.name, recolor=lambda m: mat("smoke"))
    for k in range(3):  # cartridges visible at the feed lips
        p.box((0.0, -0.172 - k * 0.004, 0.030 - k * 0.007), (0.009, 0.040, 0.007), "brass")
    p.finish(root, pivot)

    p = Part("ak_mag_steel")
    p.copy(base.name, recolor=lambda m: mat("mag_steel"))
    p.finish(root, pivot)


def build():
    root = bpy.data.objects[ROOT]
    rename_base(root)
    muzzles(root)
    barrels(root)
    handguards(root)
    covers(root)
    grips(root)
    stocks(root)
    magazines(root)
    tris = {}
    for o in root.children:
        if o.type == "MESH":
            tris[o.name[6:]] = len(o.data.polygons) // 2
    return tris


result = {"parts": build()}
