"""
Zona A (Soviet industrial complex, first playable zone) - layout blockout from the concept sheets in
ArtSource/Reference/ZoneA (overview "Entorno conceptual: complejo industrial soviético Zona A" + phases 2-5).
Metres, X east, Y north, Z up; the yard centre is the origin. Everything is a flat-shaded low-poly block with the
game's black inverted-hull outline, grouped per landmark in collections so each can be refined / replaced by Tripo
assets later without moving the layout. Creates ArtSource/Maps/ZoneA_Source.blend.
Run: blender -b -P build_zone_a.py   (or exec() through the Blender MCP)
"""
import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix

ROOT = os.environ.get("POLYKOV_ROOT", r"C:\Users\Cobos\Escape from Polykov")
OUT = os.path.join(ROOT, "ArtSource", "Maps", "ZoneA_Source.blend")
OUTLINE = 0.05

PALETTE = {
    "concrete": "#8b908d", "concrete_dark": "#6a706e", "concrete_light": "#a3a7a3", "brick": "#8c5039",
    "rust": "#7b4a2e", "corrugated": "#6f7876", "roof": "#596160", "steel": "#3b4044", "window": "#27302f",
    "grass": "#56673a", "asphalt": "#4b4f50", "hazard": "#c9a227", "tank": "#9aa09d", "boxcar": "#6e3a2b",
    "wagon_green": "#4c5a3c", "grate": "#4a3a2e", "wood": "#7d6444", "door": "#474d4e", "helipad": "#c8643c", "white": "#d9d6cc", "moss": "#4f6135",
    "ground": "#6f6f66",
}
_mats = {}


def mat(key):
    if key in _mats:
        return _mats[key]
    h = PALETTE[key].lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    c = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    m = bpy.data.materials.new("M_Zone_" + key)
    m.diffuse_color = (*c, 1)
    m.roughness = 0.9
    _mats[key] = m
    return m


def outline_mat():
    m = bpy.data.materials.get("M_Outline") or bpy.data.materials.new("M_Outline")
    m.diffuse_color = (0, 0, 0, 1)
    return m


_coll_stack = []


def coll(name):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(c)
    return c


def _link(ob, collection):
    collection.objects.link(ob)
    return ob


def _finish(name, bm, material, collection, outline=True):
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    if outline:
        me_mats = [material, outline_mat()]
        ret = bmesh.ops.duplicate(bm, geom=list(bm.faces))
        faces = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMFace)]
        verts = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
        bm.normal_update()
        for v in verts:
            v.co += v.normal * OUTLINE
        bmesh.ops.reverse_faces(bm, faces=faces)
        for f in faces:
            f.material_index = 1
    else:
        me_mats = [material]
    bm.to_mesh(me)
    bm.free()
    for m in me_mats:
        me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = False
    return _link(bpy.data.objects.new(name, me), collection)


def box(name, collection, center, size, key, yaw=0.0, outline=True):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(yaw), 3, "Z"))
    ob = _finish(name, bm, mat(key), collection, outline)
    ob.location = Vector(center)  # origin at the block centre: later rotations (roof slopes, ramps) pivot in place
    return ob


def cyl(name, collection, base, radius, height, key, sides=12, top_radius=None, axis="Z", yaw=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=sides, radius1=radius,
                          radius2=radius if top_radius is None else top_radius, depth=height)
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((0, 0, height / 2)))
    if axis == "X":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"))
    elif axis == "Y":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(-math.pi / 2, 3, "X"))
    if yaw:
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(yaw), 3, "Z"))
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(base))
    return _finish(name, bm, mat(key), collection)


def windows_x(collection, x0, x1, y, z0, z1, count, rows=1, row_gap=0.6, key="window", facing=-1):
    """Band of window panes on a facade parallel to X (facing -Y by default)."""
    w = (x1 - x0) / count
    h = (z1 - z0 - row_gap * (rows - 1)) / rows
    for r in range(rows):
        z = z0 + r * (h + row_gap) + h / 2
        for i in range(count):
            box("Win", collection, (x0 + w * (i + 0.5), y + facing * 0.06, z), (w * 0.82, 0.12, h), key, outline=False)


def windows_y(collection, y0, y1, x, z0, z1, count, rows=1, row_gap=0.6, key="window", facing=-1):
    w = (y1 - y0) / count
    h = (z1 - z0 - row_gap * (rows - 1)) / rows
    for r in range(rows):
        z = z0 + r * (h + row_gap) + h / 2
        for i in range(count):
            box("Win", collection, (x + facing * 0.06, y0 + w * (i + 0.5), z), (0.12, w * 0.82, h), key, outline=False)


def stairs(name, collection, start, yaw, rise, width=1.2, key="steel"):
    steps = max(1, int(rise / 0.2))
    h, d = rise / steps, 0.26
    rot = Matrix.Rotation(math.radians(yaw), 3, "Z")
    for i in range(steps):
        c = Vector(start) + rot @ Vector((0, d * (i + 0.5), h * (i + 0.5)))
        box(name, collection, c, (width, d, h), key, yaw, outline=False)
    run = d * steps
    for s in (-1, 1):  # stringers + rails
        a = Vector(start) + rot @ Vector((s * width / 2, 0, 0))
        b = Vector(start) + rot @ Vector((s * width / 2, run, rise))
        beam(name + "_Rail", collection, a + Vector((0, 0, 1.0)), b + Vector((0, 0, 1.0)), 0.05, "rust")


def beam(name, collection, a, b, r, key):
    a, b = Vector(a), Vector(b)
    d = b - a
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * r * 2, v.co.y * r * 2, v.co.z * d.length))
    q = d.to_track_quat("Z", "Y")
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=q.to_matrix())
    bmesh.ops.translate(bm, verts=bm.verts, vec=(a + b) / 2)
    return _finish(name, bm, mat(key), collection, outline=False)


def truss(name, collection, a, b, height, width, key="rust"):
    """Walkway truss bridge from a to b (deck at a/b height) with side rails and diagonal bracing."""
    a, b = Vector(a), Vector(b)
    d = b - a
    yaw = math.degrees(math.atan2(-d.x, d.y))
    n = max(2, int(d.length / 3))
    side = Vector((-d.y, d.x, 0)).normalized() * (width / 2)
    box(name + "_Deck", collection, (a + b) / 2, (width, d.length, 0.25), "steel", yaw)
    for s in (-1, 1):
        o = side * s
        beam(name, collection, a + o + Vector((0, 0, height)), b + o + Vector((0, 0, height)), 0.12, key)
        for i in range(n + 1):
            p = a + d * (i / n) + o
            beam(name, collection, p, p + Vector((0, 0, height)), 0.08, key)
            if i < n:
                q = a + d * ((i + 1) / n) + o
                beam(name, collection, p, q + Vector((0, 0, height)), 0.06, key)


def trestle(name, collection, top, height, size=1.2, key="steel"):
    x, y, z = top
    for dx in (-size / 2, size / 2):
        for dy in (-size / 2, size / 2):
            beam(name, collection, (x + dx, y + dy, z - height), (x + dx, y + dy, z), 0.12, key)
    for k in range(int(height // 3)):
        zz = z - height + 1.5 + k * 3
        beam(name, collection, (x - size / 2, y - size / 2, zz), (x + size / 2, y + size / 2, zz + 2.5), 0.05, key)


def pipe_run(name, collection, points, r, key="tank"):
    for p, q in zip(points, points[1:]):
        p, q = Vector(p), Vector(q)
        d = q - p
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=r, radius2=r, depth=d.length)
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=d.to_track_quat("Z", "Y").to_matrix())
        bmesh.ops.translate(bm, verts=bm.verts, vec=(p + q) / 2)
        _finish(name, bm, mat(key), collection)
    for p in points[1:-1]:
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r * 1.05)
        bmesh.ops.translate(bm, verts=bm.verts, vec=Vector(p))
        _finish(name + "_Elbow", bm, mat(key), collection)


# ============================================================================================ landmarks
def ground():
    c = coll("Ground")
    box("Ground", c, (10, 10, -0.5), (200, 140, 1.0), "ground", outline=False)
    box("Yard", c, (5, -12, 0.02), (130, 46, 0.04), "asphalt", outline=False)
    for (x, y, sx, sy) in ((-60, -38, 30, 10), (85, 55, 22, 18), (-72, 30, 14, 30), (40, -40, 20, 6)):
        box("Grass", c, (x, y, 0.03), (sx, sy, 0.06), "grass", outline=False)
    # cracked slabs / debris
    for i, (x, y) in enumerate(((0, -5), (12, -20), (-8, -14), (25, 2), (-20, 0), (30, -25))):
        box("Slab", c, (x, y, 0.1), (2.2, 1.4, 0.2), "concrete_dark", yaw=i * 37)


def perimeter():
    c = coll("Perimeter")
    x0, x1, y0, y1, h = -85, 105, -48, 78, 3.5
    for a, b in (((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))):
        a, b = Vector((*a, 0)), Vector((*b, 0))
        d = b - a
        yaw = math.degrees(math.atan2(-d.x, d.y))
        box("Wall", c, (a + b) / 2 + Vector((0, 0, h / 2)), (0.5, d.length, h), "concrete", yaw)


HALL = dict(x0=-24.0, x1=24.0, y0=0.0, y1=56.0, eave=20.0, ridge=26.5)


def production_hall():
    """
    Main production floor (overview + phase 2 sheet). Gabled hall, ridge along Y; the yard facade (y0) has the big
    framed gate, the gantry (maintenance walkway) runs along the west wall at 6 m with grating stairs down at the north
    end, a rusty overhead crane crosses the hall on rails, dismantled machinery / crates / barrels form the cover.
    """
    c = coll("ProductionHall")
    x0, x1, y0, y1, eave, ridge = HALL["x0"], HALL["x1"], HALL["y0"], HALL["y1"], HALL["eave"], HALL["ridge"]
    L, W, t = y1 - y0, x1 - x0, 0.6
    gx0, gx1, gh = -9.5, 9.5, 16.0              # gate opening

    # --- walls
    box("S_L", c, ((x0 + gx0) / 2, y0, eave / 2), (gx0 - x0, t, eave), "concrete")
    box("S_R", c, ((gx1 + x1) / 2, y0, eave / 2), (x1 - gx1, t, eave), "concrete")
    box("S_Top", c, (0, y0, (gh + eave) / 2), (gx1 - gx0, t, eave - gh), "concrete")
    box("N", c, (0, y1, eave / 2), (W, t, eave), "concrete")
    box("W_Wall", c, (x0, (y0 + y1) / 2, eave / 2), (t, L, eave), "concrete")
    box("E_Wall", c, (x1, (y0 + y1) / 2, eave / 2), (t, L, eave), "concrete")
    for y in (y0, y1):  # gable triangles
        bm = bmesh.new()
        vs = [bm.verts.new(v) for v in ((x0, y - t / 2, eave), (x1, y - t / 2, eave), (0, y - t / 2, ridge),
                                        (x0, y + t / 2, eave), (x1, y + t / 2, eave), (0, y + t / 2, ridge))]
        for f in ((0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)):
            bm.faces.new([vs[i] for i in f])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        _finish("Gable", bm, mat("concrete"), c)

    # --- facade: pilasters, cornice, the protruding gate portal (overview: steel-framed portal, dark interior)
    for x in (x0, -16, gx0 - 0.9, gx1 + 0.9, 16, x1):
        box("Pilaster", c, (x, y0 - 0.7, eave / 2 + 0.4), (1.6, 1.4, eave + 0.8), "concrete_light")
    box("Cornice", c, (0, y0 - 0.6, eave + 0.5), (W + 2, 1.6, 1.0), "concrete_light")
    box("PortalTop", c, (0, y0 - 1.6, gh + 0.7), (gx1 - gx0 + 3.6, 3.2, 1.4), "concrete_dark")
    for x in (gx0 - 1.2, gx1 + 1.2):
        box("PortalLeg", c, (x, y0 - 1.6, gh / 2), (1.2, 3.2, gh), "concrete_dark")
    box("GateRoll", c, (0, y0 + 0.3, gh - 0.7), (gx1 - gx0, 0.8, 1.4), "rust")
    # window grids: facade either side of the gate, two tiers
    for xa, xb in ((x0 + 1.5, -16.8), (-15.2, gx0 - 2.2), (gx1 + 2.2, 15.2), (16.8, x1 - 1.5)):
        windows_x(c, xa, xb, y0 - 0.35, 2.5, 6.5, 3, rows=1)
        windows_x(c, xa, xb, y0 - 0.35, 8.5, 18.6, 3, rows=3, row_gap=0.4)
    # side walls: high band (the interior sheet's upper windows), outside and inside faces
    windows_y(c, y0 + 2, y1 - 2, x1 + 0.35, 12.0, 18.2, 12, rows=2, row_gap=0.4, facing=1)
    windows_y(c, y0 + 2, y1 - 2, x0 - 0.35, 12.0, 18.2, 12, rows=2, row_gap=0.4)
    windows_y(c, y0 + 2, y1 - 2, x0 + 0.35, 12.0, 18.2, 12, rows=2, row_gap=0.4, facing=1)
    windows_y(c, y0 + 2, y1 - 2, x1 - 0.35, 12.0, 18.2, 12, rows=2, row_gap=0.4)
    windows_x(c, x0 + 3, x1 - 3, y1 - 0.35, 12.0, 18.2, 8, rows=2, row_gap=0.4)

    # --- roof: two slopes with skylight panes, steel trusses inside
    slope = math.atan2(ridge - eave, W / 2)
    span = (W / 2 + 0.8) / math.cos(slope)
    for sgn in (-1, 1):
        r = box("Roof", c, (sgn * W / 4, (y0 + y1) / 2, (eave + ridge) / 2 + 0.3), (span, L + 1.2, 0.35), "roof")
        r.rotation_euler.y = -sgn * slope
        for k in range(5):
            sk = box("Skylight", c, (sgn * W / 4, y0 + 6 + k * 10.5, (eave + ridge) / 2 + 0.52), (5.2, 3.2, 0.06),
                     "window", outline=False)
            sk.rotation_euler.y = -sgn * slope
    for y in [y0 + 4 + 6 * k for k in range(9)]:  # trusses: bottom chord, rafters, king post, diagonals
        beam("Truss", c, (x0, y, eave - 0.3), (x1, y, eave - 0.3), 0.14, "steel")
        beam("Truss", c, (x0, y, eave - 0.2), (0, y, ridge - 0.4), 0.14, "steel")
        beam("Truss", c, (x1, y, eave - 0.2), (0, y, ridge - 0.4), 0.14, "steel")
        beam("Truss", c, (0, y, eave - 0.3), (0, y, ridge - 0.4), 0.1, "steel")
        for xx in (-12, 12):
            beam("Truss", c, (xx, y, eave - 0.3), (xx * 0.5, y, eave + (ridge - eave) * 0.5), 0.07, "steel")

    # --- columns + crane runway + overhead crane (phase 2: rusty girder across the upper hall)
    crane_z = 15.5
    for y in [y0 + 4 + 6 * k for k in range(9)]:
        for x in (x0 + 0.8, x1 - 0.8):
            box("Column", c, (x, y, eave / 2), (0.6, 0.6, eave), "steel")
            box("Corbel", c, (x - math.copysign(0.7, x), y, crane_z - 0.5), (0.9, 0.6, 0.6), "steel", outline=False)
    for x in (x0 + 1.6, x1 - 1.6):
        box("CraneRail", c, (x, (y0 + y1) / 2, crane_z), (0.5, L - 2, 0.5), "steel")
    cy = 24.0
    box("CraneGirder", c, (0, cy, crane_z + 0.9), (W - 2.6, 1.3, 1.3), "rust")
    box("CraneGirder2", c, (0, cy + 1.8, crane_z + 0.9), (W - 2.6, 0.7, 1.1), "rust")
    for x in (x0 + 1.6, x1 - 1.6):
        box("CraneEndTruck", c, (x, cy + 0.9, crane_z + 0.6), (1.2, 4.2, 0.9), "rust")
    box("CraneTrolley", c, (6, cy + 0.9, crane_z + 1.9), (2.4, 3.0, 1.1), "hazard")
    beam("CraneCable", c, (6, cy + 0.9, crane_z + 1.3), (6, cy + 0.9, 5.2), 0.03, "steel")
    box("CraneHook", c, (6, cy + 0.9, 5.0), (0.4, 0.2, 0.6), "steel")

    # --- maintenance gantry along the east wall at 6 m + north return, grating stairs down (phase 2 camera spot:
    # looking south toward the gate the gantry is on the left)
    gz, gw = 7.5, 2.6
    gx = x1 - 0.3 - gw / 2
    box("Gantry_E", c, (gx, (y0 + 8 + y1) / 2, gz), (gw, L - 8, 0.2), "grate")
    box("Gantry_N", c, (x1 - 10, y1 - 0.3 - gw / 2, gz), (20, gw, 0.2), "grate")
    edge = x1 - 0.3 - gw
    for y in [y0 + 8 + 3 * k for k in range(17)]:
        beam("GantryPost", c, (edge, y, gz), (edge, y, gz + 1.1), 0.04, "rust")
        beam("GantryBracket", c, (x1 - 0.3, y, gz - 1.0), (edge, y, gz - 0.1), 0.06, "steel")
    beam("GantryRail", c, (edge, y0 + 8, gz + 1.1), (edge, y1 - gw, gz + 1.1), 0.04, "rust")
    beam("GantryRail", c, (edge, y0 + 8, gz + 0.55), (edge, y1 - gw, gz + 0.55), 0.03, "rust")
    beam("GantryRail", c, (edge, y1 - gw, gz + 1.1), (x1 - 20, y1 - gw, gz + 1.1), 0.04, "rust")
    stairs("GantryStairs", c, (x1 - 19.2, y1 - 0.3 - gw - 7.8, 0), 0, gz, width=1.6, key="grate")  # top meets the north gantry

    # --- floor: slab, painted lanes, cracks
    box("Floor", c, (0, (y0 + y1) / 2, 0.05), (W, L, 0.1), "concrete_dark", outline=False)
    for x in (-6, 6):
        box("Lane", c, (x, (y0 + y1) / 2, 0.11), (0.18, L - 4, 0.02), "hazard", outline=False)
    for x, y, yaw in ((-10, 14, 20), (4, 30, -35), (12, 44, 60), (-4, 48, 10)):
        box("Crack", c, (x, y, 0.105), (3.5, 0.08, 0.02), "window", yaw=yaw, outline=False)

    # --- dismantled machinery (Tripo replaces these blocks later) + modular cover
    def marker(kind, x, y, yaw=0.0):
        """Prop placement for Unity (Tripo models in Art/Environment/ZoneA/Props): PROP_<kind>_<n>."""
        e = bpy.data.objects.new("PROP_%s_%d" % (kind, len([o for o in c.objects if o.name.startswith("PROP_" + kind)])), None)
        e.empty_display_type = "ARROWS"
        e.empty_display_size = 1.5
        e.location = (x, y, 0.1)
        e.rotation_euler.z = math.radians(yaw)
        c.objects.link(e)

    marker("Press", -9, 30, 90)
    marker("Lathe", 8, 14, 0)
    marker("Lathe", 6, 34, 180)
    marker("Lathe", -4, 42, 90)
    marker("BarrelCluster", 18, 37, 20)
    marker("BarrelCluster", -18, 12, -30)
    box("Tank", c, (-17, 40, 1.8), (2.6, 2.6, 3.6), "tank")

    def crate_stack(x, y, n, yaw=0):
        for i in range(n):
            box("Crate", c, (x + (i % 2) * 1.3, y + (i // 4) * 1.3, 0.62 + ((i // 2) % 2) * 1.24), (1.2, 1.2, 1.2),
                "wood" if i % 3 else "rust", yaw=yaw + i * 3)

    crate_stack(-14, 18, 6, 5)
    crate_stack(-2, 22, 4)
    crate_stack(14, 26, 5, -8)
    crate_stack(-12, 8, 3, 12)
    crate_stack(16, 42, 6, 4)
    crate_stack(2, 8, 2)
    # --- doors: admin block door (east wall), power plant entry (west wall at ground)
    box("AdminDoor", c, (x0 + 0.2, 26, 1.4), (0.3, 2.2, 2.8), "door")
    box("AdminDoorFrame", c, (x0 + 0.3, 26, 2.9), (0.4, 2.8, 0.3), "concrete_light")
    box("PowerDoor", c, (x0 + 0.2, 12, 1.6), (0.3, 3.4, 3.2), "door")
    # --- propaganda posters on the west wall (phase 2)
    for i, y in enumerate((30, 34.5, 42)):
        box("Poster", c, (x1 - 0.35, y, 3.3), (0.04, 2.2, 3.0), ("helipad", "white", "hazard")[i % 3], outline=False)


def boiler_house():
    """Taller block on the hall's west side with vertical ducts (left of the gate in the overview)."""
    c = coll("BoilerHouse")
    box("Boiler", c, (-30, 30, 13), (16, 28, 26), "concrete")
    box("Boiler_Roof", c, (-30, 30, 26.3), (17, 29, 0.6), "roof")
    windows_x(c, -37, -23, 16 - 0.3, 14, 22, 4)
    box("BoilerTower", c, (-25, 17, 16), (5, 5, 32), "concrete_dark")
    for i, x in enumerate((-35, -33, -31)):
        pipe_run("Duct", c, [(x, 15.4, 2), (x, 15.4, 27 + i), (x, 20, 30 + i), (x, 26, 30 + i)], 0.6, "tank")


def silos_and_bridge():
    c = coll("Silos")
    box("SiloDeck", c, (-50, 24, 3), (26, 14, 0.6), "steel")
    for gx in range(-60, -39, 4):
        beam("SiloLeg", c, (gx, 18, 0), (gx, 18, 3), 0.25, "steel")
        beam("SiloLeg", c, (gx, 30, 0), (gx, 30, 3), 0.25, "steel")
    for row, y in enumerate((20.5, 27.5)):
        for i in range(5):
            x = -59 + i * 4.3
            cyl("Silo", c, (x, y, 3.3), 1.9, 12 - row, "tank", sides=14)
            cyl("SiloCone", c, (x, y, 15.3 - row), 1.9, 1.5, "tank", sides=14, top_radius=0.5)
    box("SiloCatwalk", c, (-50, 24, 15.2), (22, 1.4, 0.2), "steel")
    pipe_run("SiloPipe", c, [(-40, 20.5, 12), (-36, 20.5, 16), (-34, 18, 16), (-34, 17, 10)], 0.7)
    pipe_run("SiloPipe", c, [(-46, 27.5, 14.5), (-46, 34, 18), (-38, 34, 18)], 0.55)
    # truss walkway bridge from the silo deck over the entry road to the boiler house / hall
    truss("Bridge", c, (-42, 13, 10.5), (-23, 6, 10.5), 1.4, 2.2)
    trestle("BridgeLeg", c, (-38, 11.5, 10.4), 10.4)
    trestle("BridgeLeg", c, (-30, 8.6, 10.4), 10.4)
    truss("Bridge2", c, (-23, 6, 10.5), (-23, 15.4, 10.5), 1.4, 2.2)


def chimneys():
    c = coll("Chimneys")
    for x, y in ((-48, 58), (-40, 64)):
        cyl("Chimney", c, (x, y, 0), 2.6, 48, "concrete_light", sides=14, top_radius=1.7)
        for z in (40, 44):
            cyl("Band", c, (x, y, z), 2.05 - (z - 40) * 0.02, 0.8, "rust", sides=14)
        cyl("ChimneyBase", c, (x, y, 0), 3.4, 3, "brick", sides=14)


def main_entry_building():
    """Brick building left-front (Acceso principal): two storeys, window rows, corrugated awning, outside stairs."""
    c = coll("MainEntry")
    x0, x1, y0, y1, h = -72, -44, -22, -4, 10
    box("Entry", c, ((x0 + x1) / 2, (y0 + y1) / 2, h / 2), (x1 - x0, y1 - y0, h), "brick")
    box("Entry_Base", c, ((x0 + x1) / 2, (y0 + y1) / 2, 0.6), (x1 - x0 + 0.4, y1 - y0 + 0.4, 1.2), "concrete")
    box("Entry_Cornice", c, ((x0 + x1) / 2, (y0 + y1) / 2, h + 0.3), (x1 - x0 + 0.8, y1 - y0 + 0.8, 0.6), "concrete_light")
    for z0 in (1.8, 6):
        windows_y(c, y0 + 1.5, y1 - 1.5, x1 + 0.3, z0, z0 + 2.6, 5, facing=1)
        windows_x(c, x0 + 1.5, x1 - 1.5, y0 - 0.3, z0, z0 + 2.6, 7)
    box("Door", c, (x1 + 0.2, -9, 1.9), (0.3, 3.6, 3.8), "door")
    box("Awning", c, (x1 + 1.6, -9, 4.3), (3.2, 5, 0.25), "corrugated")
    for i in range(3):
        box("RoofAC", c, (x0 + 5 + i * 8, (y0 + y1) / 2, h + 1.2), (3, 2.2, 1.6), "concrete_light")
    stairs("EntryStairs", c, (x1 + 2.5, -18, 0), 0, 5)
    box("EntryLanding", c, (x1 + 2.5, -16.2 + 1.3, 5), (1.6, 2.2, 0.25), "steel")
    # annex along the rail cut (front-left block with the bunker-side ramp)


def bunker():
    """Sunken bunker access: retaining walls, stairs down, ramp and the dark portal."""
    c = coll("Bunker")
    cx, cy = -22, -30
    box("PitFloor", c, (cx, cy, -3.9), (16, 12, 0.2), "concrete_dark", outline=False)
    for (x, y, sx, sy) in ((cx - 8, cy, 0.6, 12), (cx + 8, cy, 0.6, 12), (cx, cy - 6, 16, 0.6)):
        box("Retaining", c, (x, y, -1.5), (sx, sy, 5), "concrete")
    box("Portal", c, (cx - 5, cy - 3, -1.5), (5, 4, 5), "concrete_light")
    box("PortalDoor", c, (cx - 2.4, cy - 3, -2.6), (0.2, 3, 2.8), "window")
    stairs("BunkerStairs", c, (cx + 4.5, cy + 6, -3.9), 180, 3.9, width=2.2, key="concrete")
    box("Ramp", c, (cx, cy + 6, -1.95), (5, 8, 0.4), "concrete", outline=False).rotation_euler.x = math.radians(26)
    for x in (cx - 8, cx + 8):
        beam("Railing", c, (x, cy - 6, 1.1), (x, cy + 6, 1.1), 0.04, "rust")


def fuel_tanks():
    c = coll("FuelTanks")
    for i, (x, y) in enumerate(((-6, -34), (-6, -39.5))):
        cyl("FuelTank", c, (x - 5, y, 1.9), 1.7, 10, "white", sides=14, axis="X")
        for dx in (-3.5, 3.5):
            box("Saddle", c, (x + dx, y, 0.9), (0.8, 3, 1.8), "concrete")
    pipe_run("FuelPipe", c, [(5, -34, 2), (8, -34, 2), (8, -26, 2), (8, -26, 0)], 0.2, "rust")


def railway():
    c = coll("Railway")
    a, b = Vector((-30, 18, 0)), Vector((60, -62, 0))
    d = b - a
    yaw = math.degrees(math.atan2(-d.x, d.y))
    side = Vector((-d.y, d.x, 0)).normalized()
    box("Ballast", c, (a + b) / 2 + Vector((0, 0, 0.12)), (4, d.length, 0.24), "concrete_dark", yaw, outline=False)
    for s in (-0.75, 0.75):
        o = side * s
        box("Rail", c, (a + b) / 2 + o + Vector((0, 0, 0.35)), (0.12, d.length, 0.2), "steel", yaw, outline=False)
    n = int(d.length / 1.2)
    for i in range(n):
        p = a + d * (i / n)
        box("Sleeper", c, p + Vector((0, 0, 0.26)), (2.6, 0.3, 0.12), "rust", yaw, outline=False)
    # train: boxcar, flatcar with crates, tank car, small shunter loco
    def car(t, length, key, height, name):
        p = a + d * t
        box(name, c, p + Vector((0, 0, 1.2 + height / 2)), (3, length, height), key, yaw)
        box(name + "_Chassis", c, p + Vector((0, 0, 0.9)), (2.6, length + 0.6, 0.5), "steel", yaw)
    car(0.40, 11, "boxcar", 3.6, "Boxcar")
    car(0.47, 12, "boxcar", 3.6, "Boxcar2")
    car(0.33, 10, "steel", 0.3, "Flatcar")
    for k in range(3):
        box("FlatCrate", c, a + d * (0.33 + (k - 1) * 0.015) + Vector((0, 0, 1.9)), (1.6, 1.6, 1.2), "rust", yaw + k * 7)
    car(0.27, 7, "wagon_green", 2.6, "Shunter")
    p = a + d * 0.58
    cyl("TankCar", c, p + Vector((0, 0, 2.6)) - d.normalized() * 5, 1.5, 10, "concrete_dark", sides=12, axis="Y", yaw=yaw)
    box("TankCar_Chassis", c, p + Vector((0, 0, 0.9)), (2.6, 10.6, 0.5), "steel", yaw)


def watch_tower_and_walkways():
    """Stair tower with the elevated watchpoint cabin at the hall's front-right corner + upper walkways east."""
    c = coll("Walkways")
    tx, ty = 38, 11
    for dx in (-3, 3):
        for dy in (-3, 3):
            beam("TowerLeg", c, (tx + dx, ty + dy, 0), (tx + dx, ty + dy, 15), 0.2, "steel")
    for z in (5, 10, 15):
        box("TowerDeck", c, (tx, ty, z), (6.4, 6.4, 0.25), "steel")
    for i, z in enumerate((0, 5, 10)):
        stairs("TowerStairs", c, (tx - 2.2 + (i % 2) * 4.4, ty - 3, z), 0 if i % 2 == 0 else 0, 5, width=1.1)
    box("Cabin", c, (tx, ty, 16.6), (5, 5, 3), "corrugated")
    box("CabinRoof", c, (tx, ty, 18.3), (5.8, 5.8, 0.3), "rust")
    windows_x(c, tx - 2.2, tx + 2.2, ty - 2.5 - 0.02, 16.2, 17.6, 3)
    # upper walkway from the tower along the hall's east side to the admin block
    truss("Walkway_E", c, (tx + 3.2, ty, 10), (60, ty, 10), 1.2, 1.8)
    truss("Walkway_N", c, (60, ty, 10), (60, 36, 10), 1.2, 1.8)
    for p in ((50, ty, 10), (60, ty, 10), (60, 24, 10)):
        trestle("WalkLeg", c, p, 10)
    stairs("LongStairs", c, (tx + 3.2, ty - 14, 0), 0, 10, width=1.4)


def loading_docks():
    c = coll("LoadingDocks")
    x0, x1, y0, y1, h = 42, 74, -12, 6, 8
    box("Dock", c, ((x0 + x1) / 2, (y0 + y1) / 2, h / 2), (x1 - x0, y1 - y0, h), "corrugated")
    for s in (-1, 1):  # gabled corrugated roof, two slopes
        r = box("DockRoof", c, ((x0 + x1) / 2, (y0 + y1) / 2 + s * 4.6, h + 1.1), (x1 - x0 + 1, 9.6, 0.3), "rust")
        r.rotation_euler.x = math.radians(-13 * s)
    for i in range(4):
        x = x0 + 4 + i * 8
        box("RollDoor", c, (x, y0 - 0.25, 2.3), (4.4, 0.3, 4.6), "door")
    box("DockPlatform", c, ((x0 + x1) / 2, y0 - 2, 0.6), (x1 - x0, 4, 1.2), "concrete")
    box("HazardEdge", c, ((x0 + x1) / 2, y0 - 4.05, 1.1), (x1 - x0, 0.12, 0.2), "hazard", outline=False)
    stairs("DockStairs", c, (x1 + 1.2, y0 - 2, 0), 90, 6, width=1.3)
    windows_x(c, x0 + 1, x1 - 1, y0 - 0.3, 5.8, 7.2, 10)


def admin_block():
    c = coll("AdminBlock")
    x0, x1, y0, y1, h = 58, 82, 28, 42, 12
    box("Admin", c, ((x0 + x1) / 2, (y0 + y1) / 2, h / 2), (x1 - x0, y1 - y0, h), "concrete_light")
    box("Admin_Roof", c, ((x0 + x1) / 2, (y0 + y1) / 2, h + 0.3), (x1 - x0 + 0.6, y1 - y0 + 0.6, 0.6), "roof")
    for z0 in (1.5, 5.5, 9.3):
        windows_x(c, x0 + 1, x1 - 1, y0 - 0.3, z0, z0 + 1.9, 8)
        windows_y(c, y0 + 1, y1 - 1, x0 - 0.3, z0, z0 + 1.9, 4)
    box("AdminDoor", c, ((x0 + x1) / 2, y0 - 0.2, 1.3), (2.2, 0.3, 2.6), "door")


def power_plant():
    c = coll("PowerPlant")
    x0, x1, y0, y1 = 76, 102, -44, -14
    for a, b in (((x0, y0), (x1, y0)), ((x0, y1), (x1, y1)), ((x0, y0), (x0, y1))):
        a3, b3 = Vector((*a, 0)), Vector((*b, 0))
        d = b3 - a3
        box("PlantWall", c, (a3 + b3) / 2 + Vector((0, 0, 1.6)), (0.5 if d.x == 0 else d.length, 0.5 if d.y == 0 else d.length, 3.2), "concrete")
    box("PlantHouse", c, (94, -22, 3.5), (12, 10, 7), "concrete")
    for i in range(3):
        x = 80 + i * 5
        box("Transformer", c, (x, -32, 1.6), (3, 2.2, 3.2), "wagon_green")
        for k in range(3):
            cyl("Insulator", c, (x - 0.8 + k * 0.8, -32, 3.2), 0.18, 1.2, "rust", sides=6)
        cyl("TransformerTank", c, (x, -30.4, 3.2), 0.6, 2.5, "wagon_green", sides=10, axis="X")
    for y in (-40, -18):
        beam("PylonLeg", c, (84, y, 0), (84, y, 9), 0.2, "steel")
        beam("PylonArm", c, (82, y, 8.5), (86, y, 8.5), 0.12, "steel")


def waste_and_extraction():
    """Waste storage bay (front-right) and the helipad extraction behind the hall (phase 5)."""
    w = coll("WasteStorage")
    box("WasteShed", w, (62, -36, 2.5), (10, 8, 5), "concrete")
    for sgn in (-1, 1):  # gabled roof
        r = box("WasteRoof", w, (62 + sgn * 2.6, -36, 5.6), (5.6, 8.6, 0.25), "concrete_light")
        r.rotation_euler.y = math.radians(-22 * sgn)
    box("WasteDoor", w, (62, -40.1, 1.8), (4, 0.3, 3.6), "door")
    for i in range(6):
        cyl("Drum", w, (56 + (i % 3) * 1.1, -41.5 + (i // 3) * 1.1, 0), 0.45, 1.0, "rust" if i % 2 else "wagon_green", sides=8)
    c = coll("Extraction")
    box("Helipad", c, (70, 62, 0.08), (22, 22, 0.16), "concrete_dark", outline=False)
    box("HelipadSquare", c, (70, 62, 0.18), (12, 12, 0.04), "helipad", outline=False)
    for (x, y, sx, sy) in ((-2.4, 0, 1.2, 7), (2.4, 0, 1.2, 7), (0, 0, 3.6, 1.2)):
        box("H", c, (70 + x, 62 + y, 0.22), (sx, sy, 0.04), "white", outline=False)
    # extraction trigger marker (the game reads objects named EXTRACT_*)
    e = bpy.data.objects.new("EXTRACT_Helipad", None)
    e.empty_display_type = "CUBE"
    e.empty_display_size = 1
    e.scale = (8, 8, 3)
    e.location = (70, 62, 3)
    c.objects.link(e)
    # player spawn marker at the main entry road
    s = bpy.data.objects.new("SPAWN_MainEntry", None)
    s.empty_display_type = "SINGLE_ARROW"
    s.location = (-60, -32, 0.1)
    s.rotation_euler.z = math.radians(-60)
    coll("Markers").objects.link(s)


def background_city():
    c = coll("Background")
    import random
    rnd = random.Random(7)
    for i in range(26):
        x = -120 + i * 10 + rnd.uniform(-3, 3)
        h = rnd.uniform(12, 34)
        box("Skyline", c, (x, 110 + rnd.uniform(-8, 8), h / 2), (rnd.uniform(6, 11), 8, h), "concrete_dark", outline=False)
    for x, y in ((-95, 40), (-100, 60)):
        cyl("DistantTank", c, (x, y, 0), 4, 10, "tank", sides=12)


def fx_and_ai_markers():
    """Absolute positions (after the layout pass) read by the Unity builder: FX_<kind>_<n>, SCAV_<n>."""
    c = coll("Markers")
    hx, hy = LAYOUT["ProductionHall"][0], LAYOUT["ProductionHall"][1]
    marks = {
        "FX_ChimneySmoke_0": (-50, 59, 48.5), "FX_ChimneySmoke_1": (-42, 65, 48.5),
        "FX_HallDust_0": (hx, hy, 9), "FX_Sparks_0": (hx + 14, hy + 16, 9.5), "FX_Drip_0": (hx - 8, hy + 6, 19),
        "FX_Drip_1": (hx + 6, hy - 10, 19), "FX_BurnBarrel_0": (-14, -8, 0),
        "SCAV_0": (hx - 6, hy - 4, 0.1), "SCAV_1": (hx + 10, hy + 12, 0.1), "SCAV_2": (20, -8, 0.1), "SCAV_3": (-30, -2, 0.1),
    }
    for name, loc in marks.items():
        e = bpy.data.objects.new(name, None)
        e.empty_display_type = "SPHERE"
        e.location = loc
        c.objects.link(e)


def camera_views():
    scn = bpy.context.scene
    views = {
        # matches the overview sheet: elevated from the south-west corner looking north-east over the yard
        "CAM_Overview": ((-10, -92, 46), (2, 12, 0), 24),
        # matches phase 5: from the admin roof side (south-west, higher) toward the helipad
        "CAM_Phase5": ((-70, -70, 42), (40, 30, 5), 28),
    }
    for name, (loc, target, lens) in views.items():
        cam = bpy.data.objects.get(name) or bpy.data.objects.new(name, bpy.data.cameras.new(name))
        if cam.name not in scn.collection.objects:
            scn.collection.objects.link(cam)
        cam.location = loc
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = lens
        cam.data.clip_end = 1000
    # phase 2 sheet: on the maintenance gantry at the north-east corner, looking south toward the open gate
    hall = bpy.data.collections["ProductionHall"]
    off = Vector((LAYOUT["ProductionHall"][0], LAYOUT["ProductionHall"][1] - (HALL["y0"] + HALL["y1"]) / 2, 0))
    cam = bpy.data.objects.get("CAM_Hall") or bpy.data.objects.new("CAM_Hall", bpy.data.cameras.new("CAM_Hall"))
    if cam.name not in scn.collection.objects:
        scn.collection.objects.link(cam)
    cam.location = Vector((HALL["x1"] - 11, HALL["y1"] - 1.2, 9.6)) + off
    cam.rotation_euler = (Vector((-3, HALL["y0"], 1.5)) + off - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 18
    cam.data.clip_end = 1000
    scn.camera = bpy.data.objects["CAM_Overview"]


# Composition from the overview sheet: screen positions of each landmark back-projected through CAM_Overview
# (24 mm, from the south looking north). Per landmark: absolute footprint centre (x, y) and scale about it.
LAYOUT = {
    "ProductionHall": (-4, 38, 1.0), "BoilerHouse": (-40, 34, 1.0), "Walkways": (12, 2, 1.45),
    "LoadingDocks": (31, -2, 1.2), "WasteStorage": (6, -52, 1.0), "AdminBlock": (60, 38, 1.0), "MainEntry": (-40, -20, 1.0), "Silos": (-44, 18, 1.15),
    "Bunker": (-22, -46, 0.9), "FuelTanks": (-8, -42, 0.8), "Extraction": (40, 60, 1.0),
    "Chimneys": (-46, 62, 1.0), "PowerPlant": (52, -20, 0.8),
}


def apply_layout():
    bpy.context.view_layer.update()  # matrix_world is stale for objects placed this run
    for name, (tx, ty, k) in LAYOUT.items():
        c = bpy.data.collections.get(name)
        if c is None:
            continue
        objs = [o for o in c.objects if o.parent is None]
        pts = [o.matrix_world @ Vector(b) for o in objs if o.type == "MESH" for b in o.bound_box]
        if not pts:
            continue
        pivot = Vector(((min(p.x for p in pts) + max(p.x for p in pts)) / 2, (min(p.y for p in pts) + max(p.y for p in pts)) / 2, 0))
        for o in objs:
            o.location = Vector((tx, ty, 0)) + (o.location - pivot) * k
            o.scale = o.scale * k


def build():
    bpy.ops.wm.read_homefile(use_empty=True)
    _mats.clear()
    ground()
    perimeter()
    production_hall()
    boiler_house()
    silos_and_bridge()
    chimneys()
    main_entry_building()
    bunker()
    fuel_tanks()
    railway()
    watch_tower_and_walkways()
    loading_docks()
    admin_block()
    power_plant()
    waste_and_extraction()
    background_city()
    apply_layout()
    fx_and_ai_markers()
    camera_views()
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_WORKBENCH"
    scn.display.shading.light = "STUDIO"
    scn.display.shading.color_type = "MATERIAL"
    scn.display.shading.show_backface_culling = True
    scn.display.shading.show_cavity = True
    scn.display.shading.show_shadows = True
    scn.display.shading.shadow_intensity = 0.35
    scn.display.shading.show_object_outline = False
    world = bpy.data.worlds.new("W")
    scn.world = world
    scn.render.resolution_x, scn.render.resolution_y = 1600, 1070
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    return {"objects": len(bpy.data.objects)}


UNITY_OUT = os.path.join(ROOT, r"Assets\_Project\Art\Environment\ZoneA")


def export_unity():
    """One FBX per landmark collection, its meshes merged (materials kept: M_Zone_<key> + M_Outline). Empties named
    PROP_* / SPAWN_* / EXTRACT_* are exported in ZoneA_Markers.fbx for the Unity scene builder."""
    os.makedirs(UNITY_OUT, exist_ok=True)
    bpy.context.view_layer.update()
    written = []
    for c in list(bpy.context.scene.collection.children):
        meshes = [o for o in c.objects if o.type == "MESH"]
        if not meshes:
            continue
        bm = bmesh.new()
        mats = []
        for o in meshes:
            me = o.data.copy()
            me.transform(o.matrix_world)
            remap = []
            for m in me.materials:
                if m not in mats:
                    mats.append(m)
                remap.append(mats.index(m))
            start = len(bm.faces)
            bm.from_mesh(me)
            bm.faces.ensure_lookup_table()
            for f in bm.faces[start:]:
                f.material_index = remap[f.material_index] if remap else 0
            bpy.data.meshes.remove(me)
        me = bpy.data.meshes.new("ZoneA_" + c.name)
        bm.to_mesh(me)
        bm.free()
        for m in mats:
            me.materials.append(m)
        ob = bpy.data.objects.new("ZoneA_" + c.name, me)
        bpy.context.scene.collection.objects.link(ob)
        path = os.path.join(UNITY_OUT, "ZoneA_" + c.name + ".fbx")
        with bpy.context.temp_override(selected_objects=[ob], active_object=ob):
            bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                                     axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", bake_anim=False,
                                     path_mode="STRIP")
        bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.meshes.remove(me)
        written.append(os.path.basename(path))
    markers = [o for o in bpy.data.objects if o.type == "EMPTY" and o.name.split("_")[0] in ("PROP", "SPAWN", "EXTRACT", "FX", "SCAV")]
    with bpy.context.temp_override(selected_objects=markers, active_object=markers[0]):
        bpy.ops.export_scene.fbx(filepath=os.path.join(UNITY_OUT, "ZoneA_Markers.fbx"), use_selection=True,
                                 object_types={"EMPTY"}, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                                 axis_up="Y", bake_anim=False)
    written.append("ZoneA_Markers.fbx (%d markers)" % len(markers))
    return written


result = build()
if globals().get("EXPORT_UNITY"):
    result["exported"] = export_unity()
