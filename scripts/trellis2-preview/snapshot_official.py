#!/usr/bin/env python3
"""Official TRELLIS.2 snapshots: Normal / Clay / Base color / HDRI forest/sunset/courtyard.

Does not run the 4B pipeline. Does not overwrite uniquely named GLBs.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from typing import Dict, Optional

os.environ.setdefault("OPENCV_IO_ENABLE_OPENEXR", "1")
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")

APP_DIR = os.environ.get("TRELLIS2_APP", "/home/ubuntu/trellis2/app")
if APP_DIR not in sys.path:
    sys.path.insert(0, APP_DIR)

from PIL import Image
import numpy as np

from hdri_utils import dummy_envmap, load_official_envmaps
from glb_to_pbr_mesh import glb_to_pbr_mesh

MODE_FILES = [
    ("normal", "normal", False),
    ("clay", "clay", False),
    ("base_color", "base_color", False),
    ("shaded_forest", "hdri_forest", True),
    ("shaded_sunset", "hdri_sunset", True),
    ("shaded_courtyard", "hdri_courtyard", True),
]
DEFAULT_STEP = 3
STEPS = 8


def _save_png(arr: np.ndarray, path: str) -> None:
    img = Image.fromarray(arr)
    if img.mode not in ("RGB", "L"):
        img = img.convert("RGB")
    img.save(path, format="PNG")


def save_snapshots_from_mesh(
    mesh,
    prefix: str,
    envmap=None,
    nviews: int = STEPS,
    resolution: int = 1024,
    default_step: int = DEFAULT_STEP,
    hdri_status: Optional[dict] = None,
) -> dict:
    from trellis2.utils import render_utils

    os.makedirs(os.path.dirname(prefix) or ".", exist_ok=True)
    used_hdri = False
    status = hdri_status or {}
    if envmap is None:
        envmap, status = load_official_envmaps(device="cuda")
        used_hdri = envmap is not None and status.get("hdri_ready")
        if envmap is None:
            envmap = dummy_envmap(device="cuda")
            status["hdri_ready"] = False
            status.setdefault("errors", []).append("HDRI EnvMap unavailable; using dummy for Normal/Clay/Base color")
        else:
            used_hdri = True
    else:
        used_hdri = bool(envmap) and "_dummy" not in envmap
        status = hdri_status or {"hdri_ready": used_hdri, "loaded_keys": list(envmap)}

    print("render_snapshot", "nviews", nviews, "res", resolution, "hdri", used_hdri, "keys", list(envmap), flush=True)
    try:
        images = render_utils.render_snapshot(
            mesh,
            resolution=resolution,
            r=2,
            fov=36,
            nviews=nviews,
            envmap=envmap,
        )
    except Exception as e:
        if used_hdri:
            print("HDRI render_snapshot failed; retry dummy EnvMap for Normal/Clay/Base color:", e, flush=True)
            status["hdri_ready"] = False
            status.setdefault("errors", []).append(f"render_snapshot: {type(e).__name__}: {e}")
            envmap = dummy_envmap(device="cuda")
            used_hdri = False
            images = render_utils.render_snapshot(
                mesh,
                resolution=resolution,
                r=2,
                fov=36,
                nviews=nviews,
                envmap=envmap,
            )
        else:
            raise

    written = {}
    step = min(max(default_step, 0), nviews - 1)
    for render_key, suffix, is_hdri in MODE_FILES:
        frames = images.get(render_key)
        if frames is None:
            written[render_key] = {
                "ok": False,
                "reason": "EXR 未就绪" if is_hdri else f"missing key {render_key}",
            }
            continue
        view_files = []
        for i, frame in enumerate(frames):
            view_path = f"{prefix}_{suffix}_s{i}.png"
            _save_png(frame, view_path)
            view_files.append(os.path.basename(view_path))
        main_path = f"{prefix}_{suffix}.png"
        _save_png(frames[step], main_path)
        written[render_key] = {
            "ok": True,
            "file": os.path.basename(main_path),
            "views": view_files,
            "default_step": step,
        }
        print("wrote", main_path, "views", len(view_files), flush=True)

    meta = {
        "prefix": os.path.basename(prefix),
        "nviews": nviews,
        "resolution": resolution,
        "default_step": step,
        "hdri_ready": bool(status.get("hdri_ready")),
        "hdri_status": status,
        "snapshots": written,
        "source": "render_utils.render_snapshot",
    }
    meta_path = f"{prefix}_snapshot_meta.json"
    with open(meta_path, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    print("meta", meta_path, flush=True)
    return meta


def snapshot_glb(glb_path: str, **kwargs) -> dict:
    prefix = os.path.splitext(glb_path)[0]
    print("load_glb", glb_path, flush=True)
    mesh = glb_to_pbr_mesh(glb_path)
    print("mesh", type(mesh).__name__, "V", tuple(mesh.vertices.shape), "F", tuple(mesh.faces.shape), flush=True)
    meta = save_snapshots_from_mesh(mesh, prefix, **kwargs)
    meta["glb"] = os.path.basename(glb_path)
    meta["mesh_type"] = type(mesh).__name__
    meta["note"] = (
        "Existing GLB has no MeshWithVoxel volume; snapshots use MeshWithPbrMaterial "
        "(baked textures). Next generate (run_grace.py) snapshots MeshWithVoxel in-memory."
    )
    with open(f"{prefix}_snapshot_meta.json", "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    return meta


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--glb", required=True, help="Path to uniquely named .glb (not overwritten)")
    parser.add_argument("--nviews", type=int, default=STEPS)
    parser.add_argument("--resolution", type=int, default=1024)
    parser.add_argument("--default-step", type=int, default=DEFAULT_STEP)
    parser.add_argument("--skip-hdri", action="store_true")
    args = parser.parse_args()
    glb = os.path.abspath(args.glb)
    if not os.path.isfile(glb):
        raise SystemExit(f"missing glb: {glb}")
    if os.path.basename(glb) == "sample.glb":
        raise SystemExit("refusing to operate on sample.glb overwrite path")

    envmap = None
    hdri_status = None
    if args.skip_hdri:
        envmap = dummy_envmap(device="cuda")
        hdri_status = {"hdri_ready": False, "errors": ["--skip-hdri"], "loaded_keys": []}
    meta = snapshot_glb(
        glb,
        envmap=envmap,
        nviews=args.nviews,
        resolution=args.resolution,
        default_step=args.default_step,
        hdri_status=hdri_status,
    )
    print(json.dumps({k: meta[k] for k in ("glb", "hdri_ready", "snapshots") if k in meta}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
