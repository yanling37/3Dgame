#!/usr/bin/env python3
"""Probe official TRELLIS.2 HDRI EXRs and optionally wrap EnvMap (no 4B)."""
from __future__ import annotations

import argparse
import json
import os
import sys

os.environ.setdefault("OPENCV_IO_ENABLE_OPENEXR", "1")
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")

APP_DIR = os.environ.get("TRELLIS2_APP", "/home/ubuntu/trellis2/app")
if APP_DIR not in sys.path:
    sys.path.insert(0, APP_DIR)

from hdri_utils import load_official_envmaps, probe_official_hdris


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--wrap-envmap", action="store_true", help="also build EnvMap cubemap on CUDA")
    parser.add_argument("--out", default="/tmp/hdri_probe.json")
    args = parser.parse_args()

    result = probe_official_hdris()
    result["envmap_wrap"] = None
    if args.wrap_envmap:
        try:
            envmap, status = load_official_envmaps(device="cuda")
            result = status
            wrap = {}
            if envmap:
                # Trigger cubemap + mips once to prove EnvMap, not just EXR decode.
                _ = envmap[next(iter(envmap))]._backend
                wrap["ok"] = True
                wrap["keys"] = list(envmap)
                wrap["backend"] = type(envmap[next(iter(envmap))]._backend).__name__
            else:
                wrap["ok"] = False
                wrap["error"] = status.get("errors")
            result["envmap_wrap"] = wrap
        except Exception as e:
            result["envmap_wrap"] = {"ok": False, "error": f"{type(e).__name__}: {e}"}

    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2, ensure_ascii=False)
    print(json.dumps(result, indent=2, ensure_ascii=False))
    print("wrote", args.out)
    return 0 if result.get("files", {}).get("forest", {}).get("rgb") else 1


if __name__ == "__main__":
    raise SystemExit(main())
