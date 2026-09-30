"""
Renders the AK-74N variant catalogue (Workbench, orthographic right side): one image per part, the rest of the rifle
in its factory build, plus a contact sheet docs/screenshots/ak_variants.png. Run after build_ak74n_variants.py.
"""
import bpy
import math
import os
from mathutils import Vector

ROOT = "AK74N"
OUT = os.path.join(os.environ.get("POLYKOV_ROOT", r"C:\Users\Cobos\Escape from Polykov"), "docs", "screenshots")
TMP = os.path.join(bpy.app.tempdir, "ak_variants")
FACTORY = {"muzzle": "ak_muzzle_brake", "barrel": "ak_barrel_standard", "hg": "ak_hg_polymer",
           "cover": "ak_cover_standard", "grip": "ak_grip_polymer", "stock": "ak_stock_polymer", "mag": "ak_mag_30"}


def slot_of(att):
    if att == "ak_suppressor" or att.startswith("ak_muzzle_"):
        return "muzzle"
    return att.split("_")[1]


def variants(root):
    return {o.name[6:]: o for o in root.children if o.type == "MESH" and o.name[6:].startswith("ak_")}


def show(root, build):
    parts = variants(root)
    for att, o in parts.items():
        o.hide_render = build.get(slot_of(att)) != att
    # muzzle device follows the barrel tip
    barrel = parts[build["barrel"]]
    tip = [c for c in barrel.children if c.name.startswith("Socket_Tip_")][0]
    std_tip = [c for c in parts["ak_barrel_standard"].children if c.name.startswith("Socket_Tip_")][0]
    bpy.context.view_layer.update()
    delta = tip.matrix_world.translation - std_tip.matrix_world.translation
    for att, o in parts.items():
        if slot_of(att) == "muzzle":
            o["base_loc"] = o.get("base_loc", list(o.location))
            o.location = Vector(o["base_loc"]) + root.matrix_world.inverted().to_3x3() @ delta


def render_all():
    sc = bpy.context.scene
    root = bpy.data.objects[ROOT]
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.show_backface_culling = True
    cam = bpy.data.objects.get("RefCam") or bpy.data.objects.new("RefCam", bpy.data.cameras.new("RefCam"))
    if cam.name not in sc.collection.objects:
        sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 1.15
    R = root.matrix_world
    cam.location = R @ Vector((-2, -0.19, 0.0))
    cam.rotation_euler = (math.pi / 2, 0, -math.pi / 2)
    sc.render.resolution_x, sc.render.resolution_y = 900, 330
    os.makedirs(TMP, exist_ok=True)
    shots = []
    order = ["muzzle", "barrel", "hg", "cover", "grip", "stock", "mag"]
    for slot in order:
        for att in sorted(a for a in variants(root) if slot_of(a) == slot):
            build = dict(FACTORY)
            build[slot] = att
            show(root, build)
            path = os.path.join(TMP, att + ".png")
            sc.render.filepath = path
            bpy.ops.render.render(write_still=True)
            shots.append((slot, att, path))
    show(root, FACTORY)
    for o in variants(root).values():
        if "base_loc" in o:
            o.location = Vector(o["base_loc"])
            del o["base_loc"]
    return shots


result = {"shots": [(s, a) for s, a, _ in render_all()], "dir": TMP}
