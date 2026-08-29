#!/usr/bin/env python3
"""Patch ComfyUI-Trellis2 PreProcess to accept RGB sheets (no chroma-key).

Official preprocess_image always reads output_np[:, :, 3]. JPEG/PNG character
sheets are RGB, and Trellis2LoadImageWithTransparency's image_with_alpha output
is still 3-channel when the file has no alpha — that raises IndexError.

This is an instance-store custom-node patch. Do not touch microsoft/TRELLIS.2
math. Do not chroma-key white (dresses may be white).
"""
from __future__ import annotations

import os
import sys

MARKER = "PATCH: opaque-alpha-if-rgb"
NODES = os.environ.get(
    "TRELLIS2_NODES_PY",
    "/home/ubuntu/trellis2/cache/comfyui/custom_nodes/ComfyUI-Trellis2/nodes.py",
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


def main() -> int:
    path = NODES
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
    # OLD already contains the first `bbox = np.argwhere` line's predecessor only.
    # Insert before the original bbox line by replacing the alpha read, then
    # skip the duplicate bbox that NEW now includes.
    old_with_bbox = OLD + "        bbox = np.argwhere(alpha > 0.8 * 255)\n"
    if old_with_bbox in text:
        text = text.replace(old_with_bbox, NEW, 1)
    else:
        text = text.replace(OLD, NEW, 1)
    open(path, "w", encoding="utf-8").write(text)
    print("patched", path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
