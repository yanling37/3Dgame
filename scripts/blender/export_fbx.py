"""
Export selected/all mesh objects from a .blend file to FBX for Unity.

Usage:
  blender --background ArtSource/Props/SM_Crate.blend --python scripts/blender/export_fbx.py -- \
      --out ArtSource/Exports/SM_Crate.fbx

Then copy/move the FBX into Assets/Art/...
"""

import argparse
import sys
from pathlib import Path

import bpy


def parse_args(argv):
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []

    parser = argparse.ArgumentParser(description="Export FBX for Unity")
    parser.add_argument("--out", required=True, help="Output .fbx path")
    parser.add_argument(
        "--selected-only",
        action="store_true",
        help="Export only selected objects",
    )
    return parser.parse_args(argv)


def main():
    args = parse_args(sys.argv)
    out = Path(args.out).resolve()
    out.parent.mkdir(parents=True, exist_ok=True)

    # Ensure metric units for Unity (1 Blender unit = 1 meter)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0

    bpy.ops.export_scene.fbx(
        filepath=str(out),
        use_selection=args.selected_only,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        bake_space_transform=True,
        object_types={"MESH", "ARMATURE", "EMPTY"},
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        path_mode="AUTO",
    )
    print(f"[export_fbx] Wrote {out}")


if __name__ == "__main__":
    main()
