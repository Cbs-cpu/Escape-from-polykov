"""
Tripo M1911 -> game-ready modular low-poly (no regeneration: works from the imported Tripo mesh).
Steps: copy source, orient (muzzle -Y), scale to 216 mm, decimate, recalc normals outward, split into the
parts the weapon system drives (with real pivots), magazine body, inverted-hull outline, sockets.
Run in M1911_Tripo_Source.blend: exec(open(r"<path>", encoding="utf-8").read())
"""
import bpy
import bmesh
import math
from mathutils import Vector

SOURCE = "handgun 3d model"
TARGET_TRIS = 7000
OUTLINE = 0.0008                      # hull thickness (m)
GRIP = Vector((0.0, 0.065, 0.055))    # root origin = firing-hand grip point

PARTS = ["M1911_Frame", "M1911_Slide", "M1911_Hammer", "M1911_GripSafety", "M1911_Trigger", "M1911_SlideStop",
         "M1911_MagRelease", "M1911_Safety", "M1911_Magazine", "M1911_Grip_L", "M1911_Grip_R"]
PIVOTS = {"M1911_Hammer": (0, 0.08375, 0.10375), "M1911_Trigger": (0, 0.018, 0.091),
          "M1911_SlideStop": (0.012, 0.00425, 0.10175), "M1911_GripSafety": (0, 0.095, 0.098),
          "M1911_Magazine": (0, 0.066, 0.095), "M1911_MagRelease": (0.012, 0.034, 0.072),
          "M1911_Safety": (0.012, 0.070, 0.1015)}
SOCKETS = {"Socket_Muzzle": (0, -0.109, 0.12), "Socket_RearSight": (0, 0.0635, 0.139),
           "Socket_FrontSight": (0, -0.095, 0.1365), "Socket_EjectionPort": (-0.017, 0.0, 0.123),
           "Socket_RightHand": tuple(GRIP), "Socket_LeftHand": (0.03, 0.05, 0.035), "Socket_MagWell": (0, 0.075, 0.0)}


def label(c, n):
    X, Y, Z = c
    # Hammer: narrow and behind the slide; the slide keeps its full-width rear face (it used to leave a hole).
    if Y > 0.084 and abs(X) < 0.0055 and 0.0985 < Z < 0.135:
        return "M1911_Hammer"
    if Y > 0.0913 and abs(X) < 0.0115 and 0.0850 < Z <= 0.0985:
        return "M1911_GripSafety"
    if (Y < -0.0625 and Z > 0.0955) or (Y <= 0.090 and Z > 0.1055):
        return "M1911_Slide"
    if 0.0105 < Y < 0.0255 and 0.066 < Z < 0.0915 and abs(X) < 0.009:
        return "M1911_Trigger"
    if X > 0.0112 and 0.064 < Y < 0.082 and 0.0965 < Z < 0.1062:
        return "M1911_Safety"
    if X > 0.0115 and 0.0 < Y < 0.043 and 0.0975 < Z <= 0.1055:
        return "M1911_SlideStop"
    if X > 0.0115 and 0.027 < Y < 0.041 and 0.066 < Z < 0.078:
        return "M1911_MagRelease"
    if 0.048 < Y < 0.100 and Z < 0.008:
        return "M1911_Magazine"
    if 0.033 < Y < 0.099 and 0.008 < Z < 0.097 and abs(X) > 0.0135 and abs(n.x) > 0.45:
        return "M1911_Grip_L" if X > 0 else "M1911_Grip_R"
    return "M1911_Frame"


def clean_previous():
    for name in PARTS + list(SOCKETS) + ["M1911_Barrel", "M1911", "M1911_Tripo_LP"]:
        ob = bpy.data.objects.get(name)
        if ob:
            bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)


def make_lowpoly(src):
    lp = src.copy()
    lp.data = src.data.copy()
    lp.name = lp.data.name = "M1911_Tripo_LP"
    src.users_collection[0].objects.link(lp)
    s = 0.216 / src.dimensions.x
    lp.rotation_euler = (0, 0, math.radians(-90))
    lp.scale = (s, s, s)
    lp.location = (0, 0, 0)
    # Bake orientation + scale into the mesh. Built explicitly: matrix_world is stale for hidden copies.
    from mathutils import Matrix, Euler
    lp.data.transform(Matrix.LocRotScale(Vector((0, 0, 0)), Euler((0, 0, math.radians(-90))), Vector((s, s, s))))
    lp.rotation_euler = (0, 0, 0)
    lp.scale = (1, 1, 1)
    bpy.context.view_layer.objects.active = lp
    lp.hide_viewport = False
    lp.select_set(True)
    mod = lp.modifiers.new("Decimate", "DECIMATE")
    mod.ratio = TARGET_TRIS / len(lp.data.polygons)
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="Decimate")
    bm = bmesh.new()
    bm.from_mesh(lp.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00002)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)   # the fix for "invisible" back-facing areas
    bm.to_mesh(lp.data)
    bm.free()
    for p in lp.data.polygons:
        p.use_smooth = False
    lp.hide_viewport = lp.hide_render = True
    return lp


def split(lp, root, coll):
    lp.data.materials[0].name = "M_M1911_Tripo"
    out = {}
    for name in PARTS:
        bm = bmesh.new()
        bm.from_mesh(lp.data)
        bm.normal_update()
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if label(f.calc_center_median(), f.normal) != name], context="FACES")
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        me.materials.append(lp.data.materials[0])
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        ob.parent = root
        pivot = Vector(PIVOTS.get(name, tuple(GRIP)))
        for v in me.vertices:
            v.co -= pivot
        ob.location = pivot - GRIP
        out[name] = ob
    return out


def add_magazine_body(mag, dark_uv):
    bm = bmesh.new()
    bm.from_mesh(mag.data)
    uv = bm.loops.layers.uv.active
    pivot = Vector(PIVOTS["M1911_Magazine"])
    bot, top = Vector((0, 0.0755, 0.008)) - pivot, Vector((0, 0.059, 0.094)) - pivot
    axis = (top - bot).normalized()
    fwd = Vector((0, -1, 0))
    fwd = (fwd - axis * fwd.dot(axis)).normalized()
    side = Vector((1, 0, 0))
    vs = [bm.verts.new(a + side * 0.0088 * sx + fwd * 0.0145 * sf) for a in (bot, top) for sx in (-1, 1) for sf in (-1, 1)]
    for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        face = bm.faces.new([vs[i] for i in f])
        for loop in face.loops:
            loop[uv].uv = dark_uv
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mag.data)
    bm.free()


def add_outline(ob):
    om = bpy.data.materials.get("M_Outline") or bpy.data.materials.new("M_Outline")
    om.diffuse_color = (0, 0, 0, 1)
    me = ob.data
    me.materials.append(om)
    idx = len(me.materials) - 1
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    ret = bmesh.ops.duplicate(bm, geom=list(bm.faces))
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
    clean_previous()
    src = bpy.data.objects[SOURCE]
    src.hide_viewport = src.hide_render = True
    coll = src.users_collection[0]
    root = bpy.data.objects.new("M1911", None)
    coll.objects.link(root)
    root.location = GRIP
    lp = make_lowpoly(src)
    parts = split(lp, root, coll)
    grip = parts["M1911_Grip_L"].data
    dark_uv = grip.uv_layers.active.data[grip.polygons[len(grip.polygons) // 4].loop_indices[0]].uv.copy()
    add_magazine_body(parts["M1911_Magazine"], dark_uv)
    for ob in parts.values():
        for p in ob.data.polygons:
            p.use_smooth = False
        add_outline(ob)
    for name, p in list(SOCKETS.items()) + [("M1911_Barrel", (0, -0.02, 0.12))]:
        e = bpy.data.objects.new(name, None)
        e.empty_display_size = 0.01
        coll.objects.link(e)
        e.parent = root
        e.location = Vector(p) - GRIP
    return {n: len(o.data.polygons) for n, o in parts.items()}


result = build()
