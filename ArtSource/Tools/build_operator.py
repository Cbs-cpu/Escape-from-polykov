"""
Procedural low-poly tactical operator, v2 (reference: character sheet — baseball cap with patch,
wraparound sunglasses, full beard, olive combat jacket with polygon camo, cargo pants, boots).
Builds mesh + Unity-humanoid armature (with fingers) + explicit skin weights.

Conventions: meters, Z up, character faces -Y (exports to Unity +Z forward), T-pose, palms down.
Two skinned meshes: Operator_Body and Operator_Head (the head is hidden for the local first-person camera).

Run inside Blender:  exec(open(r"<path>/build_operator.py", encoding="utf-8").read())
"""
import bpy
import bmesh
import math
from mathutils import Vector, Euler, noise

COLLECTION = "Operator"

# ---------------------------------------------------------------- materials
PALETTE = {
    "M_Skin": ("#c49a78", 0.75, 0.0),
    "M_SkinShade": ("#a97f60", 0.8, 0.0),
    "M_Jacket": ("#61654d", 0.9, 0.0),
    "M_JacketCamoA": ("#4f543f", 0.9, 0.0),
    "M_JacketCamoB": ("#72705a", 0.9, 0.0),
    "M_JacketCamoC": ("#5e5646", 0.9, 0.0),
    "M_JacketDark": ("#474b38", 0.9, 0.0),
    "M_Pants": ("#5a5e47", 0.9, 0.0),
    "M_PantsCamoA": ("#4a4f3b", 0.9, 0.0),
    "M_PantsCamoB": ("#6a6850", 0.9, 0.0),
    "M_PantsCamoC": ("#574f40", 0.9, 0.0),
    "M_PantsDark": ("#43472f", 0.9, 0.0),
    "M_Boots": ("#34322f", 0.7, 0.0),
    "M_BootsDark": ("#262422", 0.7, 0.0),
    "M_Sole": ("#1a1918", 0.85, 0.0),
    "M_Cap": ("#5a5d48", 0.9, 0.0),
    "M_CapDark": ("#4a4d3a", 0.9, 0.0),
    "M_Patch": ("#7b7a62", 0.9, 0.0),
    "M_Beard": ("#553f30", 1.0, 0.0),
    "M_Hair": ("#3a2c22", 1.0, 0.0),
    "M_Glasses": ("#0d0e0f", 0.15, 0.3),
    "M_Metal": ("#5d5f60", 0.4, 0.8),
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


# ---------------------------------------------------------------- mesh helpers
def _coll():
    c = bpy.data.collections.get(COLLECTION)
    if c is None:
        c = bpy.data.collections.new(COLLECTION)
        bpy.context.scene.collection.children.link(c)
    return c


def _finish(bm, name, mat_name, recalc=True):
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    _coll().objects.link(ob)
    me.materials.append(material(mat_name))
    return ob


def _ring_point(c, ring, a, axis):
    """ring = (rx, ry) or (rx, ry_front, ry_back) or (rx, ry_front, ry_back, n) — n = superellipse exponent."""
    rx = ring[0]
    ryf = ring[1]
    ryb = ring[2] if len(ring) > 2 else ring[1]
    n = ring[3] if len(ring) > 3 else 2.0
    ca, sa = math.cos(a), math.sin(a)
    e = 2.0 / n
    u = math.copysign(abs(ca) ** e, ca) * rx
    v = math.copysign(abs(sa) ** e, sa) * (ryf if sa < 0 else ryb)
    if axis == "Z":      # ring in XY, v<0 is front (-Y)
        return (c[0] + u, c[1] + v, c[2])
    if axis == "X":      # ring in YZ: u -> Y, v -> Z
        return (c[0], c[1] + u, c[2] + v)
    return (c[0] + u, c[1], c[2] + v)  # "Y": ring in XZ


def loft(name, rings, mat_name, sides=8, axis="Z", cap_start=True, cap_end=True, phase=0.0):
    """rings: [((cx,cy,cz), rx, ry[, ry_back[, n]]), ...]"""
    bm = bmesh.new()
    loops = []
    for r in rings:
        c, shape = r[0], r[1:]
        loops.append([bm.verts.new(_ring_point(c, shape, 2.0 * math.pi * i / sides + phase, axis))
                      for i in range(sides)])
    for k in range(len(loops) - 1):
        a, b = loops[k], loops[k + 1]
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if cap_start:
        bm.faces.new(list(reversed(loops[0])))
    if cap_end:
        bm.faces.new(loops[-1])
    return _finish(bm, name, mat_name)


def tube(name, points, radii, mat_name, sides=6):
    """Loft along an arbitrary polyline (fingers, straps). radii per point."""
    bm = bmesh.new()
    loops = []
    for i, p in enumerate(points):
        p = Vector(p)
        d = (Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(i - 1, 0)])).normalized()
        ref = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((0, 1, 0))
        u = d.cross(ref).normalized()
        v = d.cross(u).normalized()
        r = radii[i]
        rr = r if isinstance(r, tuple) else (r, r)
        loops.append([bm.verts.new(p + u * math.cos(2 * math.pi * k / sides) * rr[0]
                                   + v * math.sin(2 * math.pi * k / sides) * rr[1]) for k in range(sides)])
    for k in range(len(loops) - 1):
        a, b = loops[k], loops[k + 1]
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(loops[0])))
    bm.faces.new(loops[-1])
    return _finish(bm, name, mat_name)


def box(name, center, size, mat_name, rot=(0.0, 0.0, 0.0), taper=1.0):
    """taper < 1 shrinks the +Z face (for flaps, pockets, toe caps)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        t = taper if v.co.z > 0 else 1.0
        v.co = Vector((v.co.x * size[0] * t, v.co.y * size[1] * t, v.co.z * size[2]))
    m = Euler(rot).to_matrix()
    for v in bm.verts:
        v.co = m @ v.co + Vector(center)
    return _finish(bm, name, mat_name)


def delete_verts(ob, predicate):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if predicate(v.co)], context="VERTS")
    bm.to_mesh(ob.data)
    bm.free()


def mirror_x(ob, name):
    me = ob.data.copy()
    me.name = name
    bm = bmesh.new()
    bm.from_mesh(me)
    for v in bm.verts:
        v.co.x = -v.co.x
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    new = bpy.data.objects.new(name, me)
    _coll().objects.link(new)
    return new


# ---------------------------------------------------------------- skeleton (Unity humanoid names)
# (head, tail, parent) in Blender space. Left = +X because the character faces -Y.
BONES = {
    "Hips": ((0, 0.0, 0.95), (0, 0.0, 1.05), None),
    "Spine": ((0, 0.0, 1.05), (0, 0.0, 1.20), "Hips"),
    "Chest": ((0, 0.0, 1.20), (0, 0.0, 1.34), "Spine"),
    "UpperChest": ((0, 0.0, 1.34), (0, 0.0, 1.47), "Chest"),
    "Neck": ((0, -0.005, 1.47), (0, -0.01, 1.58), "UpperChest"),
    "Head": ((0, -0.01, 1.58), (0, -0.01, 1.80), "Neck"),
    "LeftShoulder": ((0.03, 0.0, 1.44), (0.15, 0.0, 1.44), "UpperChest"),
    "LeftUpperArm": ((0.15, 0.0, 1.43), (0.46, 0.0, 1.43), "LeftShoulder"),
    "LeftLowerArm": ((0.46, 0.0, 1.43), (0.71, 0.0, 1.43), "LeftUpperArm"),
    "LeftHand": ((0.71, 0.0, 1.43), (0.795, 0.0, 1.43), "LeftLowerArm"),
    "LeftUpperLeg": ((0.095, 0.0, 0.93), (0.105, -0.005, 0.53), "Hips"),
    "LeftLowerLeg": ((0.105, -0.005, 0.53), (0.115, 0.01, 0.09), "LeftUpperLeg"),
    "LeftFoot": ((0.115, 0.01, 0.09), (0.115, -0.10, 0.03), "LeftLowerLeg"),
    "LeftToes": ((0.115, -0.10, 0.03), (0.115, -0.17, 0.03), "LeftFoot"),
}

# Fingers: (y offset, length, radius). Palm ends at x = 0.795.
FINGERS = {
    "Index": (-0.029, 0.074, 0.0098),
    "Middle": (-0.009, 0.082, 0.0100),
    "Ring": (0.011, 0.076, 0.0095),
    "Little": (0.029, 0.062, 0.0085),
}
SEGMENTS = (("Proximal", 0.45), ("Intermediate", 0.30), ("Distal", 0.25))
FINGER_Z = 1.426
PALM_END = 0.795

for _f, (_y, _len, _) in FINGERS.items():
    x = PALM_END
    parent = "LeftHand"
    for _seg, frac in SEGMENTS:
        name = f"Left{_f}{_seg}"
        BONES[name] = ((x, _y, FINGER_Z), (x + _len * frac, _y, FINGER_Z), parent)
        x += _len * frac
        parent = name
# Thumb: from the base of the palm, pointing forward (-Y) and out (+X), slightly down.
THUMB = [(0.722, -0.028, 1.420), (0.750, -0.055, 1.414), (0.772, -0.075, 1.410), (0.790, -0.090, 1.407)]
BONES["LeftThumbProximal"] = (THUMB[0], THUMB[1], "LeftHand")
BONES["LeftThumbIntermediate"] = (THUMB[1], THUMB[2], "LeftThumbProximal")
BONES["LeftThumbDistal"] = (THUMB[2], THUMB[3], "LeftThumbIntermediate")

for _n in [n for n in BONES if n.startswith("Left")]:
    h, t, p = BONES[_n]
    BONES["Right" + _n[4:]] = ((-h[0], h[1], h[2]), (-t[0], t[1], t[2]),
                              ("Right" + p[4:]) if p and p.startswith("Left") else p)


def build_armature():
    arm = bpy.data.armatures.new("Operator_Armature")
    ob = bpy.data.objects.new("Operator_Rig", arm)
    _coll().objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (h, t, _) in BONES.items():
        b = arm.edit_bones.new(name)
        b.head, b.tail = Vector(h), Vector(t)
        b.roll = 0.0
    for name, (_, _, parent) in BONES.items():
        if parent:
            arm.edit_bones[name].parent = arm.edit_bones[parent]
            arm.edit_bones[name].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    arm.display_type = "STICK"
    ob.show_in_front = True
    return ob


# ---------------------------------------------------------------- weighting (explicit, region based)
def _smooth(e0, e1, x):
    if e1 == e0:
        return 1.0 if x >= e1 else 0.0
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def chain(s, bones, cuts, width):
    """Piecewise weights along a scalar s (increasing). bones[i] covers (cuts[i-1], cuts[i])."""
    ts = [_smooth(c - width, c + width, s) for c in cuts]
    out = {}
    for i, b in enumerate(bones):
        lo = ts[i - 1] if i > 0 else 1.0
        hi = ts[i] if i < len(ts) else 0.0
        w = lo - hi
        if w > 0.001:
            out[b] = out.get(b, 0.0) + w
    return out


def apply_weights(ob, fn):
    for v in ob.data.vertices:
        ws = fn(ob.matrix_world @ v.co)
        total = sum(ws.values())
        for bone, w in ws.items():
            if w / total < 0.01:
                continue
            g = ob.vertex_groups.get(bone) or ob.vertex_groups.new(name=bone)
            g.add([v.index], w / total, "REPLACE")


def _side(p):
    return "Left" if p.x > 0 else "Right"


def w_torso(p):
    z = p.z
    ws = chain(z, ["Hips", "Spine", "Chest", "UpperChest", "Neck"], [1.02, 1.20, 1.34, 1.52], 0.05)
    side = _side(p)
    sh = _smooth(0.10, 0.19, abs(p.x)) * _smooth(1.30, 1.42, z)
    if sh > 0:
        ws = {k: v * (1 - sh) for k, v in ws.items()}
        ws[side + "Shoulder"] = ws.get(side + "Shoulder", 0) + sh * 0.6
        ws[side + "UpperArm"] = ws.get(side + "UpperArm", 0) + sh * 0.4 * _smooth(0.16, 0.22, abs(p.x))
    # Jacket hem follows the thighs a little.
    hem = 1.0 - _smooth(0.86, 0.95, z)
    if hem > 0:
        ws = {k: v * (1 - hem * 0.4) for k, v in ws.items()}
        ws[side + "UpperLeg"] = ws.get(side + "UpperLeg", 0) + hem * 0.4 * _smooth(0.0, 0.08, abs(p.x))
    return ws


def w_arm(p):
    side = _side(p)
    return chain(abs(p.x), ["UpperChest", side + "Shoulder", side + "UpperArm", side + "LowerArm", side + "Hand"],
                 [0.11, 0.19, 0.46, 0.71], 0.035)


def w_finger(finger):
    y, length, _ = FINGERS[finger]
    c1 = PALM_END + length * SEGMENTS[0][1]
    c2 = c1 + length * SEGMENTS[1][1]

    def fn(p):
        side = _side(p)
        return chain(abs(p.x), [side + "Hand"] + [side + finger + s for s, _ in SEGMENTS],
                     [PALM_END, c1, c2], 0.006)
    return fn


def w_thumb(p):
    side = _side(p)
    a, d = Vector(THUMB[0]), Vector(THUMB[3]) - Vector(THUMB[0])
    q = Vector((abs(p.x), p.y, p.z))
    t = (q - a).dot(d) / d.length_squared
    l1 = (Vector(THUMB[1]) - a).length / d.length
    l2 = (Vector(THUMB[2]) - a).length / d.length
    return chain(t, [side + "Hand", side + "ThumbProximal", side + "ThumbIntermediate", side + "ThumbDistal"],
                 [0.02, l1, l2], 0.06)


def w_leg(p):
    side = _side(p)
    return chain(-p.z, ["Hips", side + "UpperLeg", side + "LowerLeg", side + "Foot"], [-0.95, -0.53, -0.11], 0.05)


def w_boot(p):
    side = _side(p)
    if p.z > 0.14:
        return {side + "LowerLeg": 1.0}
    ws = chain(-p.y, [side + "Foot", side + "Toes"], [0.095], 0.03)
    up = _smooth(0.08, 0.14, p.z)
    ws = {k: v * (1 - up) for k, v in ws.items()}
    ws[side + "LowerLeg"] = up
    return ws


def w_head(p):
    return chain(p.z, ["UpperChest", "Neck", "Head"], [1.495, 1.585], 0.03)


# ---------------------------------------------------------------- body parts
def build_parts():
    parts = []  # (object, weight_fn, group)

    def add(ob, fn, group="Body"):
        parts.append((ob, fn, group))
        return ob

    def add_lr(ob, fn, group="Body"):
        add(ob, fn, group)
        add(mirror_x(ob, ob.name.replace("_L", "_R")), fn, group)

    # ======================= JACKET
    # (center, half width, half depth front, half depth back, superellipse n)
    add(loft("Jacket_Torso", [
        ((0, 0.000, 0.820), 0.196, 0.138, 0.128, 2.4),
        ((0, 0.000, 0.845), 0.194, 0.136, 0.126, 2.4),
        ((0, 0.000, 0.930), 0.188, 0.130, 0.122, 2.4),
        ((0, 0.000, 1.040), 0.172, 0.124, 0.116, 2.5),
        ((0, -0.004, 1.160), 0.182, 0.134, 0.120, 2.6),
        ((0, -0.010, 1.270), 0.203, 0.146, 0.124, 2.7),
        ((0, -0.008, 1.360), 0.220, 0.138, 0.126, 2.8),
        ((0, -0.002, 1.425), 0.212, 0.118, 0.114, 2.8),
        ((0, 0.000, 1.462), 0.172, 0.100, 0.102, 2.5),
        ((0, 0.000, 1.492), 0.108, 0.086, 0.090, 2.2),
        ((0, 0.000, 1.505), 0.084, 0.080, 0.082, 2.0),
    ], "M_Jacket", sides=16, cap_start=True, cap_end=False), w_torso)
    # Stand collar, open at the front.
    collar = loft("Jacket_Collar", [
        ((0, 0.000, 1.490), 0.090, 0.084, 0.088),
        ((0, 0.000, 1.535), 0.088, 0.082, 0.086),
        ((0, 0.002, 1.572), 0.094, 0.088, 0.088),
    ], "M_Jacket", sides=16, cap_start=False, cap_end=False)
    delete_verts(collar, lambda c: c.y < -0.060 and abs(c.x) < 0.030)
    add(collar, w_torso)
    add(box("Jacket_Zip", (0, -0.142, 1.16), (0.010, 0.012, 0.64), "M_JacketDark", rot=(math.radians(-1.5), 0, 0)), w_torso)
    for sx in (1, -1):
        # Diagonal chest zip pockets (typical combat shirt).
        add(box(f"Jacket_ChestZip_{sx}", (0.085 * sx, -0.153, 1.335), (0.105, 0.008, 0.008), "M_JacketDark",
                rot=(0, math.radians(28 * sx), 0)), w_torso)
        add(box(f"Jacket_ChestPocket_{sx}", (0.090 * sx, -0.147, 1.300), (0.100, 0.016, 0.085), "M_JacketCamoA",
                rot=(0, math.radians(8 * sx), 0)), w_torso)
        # Raglan seams from collar to armpit.
        add(box(f"Jacket_Raglan_{sx}", (0.140 * sx, -0.118, 1.425), (0.006, 0.008, 0.150), "M_JacketDark",
                rot=(0, math.radians(-52 * sx), 0)), w_torso)
        add(box(f"Jacket_HipPocket_{sx}", (0.118 * sx, -0.134, 0.915), (0.110, 0.020, 0.105), "M_JacketCamoA"), w_torso)
        add(box(f"Jacket_HipFlap_{sx}", (0.118 * sx, -0.141, 0.965), (0.118, 0.014, 0.030), "M_JacketDark"), w_torso)
    add(box("Jacket_BackYoke", (0, 0.125, 1.405), (0.30, 0.010, 0.012), "M_JacketDark"), w_torso)

    sleeve = loft("Jacket_Sleeve_L", [
        ((0.120, 0.000, 1.425), 0.078, 0.082),
        ((0.200, 0.000, 1.433), 0.085, 0.088),
        ((0.255, 0.000, 1.433), 0.080, 0.082),
        ((0.330, 0.000, 1.431), 0.068, 0.070),
        ((0.420, 0.000, 1.430), 0.058, 0.060),
        ((0.455, -0.004, 1.428), 0.060, 0.056),
        ((0.490, 0.000, 1.430), 0.056, 0.057),
        ((0.600, 0.000, 1.430), 0.051, 0.051),
        ((0.668, 0.000, 1.430), 0.050, 0.050),
        ((0.678, 0.000, 1.430), 0.054, 0.054),
        ((0.705, 0.000, 1.430), 0.053, 0.053),
        ((0.708, 0.000, 1.430), 0.040, 0.040),
    ], "M_Jacket", sides=10, axis="X", cap_start=False, cap_end=True, phase=math.pi / 10)
    add(sleeve, w_arm)
    add(mirror_x(sleeve, "Jacket_Sleeve_R"), w_arm)
    add_lr(box("Jacket_ArmPocket_L", (0.300, -0.030, 1.482), (0.090, 0.070, 0.016), "M_JacketCamoA",
               rot=(math.radians(-25), 0, 0)), w_arm)
    add_lr(box("Jacket_ArmPatch_L", (0.300, -0.036, 1.494), (0.060, 0.045, 0.008), "M_Patch",
               rot=(math.radians(-25), 0, 0)), w_arm)
    add_lr(box("Jacket_CuffTab_L", (0.690, -0.030, 1.465), (0.024, 0.030, 0.010), "M_JacketDark",
               rot=(math.radians(-35), 0, 0)), w_arm)

    # ======================= HANDS (palm + 4 fingers + thumb)
    palm = loft("Hand_Palm_L", [
        ((0.700, 0.000, 1.430), 0.034, 0.027),
        ((0.722, 0.000, 1.430), 0.043, 0.030),
        ((0.765, 0.000, 1.428), 0.046, 0.026),
        ((0.795, 0.000, 1.426), 0.044, 0.018),
    ], "M_Skin", sides=8, axis="X")
    add_lr(palm, w_arm)
    for f, (y, length, r) in FINGERS.items():
        x0 = PALM_END - 0.004
        pts = [(x0, y, FINGER_Z)]
        x = PALM_END
        for _, frac in SEGMENTS:
            x += length * frac
            pts.append((x, y, FINGER_Z - 0.001))
        radii = [(r, r * 0.95), (r * 0.95, r * 0.9), (r * 0.88, r * 0.85), (r * 0.72, r * 0.7)]
        add_lr(tube(f"Hand_{f}_L", pts, radii, "M_Skin", sides=6), w_finger(f))
    add_lr(tube("Hand_Thumb_L", THUMB, [0.014, 0.013, 0.011, 0.009], "M_Skin", sides=6), w_thumb)

    # ======================= PANTS
    leg = loft("Pants_Leg_L", [
        ((0.083, 0.000, 1.000), 0.104, 0.122, 0.118),
        ((0.093, 0.000, 0.880), 0.100, 0.108, 0.112),
        ((0.099, 0.000, 0.760), 0.090, 0.094, 0.098),
        ((0.104, 0.000, 0.630), 0.075, 0.080, 0.080),
        ((0.106, -0.004, 0.570), 0.068, 0.074, 0.070),
        ((0.107, -0.008, 0.530), 0.066, 0.074, 0.066),
        ((0.108, -0.002, 0.480), 0.063, 0.068, 0.068),
        ((0.110, 0.004, 0.360), 0.065, 0.068, 0.076),
        ((0.112, 0.006, 0.250), 0.060, 0.062, 0.066),
        ((0.113, 0.004, 0.205), 0.066, 0.068, 0.070),
        ((0.114, 0.004, 0.175), 0.060, 0.064, 0.064),
    ], "M_Pants", sides=12, cap_start=False, cap_end=True)
    add_lr(leg, w_leg)
    add_lr(box("Pants_CargoPocket_L", (0.188, -0.006, 0.665), (0.034, 0.130, 0.160), "M_PantsCamoA",
               rot=(0, math.radians(4), 0)), w_leg)
    add_lr(box("Pants_CargoFlap_L", (0.197, -0.006, 0.748), (0.026, 0.138, 0.040), "M_PantsDark",
               rot=(0, math.radians(4), 0)), w_leg)
    add_lr(box("Pants_KneeSeam_L", (0.107, -0.078, 0.540), (0.090, 0.012, 0.012), "M_PantsDark"), w_leg)
    add_lr(box("Pants_KneePanel_L", (0.107, -0.074, 0.505), (0.088, 0.014, 0.075), "M_PantsCamoC"), w_leg)
    add(box("Pants_Crotch", (0, 0.0, 0.905), (0.10, 0.16, 0.10), "M_Pants"), w_torso)

    # ======================= BOOTS
    add_lr(loft("Boot_Shaft_L", [
        ((0.114, 0.004, 0.210), 0.061, 0.066, 0.068),
        ((0.115, 0.006, 0.160), 0.063, 0.070, 0.070),
        ((0.115, 0.004, 0.110), 0.060, 0.074, 0.068),
        ((0.115, -0.004, 0.065), 0.058, 0.080, 0.070),
    ], "M_Boots", sides=12), w_boot)
    add_lr(loft("Boot_Collar_L", [((0.114, 0.004, 0.198), 0.066, 0.072), ((0.114, 0.004, 0.222), 0.065, 0.071)],
                "M_BootsDark", sides=12, cap_start=False, cap_end=False), w_boot)
    add_lr(loft("Boot_Foot_L", [
        ((0.115, 0.078, 0.052), 0.049, 0.042),
        ((0.115, 0.020, 0.060), 0.053, 0.052),
        ((0.115, -0.060, 0.052), 0.055, 0.044),
        ((0.115, -0.120, 0.044), 0.052, 0.034),
        ((0.115, -0.160, 0.038), 0.044, 0.026),
        ((0.115, -0.182, 0.034), 0.030, 0.018),
    ], "M_Boots", sides=10, axis="Y"), w_boot)
    add_lr(box("Boot_Laces_L", (0.115, -0.050, 0.090), (0.034, 0.110, 0.010), "M_BootsDark",
               rot=(math.radians(-22), 0, 0)), w_boot)
    add_lr(box("Boot_ToeCap_L", (0.115, -0.150, 0.040), (0.090, 0.060, 0.040), "M_BootsDark", taper=0.8), w_boot)
    add_lr(box("Boot_Sole_L", (0.115, -0.052, 0.013), (0.114, 0.272, 0.026), "M_Sole"), w_boot)
    add_lr(box("Boot_Heel_L", (0.115, 0.050, 0.018), (0.104, 0.075, 0.036), "M_Sole"), w_boot)

    # ======================= HEAD GROUP
    add(loft("Neck", [((0, 0.000, 1.470), 0.058, 0.056, 0.060), ((0, -0.006, 1.540), 0.055, 0.056, 0.058),
                      ((0, -0.010, 1.605), 0.054, 0.058, 0.056)],
             "M_Skin", sides=10, cap_start=False, cap_end=False), w_head, "Head")
    # Skull: (half width, face depth, back depth, n)
    add(loft("Head_Skull", [
        ((0, -0.052, 1.570), 0.028, 0.020, 0.020, 2.0),
        ((0, -0.044, 1.584), 0.046, 0.034, 0.030, 2.2),
        ((0, -0.022, 1.604), 0.066, 0.070, 0.052, 2.3),
        ((0, -0.010, 1.632), 0.074, 0.086, 0.072, 2.3),
        ((0, -0.004, 1.662), 0.080, 0.094, 0.086, 2.4),
        ((0, 0.000, 1.700), 0.082, 0.095, 0.096, 2.4),
        ((0, 0.003, 1.740), 0.081, 0.091, 0.098, 2.3),
        ((0, 0.004, 1.778), 0.073, 0.080, 0.090, 2.2),
        ((0, 0.005, 1.808), 0.054, 0.058, 0.068, 2.1),
        ((0, 0.005, 1.826), 0.022, 0.024, 0.030, 2.0),
    ], "M_Skin", sides=14), w_head, "Head")
    # Nose: tapered wedge from brow to tip.
    nose = tube("Head_Nose", [(0, -0.091, 1.703), (0, -0.098, 1.682), (0, -0.103, 1.664), (0, -0.098, 1.655)],
                [(0.007, 0.005), (0.009, 0.006), (0.012, 0.008), (0.009, 0.005)], "M_Skin", sides=6)
    add(nose, w_head, "Head")
    add_lr(tube("Head_Ear_L", [(0.080, 0.010, 1.668), (0.086, 0.012, 1.690), (0.086, 0.014, 1.712)],
                [(0.010, 0.018), (0.008, 0.020), (0.006, 0.014)], "M_Skin", sides=6), w_head, "Head")
    # Full beard: jaw/chin/cheek shell with sideburns; upper-front removed to show cheeks and nose.
    beard = loft("Head_Beard", [
        ((0, -0.060, 1.562), 0.024, 0.018, 0.016),
        ((0, -0.052, 1.575), 0.044, 0.032, 0.026),
        ((0, -0.026, 1.600), 0.071, 0.078, 0.052),
        ((0, -0.012, 1.630), 0.079, 0.092, 0.074),
        ((0, -0.005, 1.660), 0.084, 0.099, 0.088),
        ((0, -0.001, 1.692), 0.086, 0.098, 0.096),
    ], "M_Beard", sides=14, cap_start=True, cap_end=False)
    delete_verts(beard, lambda c: c.y > 0.022 or (c.z > 1.650 and c.y < -0.045) or (c.z > 1.672 and abs(c.x) < 0.076))
    add(beard, w_head, "Head")
    add(tube("Head_Mustache", [(-0.028, -0.094, 1.634), (0.0, -0.101, 1.641), (0.028, -0.094, 1.634)],
             [(0.008, 0.006), (0.010, 0.007), (0.008, 0.006)], "M_Beard", sides=6), w_head, "Head")
    add(box("Head_Brows", (0, -0.094, 1.724), (0.112, 0.012, 0.011), "M_Hair"), w_head, "Head")
    hair = loft("Head_Hair", [
        ((0, 0.004, 1.660), 0.083, 0.096, 0.098),
        ((0, 0.004, 1.700), 0.086, 0.099, 0.101),
        ((0, 0.004, 1.745), 0.085, 0.096, 0.101),
    ], "M_Hair", sides=14, cap_start=False, cap_end=False)
    delete_verts(hair, lambda c: c.y < -0.010 or (c.z < 1.690 and c.y < 0.040))
    add(hair, w_head, "Head")
    # Wraparound sunglasses: curved lens strip, bridge, temples.
    lens_pts = []
    for i in range(9):
        a = math.radians(-62 + i * (124 / 8))
        lens_pts.append((math.sin(a) * 0.090, -math.cos(a) * 0.101 + 0.004, 1.702 - 0.004 * abs(math.sin(a))))
    lens = bmesh.new()
    top, bot = [], []
    for x, y, z in lens_pts:
        dip = 0.004 if abs(x) < 0.012 else 0.0  # nose bridge notch
        top.append(lens.verts.new((x, y, z + 0.015)))
        bot.append(lens.verts.new((x, y, z - 0.013 + dip * 3)))
    for i in range(len(top) - 1):
        lens.faces.new((top[i], top[i + 1], bot[i + 1], bot[i]))
    lens_ob = _finish(lens, "Glasses_Lens", "M_Glasses", recalc=False)
    sol = lens_ob.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = 0.004
    add(lens_ob, w_head, "Head")
    add_lr(box("Glasses_Temple_L", (0.086, -0.030, 1.708), (0.006, 0.090, 0.007), "M_Glasses"), w_head, "Head")
    # Baseball cap: crown (front panel higher), curved brim, velcro patch.
    add(loft("Cap_Crown", [
        ((0, 0.004, 1.730), 0.089, 0.104, 0.106, 2.2),
        ((0, 0.004, 1.775), 0.088, 0.102, 0.104, 2.2),
        ((0, 0.004, 1.812), 0.078, 0.092, 0.090, 2.2),
        ((0, 0.006, 1.838), 0.056, 0.066, 0.064, 2.1),
        ((0, 0.008, 1.853), 0.022, 0.026, 0.026, 2.0),
    ], "M_Cap", sides=14, cap_start=False, cap_end=True), w_head, "Head")
    add(box("Cap_Button", (0, 0.008, 1.858), (0.016, 0.016, 0.008), "M_CapDark"), w_head, "Head")
    brim = bmesh.new()
    inner, outer = [], []
    steps = 10
    for i in range(steps + 1):
        a = math.radians(-60 + i * (120 / steps))
        ix, iy = math.sin(a) * 0.087, -math.cos(a) * 0.103
        ext = 0.078 * max(math.cos(a) - 0.45, 0.0) / 0.55 + 0.002
        d = Vector((math.sin(a) * 0.35, -1.0, 0.0)).normalized()
        droop = 0.010 * math.sin(a) ** 2
        z_in = 1.738
        inner.append(brim.verts.new((ix, iy, z_in)))
        outer.append(brim.verts.new((ix + d.x * ext, iy + d.y * ext, z_in - 0.006 - droop - 0.10 * ext)))
    for i in range(steps):
        brim.faces.new((inner[i], inner[i + 1], outer[i + 1], outer[i]))
    brim_ob = _finish(brim, "Cap_Brim", "M_CapDark", recalc=False)
    sol = brim_ob.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = 0.009
    add(brim_ob, w_head, "Head")
    add(box("Cap_Patch", (0, -0.100, 1.786), (0.058, 0.010, 0.040), "M_Patch", rot=(math.radians(-16), 0, 0)), w_head, "Head")
    add(box("Cap_Strap", (0, 0.108, 1.742), (0.070, 0.008, 0.020), "M_CapDark"), w_head, "Head")
    return parts


# ---------------------------------------------------------------- camo + assembly
def apply_camo(ob, base, shades, scale=5.5, seed=0.0):
    """Low-poly 'polygon camo': faces clustered by 3D noise into four shades (base + 3)."""
    for name in [base] + shades:
        m = material(name)
        if m.name not in [s.name for s in ob.data.materials if s]:
            ob.data.materials.append(m)
    names = [m.name if m else "" for m in ob.data.materials]
    base_index = names.index(base)
    for poly in ob.data.polygons:
        if poly.material_index != base_index:
            continue
        c = poly.center * scale + Vector((seed, seed * 0.7, seed * 1.3))
        n = noise.noise(c)
        n2 = noise.noise(c * 1.9 + Vector((4.2, 1.1, 7.3)))
        if n > 0.20:
            poly.material_index = names.index(shades[1])
        elif n < -0.20:
            poly.material_index = names.index(shades[0])
        elif n2 > 0.30:
            poly.material_index = names.index(shades[2])


def clear_previous():
    c = bpy.data.collections.get(COLLECTION)
    if c:
        for ob in list(c.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)
    for a in list(bpy.data.armatures):
        if a.users == 0:
            bpy.data.armatures.remove(a)


def join(objs, name):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        bpy.context.view_layer.objects.active = o
        for mod in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    return ob


def build():
    active = getattr(bpy.context, "object", None)
    if active and active.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    clear_previous()
    rig = build_armature()
    parts = build_parts()
    for ob, fn, _ in parts:
        apply_weights(ob, fn)
    body = join([o for o, _, g in parts if g == "Body"], "Operator_Body")
    head = join([o for o, _, g in parts if g == "Head"], "Operator_Head")
    apply_camo(body, "M_Jacket", ["M_JacketCamoA", "M_JacketCamoB", "M_JacketCamoC"], seed=3.1)
    apply_camo(body, "M_Pants", ["M_PantsCamoA", "M_PantsCamoB", "M_PantsCamoC"], seed=7.7)
    for ob in (body, head):
        ob.parent = rig
        mod = ob.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
    tris = sum(len(p.vertices) - 2 for ob in (body, head) for p in ob.data.polygons)
    return {"triangles": tris, "body_verts": len(body.data.vertices), "head_verts": len(head.data.vertices),
            "bones": len(BONES)}


result = build()
