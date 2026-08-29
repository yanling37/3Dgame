#!/usr/bin/env python3
"""Rewrite ComfyUI-Trellis2 UI workflows to Linux Grace paths."""
from __future__ import annotations

import argparse
import copy
import json
import os
import shutil
from typing import Any

APP = os.environ.get("TRELLIS2_APP", "/home/ubuntu/trellis2/app")
COMFY = os.environ.get("COMFYUI_DIR", "/home/ubuntu/trellis2/cache/comfyui")
WF_SRC = os.path.join(COMFY, "custom_nodes", "ComfyUI-Trellis2", "example_workflows")
WF_DST = os.path.join(APP, "workflows")
GRACE = os.path.join(APP, "assets", "multiview", "grace")

MAP = {
    "texturing": ("MeshTexturing_MultiView.json", "grace_mv_texturing.json", "grace_mv"),
    "full": ("MeshWithTexturing_MultiView.json", "grace_mv_full.json", "grace_mv"),
    "mesh_only": ("MeshOnly_MultiView.json", "grace_mv_mesh_only.json", "grace_mv"),
    "projection": ("Projection_NvDiffrast_Qwen_XViews.json", "grace_projection_qwen.json", "grace_proj"),
}
PROJ_FALLBACK = "Projection_Hy20_Qwen_4Views.json"


def _is_windows_path(s: str) -> bool:
    return s.lower().startswith("c:") or "\\" in s or "/git/comfyui" in s.lower()


def _patch_node(node: dict, prefix: str, resolution: int, texture_size: int, steps: int) -> None:
    ntype = node.get("type") or ""
    title = (node.get("title") or "").lower()
    wv = node.get("widgets_values")
    if not isinstance(wv, list) or not wv:
        node["widgets_values"] = wv
        return

    if ntype == "Trellis2LoadMesh":
        mesh = os.path.join(GRACE, "mesh_from_singleview.glb")
        wv[0] = mesh
    elif ntype == "Trellis2LoadImageWithTransparency":
        if "front" in title:
            wv[0] = "grace/front.png"
        elif "back" in title:
            wv[0] = "grace/back.png"
        elif "left" in title or "side" in title:
            side = os.path.join(GRACE, "left.png")
            alt = os.path.join(GRACE, "side.png")
            wv[0] = "grace/left.png" if os.path.isfile(side) else (
                "grace/side.png" if os.path.isfile(alt) else "grace/side.png"
            )
        elif "right" in title:
            wv[0] = "grace/right.png"
        elif isinstance(wv[0], str) and wv[0].endswith(".png"):
            # first unmatched image loader: leave; caller may have only front/back
            pass
    elif ntype == "Trellis2ExportMesh":
        if isinstance(wv[0], str):
            wv[0] = prefix
        if len(wv) > 1:
            wv[1] = "glb"
    elif ntype == "Trellis2MeshTexturingMultiView":
        # [seed, control, steps, ...]
        if len(wv) > 2 and isinstance(wv[2], int):
            wv[2] = steps
        if len(wv) > 6 and isinstance(wv[6], int):
            wv[6] = resolution
        if len(wv) > 7 and isinstance(wv[7], int):
            wv[7] = texture_size
    elif ntype in (
        "Trellis2SparseMultiViewGenerator",
        "Trellis2ShapeMultiViewGenerator",
        "Trellis2ShapeCascadeMultiViewGenerator",
        "Trellis2TexSlatMultiViewGenerator",
    ):
        for i, v in enumerate(wv):
            if v == 12 or (isinstance(v, int) and i < 8 and v in (12, 32)):
                if i in (0, 1) and isinstance(wv[0], int) and wv[0] > 100:
                    continue
        # steps widget is typically 12 already
        if ntype.endswith("SparseMultiViewGenerator") and len(wv) > 2:
            wv[2] = steps
        if ntype == "Trellis2ShapeMultiViewGenerator" and len(wv) > 1:
            wv[0] = resolution
            wv[1] = steps
        if ntype == "Trellis2TexSlatMultiViewGenerator" and len(wv) > 1:
            wv[0] = resolution
            wv[1] = steps
    elif ntype == "PrimitiveInt":
        if "texture" in title and isinstance(wv[0], int):
            wv[0] = texture_size
        if "decimation" in title or wv[0] == 500000:
            pass
    elif ntype == "PrimitiveString":
        if isinstance(wv[0], str) and wv[0] in ("MV", "Armor", "Textured", "Test_v2"):
            wv[0] = prefix
    elif ntype == "Preview3D" and isinstance(wv[0], str) and _is_windows_path(str(wv[0])):
        wv[0] = ""
    elif ntype == "Trellis2PreProcessImage" and len(wv) >= 3 and isinstance(wv[2], int):
        wv[2] = resolution

    node["widgets_values"] = wv


def patch_workflow(src: str, dst: str, prefix: str, resolution: int, texture_size: int, steps: int) -> dict:
    data = json.load(open(src, encoding="utf-8"))
    out = copy.deepcopy(data)
    unknown_uuid = []
    for node in out.get("nodes") or []:
        ntype = node.get("type") or ""
        if len(ntype) >= 32 and "-" in ntype:
            unknown_uuid.append(ntype)
        _patch_node(node, prefix, resolution, texture_size, steps)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    with open(dst, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)
        f.write("\n")
    return {"src": src, "dst": dst, "subgraph_uuids": unknown_uuid}


def main() -> int:
    parser = argparse.ArgumentParser(description="Patch official ComfyUI-Trellis2 workflows for Grace")
    parser.add_argument("--resolution", type=int, default=1024)
    parser.add_argument("--texture-size", type=int, default=2048)
    parser.add_argument("--steps", type=int, default=12)
    args = parser.parse_args()
    os.makedirs(WF_DST, exist_ok=True)
    report = []
    for key, (src_name, dst_name, prefix) in MAP.items():
        src = os.path.join(WF_SRC, src_name)
        if key == "projection" and not os.path.isfile(src):
            src = os.path.join(WF_SRC, PROJ_FALLBACK)
        dst = os.path.join(WF_DST, dst_name)
        if not os.path.isfile(src):
            report.append({"key": key, "ok": False, "error": f"missing {src}"})
            continue
        info = patch_workflow(src, dst, prefix, args.resolution, args.texture_size, args.steps)
        info["key"] = key
        info["ok"] = True
        report.append(info)
        print("patched", dst)
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
