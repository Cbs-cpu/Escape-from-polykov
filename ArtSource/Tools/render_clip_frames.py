"""
Render sample frames of animation clips (side + three-quarter) to check poses without Unity.
Set CLIP_FRAMES = {"Crouch_F": [0, 5, 10], ...} before exec, or it renders every Crouch_* clip at 3 phases.
Output: docs/screenshots/clip_<name>_<frame>_<view>.png (POLYKOV_ROOT = repo path).
"""
import bpy
import math
import os
from mathutils import Vector

ROOT = os.environ.get("POLYKOV_ROOT", r"C:\\Users\\Cobos\\Escape from Polykov")
OUT = os.path.join(ROOT, "docs", "screenshots")
os.makedirs(OUT, exist_ok=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
sh = scene.display.shading
sh.light = "STUDIO"
sh.color_type = "MATERIAL"
sh.show_cavity = True
scene.render.image_settings.color_depth = "8"
scene.render.image_settings.compression = 90
scene.render.resolution_x, scene.render.resolution_y = 600, 800
if scene.world is None:
    scene.world = bpy.data.worlds.new("World")
scene.world.color = (0.05, 0.05, 0.055)

rig = bpy.data.objects["Operator_Rig"]
rig.hide_render = True
cam_data = bpy.data.cameras.get("ClipCam") or bpy.data.cameras.new("ClipCam")
cam = bpy.data.objects.get("ClipCam") or bpy.data.objects.new("ClipCam", cam_data)
if cam.name not in scene.collection.objects:
    scene.collection.objects.link(cam)
scene.camera = cam
cam_data.type = "ORTHO"
cam_data.ortho_scale = 2.1

# Ground plane for reading foot contact.
if "PreviewFloor" not in bpy.data.objects:
    me = bpy.data.meshes.new("PreviewFloor")
    me.from_pydata([(-2, -2, 0), (2, -2, 0), (2, 2, 0), (-2, 2, 0)], [], [(0, 1, 2, 3)])
    floor = bpy.data.objects.new("PreviewFloor", me)
    scene.collection.objects.link(floor)

clips = globals().get("CLIP_FRAMES")
if clips is None:
    clips = {}
    for a in bpy.data.actions:
        if a.name.startswith("Crouch_"):
            end = int(a.frame_range[1])
            clips[a.name] = [0, end // 3, 2 * end // 3]

target = Vector((0, 0, 0.8))
views = {"side": (Vector((-1, 0, 0)), 0.0), "tq": (Vector((-0.7, -0.75, 0.1)), 0.0)}
rig.animation_data_create()
for name, frames in clips.items():
    rig.animation_data.action = bpy.data.actions[name]
    for f in frames:
        scene.frame_set(f)
        for view, (direction, _) in views.items():
            cam.location = target + direction.normalized() * 5.0
            cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(OUT, f"clip_{name}_{f}_{view}.png")
            bpy.ops.render.render(write_still=True)
print("[clips] rendered", {k: v for k, v in clips.items()})
