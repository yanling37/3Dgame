#!/usr/bin/env python3
"""Patch ComfyUI-Trellis2 so RGB sheets and frozen o_voxel 0.0.1 can run.

1. PreProcess: official preprocess_image always reads output_np[:, :, 3].
   JPEG/PNG character sheets are RGB; image_with_alpha is still 3-channel
   when the file has no alpha — IndexError. Synthesize opaque A=255.
   Do not chroma-key white (dresses may be white).

2. fdg_vae: ComfyUI-Trellis2 vendors a newer trellis2 that imports
   tiled_flexible_dual_grid_to_mesh. The frozen env's o_voxel 0.0.1 (torch
   2.6.0+cu124) only has flexible_dual_grid_to_mesh, matching microsoft
   TRELLIS.2. Fall back instead of upgrading o_voxel.

Do not touch /home/ubuntu/trellis2/app official TRELLIS.2 math.
"""
from __future__ import annotations

import os
import sys

MARKER = "PATCH: opaque-alpha-if-rgb"
FDG_MARKER = "PATCH: o_voxel-no-tiled"
NODES = os.environ.get(
    "TRELLIS2_NODES_PY",
    "/home/ubuntu/trellis2/cache/comfyui/custom_nodes/ComfyUI-Trellis2/nodes.py",
)
FDG = os.environ.get(
    "TRELLIS2_FDG_PY",
    "/home/ubuntu/trellis2/cache/comfyui/custom_nodes/ComfyUI-Trellis2/trellis2/models/sc_vaes/fdg_vae.py",
)

OLD = """        output = input
        output_np = np.array(output)
        alpha = output_np[:, :, 3]
"""

NEW = """        # PATCH: opaque-alpha-if-rgb
        # RGB sheets have no alpha. Synthesize opaque A=255. Never chroma-key white.
        if getattr(input, "mode", None) != "RGBA":
            input = input.convert("RGBA")
        output = input
        output_np = np.array(output)
        if output_np.ndim < 3 or output_np.shape[-1] < 4:
            rgb = output_np if output_np.ndim == 3 else np.stack([output_np] * 3, axis=-1)
            rgb = rgb[:, :, :3]
            alpha_ch = np.full(rgb.shape[:2] + (1,), 255, dtype=rgb.dtype)
            output_np = np.concatenate([rgb, alpha_ch], axis=-1)
            output = Image.fromarray(output_np)
        alpha = output_np[:, :, 3]
        bbox = np.argwhere(alpha > 0.8 * 255)
        if bbox.size == 0:
            rgb = output_np[:, :, :3]
            return Image.fromarray(rgb)
"""

FDG_OLD_IMPORT = (
    "from o_voxel.convert import flexible_dual_grid_to_mesh, tiled_flexible_dual_grid_to_mesh\n"
)
FDG_NEW_IMPORT = """from o_voxel.convert import flexible_dual_grid_to_mesh
try:
    from o_voxel.convert import tiled_flexible_dual_grid_to_mesh
except ImportError:  # PATCH: o_voxel-no-tiled (o_voxel 0.0.1 / torch 2.6 has no tiled convert)
    tiled_flexible_dual_grid_to_mesh = None
"""
FDG_OLD_IF = "            if useTiled:\n"
FDG_NEW_IF = "            if useTiled and tiled_flexible_dual_grid_to_mesh is not None:\n"


def _patch_preprocess(path: str) -> int:
    if not os.path.isfile(path):
        print("skip: missing", path)
        return 0
    text = open(path, encoding="utf-8").read()
    if MARKER in text:
        print("already_patched", path)
        return 0
    if OLD not in text:
        print("warn: preprocess_image blob not found; not patching", path, file=sys.stderr)
        return 1
    old_with_bbox = OLD + "        bbox = np.argwhere(alpha > 0.8 * 255)\n"
    if old_with_bbox in text:
        text = text.replace(old_with_bbox, NEW, 1)
    else:
        text = text.replace(OLD, NEW, 1)
    open(path, "w", encoding="utf-8").write(text)
    print("patched", path)
    return 0


def _patch_fdg(path: str) -> int:
    if not os.path.isfile(path):
        print("skip: missing", path)
        return 0
    text = open(path, encoding="utf-8").read()
    if FDG_MARKER in text:
        print("already_patched", path)
        return 0
    if FDG_OLD_IMPORT not in text:
        print("warn: fdg_vae tiled import not found; not patching", path, file=sys.stderr)
        return 1
    text = text.replace(FDG_OLD_IMPORT, FDG_NEW_IMPORT, 1)
    if FDG_OLD_IF in text:
        text = text.replace(FDG_OLD_IF, FDG_NEW_IF, 1)
    open(path, "w", encoding="utf-8").write(text)
    print("patched", path)
    return 0


def main() -> int:
    rc = _patch_preprocess(NODES)
    rc2 = _patch_fdg(FDG)
    return rc or rc2


if __name__ == "__main__":
    raise SystemExit(main())
