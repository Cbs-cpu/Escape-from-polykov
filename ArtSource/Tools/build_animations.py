"""
Parametric in-place locomotion clips for Operator_Rig (Unity humanoid bone names).
Stride length per clip matches MovementTuning speeds, so feet don't slide in the blend tree.

Character space: +X = character's left, +Y = back, +Z = up (character faces -Y).
Rotations are authored in character space and converted to each bone's local rest frame.
Sign guide (verified with pose tests and renders):
  legs  : Rx(-a) swings the thigh forward, Rx(+a) on the shin flexes the knee, Rx(-a) lifts the toes.
  torso : bones pointing up are the opposite: Rx(+a) leans hips/spine/chest FORWARD, Rx(-a) leans back
          (and Rx(+a) on the pelvis swings the legs backward, so thighs must compensate).
  arms  : Ry(+a) lowers the left arm from T-pose (Ry(-a) for the right), then Rx(-a) swings it forward.
  finger: Ry(+a) curls left fingers toward the palm (Ry(-a) right).
"""
import bpy
import math
from mathutils import Matrix, Vector, Quaternion

FPS = 30
RIG = bpy.data.objects["Operator_Rig"]
bpy.context.scene.render.fps = FPS


def Rx(a): return Matrix.Rotation(math.radians(a), 3, "X")
def Ry(a): return Matrix.Rotation(math.radians(a), 3, "Y")
def Rz(a): return Matrix.Rotation(math.radians(a), 3, "Z")


REST = {b.name: b.matrix_local.to_3x3() for b in RIG.data.bones}
FINGER_NAMES = ["Index", "Middle", "Ring", "Little"]


def to_local(name, rot_char):
    m = REST[name]
    return (m.inverted() @ rot_char @ m).to_quaternion()


def loc_local(name, v_char):
    return REST[name].inverted() @ Vector(v_char)


# ---------------------------------------------------------------- base poses
def relaxed_hands(pose, curl=18.0, thumb=10.0):
    for side, s in (("Left", 1), ("Right", -1)):
        for f in FINGER_NAMES:
            for i, seg in enumerate(("Proximal", "Intermediate", "Distal")):
                pose[f"{side}{f}{seg}"] = Ry(s * curl * (1.0 + 0.25 * i))
        pose[f"{side}ThumbProximal"] = Rz(-s * thumb)
        pose[f"{side}ThumbIntermediate"] = Ry(s * thumb)


def arms(pose, lower=72.0, swing_l=0.0, swing_r=0.0, elbow_l=12.0, elbow_r=12.0, inward=6.0):
    # Upper arm: lower from T-pose, bring slightly in front of the body, then swing forward/back.
    # Yaw slightly forward in T-pose first, so the lowered arms hang in front of the thighs.
    pose["LeftUpperArm"] = Rx(swing_l) @ Ry(lower) @ Rz(-inward)
    pose["RightUpperArm"] = Rx(swing_r) @ Ry(-lower) @ Rz(inward)
    # Elbow flex: in the T-pose rest frame the forearm hinges around Z; Rz(-a) brings the left forearm forward.
    pose["LeftLowerArm"] = Rz(-elbow_l)
    pose["RightLowerArm"] = Rz(elbow_r)


def gait_pose(phase, p, backward=False):
    """phase in [0,1). p: gait parameters dict."""
    pose, loc = {}, {}
    ph = -phase if backward else phase
    w = 2 * math.pi
    for side, off in (("Left", 0.0), ("Right", 0.5)):
        f = ph + off
        swing = math.cos(w * f)                     # >0 while the leg swings forward
        thigh = -p["thigh"] * math.sin(w * f) + p["thigh_bias"]
        knee = p["knee_stance"] + p["knee_swing"] * max(0.0, swing) ** 1.4
        # Toe-off near the end of stance, heel strike at the end of swing.
        toe_off = p["toe_off"] * max(0.0, -math.sin(w * (f + 0.12))) ** 3
        heel = -p["heel"] * max(0.0, math.sin(w * (f - 0.18))) ** 4
        # Absolute shin angle includes the pelvis tilt (forward lean swings the legs back).
        pelvis = p["lean"] * 0.3 * (-0.6 if backward else 1.0)
        foot = -(thigh + knee + pelvis) * 0.85 + toe_off + heel
        if backward:
            foot = -(thigh + knee + pelvis) * 0.85 + 0.4 * toe_off
        pose[side + "UpperLeg"] = Rx(thigh - pelvis)
        pose[side + "LowerLeg"] = Rx(knee)
        pose[side + "Foot"] = Rx(foot)
        pose[side + "Toes"] = Rx(-toe_off * 0.6)
    s = math.sin(w * ph)
    bob = -p["bob"] * math.cos(2 * w * (ph - 0.25))
    loc["Hips"] = (0.0, 0.0, bob - p["crouch"])
    lean = p["lean"] * (-0.6 if backward else 1.0)
    pose["Hips"] = Rz(p["pelvis_yaw"] * s) @ Rx(lean * 0.3)
    pose["Spine"] = Rz(-p["pelvis_yaw"] * s * 0.6) @ Rx(lean * 0.4)
    pose["Chest"] = Rz(-p["pelvis_yaw"] * s * 0.6) @ Rx(lean * 0.3)
    pose["UpperChest"] = Rx(0.0)
    # Neck and head counter the lean so the eyes stay level.
    pose["Neck"] = Rx(-lean * 0.4)
    pose["Head"] = Rx(-lean * 0.5 + p["bob"] * 40 * math.cos(2 * w * (ph - 0.25)))
    arm = p["arm"] * s
    elbow_extra = p["elbow_swing"]
    arms(pose, lower=p["arm_lower"],
         swing_l=arm, swing_r=-arm,
         elbow_l=p["elbow"] + elbow_extra * max(0.0, -s),
         elbow_r=p["elbow"] + elbow_extra * max(0.0, s))
    relaxed_hands(pose, curl=p["curl"])
    return pose, loc


def strafe_pose(phase, p, direction):
    """direction +1 = strafe left (+X), -1 = strafe right."""
    pose, loc = {}, {}
    w = 2 * math.pi
    for side, off, sgn in (("Left", 0.0, 1), ("Right", 0.5, -1)):
        f = phase + off
        swing = math.cos(w * f)
        # Lateral swing: Ry(-a) moves a down-pointing leg toward +X.
        lateral = -direction * p["side"] * math.sin(w * f)
        knee = p["knee_stance"] + p["knee_swing"] * max(0.0, swing) ** 1.3
        thigh = -knee * 0.45
        pose[side + "UpperLeg"] = Ry(lateral) @ Rx(thigh)
        pose[side + "LowerLeg"] = Rx(knee)
        pose[side + "Foot"] = Rx(-(thigh + knee) * 0.9) @ Ry(-lateral * 0.8)
        pose[side + "Toes"] = Rx(0.0)
    bob = -p["bob"] * math.cos(2 * w * (phase - 0.25))
    loc["Hips"] = (direction * p["sway"] * math.sin(w * phase), 0.0, bob - p["crouch"])
    pose["Hips"] = Ry(direction * 2.0)
    pose["Spine"] = Ry(-direction * 1.5) @ Rx(p["lean"] * 0.3)
    pose["Neck"] = Rx(-p["lean"] * 0.15)
    pose["Chest"] = Ry(-direction * 1.0)
    pose["Head"] = Ry(direction * 1.0)
    s = math.sin(w * phase)
    arms(pose, lower=p["arm_lower"], swing_l=p["arm"] * s * 0.3, swing_r=-p["arm"] * s * 0.3,
         elbow_l=p["elbow"], elbow_r=p["elbow"])
    relaxed_hands(pose, curl=p["curl"])
    return pose, loc


def idle_pose(phase):
    pose, loc = {}, {}
    w = 2 * math.pi
    breath = math.sin(w * phase)
    shift = math.sin(w * phase * 0.5 + 0.3)
    loc["Hips"] = (0.008 * shift, 0.0, -0.006 + 0.002 * breath)
    pose["Hips"] = Ry(-1.2 * shift)
    pose["Spine"] = Rx(-1.0 * breath) @ Ry(0.6 * shift)
    pose["Chest"] = Rx(-1.2 * breath)
    pose["UpperChest"] = Rx(-0.6 * breath)
    pose["Neck"] = Rx(0.6 * breath)
    pose["Head"] = Rx(0.8 * breath) @ Rz(1.2 * math.sin(w * phase * 0.5))
    for side, s in (("Left", 1), ("Right", -1)):
        pose[side + "UpperLeg"] = Rx(-2.0) @ Ry(-s * 2.5 + 0.6 * shift)
        pose[side + "LowerLeg"] = Rx(5.0)
        pose[side + "Foot"] = Rx(-3.0)
    arms(pose, lower=73.0 + 1.0 * breath, swing_l=-3.0 + 1.5 * breath, swing_r=-3.0 + 1.5 * breath,
         elbow_l=14.0, elbow_r=14.0)
    relaxed_hands(pose, curl=22.0)
    return pose, loc


def jump_pose(kind, t):
    """kind: 'start' (t 0..1 crouch+push), 'air' (loop), 'land' (t 0..1 absorb+recover)."""
    pose, loc = {}, {}
    if kind == "start":
        c = math.sin(math.pi * min(t / 0.6, 1.0)) if t < 0.6 else 0.0
        ext = max(0.0, (t - 0.55) / 0.45)
        knee = 45 * c + 8 * (1 - ext)
        thigh = -knee * 0.55
        loc["Hips"] = (0.0, 0.0, -0.08 * c + 0.03 * ext)
        up = 14 * ext
        spine = -10 * c
    elif kind == "air":
        s = math.sin(2 * math.pi * t)
        knee = 38 + 4 * s
        thigh = -30 - 3 * s
        loc["Hips"] = (0.0, 0.0, 0.0)
        up = 8 + 2 * s
        spine = -4
    else:  # land
        c = math.sin(math.pi * min(t / 0.7, 1.0)) if t < 0.7 else 0.0
        knee = 8 + 50 * c
        thigh = -knee * 0.55
        loc["Hips"] = (0.0, 0.0, -0.10 * c)
        up = 10 * (1 - t)
        spine = -12 * c
    for side in ("Left", "Right"):
        pose[side + "UpperLeg"] = Rx(thigh)
        pose[side + "LowerLeg"] = Rx(knee)
        pose[side + "Foot"] = Rx(-(thigh + knee) * 0.8)
    pose["Spine"] = Rx(spine * 0.5)
    pose["Chest"] = Rx(spine * 0.5)
    pose["Head"] = Rx(-spine * 0.6)
    arms(pose, lower=72.0 - up, swing_l=-up * 0.8, swing_r=-up * 0.8, elbow_l=20 + up, elbow_r=20 + up)
    relaxed_hands(pose, curl=24.0)
    return pose, loc


def crouch_idle_pose(phase):
    """Low ready crouch: hips ~0.36 m lower, knees ~110 deg, torso leaning forward, feet flat."""
    pose, loc = {}, {}
    w = 2 * math.pi
    breath = math.sin(w * phase)
    shift = math.sin(w * phase * 0.5 + 0.3)
    # Extra drop so the ankles rest at their standing height (measured: +2.5 cm otherwise).
    loc["Hips"] = (0.006 * shift, 0.0, -CROUCH_DROP - 0.025 + 0.003 * breath)
    pelvis = 12.0
    pose["Hips"] = Rx(pelvis) @ Ry(-1.0 * shift)
    pose["Spine"] = Rx(8.0 + 1.0 * breath)
    pose["Chest"] = Rx(5.0 + 1.0 * breath)
    pose["UpperChest"] = Rx(0.5 * breath)
    pose["Neck"] = Rx(-10.0 - 0.5 * breath)
    pose["Head"] = Rx(-12.0 - 0.6 * breath)
    for side, s in (("Left", 1), ("Right", -1)):
        thigh, knee = -58.0, 112.0  # absolute thigh angle (forward), knee flex
        pose[side + "UpperLeg"] = Rx(thigh - pelvis) @ Ry(-s * 6.0)
        pose[side + "LowerLeg"] = Rx(knee)
        pose[side + "Foot"] = Rx(-(thigh + knee) * 0.95)
    arms(pose, lower=70.0 + breath, swing_l=-8.0, swing_r=-8.0, elbow_l=30.0, elbow_r=30.0)
    relaxed_hands(pose, curl=26.0)
    return pose, loc


# ---------------------------------------------------------------- gait presets (stride matches speed)
CROUCH_DROP = 0.36
BASE = dict(thigh_bias=0.0, knee_stance=6.0, toe_off=18.0, heel=10.0, crouch=0.0, curl=20.0,
            arm_lower=73.0, elbow_swing=10.0)
WALK = dict(BASE, thigh=24.0, knee_swing=48.0, bob=0.016, lean=3.0, pelvis_yaw=5.0, arm=13.0, elbow=14.0)
RUN = dict(BASE, thigh=34.0, thigh_bias=-4.0, knee_stance=16.0, knee_swing=88.0, bob=0.028, crouch=0.02,
           lean=8.0, pelvis_yaw=8.0, arm=24.0, elbow=52.0, elbow_swing=10.0, arm_lower=70.0, toe_off=25.0, curl=35.0)
SPRINT = dict(BASE, thigh=44.0, thigh_bias=-7.0, knee_stance=20.0, knee_swing=108.0, bob=0.036, crouch=0.03,
              lean=14.0, pelvis_yaw=9.0, arm=34.0, elbow=66.0, elbow_swing=6.0, arm_lower=68.0, toe_off=30.0, curl=45.0)
STRAFE_WALK = dict(BASE, side=13.0, knee_stance=6.0, knee_swing=30.0, bob=0.012, sway=0.018, lean=2.0,
                   arm=8.0, elbow=16.0)
STRAFE_RUN = dict(BASE, side=20.0, knee_stance=14.0, knee_swing=55.0, bob=0.022, sway=0.028, crouch=0.02,
                  lean=6.0, arm=16.0, elbow=45.0, arm_lower=70.0, curl=30.0)


CROUCH_WALK = dict(BASE, thigh=18.0, thigh_bias=-52.0, knee_stance=104.0, knee_swing=26.0, bob=0.008,
                   crouch=CROUCH_DROP, lean=16.0, pelvis_yaw=4.0, arm=6.0, elbow=30.0, arm_lower=70.0,
                   toe_off=8.0, heel=4.0, curl=26.0)
CROUCH_STRAFE = dict(BASE, side=11.0, knee_stance=104.0, knee_swing=22.0, bob=0.008, sway=0.012,
                     crouch=CROUCH_DROP, lean=12.0, arm=5.0, elbow=30.0, arm_lower=70.0, curl=26.0)


def frames_for(speed, cycle_distance):
    return max(8, round(cycle_distance / speed * FPS))


# name: (generator(phase)->(pose, loc), frames, loop)
CLIPS = {
    "Idle": (idle_pose, 75, True),
    "Walk_F": (lambda ph: gait_pose(ph, WALK), frames_for(1.8, 1.5), True),
    "Run_F": (lambda ph: gait_pose(ph, RUN), frames_for(3.6, 2.2), True),
    "Sprint_F": (lambda ph: gait_pose(ph, SPRINT), frames_for(5.8, 3.0), True),
    "Walk_B": (lambda ph: gait_pose(ph, dict(WALK, thigh=20.0, arm=8.0)), frames_for(1.26, 1.2), True),
    "Run_B": (lambda ph: gait_pose(ph, dict(RUN, thigh=26.0, lean=4.0, arm=16.0)), frames_for(2.52, 1.7), True),
    "Walk_L": (lambda ph: strafe_pose(ph, STRAFE_WALK, 1), frames_for(1.53, 1.1), True),
    "Walk_R": (lambda ph: strafe_pose(ph, STRAFE_WALK, -1), frames_for(1.53, 1.1), True),
    "Run_L": (lambda ph: strafe_pose(ph, STRAFE_RUN, 1), frames_for(3.06, 1.7), True),
    "Run_R": (lambda ph: strafe_pose(ph, STRAFE_RUN, -1), frames_for(3.06, 1.7), True),
    # Crouch set (speeds match MovementTuning.CrouchSpeed = 1.5 m/s; strafe/back use the directional multipliers).
    "Crouch_Idle": (crouch_idle_pose, 90, True),
    "Crouch_F": (lambda ph: gait_pose(ph, CROUCH_WALK), frames_for(1.5, 1.0), True),
    "Crouch_B": (lambda ph: gait_pose(ph, dict(CROUCH_WALK, thigh=14.0, lean=10.0)), frames_for(1.05, 0.8), True),
    "Crouch_L": (lambda ph: strafe_pose(ph, CROUCH_STRAFE, 1), frames_for(1.28, 0.8), True),
    "Crouch_R": (lambda ph: strafe_pose(ph, CROUCH_STRAFE, -1), frames_for(1.28, 0.8), True),
    "Jump_Start": (lambda t: jump_pose("start", t), 7, False),
    "Jump_Air": (lambda t: jump_pose("air", t), 24, True),
    "Jump_Land": (lambda t: jump_pose("land", t), 12, False),
}


def bake_clip(name, gen, frames, loop):
    act = bpy.data.actions.get(name)
    if act:
        bpy.data.actions.remove(act)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    RIG.animation_data_create()
    RIG.animation_data.action = act
    bones = RIG.pose.bones
    for pb in bones:
        pb.rotation_mode = "QUATERNION"
    last = frames if loop else frames - 1
    for f in range(0, last + 1):
        t = (f % frames) / frames if loop else f / max(frames - 1, 1)
        pose, loc = gen(t)
        for pb in bones:
            q = to_local(pb.name, pose[pb.name]) if pb.name in pose else Quaternion()
            pb.rotation_quaternion = q
            pb.location = loc_local(pb.name, loc[pb.name]) if pb.name in loc else Vector()
            pb.keyframe_insert("rotation_quaternion", frame=f)
            if pb.name == "Hips":
                pb.keyframe_insert("location", frame=f)
    act.frame_range = (0, last)
    return act


def bake_all():
    out = {}
    for name, (gen, frames, loop) in CLIPS.items():
        bake_clip(name, gen, frames, loop)
        out[name] = frames
    RIG.animation_data.action = bpy.data.actions["Idle"]
    for pb in RIG.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
    return out


result = bake_all()
