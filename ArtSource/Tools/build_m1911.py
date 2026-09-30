"""
Procedural low-poly M1911 (reference: M1911 tactical modular weapon sheet). Every part is its own object
with its origin on its real motion pivot, so Unity can animate slide, hammer, trigger, safeties and magazine.

Gun space: u = forward from the rear of the slide (m), v = up from the bottom of the grip (m), x = lateral.
Blender mapping: X = x (+X is the gun's LEFT side, it becomes Unity -X), Y = -u (muzzle toward -Y ->
Unity +Z), Z = v. Everything is offset so the root origin sits on the firing-hand grip point.

Run inside Blender: exec(open(r"<path>/build_m1911.py", encoding="utf-8").read())
"""
import bpy
import bmesh
import math
from mathutils import Vector

COLLECTION = "M1911"
ROOT_U, ROOT_V = 0.020, 0.045        # right-hand grip point
BORE_V = 0.1155

PALETTE = {
    # name: (hex, roughness, metallic)
    "G_Frame": ("#2b2d30", 0.55, 0.65),
    "G_Slide": ("#34373b", 0.45, 0.7),
    "G_Dark": ("#141517", 0.8, 0.3),
    "G_Steel": ("#55585d", 0.35, 0.85),
    "G_Hood": ("#43464a", 0.4, 0.8),
    "G_Wood": ("#6f3b21", 0.6, 0.0),
    "G_WoodDark": ("#4b2615", 0.7, 0.0),
    "G_Screw": ("#8a8c8f", 0.35, 0.9),
    "G_Brass": ("#b88b3c", 0.3, 0.95),
    "G_Copper": ("#a8683f", 0.35, 0.9),
    "G_MagBody": ("#26282b", 0.5, 0.6),
}


def _lin(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]


def material(name):
    hexc, rough, metal = PALETTE[name]
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    rgb = _lin(hexc)
    m.diffuse_color = (*rgb, 1.0)
    m.roughness = rough
    m.metallic = metal
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Roughness"].default_value = rough
        bsdf.inputs["Metallic"].default_value = metal
    return m


def P(u, v, x=0.0):
    return Vector((x, -(u - ROOT_U), v - ROOT_V))


def _coll():
    c = bpy.data.collections.get(COLLECTION)
    if c is None:
        c = bpy.data.collections.new(COLLECTION)
        bpy.context.scene.collection.children.link(c)
    return c


class Part:
    """Accumulates several primitives into one object (one motion part)."""

    def __init__(self, name, pivot_uv=None, pivot_x=0.0):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.pivot = P(*pivot_uv, pivot_x) if pivot_uv else Vector((0, 0, 0))

    def _mat_index(self, mat_name):
        if mat_name not in self.mats:
            self.mats.append(mat_name)
        return self.mats.index(mat_name)

    def _faces(self, faces, mat_name):
        idx = self._mat_index(mat_name)
        for f in faces:
            f.material_index = idx

    def extrude_side(self, uv_pts, x0, x1, mat_name, bevel=0.0):
        """Side-profile polygon (u, v) extruded across x0..x1."""
        bm = bmesh.new()
        a = [bm.verts.new(P(u, v, x0)) for u, v in uv_pts]
        b = [bm.verts.new(P(u, v, x1)) for u, v in uv_pts]
        bm.faces.new(a)
        bm.faces.new(list(reversed(b)))
        n = len(a)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
        self._merge(bm, mat_name, bevel)

    def extrude_section(self, xv_pts, u0, u1, mat_name, bevel=0.0):
        """Cross-section polygon (x, v) extruded along the barrel axis from u0 to u1."""
        bm = bmesh.new()
        a = [bm.verts.new(P(u0, v, x)) for x, v in xv_pts]
        b = [bm.verts.new(P(u1, v, x)) for x, v in xv_pts]
        bm.faces.new(a)
        bm.faces.new(list(reversed(b)))
        n = len(a)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
        self._merge(bm, mat_name, bevel)

    def box(self, u0, u1, v0, v1, x0, x1, mat_name, bevel=0.0):
        self.extrude_side([(u0, v0), (u1, v0), (u1, v1), (u0, v1)], x0, x1, mat_name, bevel)

    def cylinder_u(self, u0, u1, v, x, r, mat_name, sides=10, r1=None):
        """Cylinder along the barrel axis."""
        r1 = r if r1 is None else r1
        bm = bmesh.new()
        a, b = [], []
        for i in range(sides):
            ang = 2 * math.pi * i / sides + math.pi / sides
            a.append(bm.verts.new(P(u0, v + math.sin(ang) * r, x + math.cos(ang) * r)))
            b.append(bm.verts.new(P(u1, v + math.sin(ang) * r1, x + math.cos(ang) * r1)))
        bm.faces.new(list(reversed(a)))
        bm.faces.new(b)
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((a[i], a[j], b[j], b[i]))
        self._merge(bm, mat_name)

    def cylinder_x(self, u, v, x0, x1, r, mat_name, sides=8):
        """Cylinder across the gun (pins, screws, buttons)."""
        bm = bmesh.new()
        a, b = [], []
        for i in range(sides):
            ang = 2 * math.pi * i / sides
            a.append(bm.verts.new(P(u + math.cos(ang) * r, v + math.sin(ang) * r, x0)))
            b.append(bm.verts.new(P(u + math.cos(ang) * r, v + math.sin(ang) * r, x1)))
        bm.faces.new(a)
        bm.faces.new(list(reversed(b)))
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((a[i], a[j], b[j], b[i]))
        self._merge(bm, mat_name)

    def tube(self, uv_pts, x, width, thick, mat_name):
        """Rectangular tube following a (u, v) polyline (trigger guard)."""
        bm = bmesh.new()
        loops = []
        for i, (u, v) in enumerate(uv_pts):
            pu, pv = uv_pts[max(i - 1, 0)]
            nu, nv = uv_pts[min(i + 1, len(uv_pts) - 1)]
            du, dv = nu - pu, nv - pv
            L = math.hypot(du, dv) or 1.0
            nu_, nv_ = -dv / L, du / L  # in-plane normal
            ring = []
            for sx, sn in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                ring.append(bm.verts.new(P(u + nu_ * thick * sn, v + nv_ * thick * sn, x + width * sx)))
            loops.append(ring)
        for k in range(len(loops) - 1):
            a, b = loops[k], loops[k + 1]
            for i in range(4):
                j = (i + 1) % 4
                bm.faces.new((a[i], a[j], b[j], b[i]))
        bm.faces.new(list(reversed(loops[0])))
        bm.faces.new(loops[-1])
        self._merge(bm, mat_name)

    def _merge(self, bm, mat_name, bevel=0.0):
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type="OFFSET", segments=1,
                            profile=0.5, affect="EDGES", clamp_overlap=True)
        me = bpy.data.meshes.new("tmp")
        bm.to_mesh(me)
        bm.free()
        idx = self._mat_index(mat_name)
        for p in me.polygons:
            p.material_index = idx
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def build(self, parent):
        me = bpy.data.meshes.new(self.name)
        for v in self.bm.verts:
            v.co -= self.pivot
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = False
        for m in self.mats:
            me.materials.append(material(m))
        ob = bpy.data.objects.new(self.name, me)
        _coll().objects.link(ob)
        ob.parent = parent
        ob.location = self.pivot
        return ob


def socket(name, u, v, x, parent, forward=True):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "ARROWS"
    e.empty_display_size = 0.02
    _coll().objects.link(e)
    e.parent = parent
    e.location = P(u, v, x)
    return e


# ---------------------------------------------------------------- parts
RIGHT, LEFT = -1.0, 1.0   # Blender X sign of the gun's right / left side
SW, FW, GW = 0.0115, 0.0105, 0.011   # half widths: slide, frame, grip frame


def build_frame():
    p = Part("M1911_Frame")
    # Receiver / dust cover (side profile)
    p.extrude_side([(-0.012, 0.100), (0.185, 0.100), (0.185, 0.090), (0.150, 0.0875), (0.108, 0.0875),
                    (0.070, 0.085), (0.061, 0.079), (-0.004, 0.079), (-0.018, 0.087), (-0.020, 0.096)],
                   -FW, FW, "G_Frame", bevel=0.0007)
    # Slide rails (subtle step under the slide)
    p.box(0.004, 0.182, 0.0995, 0.1015, -FW + 0.0012, FW - 0.0012, "G_Frame")
    # Grip frame (18° grip angle, arched mainspring housing)
    p.extrude_side([(0.062, 0.080), (0.059, 0.052), (0.047, 0.004), (0.031, -0.029), (-0.033, -0.029),
                    (-0.031, -0.010), (-0.024, 0.030), (-0.012, 0.066), (-0.006, 0.080)],
                   -GW, GW, "G_Frame", bevel=0.0008)
    # Mainspring housing serrations (rear strap)
    for i in range(7):
        t = i / 6
        u = -0.031 + (-0.013 + 0.031) * t
        v = -0.020 + (0.058 + 0.020) * t
        p.box(u - 0.0012, u + 0.0002, v - 0.0008, v + 0.0008, -GW + 0.002, GW - 0.002, "G_Dark")
    # Trigger guard
    p.tube([(0.064, 0.078), (0.068, 0.060), (0.078, 0.0525), (0.128, 0.0525), (0.140, 0.059),
            (0.1455, 0.072), (0.1445, 0.0875)], 0.0, 0.0045, 0.0022, "G_Frame")
    # Pins: hammer pin, sear pin, slide-stop pin (both sides), plunger tube (left)
    for u, v in ((-0.004, 0.0935), (0.011, 0.0925), (0.089, 0.0955)):
        p.cylinder_x(u, v, -FW - 0.0006, FW + 0.0006, 0.0018, "G_Steel", sides=6)
    p.cylinder_u(0.030, 0.052, 0.1035, FW + 0.0014, 0.0022, "G_Frame", sides=6)
    # Magazine well opening (dark) at the bottom of the grip
    p.extrude_side([(0.027, -0.0292), (-0.028, -0.0292), (-0.026, -0.0270), (0.025, -0.0270)],
                   -GW + 0.0025, GW - 0.0025, "G_Dark")
    return p


def build_slide():
    p = Part("M1911_Slide")
    section = [(-SW, 0.1005), (SW, 0.1005), (SW, 0.1245), (SW - 0.003, 0.1310),
               (-SW + 0.003, 0.1310), (-SW, 0.1245)]
    p.extrude_section(section, 0.0, 0.215, "G_Slide", bevel=0.0006)
    # Rear serrations, both sides
    for i in range(9):
        u = 0.008 + i * 0.0036
        for side in (RIGHT, LEFT):
            x0 = side * SW
            p.box(u, u + 0.0014, 0.1035, 0.1235, x0 - 0.0003 * side, x0 + 0.0004 * side, "G_Dark")
    # Ejection port frame (right side). The barrel hood fills the opening.
    for u0, u1, v0, v1 in ((0.068, 0.117, 0.1275, 0.1290), (0.068, 0.117, 0.1115, 0.1130),
                           (0.068, 0.0695, 0.1115, 0.1290), (0.1155, 0.117, 0.1115, 0.1290)):
        p.box(u0, u1, v0, v1, RIGHT * SW - 0.0006, RIGHT * SW + 0.0002, "G_Dark")
    # Barrel bushing + recoil spring plug on the front face
    p.cylinder_u(0.213, 0.2175, BORE_V, 0.0, 0.0098, "G_Steel", sides=12)
    p.cylinder_u(0.212, 0.2165, 0.1045, 0.0, 0.0052, "G_Steel", sides=8)
    # Sights: rear (two posts + base), front blade
    p.box(0.010, 0.022, 0.1310, 0.1335, -0.0082, 0.0082, "G_Dark", bevel=0.0003)
    for side in (-1, 1):
        p.box(0.012, 0.021, 0.1335, 0.1375, side * 0.0022, side * 0.0078, "G_Dark", bevel=0.0003)
    p.extrude_side([(0.194, 0.1310), (0.204, 0.1310), (0.2035, 0.1368), (0.1965, 0.1368)],
                   -0.0017, 0.0017, "G_Dark")
    # Extractor (right side, behind the port)
    p.box(0.020, 0.062, 0.1195, 0.1225, RIGHT * SW - 0.0005, RIGHT * SW, "G_Hood")
    return p


def build_barrel():
    p = Part("M1911_Barrel")
    # Hood visible through the ejection port, chamber block, muzzle crown and bore
    p.box(0.0695, 0.1155, 0.1130, 0.1275, RIGHT * SW - 0.00005, 0.006, "G_Hood")
    p.cylinder_u(0.117, 0.2172, BORE_V, 0.0, 0.0070, "G_Steel", sides=10)
    p.cylinder_u(0.2172, 0.2180, BORE_V, 0.0, 0.0058, "G_Steel", sides=10)
    p.cylinder_u(0.2176, 0.2182, BORE_V, 0.0, 0.0034, "G_Dark", sides=8)
    return p


def build_grip(side):
    name = "M1911_Grip_R" if side == RIGHT else "M1911_Grip_L"
    p = Part(name)
    outline = [(0.054, 0.071), (0.041, 0.010), (0.028, -0.021), (-0.027, -0.021), (-0.024, 0.000),
               (-0.015, 0.046), (-0.005, 0.071)]
    x0, x1 = side * GW, side * (GW + 0.0042)
    p.extrude_side(outline, min(x0, x1), max(x0, x1), "G_Wood", bevel=0.0012)
    # Double-diamond checkering fields (slightly raised, darker) and two screws
    xs = side * (GW + 0.0042)
    for cu, cv in ((0.024, 0.052), (0.003, -0.004)):
        diamond = [(cu, cv + 0.013), (cu + 0.010, cv), (cu, cv - 0.013), (cu - 0.010, cv)]
        p.extrude_side(diamond, min(xs, xs + side * 0.0004), max(xs, xs + side * 0.0004), "G_WoodDark")
        for du, dv, sz in ((-0.004, 0.022, 0.004), (0.008, -0.020, 0.004)):
            pass
    for cu, cv in ((0.028, 0.061), (0.004, -0.013)):
        p.cylinder_x(cu, cv, min(xs, xs + side * 0.0012), max(xs, xs + side * 0.0012), 0.0028, "G_Screw", sides=8)
    return p


def build_magazine():
    # Magazine follows the grip angle; pivot at the top of the magazine (so it can slide out along its axis).
    ang = math.radians(18.0)
    top_u, top_v = 0.018, 0.078
    p = Part("M1911_Magazine", pivot_uv=(top_u, top_v))
    d = (-math.sin(ang), -math.cos(ang))     # down the grip
    f = (math.cos(ang), -math.sin(ang))      # toward the front strap

    def q(along, fwd):
        return (top_u + d[0] * along + f[0] * fwd, top_v + d[1] * along + f[1] * fwd)

    body = [q(0.0, -0.015), q(0.0, 0.017), q(0.108, 0.017), q(0.108, -0.015)]
    p.extrude_side(body, -0.0094, 0.0094, "G_MagBody", bevel=0.0005)
    base = [q(0.106, -0.018), q(0.106, 0.020), q(0.1135, 0.020), q(0.1135, -0.018)]
    p.extrude_side(base, -0.0108, 0.0108, "G_MagBody", bevel=0.0010)
    # Top round (brass case + copper bullet) sitting on the follower
    cu, cv = q(0.004, -0.004)
    p.cylinder_u(cu - 0.008, cu + 0.012, cv + 0.004, 0.0, 0.0058, "G_Brass", sides=8)
    p.cylinder_u(cu + 0.012, cu + 0.020, cv + 0.004, 0.0, 0.0058, "G_Copper", sides=8, r1=0.0030)
    return p


def build_hammer():
    p = Part("M1911_Hammer", pivot_uv=(-0.004, 0.0935))
    # Cocked ring hammer: spur back and up.
    p.extrude_side([(0.003, 0.090), (0.004, 0.103), (-0.003, 0.113), (-0.013, 0.121), (-0.019, 0.120),
                    (-0.018, 0.114), (-0.010, 0.105), (-0.009, 0.090)], -0.0035, 0.0035, "G_Steel", bevel=0.0004)
    p.cylinder_x(-0.0145, 0.1160, -0.0036, 0.0036, 0.0022, "G_Dark", sides=6)  # ring hole look
    return p


def build_trigger():
    p = Part("M1911_Trigger", pivot_uv=(0.101, 0.0875))
    p.extrude_side([(0.098, 0.0870), (0.106, 0.0870), (0.1065, 0.0640), (0.1015, 0.0620), (0.0975, 0.0640)],
                   -0.0048, 0.0048, "G_Steel", bevel=0.0004)
    for i in range(3):  # face grooves
        v = 0.068 + i * 0.006
        p.box(0.0968, 0.0978, v, v + 0.0015, -0.0046, 0.0046, "G_Dark")
    return p


def build_safety():
    p = Part("M1911_Safety", pivot_uv=(0.011, 0.0925), pivot_x=LEFT * FW)
    x0, x1 = LEFT * FW, LEFT * (FW + 0.0022)
    p.extrude_side([(0.002, 0.0960), (0.030, 0.0985), (0.034, 0.1030), (0.028, 0.1045), (0.004, 0.1015),
                    (-0.002, 0.0980)], min(x0, x1), max(x0, x1), "G_Frame", bevel=0.0004)
    return p


def build_grip_safety():
    p = Part("M1911_GripSafety", pivot_uv=(-0.012, 0.090))
    p.extrude_side([(-0.004, 0.0920), (-0.028, 0.0960), (-0.036, 0.0915), (-0.030, 0.0880),
                    (-0.018, 0.0760), (-0.016, 0.0580), (-0.010, 0.0580), (-0.006, 0.0800)],
                   -0.0092, 0.0092, "G_Frame", bevel=0.0007)
    return p


def build_slide_stop():
    p = Part("M1911_SlideStop", pivot_uv=(0.089, 0.0955), pivot_x=LEFT * FW)
    x0, x1 = LEFT * FW, LEFT * (FW + 0.0024)
    p.extrude_side([(0.093, 0.0930), (0.093, 0.0990), (0.070, 0.1010), (0.060, 0.1000), (0.058, 0.0950),
                    (0.066, 0.0925)], min(x0, x1), max(x0, x1), "G_Frame", bevel=0.0004)
    for i in range(3):
        u = 0.061 + i * 0.003
        p.box(u, u + 0.0012, 0.0955, 0.1000, min(x1, x1 + 0.0004), max(x1, x1 + 0.0004), "G_Dark")
    return p


def build_mag_release():
    p = Part("M1911_MagRelease", pivot_uv=(0.066, 0.0735), pivot_x=LEFT * GW)
    p.cylinder_x(0.066, 0.0735, LEFT * GW, LEFT * (GW + 0.0026), 0.0042, "G_Frame", sides=10)
    return p


def build_ammo():
    """Separate objects: spent casing and live round (.45 ACP), origin at their center."""
    casing = Part("Casing_45ACP", pivot_uv=(1.0115, 0.2))
    casing.cylinder_u(1.0, 1.0228, 0.2, 0.0, 0.0060, "G_Brass", sides=8)
    casing.cylinder_u(0.9995, 1.0015, 0.2, 0.0, 0.0061, "G_Brass", sides=8)
    casing.cylinder_u(1.0226, 1.0230, 0.2, 0.0, 0.0045, "G_Dark", sides=8)
    rnd = Part("Round_45ACP", pivot_uv=(1.016, 0.3))
    rnd.cylinder_u(1.0, 1.0228, 0.3, 0.0, 0.0060, "G_Brass", sides=8)
    rnd.cylinder_u(1.0228, 1.0300, 0.3, 0.0, 0.0057, "G_Copper", sides=8, r1=0.0040)
    rnd.cylinder_u(1.0300, 1.0325, 0.3, 0.0, 0.0040, "G_Copper", sides=8, r1=0.0018)
    return casing, rnd


# ---------------------------------------------------------------- assemble
def clear_previous():
    c = bpy.data.collections.get(COLLECTION)
    if c:
        for ob in list(c.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)


def build():
    clear_previous()
    root = bpy.data.objects.new("M1911", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.03
    _coll().objects.link(root)
    parts = [build_frame(), build_slide(), build_barrel(), build_grip(RIGHT), build_grip(LEFT),
             build_magazine(), build_hammer(), build_trigger(), build_safety(), build_grip_safety(),
             build_slide_stop(), build_mag_release()]
    objs = [p.build(root) for p in parts]
    # Sockets (empties). Hands: right hand wraps the grip, left (support) hand cups it from the left side.
    socket("Socket_Muzzle", 0.2185, BORE_V, 0.0, root)
    socket("Socket_EjectionPort", 0.092, 0.121, RIGHT * (SW + 0.002), root)
    socket("Socket_RearSight", 0.0165, 0.1352, 0.0, root)
    socket("Socket_FrontSight", 0.199, 0.1368, 0.0, root)
    socket("Socket_RightHand", ROOT_U, ROOT_V, 0.0, root)
    socket("Socket_LeftHand", 0.030, 0.030, LEFT * 0.030, root)
    socket("Socket_MagWell", 0.0, -0.029, 0.0, root)
    ammo_root = bpy.data.objects.new("Ammo_45ACP", None)
    _coll().objects.link(ammo_root)
    casing, rnd = build_ammo()
    c_ob = casing.build(ammo_root)
    r_ob = rnd.build(ammo_root)
    tris = sum(len(p.vertices) - 2 for o in objs + [c_ob, r_ob] for p in o.data.polygons)
    return {"parts": [o.name for o in objs], "triangles": tris}


result = build()
