"""Render orthographic/perspective turnaround previews of the Operator into docs/screenshots."""
import bpy
import math
import os
from mathutils import Vector

OUT = r"C:\Users\Cobos\Escape from Polykov\docs\screenshots"
os.makedirs(OUT, exist_ok=True)
scene = bpy.context.scene

scene.render.engine = "BLENDER_WORKBENCH"
shading = scene.display.shading
shading.light = "STUDIO"
shading.color_type = "MATERIAL"
shading.show_shadows = True
shading.show_cavity = True
shading.cavity_type = "WORLD"
shading.background_type = "VIEWPORT" if hasattr(shading, "background_type") else shading.background_type
scene.render.resolution_x = 700
scene.render.resolution_y = 1000
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("World")
scene.world.color = (0.05, 0.05, 0.055)

cam_data = bpy.data.cameras.get("PreviewCam") or bpy.data.cameras.new("PreviewCam")
cam = bpy.data.objects.get("PreviewCam") or bpy.data.objects.new("PreviewCam", cam_data)
if cam.name not in scene.collection.objects:
    scene.collection.objects.link(cam)
scene.camera = cam
cam_data.type = "ORTHO"
cam_data.ortho_scale = 2.05

rig = bpy.data.objects.get("Operator_Rig")
rig_visible = rig.hide_render if rig else None
if rig:
    rig.hide_render = True

target = Vector((0, 0, 0.93))
VIEWS = {
    "front": (0.0, "ORTHO"),
    "left": (90.0, "ORTHO"),
    "back": (180.0, "ORTHO"),
    "three_quarter": (-35.0, "PERSP"),
}
# yaw measured around Z; 0 = camera in front of the character (character faces -Y).
views = globals().get("PREVIEW_VIEWS", list(VIEWS.keys()))
paths = []
for name in views:
    yaw, kind = VIEWS[name]
    cam_data.type = kind
    dist = 6.0 if kind == "ORTHO" else 3.4
    a = math.radians(yaw)
    pos = target + Vector((math.sin(a) * dist, -math.cos(a) * dist, 0.15 if kind == "PERSP" else 0.0))
    cam.location = pos
    direction = target - pos
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    if kind == "PERSP":
        cam_data.lens = 50
    path = os.path.join(OUT, f"operator_{name}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    paths.append(path)

if rig:
    rig.hide_render = rig_visible
result = {"rendered": paths}
