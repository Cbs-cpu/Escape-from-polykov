"""
Low-poly Colt M1911A1 (.45 ACP) for Escape from Polykov, modular like the reference sheet:
slide, frame, barrel/bushing, grip panels, magazine, hammer, trigger, safety, slide stop.

Hierarchy and names match Unity's WeaponModel (Scripts/Weapons/WeaponModel.cs):
  M1911 (root, origin on the frame top above the trigger)
    Slide, Frame, Grips, Trigger (pivot), Hammer (pivot), Safety, SlideStop,
    GripFrame (empty, raked 18 deg) -> GripCenter, Magazine -> MagazineGrab
    Muzzle, SightLine (empties)

Authoring is done in *Unity weapon space* (x right, y up, z toward the muzzle, meters) through U(),
which converts to Blender space (character convention: forward = -Y, up = +Z, right = -X).

Run inside Blender:  exec(open(r"<repo>/ArtSource/Tools/build_m1911.py", encoding="utf-8").read())
or headless:         blender -b -P ArtSource/Tools/build_m1911.py
"""
import bpy
import bmesh
import math
from mathutils import Vector, Matrix, Euler

COLLECTION = "M1911"

PALETTE = {
    "M_Steel": ("#2a2c2e", 0.45, 0.85),
    "M_SteelDark": ("#161718", 0.55, 0.8),
    "M_SteelWorn": ("#4a4c4e", 0.35, 0.9),
    "M_Wood": ("#5a3520", 0.7, 0.0),
    "M_WoodDark": ("#3d2415", 0.75, 0.0),
    "M_Brass": ("#b08d45", 0.35, 0.9),
}


def U(x, y, z):
    """Unity weapon space -> Blender space."""
    return Vector((-x, -z, y))


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
    return m


def _coll():
    c = bpy.data.collections.get(COLLECTION)
    if c is None:
        c = bpy.data.collections.new(COLLECTION)
        bpy.context.scene.collection.children.link(c)
    return c


class Part:
    """Accumulates boxes into one bmesh with per-face materials, then becomes one object."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def _mat_index(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def box(self, center, size, mat, bevel=0.0, rot=(0.0, 0.0, 0.0), taper_top=1.0, taper_front=1.0):
        """center/size/rot in Unity weapon space (degrees). taper_* shrink the +Y / +Z faces."""
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        for v in tmp.verts:
            x, y, z = v.co
            sx = size[0] * (taper_top if y > 0 else 1.0) * (taper_front if z > 0 else 1.0)
            sy = size[1] * (taper_front if z > 0 else 1.0)
            v.co = Vector((x * sx, y * sy, z * size[2]))
        if bevel > 0:
            bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=bevel, segments=1, affect="EDGES", profile=0.5)
        r = Euler([math.radians(a) for a in rot], "ZXY").to_matrix()
        idx = self._mat_index(mat)
        mapping = {}
        for v in tmp.verts:
            p = r @ v.co + Vector(center)
            mapping[v] = self.bm.verts.new(U(*p))
        for f in tmp.faces:
            nf = self.bm.faces.new([mapping[v] for v in f.verts])
            nf.material_index = idx
        tmp.free()

    def cylinder(self, center, radius, length, mat, sides=10, axis="z"):
        tmp = bmesh.new()
        bmesh.ops.create_cone(tmp, cap_ends=True, cap_tris=False, segments=sides, radius1=radius, radius2=radius,
                              depth=length)
        rot = {"z": Matrix.Identity(3), "x": Matrix.Rotation(math.pi / 2, 3, "Y"),
               "y": Matrix.Rotation(math.pi / 2, 3, "X")}[axis]
        idx = self._mat_index(mat)
        mapping = {}
        for v in tmp.verts:
            p = rot @ v.co + Vector(center)
            mapping[v] = self.bm.verts.new(U(*p))
        for f in tmp.faces:
            nf = self.bm.faces.new([mapping[v] for v in f.verts])
            nf.material_index = idx
        tmp.free()

    def build(self, pivot=(0.0, 0.0, 0.0), parent=None):
        """Object origin at pivot (weapon space); vertices stored relative to it."""
        offset = U(*pivot)
        for v in self.bm.verts:
            v.co -= offset
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = False
        for m in self.mats:
            me.materials.append(material(m))
        ob = bpy.data.objects.new(self.name, me)
        _coll().objects.link(ob)
        ob.location = offset
        if parent is not None:
            set_parent(ob, parent)
        return ob


def empty(name, position, parent, rot_x=0.0, size=0.01):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "PLAIN_AXES"
    ob.empty_display_size = size
    _coll().objects.link(ob)
    ob.location = U(*position)
    # Unity +X rotation (muzzle down) == rotation about Blender -X.
    ob.rotation_euler = Euler((math.radians(-rot_x), 0.0, 0.0))
    if parent is not None:
        set_parent(ob, parent)
    return ob


def set_parent(child, parent):
    # Matrices of freshly created objects are stale until the depsgraph updates (always in background mode).
    bpy.context.view_layer.update()
    world = child.matrix_world.copy()
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()
    child.matrix_world = world


def clear_previous():
    c = bpy.data.collections.get(COLLECTION)
    if c:
        for ob in list(c.objects):
            data = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if data is not None and data.users == 0:
                bpy.data.meshes.remove(data)


def grip_space(p):
    """Point in the raked grip frame (origin (0,-0.02,-0.044), 18 deg) -> weapon space."""
    r = Euler((math.radians(18.0), 0.0, 0.0), "ZXY").to_matrix()
    return tuple(Vector((0.0, -0.02, -0.044)) + r @ Vector(p))


def build():
    clear_previous()
    root = bpy.data.objects.new("M1911", None)
    root.empty_display_type = "ARROWS"
    root.empty_display_size = 0.05
    _coll().objects.link(root)

    # ---------------------------------------------------------------- slide (moves along -Z)
    s = Part("Slide")
    s.box((0.0, 0.0155, 0.045), (0.0235, 0.029, 0.212), "M_Steel", bevel=0.0015, taper_top=0.78)
    s.box((0.0, 0.0312, 0.045), (0.0125, 0.0035, 0.2), "M_Steel", bevel=0.0008)                   # flat top rib
    s.box((0.0, 0.0368, 0.142), (0.0032, 0.0062, 0.009), "M_Steel", bevel=0.0006, taper_top=0.7)  # front sight
    for x in (-0.0052, 0.0052):                                                                     # rear sight ears
        s.box((x, 0.0362, -0.0525), (0.0055, 0.0058, 0.008), "M_Steel", bevel=0.0006)
    s.box((0.0, 0.0342, -0.0525), (0.0165, 0.0022, 0.008), "M_Steel")                              # rear sight base
    for i in range(7):                                                                              # rear serrations
        z = -0.053 + i * 0.0048
        for x in (0.0121, -0.0121):
            s.box((x, 0.0165, z), (0.0012, 0.021, 0.0022), "M_SteelDark")
    s.box((0.0118, 0.0245, 0.028), (0.0016, 0.011, 0.032), "M_SteelDark")                          # ejection port
    s.box((0.0, 0.012, 0.1515), (0.0175, 0.0175, 0.004), "M_SteelWorn", bevel=0.001)               # bushing
    s.cylinder((0.0, 0.012, 0.154), 0.0062, 0.004, "M_SteelDark", sides=10, axis="z")               # muzzle crown
    s.cylinder((0.0, 0.012, 0.1545), 0.0036, 0.0045, "M_SteelDark", sides=8, axis="z")              # bore
    slide = s.build(parent=root)

    # ---------------------------------------------------------------- frame
    f = Part("Frame")
    f.box((0.0, -0.0065, 0.079), (0.0215, 0.013, 0.112), "M_Steel", bevel=0.0012)                 # dust cover
    f.box((0.0, -0.011, -0.03), (0.0235, 0.022, 0.1), "M_Steel", bevel=0.0012)                    # frame body
    f.box((0.0, -0.0205, 0.018), (0.0235, 0.009, 0.018), "M_Steel", bevel=0.001)                  # trigger housing
    # trigger guard: a loop of boxes
    # (bottom bar runs from the grip's front strap, z ~ -0.03, to the front loop)
    f.box((0.0, -0.0385, 0.0085), (0.0075, 0.004, 0.078), "M_Steel", bevel=0.0012)
    f.box((0.0, -0.0245, 0.0465), (0.0075, 0.03, 0.0042), "M_Steel", bevel=0.0012, rot=(-12.0, 0.0, 0.0))
    f.box((0.0, -0.0015, -0.0765), (0.0255, 0.0065, 0.032), "M_Steel", bevel=0.0015, taper_front=0.8)  # beavertail
    # grip frame (raked)
    f.box(grip_space((0.0, -0.046, 0.0)), (0.0255, 0.092, 0.041), "M_Steel", bevel=0.0015, rot=(18.0, 0.0, 0.0))
    f.box(grip_space((0.0, -0.047, -0.0215)), (0.022, 0.078, 0.004), "M_SteelDark", rot=(18.0, 0.0, 0.0))  # MSH checkering
    f.box(grip_space((0.0, -0.047, 0.0215)), (0.02, 0.07, 0.003), "M_SteelDark", rot=(18.0, 0.0, 0.0))     # front strap
    frame = f.build(parent=root)

    g = Part("Grips")
    for x in (0.0145, -0.0145):
        g.box(grip_space((x, -0.047, 0.001)), (0.0045, 0.075, 0.035), "M_Wood", bevel=0.0012, rot=(18.0, 0.0, 0.0))
        for k in range(4):                                                                        # diamond cuts
            yy = -0.03 - k * 0.013
            g.box(grip_space((x * 1.2, yy, 0.001)), (0.001, 0.004, 0.026), "M_WoodDark", rot=(18.0, 0.0, 0.0))
        g.cylinder(grip_space((x * 1.22, -0.022, 0.0)), 0.0028, 0.0012, "M_SteelWorn", sides=8, axis="x")  # screws
        g.cylinder(grip_space((x * 1.22, -0.072, 0.0)), 0.0028, 0.0012, "M_SteelWorn", sides=8, axis="x")
    g.build(parent=root)

    # ---------------------------------------------------------------- small parts
    t = Part("Trigger")
    t.box((0.0, -0.0185, 0.0065), (0.0062, 0.017, 0.0048), "M_SteelWorn", bevel=0.001, rot=(-8.0, 0.0, 0.0))
    t.build(pivot=(0.0, -0.01, 0.006), parent=root)

    h = Part("Hammer")
    h.box((0.0, 0.012, -0.0655), (0.0075, 0.017, 0.0065), "M_Steel", bevel=0.0012, rot=(-10.0, 0.0, 0.0))
    h.box((0.0, 0.0205, -0.0685), (0.0105, 0.0045, 0.009), "M_SteelDark", bevel=0.001)          # spur
    h.build(pivot=(0.0, 0.004, -0.062), parent=root)

    sf = Part("Safety")
    sf.box((-0.0138, 0.0035, -0.05), (0.0028, 0.0055, 0.017), "M_Steel", bevel=0.0008)
    sf.box((-0.0152, 0.0065, -0.042), (0.004, 0.0025, 0.009), "M_Steel", bevel=0.0006)
    sf.build(parent=root)

    st = Part("SlideStop")
    st.box((-0.0138, -0.004, 0.022), (0.0028, 0.0065, 0.021), "M_Steel", bevel=0.0008)
    st.cylinder((-0.0128, -0.0015, 0.0335), 0.0022, 0.004, "M_SteelWorn", sides=8, axis="x")
    st.build(parent=root)

    # ---------------------------------------------------------------- grip frame, magazine, points
    grip_frame = empty("GripFrame", (0.0, -0.02, -0.044), root, rot_x=18.0)
    empty("GripCenter", grip_space((0.0, -0.045, 0.0)), grip_frame)

    m = Part("Magazine")
    m.box(grip_space((0.0, -0.05, 0.002)), (0.0195, 0.098, 0.031), "M_SteelDark", rot=(18.0, 0.0, 0.0))
    m.box(grip_space((0.0, -0.0985, 0.002)), (0.025, 0.005, 0.036), "M_Steel", bevel=0.0012, rot=(18.0, 0.0, 0.0))
    m.box(grip_space((0.0, -0.0015, 0.004)), (0.011, 0.004, 0.02), "M_Brass", bevel=0.001, rot=(18.0, 0.0, 0.0))  # top round
    mag = m.build(pivot=(0.0, -0.02, -0.044), parent=grip_frame)
    empty("MagazineGrab", grip_space((-0.03, -0.13, -0.01)), mag)

    empty("Muzzle", (0.0, 0.012, 0.156), root)
    empty("SightLine", (0.0, 0.0395, -0.0525), root)

    tris = sum(len(p.vertices) - 2 for ob in _coll().objects if ob.type == "MESH" for p in ob.data.polygons)
    return {"objects": len(_coll().objects), "triangles": tris}


result = build()
print("[M1911]", result)
