"""Create a simple Unity-ready crate prop as a starter Blender asset."""

import bpy
from pathlib import Path

# Reset scene
bpy.ops.wm.read_factory_settings(use_empty=True)

bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.5))
obj = bpy.context.active_object
obj.name = "SM_Crate"

# Metric units
bpy.context.scene.unit_settings.system = "METRIC"
bpy.context.scene.unit_settings.scale_length = 1.0

blend_path = Path("/workspace/ArtSource/Props/SM_Crate.blend")
fbx_path = Path("/workspace/ArtSource/Exports/SM_Crate.fbx")
assets_fbx = Path("/workspace/Assets/Art/Props/SM_Crate.fbx")

blend_path.parent.mkdir(parents=True, exist_ok=True)
fbx_path.parent.mkdir(parents=True, exist_ok=True)
assets_fbx.parent.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

bpy.ops.export_scene.fbx(
    filepath=str(fbx_path),
    use_selection=False,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    bake_space_transform=True,
    object_types={"MESH"},
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    path_mode="AUTO",
)

# Also place into Unity Art folder for immediate import
bpy.ops.export_scene.fbx(
    filepath=str(assets_fbx),
    use_selection=False,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    bake_space_transform=True,
    object_types={"MESH"},
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    path_mode="AUTO",
)

print(f"Created {blend_path}")
print(f"Exported {fbx_path}")
print(f"Copied export to {assets_fbx}")
