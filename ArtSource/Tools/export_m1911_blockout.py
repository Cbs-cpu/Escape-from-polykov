"""
Export the M1911 collection (built by build_m1911.py) to Unity as FBX, keeping the empties
(Muzzle, SightLine, GripFrame, GripCenter, MagazineGrab) so WeaponModel finds its points by name.
Materials keep their names (M_Steel, M_Wood, ...); Unity's WeaponModel remaps them to project materials.
"""
import bpy
import os

ROOT = os.environ.get("POLYKOV_ROOT", r"C:\Users\Cobos\Escape from Polykov")
OUT_DIR = os.path.join(ROOT, "Assets", "_Project", "Art", "Weapons", "M1911")
FBX_PATH = os.path.join(OUT_DIR, "M1911.fbx")
os.makedirs(OUT_DIR, exist_ok=True)

coll = bpy.data.collections["M1911"]
if bpy.context.object and bpy.context.object.mode != "OBJECT":
    bpy.ops.object.mode_set(mode="OBJECT")
bpy.ops.object.select_all(action="DESELECT")
for ob in coll.objects:
    ob.select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects["M1911"]

bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="STRIP",
)
print("[M1911 export]", FBX_PATH)
