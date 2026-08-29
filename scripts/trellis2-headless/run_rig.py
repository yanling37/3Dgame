#!/usr/bin/env python3
"""SkinToken rig via ComfyUI /prompt API. Requires 8188 and SkinToken + Trellis2LoadMesh."""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
from datetime import datetime, timezone

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from comfy_run import SERVER, _http_json, collect_output_files, wait_history
import uuid

APP = os.environ.get("TRELLIS2_APP", "/home/ubuntu/trellis2/app")


def utc_stamp() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")


def latest_glb() -> str:
    cands = []
    for folder in (
        os.path.join(APP, "output", "multiview"),
        os.path.join(APP, "output", "projection"),
        os.path.join(APP, "output"),
    ):
        if not os.path.isdir(folder):
            continue
        for name in os.listdir(folder):
            if name.endswith(".glb") and name != "sample.glb":
                p = os.path.join(folder, name)
                cands.append((os.path.getmtime(p), p))
    if not cands:
        raise SystemExit("no GLB found under output/multiview, output/projection, or output/")
    cands.sort()
    return cands[-1][1]


def build_prompt(mesh: str, info: dict, skeleton: str) -> dict:
    if "SkinTokenRigTrimesh" not in info:
        raise SystemExit("SkinToken node missing (SkinTokenRigTrimesh). Check ComfyUI custom_nodes.")
    if "Trellis2LoadMesh" not in info:
        raise SystemExit("Trellis2LoadMesh missing")
    load_in = list((info["Trellis2LoadMesh"].get("input", {}).get("required") or {}).keys())
    mesh_key = load_in[0] if load_in else "mesh"
    skel_choices = []
    skel_spec = (info["SkinTokenRigTrimesh"].get("input", {}).get("required") or {}).get("skeleton_template")
    if isinstance(skel_spec, list) and skel_spec:
        skel_choices = skel_spec[0] if isinstance(skel_spec[0], list) else []
    if skeleton not in skel_choices and skel_choices:
        # prefer Mixamo then UE5
        for want in ("Mixamo", "Unreal Engine 5"):
            if want in skel_choices:
                skeleton = want
                break
        else:
            skeleton = skel_choices[0]
    return {
        "1": {"class_type": "Trellis2LoadMesh", "inputs": {mesh_key: mesh}},
        "2": {
            "class_type": "SkinTokenRigTrimesh",
            "inputs": {
                "trimesh": ["1", 0],
                "use_transfer": True,
                "bottom_center_origin": True,
                "skeleton_template": skeleton,
                "file_format": "glb",
                "filename_prefix": "3D/grace_rig",
                "save_file": True,
            },
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="SkinToken rig a GLB (use_transfer + bottom_center_origin)")
    parser.add_argument("glb", nargs="?", help="GLB path; default = latest multiview/projection output")
    parser.add_argument("--skeleton", default="Mixamo", help="Mixamo or Unreal Engine 5")
    parser.add_argument("--no-publish", action="store_true")
    args = parser.parse_args()
    mesh = os.path.abspath(args.glb) if args.glb else latest_glb()
    if not os.path.isfile(mesh):
        raise SystemExit(f"missing glb: {mesh}")
    if os.path.basename(mesh) == "sample.glb":
        raise SystemExit("refusing sample.glb")
    blender = os.environ.get("SKINTOKEN_BLENDER_BIN") or shutil.which("blender")
    if not blender:
        raise SystemExit("blender not on PATH; set SKINTOKEN_BLENDER_BIN")
    print("mesh", mesh, flush=True)
    print("blender", blender, flush=True)
    info = _http_json("GET", "/object_info", timeout=120)
    prompt = build_prompt(mesh, info, args.skeleton)
    body = {"prompt": prompt, "client_id": str(uuid.uuid4())}
    try:
        res = _http_json("POST", "/prompt", body, timeout=120)
    except Exception as e:
        raise SystemExit(f"ComfyUI /prompt failed: {e}")
    pid = res.get("prompt_id")
    print("prompt_id", pid, flush=True)
    hist = wait_history(pid, timeout=3600)
    files = collect_output_files(hist)
    glbs = [f for f in files if f.lower().endswith(".glb")]
    if not glbs:
        raise SystemExit(f"SkinToken produced no GLB: {files}")
    src = max(glbs, key=lambda p: os.path.getmtime(p) if os.path.isfile(p) else 0)
    stamp = utc_stamp()
    dest_dir = os.path.join(APP, "output", "rigged")
    os.makedirs(dest_dir, exist_ok=True)
    dest = os.path.join(dest_dir, f"grace_rig_{stamp}.glb")
    shutil.copy2(src, dest)
    print("glb_ok", dest, flush=True)
    if not args.no_publish:
        pub = os.path.join(APP, "publish_preview.sh")
        if os.path.isfile(pub):
            subprocess.check_call(["bash", pub, dest])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
