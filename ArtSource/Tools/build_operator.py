"""
Procedural low-poly tactical operator (reference: character sheet — cap, sunglasses, beard,
olive field jacket, cargo pants, boots). Builds mesh + Unity-humanoid armature + weights.

Conventions: meters, Z up, character faces -Y (exports to Unity +Z forward), T-pose.
Two skinned meshes: Operator_Body and Operator_Head (head is hidden for the local first-person camera).

Run inside Blender:  exec(open(r"<path>/build_operator.py").read())
"""
import bpy
import bmesh
import math
from mathutils import Vector

COLLECTION = "Operator"

# ---------------------------------------------------------------- materials
PALETTE = {
    "M_Skin": ("#c79d7b", 0.75, 0.0),
    "M_Jacket": ("#63674f", 0.9, 0.0),
    "M_JacketCamoA": ("#555a42", 0.9, 0.0),
    "M_JacketCamoB": ("#6f6b52", 0.9, 0.0),
    "M_JacketDark": ("#4e533d", 0.9, 0.0),
    "M_Pants": ("#5b5f48", 0.9, 0.0),
    "M_PantsCamoA": ("#4d523d", 0.9, 0.0),
    "M_PantsCamoB": ("#67654d", 0.9, 0.0),
    "M_PantsDark": ("#4a4e39", 0.9, 0.0),
    "M_Boots": ("#3a3733", 0.7, 0.0),
    "M_Sole": ("#1c1b1a", 0.8, 0.0),
    "M_Cap": ("#5c604a", 0.9, 0.0),
    "M_Patch": ("#77785e", 0.9, 0.0),
    "M_Beard": ("#4a3a2d", 1.0, 0.0),
    "M_Hair": ("#2f251e", 1.0, 0.0),
    "M_Glasses": ("#0e0f10", 0.15, 0.3),
    "M_Belt": ("#3a3129", 0.8, 0.0),
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


def _finish(bm, name, mat_name):
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


def loft(name, rings, mat_name, sides=8, axis="Z", cap_start=True, cap_end=True, phase=0.0):
    """rings: [((cx,cy,cz), r1, r2), ...]. axis Z: ring in XY (r1=X, r2=Y); X: ring in YZ (r1=Y, r2=Z); Y: ring in XZ."""
    bm = bmesh.new()
    loops = []
    for c, r1, r2 in rings:
        ring = []
        for i in range(sides):
            a = 2.0 * math.pi * i / sides + phase
            u, v = math.cos(a) * r1, math.sin(a) * r2
            if axis == "Z":
                p = (c[0] + u, c[1] + v, c[2])
            elif axis == "X":
                p = (c[0], c[1] + u, c[2] + v)
            else:
                p = (c[0] + u, c[1], c[2] + v)
            ring.append(bm.verts.new(p))
        loops.append(ring)
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


def box(name, center, size, mat_name, rot=(0.0, 0.0, 0.0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    from mathutils import Euler
    m = Euler(rot).to_matrix()
    for v in bm.verts:
        v.co = m @ v.co + Vector(center)
    return _finish(bm, name, mat_name)


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
# (head, tail) in Blender space. Left = +X because the character faces -Y.
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
    "LeftHand": ((0.71, 0.0, 1.43), (0.80, 0.0, 1.43), "LeftLowerArm"),
    "LeftUpperLeg": ((0.095, 0.0, 0.93), (0.105, -0.005, 0.53), "Hips"),
    "LeftLowerLeg": ((0.105, -0.005, 0.53), (0.115, 0.01, 0.09), "LeftUpperLeg"),
    "LeftFoot": ((0.115, 0.01, 0.09), (0.115, -0.10, 0.03), "LeftLowerLeg"),
    "LeftToes": ((0.115, -0.10, 0.03), (0.115, -0.17, 0.03), "LeftFoot"),
}
for _n in [n for n in BONES if n.startswith("Left")]:
    h, t, p = BONES[_n]
    BONES["Right" + _n[4:]] = ((-h[0], h[1], h[2]), (-t[0], t[1], t[2]),
                              ("Right" + p[4:]) if p and p.startswith("Left") else p)


def build_armature():
    arm = bpy.data.armatures.new("Operator_Armature")
    ob = bpy.data.objects.new("Operator_Rig", arm)
    _coll().objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects:
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


def w_torso(p):
    z = p.z
    ws = chain(z, ["Hips", "Spine", "Chest", "UpperChest", "Neck"], [1.02, 1.20, 1.34, 1.50], 0.05)
    side = "Left" if p.x > 0 else "Right"
    sh = _smooth(0.10, 0.19, abs(p.x)) * _smooth(1.30, 1.42, z)
    if sh > 0:
        ws = {k: v * (1 - sh) for k, v in ws.items()}
        ws[side + "Shoulder"] = ws.get(side + "Shoulder", 0) + sh * 0.6
        ws[side + "UpperArm"] = ws.get(side + "UpperArm", 0) + sh * 0.4 * _smooth(0.16, 0.22, abs(p.x))
    return ws


def w_arm(p):
    side = "Left" if p.x > 0 else "Right"
    return chain(abs(p.x), ["UpperChest", side + "Shoulder", side + "UpperArm", side + "LowerArm", side + "Hand"],
                 [0.11, 0.19, 0.46, 0.71], 0.035)


def w_leg(p):
    side = "Left" if p.x > 0 else "Right"
    return chain(-p.z, ["Hips", side + "UpperLeg", side + "LowerLeg", side + "Foot"], [-0.95, -0.53, -0.11], 0.05)


def w_boot(p):
    side = "Left" if p.x > 0 else "Right"
    if p.z > 0.13:
        return {side + "LowerLeg": 1.0}
    ws = chain(-p.y, [side + "Foot", side + "Toes"], [0.095], 0.03)
    up = _smooth(0.08, 0.14, p.z)
    ws = {k: v * (1 - up) for k, v in ws.items()}
    ws[side + "LowerLeg"] = up
    return ws


def w_head(p):
    return chain(p.z, ["UpperChest", "Neck", "Head"], [1.49, 1.585], 0.03)


def w_rigid(bone):
    return lambda p: {bone: 1.0}


# ---------------------------------------------------------------- body parts
def build_parts():
    parts = []  # (object, weight_fn, group)

    def add(ob, fn, group="Body"):
        parts.append((ob, fn, group))
        return ob

    # Torso (field jacket), 12 sides, front slightly fuller.
    add(loft("Jacket_Torso", [
        ((0, 0.00, 0.84), 0.190, 0.130),
        ((0, 0.00, 0.93), 0.188, 0.126),
        ((0, 0.00, 1.04), 0.170, 0.118),
        ((0, -0.005, 1.17), 0.182, 0.126),
        ((0, -0.014, 1.29), 0.205, 0.140),
        ((0, -0.008, 1.38), 0.222, 0.134),
        ((0, 0.000, 1.445), 0.200, 0.112),
        ((0, 0.000, 1.475), 0.150, 0.098),
        ((0, 0.000, 1.505), 0.092, 0.084),
        ((0, 0.000, 1.555), 0.080, 0.078),
    ], "M_Jacket", sides=12, cap_start=True, cap_end=False), w_torso)
    # Collar flaps and zipper line
    add(loft("Jacket_Collar", [((0, 0.0, 1.50), 0.098, 0.090), ((0, 0.0, 1.565), 0.090, 0.086)],
             "M_JacketDark", sides=12, cap_start=False, cap_end=False), w_torso)
    add(box("Jacket_Zip", (0, -0.140, 1.18), (0.012, 0.012, 0.62), "M_JacketDark", rot=(math.radians(-2), 0, 0)), w_torso)
    for sx in (1, -1):
        add(box(f"Jacket_ChestPocket_{sx}", (0.090 * sx, -0.143, 1.30), (0.085, 0.022, 0.10), "M_JacketDark"), w_torso)
        add(box(f"Jacket_ChestFlap_{sx}", (0.090 * sx, -0.152, 1.355), (0.092, 0.014, 0.030), "M_Jacket"), w_torso)
        add(box(f"Jacket_HipPocket_{sx}", (0.110 * sx, -0.128, 0.93), (0.10, 0.024, 0.11), "M_JacketDark"), w_torso)

    # Sleeves (left, then mirrored), 8 sides along X
    sleeve = loft("Jacket_Sleeve_L", [
        ((0.130, 0.0, 1.425), 0.074, 0.078),
        ((0.225, 0.0, 1.432), 0.080, 0.084),
        ((0.270, 0.0, 1.431), 0.072, 0.075),
        ((0.330, 0.0, 1.430), 0.062, 0.064),
        ((0.460, 0.0, 1.430), 0.053, 0.055),
        ((0.600, 0.0, 1.430), 0.050, 0.050),
        ((0.690, 0.0, 1.430), 0.052, 0.052),
        ((0.700, 0.0, 1.430), 0.046, 0.046),
    ], "M_Jacket", sides=8, axis="X", cap_start=False, cap_end=True, phase=math.pi / 8)
    add(sleeve, w_arm)
    add(mirror_x(sleeve, "Jacket_Sleeve_R"), w_arm)
    patch = box("Jacket_ShoulderPatch_L", (0.235, 0.0, 1.475), (0.06, 0.07, 0.012), "M_Patch", rot=(0, math.radians(-12), 0))
    add(patch, w_arm)

    # Hands (palm down in T-pose) + thumb
    hand = loft("Hand_L", [
        ((0.700, 0.000, 1.430), 0.034, 0.028),
        ((0.730, 0.000, 1.429), 0.044, 0.030),
        ((0.790, 0.000, 1.427), 0.048, 0.027),
        ((0.805, 0.000, 1.425), 0.047, 0.020),
        ((0.865, 0.002, 1.421), 0.044, 0.018),
        ((0.892, 0.004, 1.418), 0.036, 0.013),
    ], "M_Skin", sides=8, axis="X")
    add(hand, w_arm)
    thumb = loft("Thumb_L", [
        ((0.730, -0.030, 1.422), 0.017, 0.016),
        ((0.760, -0.058, 1.416), 0.014, 0.013),
        ((0.790, -0.072, 1.412), 0.011, 0.010),
    ], "M_Skin", sides=6, axis="X")
    add(thumb, w_arm)
    add(mirror_x(hand, "Hand_R"), w_arm)
    add(mirror_x(thumb, "Thumb_R"), w_arm)

    # Cargo pants
    leg = loft("Pants_Leg_L", [
        ((0.085, 0.000, 0.99), 0.100, 0.118),
        ((0.095, 0.000, 0.88), 0.096, 0.104),
        ((0.100, 0.000, 0.75), 0.086, 0.091),
        ((0.105, 0.000, 0.62), 0.071, 0.077),
        ((0.107, -0.006, 0.53), 0.064, 0.070),
        ((0.108, -0.002, 0.45), 0.061, 0.067),
        ((0.110, 0.006, 0.33), 0.063, 0.071),
        ((0.112, 0.006, 0.22), 0.057, 0.062),
        ((0.114, 0.004, 0.15), 0.053, 0.058),
    ], "M_Pants", sides=10, cap_start=False, cap_end=True)
    add(leg, w_leg)
    add(mirror_x(leg, "Pants_Leg_R"), w_leg)
    pocket = box("Pants_CargoPocket_L", (0.185, -0.004, 0.67), (0.032, 0.125, 0.15), "M_PantsDark", rot=(0, math.radians(3), 0))
    add(pocket, w_leg)
    add(mirror_x(pocket, "Pants_CargoPocket_R"), w_leg)
    knee = box("Pants_KneePad_L", (0.107, -0.070, 0.535), (0.085, 0.020, 0.10), "M_PantsDark")
    add(knee, w_leg)
    add(mirror_x(knee, "Pants_KneePad_R"), w_leg)

    # Boots: shaft + foot + sole
    shaft = loft("Boot_Shaft_L", [
        ((0.114, 0.004, 0.215), 0.060, 0.066),
        ((0.115, 0.006, 0.13), 0.062, 0.070),
        ((0.115, 0.000, 0.07), 0.057, 0.076),
    ], "M_Boots", sides=10)
    foot = loft("Boot_Foot_L", [
        ((0.115, 0.075, 0.050), 0.047, 0.045),
        ((0.115, 0.010, 0.055), 0.052, 0.052),
        ((0.115, -0.080, 0.045), 0.054, 0.042),
        ((0.115, -0.140, 0.036), 0.049, 0.030),
        ((0.115, -0.178, 0.030), 0.034, 0.020),
    ], "M_Boots", sides=8, axis="Y")
    sole = box("Boot_Sole_L", (0.115, -0.050, 0.011), (0.110, 0.265, 0.022), "M_Sole")
    for ob in (shaft, foot, sole):
        add(ob, w_boot)
        add(mirror_x(ob, ob.name.replace("_L", "_R")), w_boot)

    # ---------------- head group
    add(loft("Neck", [((0, 0.0, 1.47), 0.056, 0.056), ((0, -0.008, 1.60), 0.052, 0.054)],
             "M_Skin", sides=8, cap_start=False, cap_end=False), w_head, "Head")
    add(loft("Head_Skull", [
        ((0, -0.040, 1.578), 0.040, 0.030),
        ((0, -0.018, 1.600), 0.068, 0.070),
        ((0, -0.005, 1.650), 0.078, 0.090),
        ((0, 0.000, 1.700), 0.082, 0.098),
        ((0, 0.002, 1.750), 0.080, 0.096),
        ((0, 0.004, 1.790), 0.064, 0.080),
        ((0, 0.004, 1.815), 0.034, 0.044),
    ], "M_Skin", sides=10), w_head, "Head")
    add(box("Head_Nose", (0, -0.103, 1.668), (0.026, 0.030, 0.045), "M_Skin", rot=(math.radians(-12), 0, 0)), w_head, "Head")
    ear = box("Head_Ear_L", (0.082, 0.004, 1.690), (0.014, 0.034, 0.052), "M_Skin")
    add(ear, w_head, "Head")
    add(mirror_x(ear, "Head_Ear_R"), w_head, "Head")
    # Full beard: lower face shell, back half removed
    beard = loft("Head_Beard", [
        ((0, -0.047, 1.568), 0.044, 0.035),
        ((0, -0.024, 1.598), 0.073, 0.077),
        ((0, -0.012, 1.628), 0.082, 0.090),
        ((0, -0.006, 1.648), 0.084, 0.095),
    ], "M_Beard", sides=10, cap_start=True, cap_end=False)
    bm = bmesh.new()
    bm.from_mesh(beard.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y > 0.035], context="VERTS")
    bm.to_mesh(beard.data)
    bm.free()
    add(beard, w_head, "Head")
    add(box("Head_Mustache", (0, -0.099, 1.641), (0.052, 0.014, 0.012), "M_Beard"), w_head, "Head")
    add(box("Head_Brows", (0, -0.094, 1.722), (0.110, 0.010, 0.010), "M_Hair"), w_head, "Head")
    add(box("Head_Hair_Back", (0, 0.060, 1.715), (0.150, 0.060, 0.070), "M_Hair"), w_head, "Head")
    # Sunglasses: two lenses, bridge, temples
    for sx in (1, -1):
        add(box(f"Glasses_Lens_{sx}", (0.036 * sx, -0.098, 1.700), (0.050, 0.008, 0.028), "M_Glasses",
                rot=(0, 0, math.radians(-8 * sx))), w_head, "Head")
        add(box(f"Glasses_Temple_{sx}", (0.080 * sx, -0.045, 1.705), (0.006, 0.100, 0.007), "M_Glasses"), w_head, "Head")
    add(box("Glasses_Bridge", (0, -0.101, 1.708), (0.024, 0.006, 0.006), "M_Glasses"), w_head, "Head")
    # Baseball cap: crown, brim, front patch
    add(loft("Cap_Crown", [
        ((0, 0.004, 1.735), 0.088, 0.104),
        ((0, 0.004, 1.780), 0.086, 0.100),
        ((0, 0.006, 1.815), 0.070, 0.084),
        ((0, 0.008, 1.838), 0.040, 0.050),
        ((0, 0.008, 1.845), 0.010, 0.012),
    ], "M_Cap", sides=12, cap_start=False, cap_end=True), w_head, "Head")
    add(box("Cap_Brim", (0, -0.140, 1.738), (0.160, 0.090, 0.010), "M_Cap", rot=(math.radians(8), 0, 0)), w_head, "Head")
    add(box("Cap_Patch", (0, -0.096, 1.785), (0.055, 0.010, 0.038), "M_Patch", rot=(math.radians(-18), 0, 0)), w_head, "Head")
    return parts


# ---------------------------------------------------------------- assemble
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
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    # Merge duplicate material slots created by join
    bpy.ops.object.material_slot_remove_unused()
    return ob


def apply_camo(ob, base, shade_a, shade_b, scale=9.0, seed=0.0):
    """Low-poly 'polygon camo': faces clustered by 3D noise into three shades."""
    from mathutils import noise
    mats = [material(base), material(shade_a), material(shade_b)]
    slot = {}
    for m in mats:
        if m.name not in [s.name for s in ob.data.materials if s]:
            ob.data.materials.append(m)
    names = [m.name if m else "" for m in ob.data.materials]
    base_index = names.index(base)
    for poly in ob.data.polygons:
        if poly.material_index != base_index:
            continue
        c = poly.center * scale + Vector((seed, seed * 0.7, seed * 1.3))
        n = noise.noise(c)
        if n > 0.22:
            poly.material_index = names.index(shade_b)
        elif n < -0.18:
            poly.material_index = names.index(shade_a)


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
    apply_camo(body, "M_Jacket", "M_JacketCamoA", "M_JacketCamoB", seed=3.1)
    apply_camo(body, "M_Pants", "M_PantsCamoA", "M_PantsCamoB", seed=7.7)
    for ob in (body, head):
        ob.parent = rig
        mod = ob.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
    tris = sum(len(p.vertices) - 2 for ob in (body, head) for p in ob.data.polygons)
    return {"triangles": tris, "body_verts": len(body.data.vertices), "head_verts": len(head.data.vertices),
            "bones": len(BONES)}


result = build()
