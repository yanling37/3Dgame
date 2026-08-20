#!/usr/bin/env python3
"""Load official TRELLIS.2 HDRI EXRs into EnvMap tensors.

OpenCV 5.0.0 on this host returns None for EXR even with
OPENCV_IO_ENABLE_OPENEXR=1. Fall back to the OpenEXR Python bindings.
Must set OPENCV_IO_ENABLE_OPENEXR before importing cv2.
"""
from __future__ import annotations

import os
import struct
from typing import Dict, Optional, Tuple

os.environ.setdefault("OPENCV_IO_ENABLE_OPENEXR", "1")

import numpy as np

APP_DIR = os.environ.get("TRELLIS2_APP", "/home/ubuntu/trellis2/app")
HDRI_DIR = os.path.join(APP_DIR, "assets", "hdri")
OFFICIAL_HDRI_KEYS = ("forest", "sunset", "courtyard")
OPENEXR_MAGIC = b"v/1\x01\x02"


def classify_exr_file(path: str) -> dict:
    info = {
        "path": path,
        "exists": os.path.isfile(path),
        "size": os.path.getsize(path) if os.path.isfile(path) else 0,
        "kind": "missing",
        "magic_hex": "",
        "note": "",
    }
    if not info["exists"]:
        info["note"] = "file not found"
        return info
    with open(path, "rb") as f:
        magic = f.read(16)
    info["magic_hex"] = magic[:8].hex()
    if magic.startswith(b"version https://git-lfs.github.com"):
        info["kind"] = "git-lfs-pointer"
        info["note"] = "Git LFS pointer, not a real EXR; run git lfs pull"
        return info
    if magic.startswith(OPENEXR_MAGIC):
        info["kind"] = "openexr"
        info["note"] = "real OpenEXR (not an LFS pointer)"
        return info
    info["kind"] = "unknown"
    info["note"] = f"unrecognized header {magic[:16]!r}"
    return info


def _load_exr_cv2(path: str) -> Optional[np.ndarray]:
    try:
        import cv2
    except Exception:
        return None
    img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    if img is None:
        return None
    if img.ndim == 2:
        img = np.stack([img, img, img], axis=-1)
    if img.shape[-1] >= 3:
        img = cv2.cvtColor(img[..., :3], cv2.COLOR_BGR2RGB)
    return np.ascontiguousarray(img.astype(np.float32))


def _channel_pixels(channel) -> np.ndarray:
    return np.array(channel.pixels, dtype=np.float32)


def _load_exr_openexr(path: str) -> np.ndarray:
    import OpenEXR

    f = OpenEXR.File(path)
    part = f.parts[0]
    channels = part.channels
    if "RGB" in channels:
        arr = _channel_pixels(channels["RGB"])
        if arr.ndim == 2:
            arr = np.stack([arr, arr, arr], axis=-1)
        return np.ascontiguousarray(arr[..., :3].astype(np.float32))
    names = {name.upper(): name for name in channels}
    if all(c in names for c in ("R", "G", "B")):
        r = _channel_pixels(channels[names["R"]])
        g = _channel_pixels(channels[names["G"]])
        b = _channel_pixels(channels[names["B"]])
        if r.ndim == 3:
            r, g, b = r[..., 0], g[..., 0], b[..., 0]
        return np.ascontiguousarray(np.stack([r, g, b], axis=-1).astype(np.float32))
    raise RuntimeError(f"OpenEXR file has no RGB channels: {list(channels)}")


def load_exr_rgb(path: str) -> Tuple[np.ndarray, str]:
    """Return (H, W, 3) float32 RGB and the backend that succeeded."""
    cv2_img = _load_exr_cv2(path)
    if cv2_img is not None and cv2_img.size > 0:
        return cv2_img, "cv2"
    arr = _load_exr_openexr(path)
    return arr, "OpenEXR"


def probe_official_hdris() -> dict:
    try:
        import cv2
        cv2_ver = cv2.__version__
    except Exception as e:
        cv2_ver = f"unavailable: {e}"
    try:
        import OpenEXR
        openexr_ver = getattr(OpenEXR, "__version__", "present")
    except Exception as e:
        openexr_ver = f"unavailable: {e}"

    result = {
        "opencv_version": cv2_ver,
        "openexr_version": openexr_ver,
        "OPENCV_IO_ENABLE_OPENEXR": os.environ.get("OPENCV_IO_ENABLE_OPENEXR"),
        "files": {},
    }
    for key in OFFICIAL_HDRI_KEYS:
        path = os.path.join(HDRI_DIR, f"{key}.exr")
        file_info = classify_exr_file(path)
        file_info["cv2"] = None
        file_info["openexr"] = None
        file_info["rgb"] = None
        if file_info["kind"] == "openexr":
            cv2_img = _load_exr_cv2(path)
            file_info["cv2"] = None if cv2_img is None else {
                "shape": list(cv2_img.shape),
                "dtype": str(cv2_img.dtype),
            }
            try:
                arr, backend = load_exr_rgb(path)
                file_info["openexr" if backend == "OpenEXR" else "cv2"] = {
                    "shape": list(arr.shape),
                    "dtype": str(arr.dtype),
                    "min": float(np.nanmin(arr)),
                    "max": float(np.nanmax(arr)),
                }
                file_info["rgb"] = {
                    "backend": backend,
                    "shape": list(arr.shape),
                    "dtype": str(arr.dtype),
                    "min": float(np.nanmin(arr)),
                    "max": float(np.nanmax(arr)),
                }
            except Exception as e:
                file_info["rgb_error"] = f"{type(e).__name__}: {e}"
        result["files"][key] = file_info
    return result


def dummy_latlong(height: int = 64, width: int = 128, value: float = 0.4) -> np.ndarray:
    return np.ones((height, width, 3), dtype=np.float32) * float(value)


def load_official_envmaps(device: str = "cuda"):
    """Load forest/sunset/courtyard EnvMaps. Returns (envmap_or_none, status)."""
    import torch
    from trellis2.renderers import EnvMap

    status = probe_official_hdris()
    envmap: Dict[str, object] = {}
    errors = []
    for key in OFFICIAL_HDRI_KEYS:
        path = os.path.join(HDRI_DIR, f"{key}.exr")
        try:
            rgb, backend = load_exr_rgb(path)
            tensor = torch.tensor(rgb, dtype=torch.float32, device=device)
            envmap[key] = EnvMap(tensor)
            status["files"][key]["envmap"] = "ok"
            status["files"][key]["rgb_backend"] = backend
        except Exception as e:
            msg = f"{type(e).__name__}: {e}"
            errors.append(f"{key}: {msg}")
            status["files"][key]["envmap"] = msg
    status["hdri_ready"] = len(envmap) == len(OFFICIAL_HDRI_KEYS)
    status["loaded_keys"] = list(envmap)
    status["errors"] = errors
    if not envmap:
        return None, status
    return envmap, status


def dummy_envmap(device: str = "cuda"):
    import torch
    from trellis2.renderers import EnvMap

    return {"_dummy": EnvMap(torch.tensor(dummy_latlong(), dtype=torch.float32, device=device))}
