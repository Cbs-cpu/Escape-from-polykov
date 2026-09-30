"""
Renders the 3D inventory icons (Tarkov style: orthographic, side-on with a slight 3/4 tilt, transparent background) to
Assets/_Project/Resources/Icons/Items/<id>.png with the Workbench engine (material colours, backface culling so the
inverted-hull outline shows, studio light + cavity). Run inside Blender (MCP): exec(open(path, encoding='utf-8').read()).

  Mode 1 (default): every "Item_<id>" object of ArtSource/Items/Items_Source.blend (built by build_item_models.py).
  Mode 2 (env ICONS_AK=1): opens ArtSource/Weapons/AK74N_H31_Source.blend and renders each mesh child of the "AK74N"
  empty named "AK74N_ak_*" alone -> <id>.png with id = name without the "AK74N_" prefix. The AK file is NOT saved.
Output size follows the projected bounds aspect; long side 256 px (512 when the longest dimension > 0.35 m).
"""
import bpy
import math
import os
from mathutils import Vector, Matrix

ROOT = os.environ.get("POLYKOV_ROOT", r"C:\Users\Cobos\Escape from Polykov")
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Icons", "Items")
ITEMS_BLEND = os.path.join(ROOT, "ArtSource", "Items", "Items_Source.blend")
AK_BLEND = os.path.join(ROOT, "ArtSource", "Weapons", "AK74N_H31_Source.blend")
MARGIN = 1.16
# per-id overrides: (azimuth deg, elevation deg)
SIDE_ON = {"knife": (12, 14), "barrel_standard": (14, 14), "barrel_threaded": (14, 14), "suppressor_45": (14, 14),
           "grips_wood": (10, 10), "magazine_7": (25, 12)}


def setup_scene():
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.resolution_percentage = 100
    sc.view_settings.view_transform = "Standard"
    sc.display.render_aa = "8"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_cavity = True
    sh.cavity_type = "BOTH"
    sh.show_object_outline = False
    sh.show_specular_highlight = False
    if hasattr(sh, "show_backface_culling"):
        sh.show_backface_culling = True
    for m in bpy.data.materials:
        m.use_backface_culling = True
    cam = bpy.data.objects.get("_IconCam")
    if cam is None:
        cam = bpy.data.objects.new("_IconCam", bpy.data.cameras.new("_IconCam"))
        sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.clip_start, cam.data.clip_end = 0.01, 50
    sc.camera = cam
    return sc, cam


def world_points(ob):
    return [ob.matrix_world @ v.co for v in ob.data.vertices]


def render_one(sc, cam, ob, out_path, az, elev, base_dir):
    d = Matrix.Rotation(math.radians(az), 3, "Z") @ Vector(base_dir)
    h = math.hypot(d.x, d.y)
    d = Vector((d.x / h * math.cos(math.radians(elev)), d.y / h * math.cos(math.radians(elev)),
                -math.sin(math.radians(elev))))
    q = d.to_track_quat("-Z", "Y")
    right, up = q @ Vector((1, 0, 0)), q @ Vector((0, 1, 0))
    pts = world_points(ob)
    xs, ys = [p.dot(right) for p in pts], [p.dot(up) for p in pts]
    w, hh = max(xs) - min(xs), max(ys) - min(ys)
    cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    dims = max(max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3))
    long_px = 512 if dims > 0.35 else 256
    W, H = w * MARGIN, hh * MARGIN
    if W >= H:
        rx, ry = long_px, max(32, round(long_px * H / W))
    else:
        rx, ry = max(32, round(long_px * W / H)), long_px
    cam.data.ortho_scale = max(W, H)
    cam.matrix_world = Matrix.Translation(right * cx + up * cy - d * 10) @ q.to_matrix().to_4x4()
    sc.render.resolution_x, sc.render.resolution_y = rx, ry
    sc.render.filepath = out_path
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = o is not ob
    bpy.ops.render.render(write_still=True)
    return rx, ry


def run():
    os.makedirs(OUT, exist_ok=True)
    done = []
    if os.environ.get("ICONS_AK") == "1":
        bpy.ops.wm.open_mainfile(filepath=AK_BLEND)
        sc, cam = setup_scene()
        root = bpy.data.objects["AK74N"]
        for ob in sorted(root.children, key=lambda o: o.name):
            if ob.type == "MESH" and ob.name.startswith("AK74N_ak_"):
                pid = ob.name[len("AK74N_"):]
                az, el = (0, 0)
                az, el = (22, 14) if "_mag_" not in pid else (18, 10)
                done.append((pid,) + render_one(sc, cam, ob, os.path.join(OUT, pid + ".png"), az, el, (1, 0, 0)))
    else:
        bpy.ops.wm.open_mainfile(filepath=ITEMS_BLEND)
        sc, cam = setup_scene()
        for ob in sorted(bpy.data.objects, key=lambda o: o.name):
            if ob.type == "MESH" and ob.name.startswith("Item_"):
                pid = ob.name[5:]
                az, el = SIDE_ON.get(pid, (30, 18))
                done.append((pid,) + render_one(sc, cam, ob, os.path.join(OUT, pid + ".png"), az, el, (0, 1, 0)))
    return done


RESULT = run()
