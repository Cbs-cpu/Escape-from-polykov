"""Render M1911 previews (right, left, top, three-quarter) into docs/screenshots. Run after build_m1911.py."""
import bpy
import math
import os
from mathutils import Vector

ROOT = os.environ.get("POLYKOV_ROOT", r"C:\Users\Cobos\Escape from Polykov")
OUT = os.path.join(ROOT, "docs", "screenshots")
os.makedirs(OUT, exist_ok=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
shading = scene.display.shading
shading.light = "STUDIO"
shading.color_type = "MATERIAL"
shading.show_shadows = False
shading.show_cavity = True
shading.cavity_type = "BOTH"
if scene.world is None:
    scene.world = bpy.data.worlds.new("World")
scene.world.color = (0.05, 0.05, 0.055)
scene.render.resolution_x = 960
scene.render.resolution_y = 600

cam_data = bpy.data.cameras.get("GunCam") or bpy.data.cameras.new("GunCam")
cam = bpy.data.objects.get("GunCam") or bpy.data.objects.new("GunCam", cam_data)
if cam.name not in scene.collection.objects:
    scene.collection.objects.link(cam)
scene.camera = cam

# Weapon center in Blender space (Unity (0, -0.04, 0.035) -> Blender (0, -0.035, -0.04)).
target = Vector((0.0, -0.035, -0.04))
scene.render.image_settings.color_depth = "8"
scene.render.image_settings.compression = 90
VIEWS = {
    "right": (Vector((-1, 0, 0)), "ORTHO", 0.3),
    "left": (Vector((1, 0, 0)), "ORTHO", 0.3),
    "top": (Vector((0, 0, 1)), "ORTHO", 0.28),
    "three_quarter": (Vector((-0.8, -0.9, 0.45)), "PERSP", 0.0),
}
paths = []
for name, (direction, kind, scale) in VIEWS.items():
    cam_data.type = kind
    if kind == "ORTHO":
        cam_data.ortho_scale = scale
    else:
        cam_data.lens = 60
    pos = target + direction.normalized() * 0.6
    cam.location = pos
    up = "Y" if name == "top" else "Y"
    cam.rotation_euler = (target - pos).to_track_quat("-Z", up).to_euler()
    if name == "top":
        cam.rotation_euler = (0.0, 0.0, math.radians(90))
    path = os.path.join(OUT, f"m1911_{name}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    paths.append(path)
print("[M1911 render]", paths)
