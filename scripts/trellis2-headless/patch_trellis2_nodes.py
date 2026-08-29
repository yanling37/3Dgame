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

3. ReconstructMeshWithQuad: skip missing reconstruct_mesh_dc_quad.
4. FillHolesNicelyWithMeshlib: skip when hole count is huge (OOM).

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


QUAD_OLD = """        # Perform Dual Contouring remeshing (rebuilds topology)
        print('Reconstructing mesh ...')
        vertices, faces = CuMesh.remeshing.reconstruct_mesh_dc_quad(vertices, faces, resolution, verbose=True, remove_inner_faces = remove_inner_faces)
"""
QUAD_NEW = """        # PATCH: cumesh-no-dc-quad (frozen cumesh has remesh_narrow_band_dc only)
        print('Reconstructing mesh ...')
        if hasattr(CuMesh.remeshing, "reconstruct_mesh_dc_quad"):
            vertices, faces = CuMesh.remeshing.reconstruct_mesh_dc_quad(
                vertices, faces, resolution, verbose=True, remove_inner_faces=remove_inner_faces
            )
        else:
            print("skip reconstruct_mesh_dc_quad; using decoded mesh (do not upgrade cumesh)")
"""
QUAD_MARKER = "PATCH: cumesh-no-dc-quad"


def _patch_reconstruct(path: str) -> int:
    if not os.path.isfile(path):
        print("skip: missing", path)
        return 0
    text = open(path, encoding="utf-8").read()
    if QUAD_MARKER in text:
        print("already_patched reconstruct", path)
        return 0
    if QUAD_OLD not in text:
        print("warn: ReconstructMeshWithQuad blob not found; not patching", path, file=sys.stderr)
        return 1
    text = text.replace(QUAD_OLD, QUAD_NEW, 1)
    open(path, "w", encoding="utf-8").write(text)
    print("patched reconstruct", path)
    return 0


HOLE_OLD = """        nb_holes = len(hole_edges)
        print(f"{nb_holes} holes found")

        if nb_holes > 0:
"""
HOLE_NEW = """        nb_holes = len(hole_edges)
        print(f"{nb_holes} holes found")
        # PATCH: skip-meshlib-many-holes (17k holes OOM-killed ComfyUI on 32G)
        if nb_holes > 64:
            print(f"skip FillHolesNicelyWithMeshlib ({nb_holes} holes > 64); passing mesh through")
            return (mesh_copy, 0)

        if nb_holes > 0:
"""
HOLE_MARKER = "PATCH: skip-meshlib-many-holes"


def _patch_holefill(path: str) -> int:
    if not os.path.isfile(path):
        print("skip: missing", path)
        return 0
    text = open(path, encoding="utf-8").read()
    if HOLE_MARKER in text:
        print("already_patched holefill", path)
        return 0
    if HOLE_OLD not in text:
        print("warn: FillHolesNicely blob not found; not patching", path, file=sys.stderr)
        return 1
    text = text.replace(HOLE_OLD, HOLE_NEW, 1)
    open(path, "w", encoding="utf-8").write(text)
    print("patched holefill", path)
    return 0


def main() -> int:
    rc = _patch_preprocess(NODES)
    rc2 = _patch_fdg(FDG)
    rc3 = _patch_reconstruct(NODES)
    rc4 = _patch_holefill(NODES)
    return rc or rc2 or rc3 or rc4


if __name__ == "__main__":
    raise SystemExit(main())
