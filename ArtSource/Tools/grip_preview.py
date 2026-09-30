"""
Preview how the Operator holds the M1911 with the *same* math Unity uses at runtime (WeaponPresenter + HandIK):
weapon posed relative to the first-person eye, analytic two-bone arm IK to the wrist targets, hand frame
(wrist->middle knuckle, little->index knuckle) aligned to the target frame, procedural finger curl.
Values mirror WeaponDefinition / HandIK defaults; tweak GRIP below, re-run, then copy good values to C#.

Headless: blender -b ArtSource/Characters/Operator.blend -P ArtSource/Tools/build_m1911.py -P ArtSource/Tools/grip_preview.py
Renders docs/screenshots/grip_<pose>_<view>.png (set POLYKOV_ROOT to the repo path).
"""
import bpy
import math
import os
from mathutils import Vector, Matrix, Quaternion, Euler

ROOT = os.environ.get("POLYKOV_ROOT", r"C:\Users\Cobos\Escape from Polykov")
OUT = os.path.join(ROOT, "docs", "screenshots")
os.makedirs(OUT, exist_ok=True)

# ---- values mirrored from C# (WeaponDefinition, HandIK, CameraSettings)
GRIP = {
    "hip_position": (0.12, -0.19, 0.36), "hip_euler": (0.0, -2.0, 0.0),
    "ads_sight_distance": 0.4,
    "right_pos": (0.032, -0.056, -0.118), "right_fwd": (-0.12, 0.05, 1.0), "right_up": (0.0, 0.95, 0.31),
    "left_pos": (-0.036, -0.08, -0.07), "left_fwd": (0.45, 0.3, 1.0), "left_up": (0.1, 1.0, 0.25),
    "right_elbow_hint": (0.45, -0.5, -0.2), "left_elbow_hint": (-0.45, -0.5, -0.2),
    "grip_curl": 72.0, "trigger_curl": 28.0, "support_curl": 62.0, "thumb_curl": 8.0,
    "right_thumb": (0.0, -0.05, 1.0), "left_thumb": (0.0, -0.15, 1.0),
    "reload_position": (0.04, -0.16, 0.33), "reload_euler": (-12.0, -28.0, 38.0),
    "mag_grab_fwd": (0.5, 0.67, 0.53), "mag_grab_up": (0.0, -0.02, 1.0), "reload_mag_out": 0.6,
    "head_eye_offset": (0.105, 0.07),  # CameraSettings.HeadBoneEyeOffset (up, forward)
}
GRIP.update(globals().get("GRIP_OVERRIDES", {}))
POSES = globals().get("GRIP_POSES", ["hip", "ads"])
SIGHT = (0.0, 0.0395, -0.0525)  # SightLine in weapon space


def U(v):
    """Unity (camera/weapon/character) space -> Blender (right=-X, up=+Z, forward=-Y)."""
    return Vector((-v[0], -v[2], v[1]))


def unity_euler_vec(e, v):
    """Rotate a Unity-space vector by a Unity Euler (ZXY) in Unity space."""
    m = Euler([math.radians(a) for a in e], "ZXY").to_matrix()
    return tuple(m @ Vector(v))


def unity_euler(e):
    """Unity Euler (ZXY, degrees) as a Blender rotation matrix, via the reflection U."""
    m = Euler([math.radians(a) for a in e], "ZXY").to_matrix()
    R = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))  # U as a matrix
    return R @ m @ R.inverted()


def look_basis(forward, side):
    """Orthonormal basis from a forward and a (rough) side/up vector. Same construction for hand and target."""
    f = forward.normalized()
    s = (side - f * side.dot(f)).normalized()
    return Matrix((f, s, f.cross(s))).transposed()


rig = bpy.data.objects["Operator_Rig"]
rig.animation_data_clear() if rig.animation_data else None
for pb in rig.pose.bones:
    pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
P = rig.pose.bones


def head(name):
    return rig.matrix_world @ P[name].head


def world_rot(name):
    return (rig.matrix_world @ P[name].matrix).to_quaternion()


def set_world_rot(name, q):
    pb = P[name]
    m = rig.matrix_world.inverted() @ Matrix.LocRotScale(rig.matrix_world @ pb.head, q, None)
    pb.matrix = m
    bpy.context.view_layer.update()


def angle_between(u, v):
    return math.acos(max(-1.0, min(1.0, u.normalized().dot(v.normalized()))))


def two_bone(upper, lower, hand, target, hint):
    """Port of TwoBoneIKSolver.Solve (C#)."""
    a, b, c = head(upper), head(lower), head(hand)
    ar, br = world_rot(upper), world_rot(lower)
    lab, lcb = (b - a).length, (c - b).length
    lat = max(1e-4, min((target - a).length, lab + lcb - 1e-4))
    ac, ab, at = c - a, b - a, target - a
    ac_ab0 = angle_between(ac, ab)
    ba_bc0 = angle_between(a - b, c - b)
    ac_ab1 = math.acos(max(-1, min(1, (lcb * lcb - lab * lab - lat * lat) / (-2 * lab * lat))))
    ba_bc1 = math.acos(max(-1, min(1, (lat * lat - lab * lab - lcb * lcb) / (-2 * lab * lcb))))
    axis0 = ac.cross(ab)
    if axis0.length < 1e-6:
        axis0 = ac.cross(hint - a)
    axis0.normalize()
    bend_a = Quaternion(axis0, ac_ab1 - ac_ab0)
    bend_b = Quaternion(axis0, ba_bc1 - ba_bc0)
    root_rot = bend_a @ ar
    mid_rot = bend_a @ bend_b @ br
    new_b = a + bend_a @ ab
    new_c = new_b + bend_a @ bend_b @ (c - b)
    swing = (new_c - a).rotation_difference(at)
    root_rot = swing @ root_rot
    mid_rot = swing @ mid_rot
    new_b = a + swing @ (new_b - a)
    axis = at.normalized()
    elbow = new_b - a - axis * (new_b - a).dot(axis)
    hint_dir = hint - a - axis * (hint - a).dot(axis)
    if elbow.length > 1e-6 and hint_dir.length > 1e-6:
        ang = angle_between(elbow, hint_dir)
        sign = 1.0 if axis.dot(elbow.cross(hint_dir)) >= 0 else -1.0
        twist = Quaternion(axis, ang * sign)
        root_rot = twist @ root_rot
        mid_rot = twist @ mid_rot
    set_world_rot(upper, root_rot)
    set_world_rot(lower, mid_rot)


def hand_frame_local(side):
    hand = f"{side}Hand"
    inv = world_rot(hand).inverted()
    fwd = inv @ (head(f"{side}MiddleProximal") - head(hand))
    s = inv @ (head(f"{side}IndexProximal") - head(f"{side}LittleProximal"))
    return look_basis(fwd, s)


def curl(side, center, curls):
    hand = f"{side}Hand"
    fwd = head(f"{side}MiddleProximal") - head(hand)
    s = head(f"{side}IndexProximal") - head(f"{side}LittleProximal")
    palm = fwd.cross(s).normalized()
    if palm.dot(center - head(hand)) < 0:
        palm = -palm
    for finger, deg in zip(("Index", "Middle", "Ring", "Little", "Thumb"), curls):
        chain = [f"{side}{finger}{seg}" for seg in ("Proximal", "Intermediate", "Distal")]
        for i, bone in enumerate(chain):
            if bone not in P:
                break
            nxt = head(chain[i + 1]) if i + 1 < len(chain) else None
            d = (nxt - head(bone)) if nxt is not None else (head(bone) - head(chain[i - 1]))
            axis = d.cross(palm)
            if axis.length < 1e-8:
                continue
            factor = (1.0, 1.1, 0.8)[i]
            set_world_rot(bone, Quaternion(axis.normalized(), math.radians(deg * factor)) @ world_rot(bone))


def aim_thumb(side, direction):
    """Point the thumb chain along a world direction (HandIK.Arm.AimThumb)."""
    chain = [f"{side}Thumb{seg}" for seg in ("Proximal", "Intermediate", "Distal")]
    for i, weight in ((0, 1.0), (1, 0.7)):
        d = head(chain[i + 1]) - head(chain[i])
        q = d.rotation_difference(direction)
        q = Quaternion().slerp(q, weight)
        set_world_rot(chain[i], q @ world_rot(chain[i]))


def pose(kind):
    for pb in P:
        pb.matrix_basis = Matrix.Identity(4)
    # Arms relaxed first (like the idle clip) so the IK starts from a natural bend.
    bpy.context.view_layer.update()
    right_frame_l = hand_frame_local("Right")
    left_frame_l = hand_frame_local("Left")

    up, fwd = GRIP["head_eye_offset"]
    eye = head("Head") + U((0.0, up, fwd))
    cam_rot = Matrix.Identity(3)  # looking straight ahead: camera space == character space
    if kind == "hip":
        gun_pos = eye + cam_rot @ U(GRIP["hip_position"])
        gun_rot = cam_rot @ unity_euler(GRIP["hip_euler"])
    elif kind == "reload":
        gun_pos = eye + cam_rot @ U(GRIP["reload_position"])
        gun_rot = cam_rot @ unity_euler(GRIP["reload_euler"])
    else:
        gun_rot = cam_rot.copy()
        gun_pos = eye + cam_rot @ U((0.0, 0.0, GRIP["ads_sight_distance"])) - gun_rot @ U(SIGHT)

    gun = bpy.data.objects["M1911"]
    gun.matrix_world = Matrix.LocRotScale(gun_pos, gun_rot.to_quaternion(), None)
    mag = bpy.data.objects["Magazine"]
    if "rest" not in mag:
        mag["rest"] = list(mag.location)
    mag.location = Vector(mag["rest"])
    bpy.context.view_layer.update()
    if kind == "reload":
        # WeaponModel.Pose: magazine slides 0.16 m along the raked grip.
        down = gun_rot @ U(unity_euler_vec((18.0, 0.0, 0.0), (0.0, -1.0, 0.0)))
        mag.matrix_world = Matrix.Translation(down * 0.16 * GRIP["reload_mag_out"]) @ mag.matrix_world
        bpy.context.view_layer.update()
    center = bpy.data.objects["GripCenter"].matrix_world.translation

    for side, key, hint_key in (("Right", "right", "right_elbow_hint"), ("Left", "left", "left_elbow_hint")):
        target = gun_pos + gun_rot @ U(GRIP[f"{key}_pos"])
        t_frame = look_basis(gun_rot @ U(GRIP[f"{key}_fwd"]), gun_rot @ U(GRIP[f"{key}_up"]))
        if kind == "reload" and side == "Left":
            target = bpy.data.objects["MagazineGrab"].matrix_world.translation.copy()
            t_frame = look_basis(gun_rot @ U(GRIP["mag_grab_fwd"]), gun_rot @ U(GRIP["mag_grab_up"]))
        hint = head(f"{side}UpperArm") + U(GRIP[hint_key])
        two_bone(f"{side}UpperArm", f"{side}LowerArm", f"{side}Hand", target, hint)
        frame_l = right_frame_l if side == "Right" else left_frame_l
        set_world_rot(f"{side}Hand", (t_frame @ frame_l.inverted()).to_quaternion())
        err = (head(f"{side}Hand") - target).length
        print(f"[grip] {kind} {side} wrist error {err * 1000:.1f} mm")

    g = GRIP
    aim_thumb("Right", gun_rot @ U(g["right_thumb"]))
    if kind != "reload":
        aim_thumb("Left", gun_rot @ U(g["left_thumb"]))
    curl("Right", center, (g["trigger_curl"], g["grip_curl"], g["grip_curl"], g["grip_curl"], g["thumb_curl"]))
    curl("Left", center, (g["support_curl"],) * 4 + (g["thumb_curl"],))
    return eye


def render(kind, eye):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_cavity = True
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 90
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.color = (0.05, 0.05, 0.055)
    cam_data = bpy.data.cameras.get("GripCam") or bpy.data.cameras.new("GripCam")
    cam = bpy.data.objects.get("GripCam") or bpy.data.objects.new("GripCam", cam_data)
    if cam.name not in scene.collection.objects:
        scene.collection.objects.link(cam)
    scene.camera = cam
    head_mesh = bpy.data.objects.get("Operator_Head")
    views = {
        # First person: from the eye, FOV 78 (vertical), looking slightly down like at the hip.
        "fp": (eye, eye + Vector((0, -1, -0.12 if kind == "hip" else 0.0)), True, 78.0, (1280, 720)),
        "tp": (eye + Vector((-1.1, -1.3, -0.1)), eye + Vector((0, -0.35, -0.2)), False, 35.0, (900, 900)),
        "side": (eye + Vector((-1.4, -0.35, -0.2)), eye + Vector((0, -0.35, -0.2)), False, 30.0, (900, 900)),
    }
    gun = bpy.data.objects["M1911"].matrix_world
    center = gun @ U((0.0, -0.045, -0.02))
    views["right_close"] = (center + gun.to_3x3() @ U((0.45, 0.02, -0.02)), center, True, 30.0, (900, 700))
    views["left_close"] = (center + gun.to_3x3() @ U((-0.45, 0.02, -0.02)), center, True, 30.0, (900, 700))
    views["front_close"] = (center + gun.to_3x3() @ U((0.12, -0.08, 0.45)), center, True, 30.0, (900, 700))
    views = {k: v for k, v in views.items() if k in globals().get("GRIP_VIEWS", views.keys())}
    for name, (pos, look, hide_head, fov, res) in views.items():
        cam.location = pos
        cam.rotation_euler = (look - pos).to_track_quat("-Z", "Y").to_euler()
        cam_data.sensor_fit = "VERTICAL"
        cam_data.angle_y = math.radians(fov)
        cam_data.clip_start = 0.03
        scene.render.resolution_x, scene.render.resolution_y = res
        if head_mesh:
            head_mesh.hide_render = hide_head
        path = os.path.join(OUT, f"grip_{kind}_{name}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
    if head_mesh:
        head_mesh.hide_render = False


for kind in POSES:
    render(kind, pose(kind))
