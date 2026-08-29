#!/usr/bin/env python3
"""Stage Grace three-views into ComfyUI input/ as RGBA PNGs.

Does not chroma-key white (the dress is white). Alpha is kept if present,
otherwise filled opaque (255). Copies basename-only files into ComfyUI input
root because Trellis2LoadImageWithTransparency only lists that directory.
"""
from __future__ import annotations

import os
import shutil
from typing import List

from PIL import Image, ImageOps

APP = os.environ.get("TRELLIS2_APP", "/home/ubuntu/trellis2/app")
COMFY = os.environ.get("COMFYUI_DIR", "/home/ubuntu/trellis2/cache/comfyui")
GRACE = os.path.join(APP, "assets", "multiview", "grace")
COMFY_INPUT = os.path.join(COMFY, "input")
NAMES = ("front.png", "back.png", "side.png", "left.png", "right.png")


def to_rgba_png(src: str, dst: str) -> None:
    im = Image.open(src)
    im = ImageOps.exif_transpose(im)
    if im.mode != "RGBA":
        im = im.convert("RGBA")
    os.makedirs(os.path.dirname(dst) or ".", exist_ok=True)
    tmp = dst + ".tmp.png"
    im.save(tmp, "PNG")
    os.replace(tmp, dst)


def main() -> int:
    os.makedirs(GRACE, exist_ok=True)
    os.makedirs(COMFY_INPUT, exist_ok=True)
    side = os.path.join(GRACE, "side.png")
    left = os.path.join(GRACE, "left.png")
    if os.path.isfile(side) and (not os.path.exists(left) or os.path.islink(left)):
        # Prefer a real file so ComfyUI's os.listdir listing is stable.
        if os.path.islink(left) or os.path.exists(left):
            os.remove(left)
        shutil.copy2(side, left)
        print("left.png <- side.png")

    done: List[str] = []
    for name in NAMES:
        src = os.path.join(GRACE, name)
        if not os.path.isfile(src):
            continue
        to_rgba_png(src, src)
        dest = os.path.join(COMFY_INPUT, name)
        if os.path.islink(dest):
            os.remove(dest)
        shutil.copy2(src, dest)
        im = Image.open(src)
        print(f"staged {name} mode={im.mode} size={im.size}")
        done.append(name)
    if not done:
        print("prepare_views: no front/back/side png under", GRACE)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
