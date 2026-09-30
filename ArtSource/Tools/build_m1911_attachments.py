"""
M1911 attachments (low-poly, same style as the Tripo M1911: flat shading + black inverted-hull outline):
- Suppressor_45: cylindrical .45 ACP can, 35 mm x 190 mm, stepped mount/booster end, chamfered front cap.
  Real references: SilencerCo Octane 45 (~35 mm x 218 mm), Gemtech Blackside 45 (~35 mm x 200 mm).
- Barrel_Threaded: threaded barrel extension protruding ~14 mm from the bushing (.578x28 style rings).
Origins sit on the mounting point (muzzle face); objects point along -Y (Unity +Z after export).
"""
import bpy
import bmesh
import math
import os
from mathutils import Vector

OUT = r"C:\Users\Cobos\Escape from Polykov\Assets\_Project\Art\Weapons\M1911_Tripo\Attachments"
OUTLINE = 0.0008
SIDES = 16


def mat(name, rgb, metal, rough):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1.0)
    m.metallic, m.roughness = metal, rough
    return m


def lathe(name, profile, material):
    """profile: [(distance_forward, radius), ...] revolved around the bore (-Y forward)."""
    bm = bmesh.new()
    rings = []
    for d, r in profile:
        rings.append([bm.verts.new((math.cos(2 * math.pi * i / SIDES) * r, -d, math.sin(2 * math.pi * i / SIDES) * r))
                      for i in range(SIDES)])
    for a, b in zip(rings, rings[1:]):
        for i in range(SIDES):
            j = (i + 1) % SIDES
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    me.materials.append(material)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


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


def export(ob, filename):
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.data.objects:
        try:
            o.select_set(False)
        except RuntimeError:
            pass
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, filename), use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", bake_anim=False, path_mode="STRIP")


def build():
    for n in ("Suppressor_45", "Barrel_Threaded", "Socket_SuppressorMuzzle"):
        if bpy.data.objects.get(n):
            bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
    can = mat("M_Suppressor", (0.035, 0.037, 0.04), 0.4, 0.55)
    steel = mat("M_BarrelSteel", (0.08, 0.085, 0.09), 0.9, 0.35)
    # Suppressor: mount collar (threads onto the barrel), booster step, main tube, front cap, bore.
    sup = lathe("Suppressor_45", [
        (-0.002, 0.0072), (0.000, 0.0120), (0.018, 0.0125), (0.020, 0.0145), (0.030, 0.0150),
        (0.032, 0.0175), (0.036, 0.0176), (0.180, 0.0176), (0.184, 0.0172), (0.188, 0.0160),
        (0.190, 0.0060), (0.1895, 0.0055), (0.186, 0.0048),
    ], can)
    add_outline(sup)
    sock = bpy.data.objects.new("Socket_SuppressorMuzzle", None)
    bpy.context.scene.collection.objects.link(sock)
    sock.parent = sup
    sock.location = (0, -0.191, 0)
    # Threaded barrel extension: plain lead-in then thread rings.
    prof = [(-0.004, 0.0068), (0.000, 0.0068)]
    d = 0.0
    for i in range(7):
        prof += [(d + 0.0008, 0.0072), (d + 0.0012, 0.0072), (d + 0.0020, 0.0066)]
        d += 0.0020
    prof += [(d + 0.0005, 0.0060), (d + 0.001, 0.0040)]
    bar = lathe("Barrel_Threaded", prof, steel)
    add_outline(bar)
    export(sup, "Suppressor_45.fbx")
    export(bar, "Barrel_Threaded.fbx")
    return {"suppressor_tris": len(sup.data.polygons), "barrel_tris": len(bar.data.polygons), "out": OUT}


result = build()
