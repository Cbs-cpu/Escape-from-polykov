"""
Procedural low-poly models for every inventory item (non-AK), used to render the 3D inventory icons.
Creates ArtSource/Items/Items_Source.blend from an empty file: one object per item id, named "Item_<id>", at real-world
size in metres (X = long axis, Y = depth, Z = up, front face toward -Y), laid out in a grid ONLY for viewing.
Style (same as build_ak74n_variants.py): flat shading, flat palette materials, black inverted-hull outline whose
thickness scales with the object size. Run inside Blender (MCP): exec(open(path, encoding='utf-8').read()).
Then run render_item_icons.py to produce Resources/Icons/Items/<id>.png.
"""
import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix, Euler

ROOT = os.environ.get("POLYKOV_ROOT", r"C:\Users\Cobos\Escape from Polykov")
OUT = os.path.join(ROOT, "ArtSource", "Items", "Items_Source.blend")

PALETTE = {
    "white": "#c9c6bb", "gauze": "#cfc7ae", "orange": "#c2622a", "red": "#8f2b26", "darkred": "#5e1d1b",
    "grey": "#5d6063", "lgrey": "#8a8d8e", "dgrey": "#2f3133", "black": "#1e1f21", "steel": "#6c7074",
    "dsteel": "#3a3d40", "alu": "#a4a7a8", "blue": "#2e5f8c", "dblue": "#1f3d5c", "green": "#4c5a34",
    "olive": "#4d5236", "dolive": "#363a28", "sand": "#8f7d58", "tan": "#a8926a", "brown": "#5a3a22",
    "wood": "#8e5a2e", "wood2": "#6e4323", "brass": "#b08d3c", "copper": "#a4622f", "yellow": "#c9a227",
    "tin": "#9a9c96", "paper": "#b9a77c", "money": "#7d8a5a", "lens": "#3b4a52", "glassblue": "#4d6a78",
    "gold": "#c8a640", "ammogreen": "#4a5a3a",
}


def _lin(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]


def mat(key):
    name = "M_Item_" + key
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*_lin(PALETTE[key]), 1)
    m.metallic, m.roughness = 0.0, 0.85
    m.use_backface_culling = True
    return m


class Part:
    def __init__(self, item_id):
        self.id = item_id
        self.bm = bmesh.new()
        self.mats = []

    def _tag(self, verts, material):
        m = mat(material)
        if m not in self.mats:
            self.mats.append(m)
        i = self.mats.index(m)
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = i

    @staticmethod
    def _mx(c, rot, scale=(1, 1, 1)):
        r = Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4()
        return Matrix.Translation(c) @ r @ Matrix.Diagonal((*scale, 1))

    def box(self, c, size, m, rot=(0, 0, 0)):
        r = bmesh.ops.create_cube(self.bm, size=1.0, matrix=self._mx(c, rot, size))
        self._tag(r["verts"], m)

    def cyl(self, c, r, length, axis, m, sides=8, r2=None, rot=(0, 0, 0), scale=(1, 1, 1)):
        base = {"x": (0, 90, 0), "y": (-90, 0, 0), "z": (0, 0, 0)}[axis]
        mx = self._mx(c, rot, scale) @ self._mx((0, 0, 0), base)
        ret = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=sides, radius1=r,
                                    radius2=r if r2 is None else r2, depth=length, matrix=mx)
        self._tag(ret["verts"], m)

    def sph(self, c, radii, m, seg=8, rings=5, rot=(0, 0, 0)):
        ret = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=1.0,
                                        matrix=self._mx(c, rot, radii))
        self._tag(ret["verts"], m)

    def prism(self, pts, y0, y1, m):
        """Polygon in the (x, z) plane extruded between y0 and y1."""
        a = [self.bm.verts.new((x, y0, z)) for x, z in pts]
        b = [self.bm.verts.new((x, y1, z)) for x, z in pts]
        n = len(pts)
        self.bm.faces.new(list(reversed(a)))
        self.bm.faces.new(b)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new((a[i], a[j], b[j], b[i]))
        self._tag(a + b, m)

    def ring(self, c, R, n, size, m, plane="xz", start=0.0, span=360.0, tilt=(0, 0, 0)):
        """Ring/arc of boxes (size = (tangent length, thickness radial-axis, width))."""
        for i in range(n):
            t = math.radians(start + span * i / (n if span >= 360 else max(n - 1, 1)))
            a, b = math.cos(t) * R, math.sin(t) * R
            if plane == "xz":
                p = (c[0] + a, c[1], c[2] + b)
                self.box(p, (size[0], size[2], size[1]), m, rot=(0, -(math.degrees(t) + 90), 0))
            else:  # "xy" flat loop lying on the table
                p = (c[0] + a, c[1] + b, c[2])
                self.box(p, size, m, rot=(0, 0, math.degrees(t) + 90))

    def finish(self, grid=(0, 0)):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        _orient_islands(bm)
        xs = [v.co for v in bm.verts]
        dims = [max(p[i] for p in xs) - min(p[i] for p in xs) for i in range(3)]
        outline = max(dims) * 0.008
        # re-centre on the bounds centre (origin at the middle of the item)
        ctr = Vector([(max(p[i] for p in xs) + min(p[i] for p in xs)) / 2 for i in range(3)])
        for v in bm.verts:
            v.co -= ctr
        name = "Item_" + self.id
        old = bpy.data.objects.get(name)
        if old:
            bpy.data.objects.remove(old, do_unlink=True)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(m)
        for p in me.polygons:
            p.use_smooth = False
        _outline(me, outline)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.location = (grid[0], grid[1], 0)
        ob["dims"] = dims
        return ob


def _orient_islands(bm):
    bm.faces.ensure_lookup_table()
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
        if out < 0:
            bmesh.ops.reverse_faces(bm, faces=island)


def _outline(me, t):
    om = bpy.data.materials.get("M_Outline") or bpy.data.materials.new("M_Outline")
    om.diffuse_color = (0, 0, 0, 1)
    om.use_backface_culling = True
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
        v.co += v.normal * t
    bmesh.ops.reverse_faces(bm, faces=faces)
    for f in faces:
        f.material_index = idx
    bm.to_mesh(me)
    bm.free()


# ==================================================================================================== items
def bandage(p):
    p.cyl((0, 0, 0.035), 0.035, 0.09, "x", "gauze", 12)
    p.cyl((0, 0, 0.035), 0.0365, 0.045, "x", "green", 12)
    p.cyl((-0.048, 0, 0.035), 0.02, 0.01, "x", "gauze", 10)
    p.box((0.0, 0, 0.002), (0.12, 0.04, 0.004), "gauze")


def ai2(p):
    p.box((0, 0, 0), (0.105, 0.03, 0.075), "orange")
    p.box((0, 0, 0.0385), (0.11, 0.032, 0.006), "red")
    p.box((0, -0.0165, 0), (0.05, 0.002, 0.016), "white")
    p.box((0, -0.0165, 0), (0.016, 0.002, 0.05), "white")
    p.box((-0.035, -0.0165, -0.03), (0.03, 0.002, 0.008), "darkred")


def carkit(p):
    p.box((0, 0, 0), (0.22, 0.07, 0.14), "red")
    p.box((0, 0, 0.0725), (0.225, 0.072, 0.008), "dgrey")
    p.box((0, -0.036, 0.0), (0.2, 0.004, 0.006), "lgrey")
    p.box((0, -0.036, 0.02), (0.05, 0.003, 0.05), "white")
    p.box((0, -0.0375, 0.02), (0.05, 0.003, 0.016), "red")
    p.box((0, -0.0375, 0.02), (0.016, 0.003, 0.05), "red")
    p.box((0, 0, 0.09), (0.09, 0.012, 0.012), "dgrey")
    p.box((-0.04, 0, 0.082), (0.012, 0.012, 0.02), "dgrey")
    p.box((0.04, 0, 0.082), (0.012, 0.012, 0.02), "dgrey")


def splint(p):
    p.box((0, 0, 0.0), (0.09, 0.012, 0.24), "orange")
    p.box((0, 0, 0.0), (0.05, 0.014, 0.242), "alu")
    p.box((0.0, -0.007, 0.06), (0.09, 0.004, 0.02), "orange", rot=(0, 0, 0))
    p.box((0, -0.007, -0.06), (0.09, 0.004, 0.02), "dgrey")
    p.box((0, 0.01, 0.0), (0.075, 0.012, 0.115), "orange")


def water(p):
    p.cyl((0, 0, 0.09), 0.033, 0.18, "z", "glassblue", 12)
    p.cyl((0, 0, 0.19), 0.033, 0.03, "z", "glassblue", 12, r2=0.014)
    p.cyl((0, 0, 0.212), 0.016, 0.02, "z", "blue", 10)
    p.cyl((0, 0, 0.09), 0.0345, 0.07, "z", "white", 12)
    p.cyl((0, 0, 0.09), 0.0352, 0.03, "z", "blue", 12)
    p.cyl((0, 0, 0.005), 0.03, 0.012, "z", "dblue", 12)


def tushonka(p):
    p.cyl((0, 0, 0.03), 0.04, 0.06, "z", "tin", 14)
    p.cyl((0, 0, 0.03), 0.0415, 0.036, "z", "red", 14)
    p.cyl((0, 0, 0.032), 0.0425, 0.014, "z", "paper", 14)
    p.cyl((0, 0, 0.0625), 0.038, 0.005, "z", "alu", 14)
    p.box((0.012, 0.0, 0.066), (0.02, 0.008, 0.004), "steel")


def crackers(p):
    p.box((0, 0, 0), (0.11, 0.022, 0.15), "dolive")
    p.box((0, -0.0115, 0.0), (0.09, 0.002, 0.11), "sand")
    p.box((0, -0.0125, -0.0), (0.06, 0.002, 0.045), "tan")
    p.box((0, 0, 0.08), (0.11, 0.024, 0.01), "olive")
    p.box((0, 0, -0.08), (0.11, 0.024, 0.01), "olive")
    p.box((0, -0.0125, 0.045), (0.05, 0.002, 0.014), "red")


def bolts(p):
    spots = [(-0.03, -0.01, 0.006, 5), (0.0, 0.015, 0.006, 40), (0.03, -0.005, 0.006, 80), (-0.012, 0.012, 0.006, 110),
             (0.015, -0.02, 0.006, 20), (0.0, -0.005, 0.017, 60), (-0.02, 0.0, 0.017, 100)]
    for x, y, z, a in spots:
        p.cyl((x, y, z), 0.004, 0.04, "x", "steel", 6, rot=(0, 0, a))
        hx = Vector((0.02, 0, 0))
        hx.rotate(Euler((0, 0, math.radians(a))))
        p.cyl((x + hx.x * 1.05, y + hx.y * 1.05, z), 0.0075, 0.007, "x", "dsteel", 6, rot=(0, 0, a))


def wires(p):
    cols = ["red", "blue", "black", "yellow"]
    n = 14
    for layer in range(3):
        for i in range(n):
            t = 2 * math.pi * (i + layer * 0.5) / n
            R = 0.035 + (layer % 2) * 0.0
            c = (math.cos(t) * R, math.sin(t) * R, 0.005 + layer * 0.0075)
            p.box(c, (0.03, 0.012, 0.0075), cols[(i // 3 + layer) % 4], rot=(0, 0, math.degrees(t) + 90))
    p.box((0.035, 0.0, 0.02), (0.012, 0.03, 0.03), "white")


def gunpowder(p):
    p.cyl((0, 0, 0.05), 0.05, 0.1, "z", "ammogreen", 14)
    p.cyl((0, 0, 0.102), 0.045, 0.006, "z", "dolive", 14)
    p.cyl((0, 0, 0.055), 0.0515, 0.04, "z", "yellow", 14)
    p.cyl((0, 0, 0.055), 0.0525, 0.012, "z", "black", 14)
    p.box((0.0, -0.05, 0.11), (0.02, 0.01, 0.004), "steel")


def cigs(p):
    p.box((0, 0, 0), (0.057, 0.022, 0.09), "red")
    p.box((0, 0, 0.0325), (0.058, 0.023, 0.025), "white")
    p.box((0, -0.0118, 0.0), (0.04, 0.002, 0.026), "white")
    p.box((0, -0.0128, 0.0), (0.02, 0.002, 0.012), "black")
    p.box((0, 0, 0.0455), (0.058, 0.024, 0.002), "gold")
    p.box((0, 0, 0.0115), (0.059, 0.0235, 0.003), "gold")


def key_dorm(p):
    p.ring((0, 0, 0), 0.0115, 8, (0.011, 0.006, 0.005), "brass", plane="xz")
    p.prism([(0.02, -0.004), (0.085, -0.004), (0.085, -0.002), (0.078, -0.0005), (0.078, -0.007), (0.07, -0.007),
             (0.07, -0.0005), (0.06, -0.0005), (0.06, -0.008), (0.05, -0.008), (0.05, 0.004), (0.02, 0.004)], -0.0022, 0.0022,
            "brass")
    p.prism([(-0.02, -0.03), (0.012, -0.03), (0.012, -0.0), (-0.02, -0.0)], -0.004, 0.0, "yellow")
    p.box((-0.002, 0.0, -0.001), (0.016, 0.004, 0.006), "steel")


def roubles(p):
    p.box((0, 0, 0), (0.16, 0.065, 0.045), "money")
    for i in range(4):
        p.box((0, 0, -0.02 + i * 0.0135), (0.162, 0.066, 0.001), "paper")
    p.box((-0.03, 0, 0), (0.028, 0.068, 0.047), "red")
    p.box((0.04, 0, 0), (0.028, 0.068, 0.047), "red")
    p.box((0, 0, 0.0235), (0.16, 0.065, 0.001), "tan")


def knife(p):
    p.prism([(0.02, -0.014), (0.16, -0.014), (0.2, -0.004), (0.205, 0.002), (0.02, 0.006)], -0.0018, 0.0018, "alu")
    p.prism([(0.02, -0.014), (0.16, -0.014), (0.2, -0.004), (0.06, 0.0), (0.02, 0.0)], -0.0026, 0.0026, "steel") \
        if False else None
    p.box((0.02, 0, -0.004), (0.006, 0.02, 0.05), "dsteel")
    p.cyl((-0.045, 0, -0.004), 0.0125, 0.13, "x", "black", 8, scale=(1, 0.75, 1))
    p.cyl((-0.11, 0, -0.004), 0.014, 0.01, "x", "dsteel", 8)
    p.cyl((-0.02, 0, -0.004), 0.0135, 0.006, "x", "dgrey", 8, scale=(1, 0.8, 1))


def cap(p):
    p.sph((0, 0, 0.0), (0.09, 0.085, 0.075), "olive", 10, 6)
    p.box((0, -0.075, -0.045), (0.16, 0.115, 0.008), "dolive", rot=(-8, 0, 0))
    p.cyl((0, 0.0, -0.012), 0.095, 0.03, "z", "dolive", 12, scale=(1, 0.95, 1))
    p.cyl((0, -0.09, 0.0), 0.009, 0.004, "y", "sand", 6)


def helmet(p):
    p.sph((0, 0, 0.0), (0.135, 0.11, 0.105), "olive", 12, 7)
    p.cyl((0, 0, -0.045), 0.145, 0.03, "z", "dolive", 14, scale=(1, 0.82, 1))
    p.box((0, -0.11, 0.045), (0.05, 0.02, 0.03), "dsteel")
    p.box((0, -0.03, 0.098), (0.03, 0.1, 0.008), "dgrey")
    p.box((0.13, 0, -0.01), (0.01, 0.05, 0.04), "dgrey")
    p.box((-0.13, 0, -0.01), (0.01, 0.05, 0.04), "dgrey")


def headset(p):
    p.ring((0, 0, -0.005), 0.075, 10, (0.03, 0.012, 0.012), "black", plane="xz", start=0, span=180)
    for s in (-1, 1):
        p.cyl((s * 0.078, 0, -0.03), 0.052, 0.05, "x", "sand", 10, scale=(1, 0.85, 1))
        p.cyl((s * 0.098, 0, -0.03), 0.046, 0.01, "x", "black", 10, scale=(1, 0.85, 1))
    p.box((0.0, 0, 0.076), (0.1, 0.02, 0.008), "sand")
    p.cyl((-0.1, -0.03, -0.03), 0.004, 0.06, "y", "black", 5)


def glasses(p):
    for s in (-1, 1):
        p.prism([(s * 0.008, 0.02), (s * 0.06, 0.02), (s * 0.068, 0.0), (s * 0.055, -0.02), (s * 0.012, -0.02),
                 (s * 0.005, 0.0)], -0.03, -0.024, "lens")
        p.box((s * 0.075, 0.03, 0.0), (0.006, 0.12, 0.012), "black")
    p.box((0, -0.03, 0.02), (0.14, 0.008, 0.008), "black")
    p.box((0, -0.03, -0.021), (0.14, 0.008, 0.005), "black")
    p.box((0, -0.03, 0.0), (0.012, 0.008, 0.016), "black")
    p.box((0, -0.03, 0.026), (0.156, 0.006, 0.008), "dgrey")


def balaclava(p):
    p.box((0, 0, 0.0), (0.22, 0.18, 0.02), "black")
    p.box((0, 0, 0.02), (0.2, 0.16, 0.02), "dgrey")
    p.box((0, 0.0, 0.04), (0.17, 0.14, 0.018), "black")
    p.box((0, -0.075, 0.03), (0.13, 0.01, 0.014), "dgrey")
    p.box((0, -0.09, 0.02), (0.15, 0.006, 0.006), "black")


def armband(p):
    p.ring((0, 0, 0.0), 0.045, 16, (0.0185, 0.006, 0.06), "blue", plane="xy")
    p.ring((0, 0, 0.0), 0.045, 16, (0.0195, 0.0075, 0.012), "white", plane="xy")
    p.box((0.045, 0.0, 0.0), (0.008, 0.062, 0.012), "dblue")


def armor(p):
    body = [(-0.15, -0.16), (-0.15, 0.12), (-0.09, 0.18), (-0.07, 0.15), (-0.05, 0.13), (0.05, 0.13), (0.07, 0.15),
            (0.09, 0.18), (0.15, 0.12), (0.15, -0.16), (0.0, -0.19)]
    p.prism(body, -0.03, 0.03, "olive")
    p.prism([(x * 0.9, z * 0.93 + 0.0) for x, z in body], -0.036, -0.03, "green")
    for s in (-1, 1):
        p.box((s * 0.075, 0.0, 0.18), (0.06, 0.04, 0.06), "dolive")
        p.box((s * 0.16, -0.0, -0.06), (0.04, 0.07, 0.2), "dolive")
    for i in range(3):
        p.box((0, -0.036, -0.02 - i * 0.05), (0.22, 0.004, 0.008), "sand")
    p.box((0, -0.04, 0.05), (0.1, 0.006, 0.05), "dolive")


def rig(p):
    p.prism([(-0.17, -0.06), (-0.17, 0.09), (-0.06, 0.09), (-0.05, 0.13), (0.05, 0.13), (0.06, 0.09), (0.17, 0.09),
             (0.17, -0.06)], -0.015, 0.015, "dolive")
    for i in range(3):
        p.box((-0.11 + i * 0.11, -0.035, 0.03), (0.095, 0.045, 0.1), "olive")
        p.box((-0.11 + i * 0.11, -0.06, 0.055), (0.09, 0.008, 0.05), "dolive")
    for i in range(3):
        p.box((-0.11 + i * 0.11, -0.035, -0.055), (0.095, 0.04, 0.075), "green")
    p.box((0, 0.02, 0.125), (0.1, 0.03, 0.02), "dgrey")
    p.box((-0.08, 0.0, -0.07), (0.01, 0.03, 0.02), "black")


def backpack(p):
    p.box((0, 0, 0), (0.3, 0.16, 0.42), "dolive")
    p.box((0, 0.0, 0.2), (0.3, 0.17, 0.05), "olive")
    p.box((0, -0.11, -0.06), (0.24, 0.08, 0.22), "olive")
    p.box((0, -0.155, -0.02), (0.2, 0.01, 0.06), "black")
    for s in (-1, 1):
        p.box((s * 0.17, -0.02, -0.07), (0.05, 0.1, 0.22), "olive")
        p.box((s * 0.09, 0.09, 0.0), (0.05, 0.03, 0.4), "black")
    p.box((0, -0.085, 0.13), (0.26, 0.012, 0.03), "black")
    p.box((0, -0.005, 0.24), (0.1, 0.03, 0.02), "black")


def sling(p):
    p.box((0, 0, 0), (0.24, 0.1, 0.26), "grey", rot=(0, 0, 0))
    p.box((0, 0, 0.13), (0.24, 0.11, 0.03), "dgrey")
    p.box((0, -0.06, -0.03), (0.2, 0.03, 0.14), "dgrey")
    p.ring((0, 0, 0.12), 0.11, 8, (0.06, 0.02, 0.02), "black", plane="xz", start=10, span=160)
    p.box((0, -0.076, 0.13), (0.18, 0.004, 0.006), "lgrey")


def alpha(p):
    p.box((0, 0, 0), (0.13, 0.05, 0.1), "black")
    p.box((0, 0, 0.0), (0.135, 0.02, 0.105), "dgrey")
    for sx in (-1, 1):
        for sz in (-1, 1):
            p.box((sx * 0.062, 0, sz * 0.047), (0.014, 0.052, 0.014), "alu")
    p.box((0, -0.027, 0.03), (0.03, 0.006, 0.014), "alu")
    p.box((0, 0, 0.056), (0.05, 0.012, 0.014), "alu")
    p.box((0, -0.027, -0.01), (0.02, 0.003, 0.02), "red")


def ammo_case(p):
    p.box((0, 0, 0), (0.28, 0.095, 0.17), "ammogreen")
    p.box((0, 0, 0.055), (0.29, 0.1, 0.012), "dolive")
    p.box((0, 0, -0.08), (0.29, 0.1, 0.012), "dolive")
    for s in (-1, 1):
        p.box((s * 0.14, 0, 0.0), (0.008, 0.1, 0.17), "dolive")
        p.box((s * 0.07, -0.05, 0.055), (0.025, 0.012, 0.03), "steel")
    p.ring((0, 0, 0.085), 0.045, 6, (0.022, 0.012, 0.014), "steel", plane="xz", start=10, span=160)
    p.box((0, -0.0485, 0.0), (0.12, 0.002, 0.03), "yellow")
    p.box((0.0, -0.0485, -0.045), (0.05, 0.002, 0.014), "yellow")


def _cartridge(p, off, case, tip, hollow, long_, r, bullet_len, neck):
    y, z = off
    p.cyl((0, y, z), r, long_, "x", case, 8, r2=r)
    if neck:
        p.cyl((long_ / 2 + 0.005, y, z), r, 0.01, "x", case, 8, r2=r * 0.62)
    x0 = long_ / 2 + (0.01 if neck else 0)
    br = r * 0.62 if neck else r * 0.98
    p.cyl((x0 + bullet_len / 2 - 0.001, y, z), br, bullet_len, "x", tip[0], 8, r2=br * (0.4 if not hollow else 0.95))
    if tip[1]:
        p.cyl((x0 + bullet_len - 0.003, y, z), br * 0.45, 0.005, "x", tip[1], 8, r2=br * 0.1)
    if hollow:
        p.cyl((x0 + bullet_len - 0.001, y, z), br * 0.45, 0.003, "x", "black", 6)
    p.cyl((-long_ / 2 - 0.0008, y, z), r * 1.1, 0.0016, "x", "brass", 8)
    p.cyl((-long_ / 2 - 0.0018, y, z), r * 0.4, 0.001, "x", "dgrey", 6)


def ammo_45_fmj(p):
    for i, (y, z) in enumerate([(0.0, 0.006), (-0.014, 0.006), (-0.007, 0.0183)]):
        _cartridge(p, (y, z), "brass", ("copper", None), False, 0.0228, 0.0058, 0.0166, False)


def ammo_45_hp(p):
    for i, (y, z) in enumerate([(0.0, 0.006), (-0.014, 0.006), (-0.007, 0.0183)]):
        _cartridge(p, (y, z), "brass", ("copper", None), True, 0.0228, 0.0058, 0.0166, False)


def ammo_545_ps(p):
    for y, z in [(0.0, 0.0057), (-0.013, 0.0057), (-0.0065, 0.0172)]:
        _cartridge(p, (y, z), "steel", ("copper", "steel"), False, 0.0339, 0.0055, 0.0195, True)


def ammo_545_bp(p):
    for y, z in [(0.0, 0.0057), (-0.013, 0.0057), (-0.0065, 0.0172)]:
        _cartridge(p, (y, z), "brass", ("copper", "black"), False, 0.0339, 0.0055, 0.0195, True)


def magazine_7(p):
    p.box((0, 0, 0), (0.0125, 0.03, 0.098), "dsteel")
    p.box((0, 0, -0.052), (0.014, 0.034, 0.008), "black")
    p.box((0, 0, 0.053), (0.0115, 0.028, 0.008), "steel")
    p.box((0, -0.0155, 0.005), (0.01, 0.002, 0.07), "black")
    for i in range(6):
        p.box((0, -0.0165, -0.03 + i * 0.011), (0.004, 0.002, 0.004), "alu")
    p.box((0, 0.0, 0.048), (0.008, 0.024, 0.006), "dgrey")


def barrel_standard(p):
    p.cyl((0.03, 0, 0), 0.0085, 0.127, "x", "dsteel", 10)
    p.cyl((0.0, 0, 0), 0.0165, 0.06, "x", "steel", 10, r2=0.0165)
    p.cyl((0.075, 0, 0), 0.0122, 0.012, "x", "steel", 10)
    p.box((-0.02, 0, -0.015), (0.03, 0.012, 0.02), "dsteel")
    p.box((-0.01, 0, 0.0165), (0.04, 0.008, 0.005), "black")


def barrel_threaded(p):
    p.cyl((0.03, 0, 0), 0.0085, 0.127, "x", "dsteel", 10)
    p.cyl((0.0, 0, 0), 0.0165, 0.06, "x", "steel", 10)
    p.cyl((0.1, 0, 0), 0.0105, 0.07, "x", "dsteel", 10)
    for i in range(5):
        p.cyl((0.08 + i * 0.012, 0, 0), 0.0122, 0.005, "x", "steel", 10)
    p.box((-0.02, 0, -0.015), (0.03, 0.012, 0.02), "dsteel")


def suppressor_45(p):
    p.cyl((0, 0, 0), 0.0215, 0.2, "x", "black", 12)
    p.cyl((-0.096, 0, 0), 0.0235, 0.012, "x", "dgrey", 12)
    p.cyl((0.1, 0, 0), 0.0225, 0.006, "x", "dgrey", 12)
    p.cyl((0.1, 0, 0), 0.008, 0.008, "x", "dsteel", 8)
    for i in range(4):
        p.cyl((-0.05 + i * 0.045, 0, 0), 0.0228, 0.004, "x", "dgrey", 12)


def grips_wood(p):
    pts = [(-0.045, 0.045), (0.03, 0.045), (0.036, 0.0), (0.028, -0.06), (0.012, -0.095), (-0.03, -0.09), (-0.045, -0.02)]
    p.prism(pts, -0.007, 0.007, "wood")
    p.prism([(x * 0.94, z * 0.94) for x, z in pts], -0.0095, -0.007, "wood2")
    for i in range(4):
        for j in range(6):
            p.box((-0.024 + j * 0.011, -0.0098, 0.028 - i * 0.02), (0.005, 0.002, 0.005), "black", rot=(0, 45, 0))
    p.cyl((-0.018, -0.011, 0.02), 0.006, 0.003, "y", "gold", 8)
    p.cyl((0.012, -0.011, -0.06), 0.005, 0.003, "y", "gold", 8)


ITEMS = [bandage, ai2, carkit, splint, water, tushonka, crackers, bolts, wires, gunpowder, cigs, key_dorm, roubles,
         knife, cap, helmet, headset, glasses, balaclava, armband, armor, rig, backpack, sling, alpha, ammo_case,
         ammo_45_fmj, ammo_45_hp, ammo_545_ps, ammo_545_bp, magazine_7, barrel_standard, barrel_threaded,
         suppressor_45, grips_wood]


def main():
    bpy.ops.wm.read_homefile(use_empty=True)
    cols = 7
    for i, fn in enumerate(ITEMS):
        p = Part(fn.__name__)
        fn(p)
        p.finish(((i % cols) * 0.6, -(i // cols) * 0.6))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    return len(ITEMS)


N = main()
