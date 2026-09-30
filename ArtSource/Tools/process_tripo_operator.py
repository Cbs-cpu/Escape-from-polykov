"""
Tripo operator -> game character on the project's humanoid skeleton (same bone names as build_operator.py).
Fits a rig to the Tripo mesh in its A-pose, skins it with automatic weights, re-poses mesh + rig to the
T-pose all project animations/IK expect, splits the head (hidden in first person) and adds a black
inverted-hull outline. Run in Operator_Tripo_Source.blend after the low-poly "Operator_Tripo_LP" exists.
"""
import bpy
import bmesh
from mathutils import Vector

MESH = globals().get("TRIPO_MESH", "Operator_Tripo_LP")
RIG = globals().get("TRIPO_RIG", "Operator_Rig")
NAMES = globals().get("TRIPO_NAMES", ("Operator_Body", "Operator_Head"))
OUTLINE = globals().get("TRIPO_OUTLINE", 0.0014)

# Left-side joints of the mesh as generated (Blender space, +X = character left, -Y = front). Default = Operator (A-pose).
JOINTS = globals().get("TRIPO_JOINTS", {
    "shoulder_in": (0.04, 0.03, 1.44), "shoulder": (0.20, 0.04, 1.46), "elbow": (0.3125, 0.06, 1.185),
    "wrist": (0.333, 0.0, 0.946), "hand_end": (0.335, -0.02, 0.862),
    "hip": (0.083, -0.02, 0.95), "knee": (0.135, 0.0, 0.49), "ankle": (0.205, 0.083, 0.10),
    "ball": (0.212, -0.05, 0.03), "toe": (0.215, -0.125, 0.03),
    "pelvis": 0.95, "spine": 1.05, "chest": 1.20, "upper_chest": 1.34, "neck": 1.46, "head": 1.58, "head_top": 1.80,
})
J = {k: (Vector(v) if isinstance(v, tuple) else v) for k, v in JOINTS.items()}

A_POSE = {
    "Hips": ((0, 0.0, J["pelvis"]), (0, 0.0, J["spine"]), None),
    "Spine": ((0, 0.0, J["spine"]), (0, 0.005, J["chest"]), "Hips"),
    "Chest": ((0, 0.005, J["chest"]), (0, 0.01, J["upper_chest"]), "Spine"),
    "UpperChest": ((0, 0.01, J["upper_chest"]), (0, 0.02, J["neck"]), "Chest"),
    "Neck": ((0, 0.02, J["neck"]), (0, 0.01, J["head"]), "UpperChest"),
    "Head": ((0, 0.01, J["head"]), (0, 0.01, J["head_top"]), "Neck"),
    "LeftShoulder": (tuple(J["shoulder_in"]), tuple(J["shoulder"].lerp(J["shoulder_in"], 0.12)), "UpperChest"),
    "LeftUpperArm": (tuple(J["shoulder"]), tuple(J["elbow"]), "LeftShoulder"),
    "LeftLowerArm": (tuple(J["elbow"]), tuple(J["wrist"]), "LeftUpperArm"),
    "LeftHand": (tuple(J["wrist"]), tuple(J["hand_end"]), "LeftLowerArm"),
    "LeftUpperLeg": (tuple(J["hip"]), tuple(J["knee"]), "Hips"),
    "LeftLowerLeg": (tuple(J["knee"]), tuple(J["ankle"]), "LeftUpperLeg"),
    "LeftFoot": (tuple(J["ankle"]), tuple(J["ball"]), "LeftLowerLeg"),
    "LeftToes": (tuple(J["ball"]), tuple(J["toe"]), "LeftFoot"),
}
# Fingers continue along the hand direction; they spread front-to-back (thumb forward) in both A- and T-poses.
FINGERS = {"Index": -0.028, "Middle": -0.010, "Ring": 0.009, "Little": 0.026}
SEGS = (("Proximal", 0.034), ("Intermediate", 0.024), ("Distal", 0.019))
_hand_dir = (J["hand_end"] - J["wrist"]).normalized()
for f, y in FINGERS.items():
    top, parent = J["hand_end"] + Vector((0, y, 0)), "LeftHand"
    for seg, length in SEGS:
        bottom = top + _hand_dir * length
        A_POSE[f"Left{f}{seg}"] = (tuple(top), tuple(bottom), parent)
        parent, top = f"Left{f}{seg}", bottom
_t0 = J["wrist"].lerp(J["hand_end"], 0.3) + Vector((0, -0.028, 0))
_tdir = (_hand_dir * 0.5 + Vector((0, -0.85, 0))).normalized()
THUMB = [_t0, _t0 + _tdir * 0.03, _t0 + _tdir * 0.052, _t0 + _tdir * 0.068]
A_POSE["LeftThumbProximal"] = (tuple(THUMB[0]), tuple(THUMB[1]), "LeftHand")
A_POSE["LeftThumbIntermediate"] = (tuple(THUMB[1]), tuple(THUMB[2]), "LeftThumbProximal")
A_POSE["LeftThumbDistal"] = (tuple(THUMB[2]), tuple(THUMB[3]), "LeftThumbIntermediate")
for n in [k for k in A_POSE if k.startswith("Left")]:
    h, t, p = A_POSE[n]
    A_POSE["Right" + n[4:]] = ((-h[0], h[1], h[2]), (-t[0], t[1], t[2]), ("Right" + p[4:]) if p and p.startswith("Left") else p)


def build_rig():
    old = bpy.data.objects.get(RIG)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    arm = bpy.data.armatures.new("Operator_Armature")
    rig = bpy.data.objects.new(RIG, arm)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (h, t, _) in A_POSE.items():
        b = arm.edit_bones.new(name)
        b.head, b.tail = Vector(h), Vector(t)
        b.roll = 0.0
    for name, (_, _, parent) in A_POSE.items():
        if parent:
            arm.edit_bones[name].parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def skin(mesh, rig):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    mesh.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


def aim(rig, name, target_dir):
    """Rotate a pose bone (armature space) so it points along target_dir, keeping its head."""
    pb = rig.pose.bones[name]
    bpy.context.view_layer.update()
    m = pb.matrix.copy()
    cur = (m.to_3x3() @ Vector((0, 1, 0))).normalized()
    q = cur.rotation_difference(Vector(target_dir).normalized())
    head = m.translation.copy()
    new = (q.to_matrix() @ m.to_3x3()).to_4x4()
    new.translation = head
    pb.matrix = new
    bpy.context.view_layer.update()


def to_t_pose(mesh, rig):
    for side, s in (("Left", 1), ("Right", -1)):
        aim(rig, side + "UpperArm", (s, 0.0, 0.0))
        aim(rig, side + "LowerArm", (s, 0.0, 0.0))
        aim(rig, side + "Hand", (s, 0.0, 0.0))
        for f in FINGERS:
            for seg, _ in SEGS:
                aim(rig, f"{side}{f}{seg}", (s, 0.0, 0.0))
        aim(rig, side + "ThumbProximal", (s * 0.6, -0.8, 0.0))
        aim(rig, side + "UpperLeg", (s * 0.03, 0.01, -1.0))   # narrower, near-vertical legs
        aim(rig, side + "LowerLeg", (s * 0.02, 0.03, -1.0))
    # Bake: apply the deformation to the mesh, then make the current pose the rest pose.
    bpy.context.view_layer.objects.active = mesh
    mod = next(m for m in mesh.modifiers if m.type == "ARMATURE")
    name = mod.name
    bpy.ops.object.modifier_apply(modifier=name)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    # Same bone orientation convention as build_operator.py (roll 0 in T-pose); weapon hand IK relies on it.
    bpy.ops.object.mode_set(mode="EDIT")
    for b in rig.data.edit_bones:
        b.roll = 0.0
    bpy.ops.object.mode_set(mode="OBJECT")
    new = mesh.modifiers.new("Armature", "ARMATURE")
    new.object = rig


def split_head(mesh):
    """Faces weighted mostly to Neck/Head above the collar -> Operator_Head."""
    head_groups = {mesh.vertex_groups[n].index for n in ("Head", "Neck") if n in mesh.vertex_groups}
    me = mesh.data

    def head_weight(v):
        return sum(g.weight for g in v.groups if g.group in head_groups)

    heavy = [head_weight(v) > 0.5 and v.co.z > J["neck"] + 0.04 for v in me.vertices]
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    for f in bm.faces:
        f.select = all(heavy[v.index] for v in f.verts)
    bm.to_mesh(me)
    bm.free()
    bpy.context.view_layer.objects.active = mesh
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    mesh.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    head = [o for o in bpy.context.selected_objects if o != mesh][0]
    mesh.name = mesh.data.name = NAMES[0]
    head.name = head.data.name = NAMES[1]
    return mesh, head


def add_outline(ob):
    om = bpy.data.materials.get("M_Outline") or bpy.data.materials.new("M_Outline")
    om.diffuse_color = (0, 0, 0, 1)
    me = ob.data
    me.materials.append(om)
    idx = len(me.materials) - 1
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    ret = bmesh.ops.duplicate(bm, geom=list(bm.faces))   # deform layer (weights) is duplicated too
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


def build():
    mesh = bpy.data.objects[MESH]
    mesh.hide_viewport = False
    rig = build_rig()
    skin(mesh, rig)
    to_t_pose(mesh, rig)
    body, head = split_head(mesh)
    for ob in (body, head):
        for p in ob.data.polygons:
            p.use_smooth = False
        add_outline(ob)
    return {"bones": len(rig.data.bones), "body_tris": len(body.data.polygons), "head_tris": len(head.data.polygons),
            "groups": len(body.vertex_groups)}


result = build()
