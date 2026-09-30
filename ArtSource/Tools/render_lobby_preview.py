"""
Recreates the Unity Lobby scene in Blender (same FBX assets, textures, camera, lights and framing as
LobbyController / LobbyStage) and renders the 3D layer of the lobby screens, so the look can be reviewed
without Unity. The IMGUI layer is drawn on top by Tools/lobby_ui_mockup.py.

Usage:  blender -b -P ArtSource/Tools/render_lobby_preview.py -- stage <out.png> <w> <h> <px py pz lx ly lz fov>
        blender -b -P ... -- armorer|armorer_suppressor <out.png> <w> <h>
        blender -b -P ... -- icon <itemId> <out.png> [suppressed]
The stage camera numbers come from Tools/UiPreview ("framing"), i.e. the same StageFraming math as the game.
Unity -> Blender coordinates: (x, y, z)_unity = (-x, z, -y)_blender, i.e. blender = (-xu, -zu, yu).
"""
import bpy
import math
import os
import sys
from mathutils import Matrix, Vector

ROOT = os.environ.get("POLYKOV_ROOT", os.getcwd())
ART = os.path.join(ROOT, "Assets", "_Project", "Art")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["stage", "lobby.png"]
SCREEN = args[0]
if SCREEN == "icon":
    ICON_ID, OUT = args[1], args[2]
    ICON_SUPPRESSED = len(args) > 3 and args[3] == "suppressed"
    WIDTH = HEIGHT = 0
else:
    OUT = args[1]
    WIDTH, HEIGHT = (int(args[2]), int(args[3])) if len(args) > 3 else (1920, 1080)
SHOT = [float(v) for v in args[4:11]] if SCREEN == "stage" and len(args) >= 11 else None


def u2b(x, y, z):
    return Vector((-x, -z, y))


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def lin(rgb):
    return tuple(srgb_to_linear(c) for c in rgb)


def unity_dir(pitch, yaw):
    """Forward of Quaternion.Euler(pitch, yaw, 0) in Unity space."""
    p, y = math.radians(pitch), math.radians(yaw)
    return (math.sin(y) * math.cos(p), -math.sin(p), math.cos(y) * math.cos(p))


bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- materials


def image(path, non_color=False):
    img = bpy.data.images.load(path, check_existing=True)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    return img


def lit_material(name, albedo=None, normal=None, metal_smooth=None, color=(1, 1, 1), metallic=0.0, smoothness=0.5):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*lin(color), 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = 1 - smoothness
    if albedo:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image(albedo)
        tex.interpolation = "Closest"
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if normal:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image(normal, True)
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tex.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    if metal_smooth:
        # URP metallic map: R = metallic, A = smoothness (scaled by _Smoothness).
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image(metal_smooth, True)
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(tex.outputs["Color"], sep.inputs["Color"])
        nt.links.new(sep.outputs["Red"], bsdf.inputs["Metallic"])
        mul = nt.nodes.new("ShaderNodeMath")
        mul.operation = "MULTIPLY"
        mul.inputs[1].default_value = smoothness
        nt.links.new(tex.outputs["Alpha"], mul.inputs[0])
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation = "SUBTRACT"
        inv.inputs[0].default_value = 1.0
        nt.links.new(mul.outputs[0], inv.inputs[1])
        nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    return m


def outline_material():
    # Inverted hull (faces reversed, drawn with back-face culling in Unity): only front-facing hull faces show.
    m = bpy.data.materials.new("Outline")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    black = nt.nodes.new("ShaderNodeEmission")
    black.inputs["Color"].default_value = (0, 0, 0, 1)
    clear = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(geo.outputs["Backfacing"], mix.inputs["Fac"])
    nt.links.new(black.outputs["Emission"], mix.inputs[1])
    nt.links.new(clear.outputs["BSDF"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    return m


def add_fog(m, start=10.0, end=20.0):
    """Unity linear fog (Lobby.unity) in the background colour, by distance from the camera."""
    nt = m.node_tree
    out = nt.nodes["Material Output"]
    lit = out.inputs["Surface"].links[0].from_node
    lp = nt.nodes.new("ShaderNodeLightPath")
    rng = nt.nodes.new("ShaderNodeMapRange")
    rng.inputs["From Min"].default_value = start
    rng.inputs["From Max"].default_value = end
    nt.links.new(lp.outputs["Ray Length"], rng.inputs["Value"])
    fog = nt.nodes.new("ShaderNodeEmission")
    fog.inputs["Color"].default_value = (*lin((0.035, 0.038, 0.04)), 1)
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(rng.outputs["Result"], mix.inputs["Fac"])
    nt.links.new(lit.outputs[0], mix.inputs[1])
    nt.links.new(fog.outputs["Emission"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])


def grid_material():
    """LobbyStage.GridTexture: 128 px tile, major line on two edges, minor every 32 px; tinted by the base colour."""
    size = 128
    img = bpy.data.images.new("LobbyGrid", size, size)
    px = []
    for y in range(size):
        for x in range(size):
            v = 150 if x < 2 or y < 2 else 92 if x % 32 == 0 or y % 32 == 0 else 60
            c = srgb_to_linear(v / 255.0)
            px += [c, c, c, 1.0]
    img.pixels = px
    m = lit_material("Grid", color=(0.34, 0.35, 0.36), smoothness=0.05)
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    uv = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (25, 25, 1)
    nt.links.new(uv.outputs["UV"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    mul.inputs["A"].default_value = (*lin((0.34, 0.35, 0.36)), 1)
    nt.links.new(tex.outputs["Color"], mul.inputs["B"])
    nt.links.new(mul.outputs["Result"], bsdf.inputs["Base Color"])
    return m


OUTLINE = outline_material()
OPERATOR = lit_material("Operator", os.path.join(ART, "Characters/Operator/T_Operator_Tripo_Albedo.png"),
                        os.path.join(ART, "Characters/Operator/T_Operator_Tripo_Normal.png"), smoothness=0.15)
WEAPON = lit_material("M1911", os.path.join(ART, "Weapons/M1911_Tripo/T_M1911_Tripo_Albedo.png"),
                      os.path.join(ART, "Weapons/M1911_Tripo/T_M1911_Tripo_Normal.png"),
                      os.path.join(ART, "Weapons/M1911_Tripo/T_M1911_Tripo_MetalSmooth.png"), metallic=1.0, smoothness=0.35)
SUPPRESSOR = lit_material("Suppressor", color=(0.05, 0.052, 0.056), metallic=0.6, smoothness=0.35)
BARREL = lit_material("BarrelSteel", color=(0.11, 0.115, 0.12), metallic=0.8, smoothness=0.5)


def import_fbx(path, body):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ART, path))
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type != "MESH":
            continue
        for slot in o.material_slots:
            outline = slot.material is not None and "Outline" in slot.material.name
            slot.link = "OBJECT"
            slot.material = OUTLINE if outline else body
    return new


def root_of(objects):
    return next(o for o in objects if o.parent is None)


# ---------------------------------------------------------------- world, lights

world = bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
wt = world.node_tree
wt.nodes.clear()
wout = wt.nodes.new("ShaderNodeOutputWorld")
# Unity Trilight ambient (sky / equator / ground) for lighting; camera rays see the lobby background colour.
coord = wt.nodes.new("ShaderNodeTexCoord")
sep = wt.nodes.new("ShaderNodeSeparateXYZ")
wt.links.new(coord.outputs["Generated"], sep.inputs["Vector"])
ramp = wt.nodes.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].position = 0.0
ramp.color_ramp.elements[0].color = (*lin((0.1725, 0.1804, 0.1882)), 1)
ramp.color_ramp.elements[1].position = 1.0
ramp.color_ramp.elements[1].color = (*lin((0.4353, 0.4667, 0.502)), 1)
mid = ramp.color_ramp.elements.new(0.5)
mid.color = (*lin((0.302, 0.3255, 0.345)), 1)
maprange = wt.nodes.new("ShaderNodeMapRange")
maprange.inputs["From Min"].default_value = -1.0
maprange.inputs["From Max"].default_value = 1.0
wt.links.new(sep.outputs["Z"], maprange.inputs["Value"])
wt.links.new(maprange.outputs["Result"], ramp.inputs["Fac"])
amb = wt.nodes.new("ShaderNodeBackground")
wt.links.new(ramp.outputs["Color"], amb.inputs["Color"])
bg = wt.nodes.new("ShaderNodeBackground")
bg.inputs["Color"].default_value = (*lin((0.035, 0.038, 0.04)), 1)
lp = wt.nodes.new("ShaderNodeLightPath")
wmix = wt.nodes.new("ShaderNodeMixShader")
wt.links.new(lp.outputs["Is Camera Ray"], wmix.inputs["Fac"])
wt.links.new(amb.outputs["Background"], wmix.inputs[1])
wt.links.new(bg.outputs["Background"], wmix.inputs[2])
wt.links.new(wmix.outputs["Shader"], wout.inputs["Surface"])


def add_light(name, kind, pos_u, pitch, yaw, color, intensity, angle=0.0):
    data = bpy.data.lights.new(name, kind)
    data.color = lin(color)
    # Unity (no 1/pi in Lambert, 1/d^2 falloff) -> Cycles radiometric units.
    data.energy = intensity * math.pi if kind == "SUN" else intensity * 4 * math.pi ** 2
    if kind == "SPOT":
        data.spot_size = math.radians(angle)
        data.spot_blend = 0.35
    if kind != "SUN":
        data.shadow_soft_size = 0.15
    ob = bpy.data.objects.new(name, data)
    scene.collection.objects.link(ob)
    ob.location = u2b(*pos_u)
    d = unity_dir(pitch, yaw)
    ob.rotation_euler = u2b(*d).to_track_quat("-Z", "Y").to_euler()
    return ob


# Scene directional light (Lobby.unity) + LobbyController.BuildLights (shadows off there: kept off here).
sun = add_light("Sun", "SUN", (0, 3, 0), 50, -30, (1, 0.96, 0.9), 0.6)
sun.data.use_shadow = False
WO = (30.0, 1.2, 0.0)
for L in (
        ("KeyLight", "SPOT", (-1.4, 3.2, -2.2), 58, 24, (1, 0.93, 0.8), 5.5, 55),
        ("RimLight", "SPOT", (1.6, 2.4, 2.4), 35, 220, (0.55, 0.7, 1), 3.2, 60),
        ("WeaponKey", "SPOT", (WO[0] + 0.9, WO[1] + 1.4, WO[2] - 1.0), 50, -40, (1, 0.94, 0.82), 4.5, 60),
        ("WeaponFill", "POINT", (WO[0] - 0.9, WO[1] + 0.4, WO[2] - 0.8), 0, 0, (0.6, 0.7, 0.9), 1.2, 0)):
    add_light(L[0], L[1], L[2], L[3], L[4], L[5], L[6], L[7]).data.use_shadow = False

# ---------------------------------------------------------------- camera

cam_data = bpy.data.cameras.new("StageCamera")
cam_data.sensor_fit = "VERTICAL"
cam_data.clip_start = 0.02
cam = bpy.data.objects.new("StageCamera", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def look(position_b, target_b, fov):
    cam.location = position_b
    cam.rotation_euler = (target_b - position_b).to_track_quat("-Z", "Y").to_euler()
    cam_data.angle_y = math.radians(fov)


# ---------------------------------------------------------------- stage

if SCREEN == "stage":
    parts = import_fbx("Characters/Operator/Operator.fbx", OPERATOR)
    rig = next(o for o in parts if o.type == "ARMATURE")
    # LobbyStage.Character yaw 165 (Unity) = -165 about Blender Z. On a parent: the Idle action keys the rig object.
    holder = bpy.data.objects.new("LobbyCharacter", None)
    scene.collection.objects.link(holder)
    # The character turns to face the camera (LobbyController): Unity yaw = atan2(cam.x, cam.z).
    yaw_u = math.degrees(math.atan2(SHOT[0], SHOT[2])) if SHOT else 165.0
    holder.rotation_euler = (0, 0, math.radians(-yaw_u))
    rig.parent = holder
    idle = next(a for a in bpy.data.actions if a.name.endswith("|Idle"))
    rig.animation_data_create()
    rig.animation_data.action = idle
    if hasattr(rig.animation_data, "action_slot") and len(getattr(idle, "slots", [])) > 0:
        rig.animation_data.action_slot = idle.slots[0]
    scene.frame_set(int(idle.frame_range[0] + (idle.frame_range[1] - idle.frame_range[0]) * 0.3))
    # LobbyStage.BuildFloor: dark floor, round stand with a gold rim (Unity cylinder = radius 0.5, height 2).
    floor_c = tuple(0.035 + (0.16 - 0.035) * 0.55 for _ in range(3))
    for name, prim, pos, scale, color, smooth in (
            ("Floor", "PLANE", (0, -0.025, 0), (10, 10, 1), floor_c, 0.1),
            ("Rim", "CYL", (0, -0.012, 0), (0.735, 0.735, 0.012), (0.46, 0.4, 0.26), 0.4),
            ("Stand", "CYL", (0, -0.01, 0), (0.72, 0.72, 0.012), (0.13, 0.13, 0.125), 0.3)):
        if prim == "PLANE":
            bpy.ops.mesh.primitive_plane_add(size=2)
        else:
            bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=1, depth=2)
        ob = bpy.context.active_object
        ob.name = name
        ob.location = u2b(*pos)
        ob.scale = scale
        mat = lit_material(name, color=color, smoothness=smooth)
        if name == "Floor":
            add_fog(mat)
        ob.data.materials.append(mat)
    bpy.data.objects["KeyLight"].data.use_shadow = True
    if SHOT:
        look(u2b(SHOT[0], SHOT[1], SHOT[2]), u2b(SHOT[3], SHOT[4], SHOT[5]), SHOT[6])
    else:
        look(u2b(0, 1.15, -4.7), u2b(0, 0.9, 0), 30.0)
elif SCREEN == "icon":
    # UnityItemIcons: orthographic side view from the weapon's right (muzzle to the right), transparent background.
    ICONS = {
        "m1911a1": ("Weapons/M1911_Tripo/M1911.fbx", WEAPON, 2, 1),
        "suppressor_45": ("Weapons/M1911_Tripo/Attachments/Suppressor_45.fbx", SUPPRESSOR, 2, 1),
        "barrel_threaded": ("Weapons/M1911_Tripo/Attachments/Barrel_Threaded.fbx", BARREL, 2, 1),
    }
    path, mat, cw, ch = ICONS[ICON_ID]
    parts = import_fbx(path, mat)
    if ICON_ID == "m1911a1" and ICON_SUPPRESSED:
        bpy.context.view_layer.update()
        muzzle = next(o for o in parts if o.name.startswith("Socket_Muzzle"))
        for extra_path, extra_mat in (("Weapons/M1911_Tripo/Attachments/Barrel_Threaded.fbx", BARREL),
                                      ("Weapons/M1911_Tripo/Attachments/Suppressor_45.fbx", SUPPRESSOR)):
            extra = import_fbx(extra_path, extra_mat)
            root_of(extra).location = muzzle.matrix_world.translation
    bpy.context.view_layer.update()
    lo = Vector((1e9, 1e9, 1e9))
    hi = -lo
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        for corner in o.bound_box:
            w = o.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    centre = (lo + hi) * 0.5
    WIDTH, HEIGHT = cw * 128, ch * 128
    aspect = WIDTH / HEIGHT
    cam_data.type = "ORTHO"
    cam_data.sensor_fit = "AUTO"
    cam_data.ortho_scale = max(hi.y - lo.y, (hi.z - lo.z) * aspect) * 1.12
    cam.location = centre + Vector((-(hi.x - lo.x) - 2.0, 0, 0))
    cam.rotation_euler = Vector((1, 0, 0)).to_track_quat("-Z", "Y").to_euler()
    for L in ("KeyLight", "RimLight", "WeaponKey", "WeaponFill"):
        bpy.data.objects[L].hide_render = True
    sun.data.energy = 1.6 * math.pi
    sun.rotation_euler = u2b(-1.0, -0.6, 0.4).to_track_quat("-Z", "Y").to_euler()
    scene.render.film_transparent = True
else:
    parts = import_fbx("Weapons/M1911_Tripo/M1911.fbx", WEAPON)
    root = root_of(parts)
    # LobbyStage: the display stand raises the muzzle 4 degrees; the model's bore runs along -Y (Unity +Z).
    root.rotation_euler = (math.radians(-4.0), 0, 0)
    root.location = u2b(*WO)
    bpy.context.view_layer.update()
    muzzle = next(o for o in parts if o.name.startswith("Socket_Muzzle"))
    if SCREEN == "armorer_suppressor":
        for path, mat in (("Weapons/M1911_Tripo/Attachments/Barrel_Threaded.fbx", BARREL),
                          ("Weapons/M1911_Tripo/Attachments/Suppressor_45.fbx", SUPPRESSOR)):
            extra = import_fbx(path, mat)
            r = root_of(extra)
            r.rotation_euler = root.rotation_euler
            r.location = muzzle.matrix_world.translation
    bpy.context.view_layer.update()
    # LobbyStage.Mount re-measures the CURRENT build (attachments included).
    lo = Vector((1e9, 1e9, 1e9))
    hi = -lo
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        for corner in o.bound_box:
            w = o.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    pivot = (lo + hi) * 0.5
    size = max(hi - lo)
    # LobbyController.FitDistance: longest side fills 36 % of the screen width (fov 28, zoom 1), yaw -90, pitch 8.
    half_width = math.tan(math.radians(14.0)) * (WIDTH / HEIGHT)
    d = size / (2 * half_width * 0.36)
    offset_u = (d * math.cos(math.radians(8)), d * math.sin(math.radians(8)), 0.0)
    cam_pos = pivot + u2b(*offset_u)
    look(cam_pos, pivot, 28.0)
    # LobbyStage.BuildBackdrop / PlaceBackdrop: 5 m grid quad 1.3 m behind the pivot, facing the camera.
    away = (pivot - cam_pos).normalized()
    bpy.ops.mesh.primitive_plane_add(size=5.0)
    backdrop = bpy.context.active_object
    backdrop.location = pivot + away * 1.3
    backdrop.rotation_euler = (-away).to_track_quat("Z", "Y").to_euler()
    backdrop.data.materials.append(grid_material())
    bpy.context.view_layer.update()
    from bpy_extras.object_utils import world_to_camera_view

    def centre(prefix):
        o = next(o for o in parts if o.name.startswith(prefix))
        pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
        return sum(pts, Vector()) / len(pts)

    def magazine_base():
        o = next(o for o in parts if o.name.startswith("M1911_Magazine"))
        pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
        c = sum(pts, Vector()) / len(pts)
        return Vector((c.x, c.y, min(p.z for p in pts) + 0.008))

    anchors = {
        "Muzzle": muzzle.matrix_world.translation if SCREEN == "armorer" else None,
        "Barrel": next(o for o in parts if o.name.startswith("M1911_Barrel")).matrix_world.translation,
        "Grips": centre("M1911_Grip_R"),
        "Magazine": magazine_base(),
    }
    if anchors["Muzzle"] is None:
        # Effective muzzle: front of the suppressor along the bore.
        sup = [o for o in bpy.data.objects if o.name.startswith("Suppressor_45")][0]
        front = min((sup.matrix_world @ Vector(c) for c in sup.bound_box), key=lambda v: v.y)
        anchors["Muzzle"] = Vector((muzzle.matrix_world.translation.x, front.y, muzzle.matrix_world.translation.z))
    with open(os.path.splitext(OUT)[0] + "_anchors.txt", "w") as f:
        for k, v in anchors.items():
            p = world_to_camera_view(scene, cam, v)
            f.write(f"{k} {p.x * WIDTH:.1f} {(1 - p.y) * HEIGHT:.1f}\n")

# ---------------------------------------------------------------- render

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = int(os.environ.get("LOBBY_SAMPLES", "48"))
scene.cycles.use_denoising = True
scene.cycles.max_bounces = 4
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.render.resolution_x = WIDTH
scene.render.resolution_y = HEIGHT
scene.render.resolution_percentage = 100
scene.render.film_transparent = SCREEN == "icon"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("rendered", OUT)
