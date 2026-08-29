#!/usr/bin/env python3
"""Launch official TRELLIS.2 Gradio apps with --listen/--port (Gradio has no --listen).

Also makes cv2.imread able to read official HDRI EXRs via OpenEXR so app.py
does not crash on OpenCV 5. Does not change PyTorch or the DINOv3 layer patch.
"""
from __future__ import annotations

import argparse
import os
import sys

APP_DIR = os.environ.get("TRELLIS2_APP", "/home/ubuntu/trellis2/app")


def _patch_cv2_exr() -> None:
    os.environ.setdefault("OPENCV_IO_ENABLE_OPENEXR", "1")
    sys.path.insert(0, APP_DIR)
    preview = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "trellis2-preview")
    sys.path.insert(0, os.path.abspath(preview))
    import cv2
    from hdri_utils import _load_exr_openexr

    if getattr(cv2, "_trellis2_exr_patched", False):
        return

    orig = cv2.imread
    orig_cvt = cv2.cvtColor

    def imread(path, flags=1):
        # Call OpenEXR directly. load_exr_rgb() tries cv2.imread first and would recurse.
        if str(path).lower().endswith(".exr"):
            rgb = _load_exr_openexr(path)
            print(f"exr_imread {path} shape={getattr(rgb, 'shape', None)}", flush=True)
            return rgb[..., ::-1].copy()  # BGR for official cvtColor(BGR2RGB)
        return orig(path, flags)

    def cvtColor(src, code, *a, **kw):
        if src is None:
            raise ValueError("cv2.cvtColor got None; EXR imread patch did not run")
        return orig_cvt(src, code, *a, **kw)

    cv2.imread = imread
    cv2.cvtColor = cvtColor
    cv2._trellis2_exr_patched = True
    print("cv2_exr_patch_ok", flush=True)


def main() -> int:
    parser = argparse.ArgumentParser(description="Launch TRELLIS.2 Gradio (binds --listen/--port)")
    parser.add_argument("--listen", default="0.0.0.0", help="Bind address (Gradio server_name)")
    parser.add_argument("--port", type=int, default=7860)
    parser.add_argument("--app", default="app.py", help="app.py or app_texturing.py")
    args = parser.parse_args()

    os.chdir(APP_DIR)
    sys.path.insert(0, APP_DIR)
    os.environ["GRADIO_SERVER_NAME"] = args.listen
    os.environ["GRADIO_SERVER_PORT"] = str(args.port)
    _patch_cv2_exr()

    import gradio as gr
    _patch_cv2_exr()  # Gradio/OpenCV import order; keep EXR patch on the live cv2 module

    orig_launch = gr.Blocks.launch

    def launch(self, *a, **kw):
        kw["server_name"] = args.listen
        kw["server_port"] = args.port
        kw.setdefault("share", False)
        return orig_launch(self, *a, **kw)

    gr.Blocks.launch = launch
    app_path = args.app if os.path.isabs(args.app) else os.path.join(APP_DIR, args.app)
    if not os.path.isfile(app_path):
        raise SystemExit(f"missing {app_path}")
    import runpy

    runpy.run_path(app_path, run_name="__main__")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
