"""
Export the Operator to Unity: collapses all flat-color materials into one palette texture
(single material = single draw call), then writes FBX with mesh, armature and every action.
Works on temporary duplicates so the .blend keeps its editable per-part materials.
"""
import bpy
import os

PROJECT = os.environ.get("POLYKOV_ROOT", r"C:\Users\Cobos\Escape from Polykov")
OUT_DIR = os.path.join(PROJECT, "Assets", "_Project", "Art", "Characters", "Operator")
FBX_PATH = os.path.join(OUT_DIR, "Operator.fbx")
PALETTE_PATH = os.path.join(OUT_DIR, "T_Operator_Palette.png")
MESHES = ["Operator_Body", "Operator_Head"]
CELL = 4
COLUMNS = 8

os.makedirs(OUT_DIR, exist_ok=True)
rig = bpy.data.objects["Operator_Rig"]
coll = rig.users_collection[0]

# ---- palette image from every material used by the meshes
materials = []
for name in MESHES:
    for m in bpy.data.objects[name].data.materials:
        if m and m not in materials:
            materials.append(m)
rows = (len(materials) + COLUMNS - 1) // COLUMNS
size = (COLUMNS * CELL, max(rows, 1) * CELL)
img = bpy.data.images.get("T_Operator_Palette")
if img:
    bpy.data.images.remove(img)
img = bpy.data.images.new("T_Operator_Palette", width=size[0], height=size[1], alpha=False)
pixels = [0.0] * (size[0] * size[1] * 4)
cell_uv = {}
for i, m in enumerate(materials):
    cx, cy = i % COLUMNS, i // COLUMNS
    # diffuse_color is linear; a byte PNG stores sRGB-encoded values, so encode before writing.
    col = [c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055 for c in m.diffuse_color[:3]]
    for y in range(cy * CELL, cy * CELL + CELL):
        for x in range(cx * CELL, cx * CELL + CELL):
            k = (y * size[0] + x) * 4
            pixels[k:k + 4] = [col[0], col[1], col[2], 1.0]
    cell_uv[m.name] = ((cx + 0.5) * CELL / size[0], (cy + 0.5) * CELL / size[1])
img.pixels = pixels
img.filepath_raw = PALETTE_PATH
img.file_format = "PNG"
img.save()

pal_mat = bpy.data.materials.get("M_Operator") or bpy.data.materials.new("M_Operator")
pal_mat.use_nodes = True
nodes = pal_mat.node_tree.nodes
tex = nodes.get("Palette") or nodes.new("ShaderNodeTexImage")
tex.name = "Palette"
tex.image = img
tex.interpolation = "Closest"
bsdf = nodes.get("Principled BSDF")
pal_mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.85

# ---- temporary export duplicates
dups, renamed = [], []
for name in MESHES:
    src = bpy.data.objects[name]
    src.name = name + "__src"
    renamed.append(src)
    dup = src.copy()
    dup.data = src.data.copy()
    dup.name = name
    dup.data.name = name
    coll.objects.link(dup)
    me = dup.data
    uv = me.uv_layers.new(name="Palette")
    for poly in me.polygons:
        m = me.materials[poly.material_index]
        u, v = cell_uv[m.name]
        for li in poly.loop_indices:
            uv.data[li].uv = (u, v)
    me.uv_layers.active = uv
    for layer in [l for l in me.uv_layers if l.name != "Palette"]:
        me.uv_layers.remove(layer)
    me.materials.clear()
    me.materials.append(pal_mat)
    dups.append(dup)

for o in bpy.context.view_layer.objects:
    o.select_set(False)
rig.select_set(True)
for d in dups:
    d.select_set(True)
bpy.context.view_layer.objects.active = rig
rig.animation_data.action = None
for pb in rig.pose.bones:
    pb.rotation_quaternion = (1, 0, 0, 0)
    pb.location = (0, 0, 0)

bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=False,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    armature_nodetype="NULL",
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,
    path_mode="STRIP",
)

for d in dups:
    me = d.data
    bpy.data.objects.remove(d, do_unlink=True)
    bpy.data.meshes.remove(me)
for src in renamed:
    src.name = src.name.replace("__src", "")
rig.animation_data.action = bpy.data.actions.get("Idle")

result = {"fbx": FBX_PATH, "palette": PALETTE_PATH, "materials": len(materials),
          "actions": [a.name for a in bpy.data.actions]}
