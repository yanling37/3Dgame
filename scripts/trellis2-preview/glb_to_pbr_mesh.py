#!/usr/bin/env python3
"""Load a TRELLIS.2 to_glb() GLB into MeshWithPbrMaterial for render_snapshot.

GLB cannot restore MeshWithVoxel (sparse attr volume). PbrMeshRenderer also
accepts MeshWithPbrMaterial, so baked textures are enough for official-mode
snapshots of already-exported meshes without re-running 4B.
"""
from __future__ import annotations

from typing import Optional

import numpy as np
import torch
import trimesh
from PIL import Image

from trellis2.representations import MeshWithPbrMaterial
from trellis2.representations.mesh.base import (
    AlphaMode,
    PbrMaterial,
    Texture,
    TextureFilterMode,
    TextureWrapMode,
)


def _pil_float(img: Image.Image, mode: str) -> np.ndarray:
    arr = np.array(img.convert(mode), dtype=np.float32) / 255.0
    if arr.ndim == 2:
        arr = arr[..., None]
    return np.ascontiguousarray(arr)


def _texture_from_array(arr: np.ndarray) -> Texture:
    t = torch.tensor(arr, dtype=torch.float32)
    return Texture(t, filter_mode=TextureFilterMode.LINEAR, wrap_mode=TextureWrapMode.REPEAT)


def _alpha_mode(name: Optional[str]) -> int:
    if not name:
        return AlphaMode.OPAQUE
    name = str(name).upper()
    if name == "MASK":
        return AlphaMode.MASK
    if name == "BLEND":
        return AlphaMode.BLEND
    return AlphaMode.OPAQUE


def restore_trellis_axes(vertices: np.ndarray) -> np.ndarray:
    """Invert to_glb() Y/Z swap so official cameras (Z-up) match the mesh."""
    out = np.array(vertices, dtype=np.float32, copy=True)
    y = out[:, 1].copy()
    z = out[:, 2].copy()
    out[:, 1] = -z
    out[:, 2] = y
    return out


def glb_to_pbr_mesh(path: str, restore_axes: bool = True) -> MeshWithPbrMaterial:
    scene = trimesh.load(path, force="scene")
    if not scene.geometry:
        raise RuntimeError(f"no geometry in {path}")
    geom = next(iter(scene.geometry.values()))
    if not isinstance(geom, trimesh.Trimesh):
        raise RuntimeError(f"unsupported geometry {type(geom)} in {path}")

    vertices = np.array(geom.vertices, dtype=np.float32)
    if restore_axes:
        vertices = restore_trellis_axes(vertices)
    faces = np.array(geom.faces, dtype=np.int32)
    vis = geom.visual
    if vis is None or getattr(vis, "uv", None) is None:
        raise RuntimeError(f"GLB has no UVs: {path}")
    uv = np.array(vis.uv, dtype=np.float32)
    # to_glb() writes glTF UVs (V flipped). nvdiffrast samples numpy textures
    # with V=0 at the top, so restore the bake UVs.
    uv[:, 1] = 1.0 - uv[:, 1]
    if uv.shape[0] != vertices.shape[0]:
        raise RuntimeError(f"UV count {uv.shape[0]} != vertices {vertices.shape[0]}")
    uv_coords = uv[faces]  # [M, 3, 2]
    material_ids = np.zeros((faces.shape[0],), dtype=np.int32)

    mat = getattr(vis, "material", None)
    base_tex = None
    metal_tex = None
    rough_tex = None
    alpha_tex = None
    base_factor = [1.0, 1.0, 1.0]
    metal_factor = 1.0
    rough_factor = 1.0
    alpha_factor = 1.0
    a_mode = AlphaMode.OPAQUE
    a_cut = 0.5

    if mat is not None:
        bcf = getattr(mat, "baseColorFactor", None)
        if bcf is not None:
            bcf = np.array(bcf, dtype=np.float32).reshape(-1)
            if bcf.size and float(np.max(np.abs(bcf))) > 1.0:
                bcf = bcf / 255.0
            base_factor = bcf[:3].tolist()
            if bcf.size >= 4:
                alpha_factor = float(bcf[3])
        metal_factor = float(getattr(mat, "metallicFactor", 1.0) or 1.0)
        rough_factor = float(getattr(mat, "roughnessFactor", 1.0) or 1.0)
        a_mode = _alpha_mode(getattr(mat, "alphaMode", None))
        a_cut = float(getattr(mat, "alphaCutoff", 0.5) or 0.5)

        bc = getattr(mat, "baseColorTexture", None)
        if bc is not None:
            rgba = _pil_float(bc, "RGBA")
            base_tex = _texture_from_array(rgba[..., :3])
            if rgba.shape[-1] == 4 and float(rgba[..., 3].min()) < 0.999:
                alpha_tex = _texture_from_array(rgba[..., 3:4])
                if a_mode == AlphaMode.OPAQUE:
                    a_mode = AlphaMode.BLEND

        mr = getattr(mat, "metallicRoughnessTexture", None)
        if mr is not None:
            packed = _pil_float(mr, "RGB")
            rough_tex = _texture_from_array(packed[..., 1:2])
            metal_tex = _texture_from_array(packed[..., 2:3])

    pbr = PbrMaterial(
        base_color_texture=base_tex,
        base_color_factor=base_factor,
        metallic_texture=metal_tex,
        metallic_factor=metal_factor,
        roughness_texture=rough_tex,
        roughness_factor=rough_factor,
        alpha_texture=alpha_tex,
        alpha_factor=alpha_factor,
        alpha_mode=a_mode,
        alpha_cutoff=a_cut,
    )
    mesh = MeshWithPbrMaterial(
        vertices=torch.tensor(vertices, dtype=torch.float32),
        faces=torch.tensor(faces, dtype=torch.int32),
        material_ids=torch.tensor(material_ids, dtype=torch.int32),
        uv_coords=torch.tensor(uv_coords, dtype=torch.float32),
        materials=[pbr],
    )
    return mesh.cuda()
