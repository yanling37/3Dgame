#!/usr/bin/env python3
"""Rewrite ComfyUI-Trellis2 UI workflows to Linux Grace paths."""
from __future__ import annotations

import argparse
import copy
import json
import os

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


def _view_file(*names: str) -> str:
    for name in names:
        if os.path.isfile(os.path.join(GRACE, name)) or os.path.isfile(
            os.path.join(COMFY, "input", name)
        ):
            return name
    return names[0]


def _max_ids(wf: dict) -> tuple[int, int]:
    max_n = int(wf.get("last_node_id") or 0)
    max_l = int(wf.get("last_link_id") or 0)
    for node in wf.get("nodes") or []:
        try:
            max_n = max(max_n, int(node.get("id")))
        except (TypeError, ValueError):
            pass
    for link in wf.get("links") or []:
        try:
            max_l = max(max_l, int(link[0]))
        except (TypeError, ValueError, IndexError):
            pass
    return max_n, max_l


def _assign_untitled_image_loaders(wf: dict) -> None:
    """MeshOnly example loaders have no Front/Back titles — map by order."""
    titled = []
    untitled = []
    for node in wf.get("nodes") or []:
        if node.get("type") != "Trellis2LoadImageWithTransparency":
            continue
        title = (node.get("title") or "").lower()
        if any(k in title for k in ("front", "back", "left", "side", "right")):
            titled.append(node)
        else:
            untitled.append(node)
    taken = set()
    for node in titled:
        wv = node.get("widgets_values") or []
        if wv and isinstance(wv[0], str):
            taken.add(wv[0])
    remaining = [n for n in ("front.png", "back.png", "left.png", "right.png") if n not in taken]
    for i, node in enumerate(untitled):
        if i >= len(remaining):
            break
        name = remaining[i]
        if name == "left.png":
            name = _view_file("left.png", "side.png")
        wv = list(node.get("widgets_values") or ["", "image"])
        wv[0] = name
        if len(wv) == 1:
            wv.append("image")
        node["widgets_values"] = wv


def _wire_optional_views(wf: dict, resolution: int, rembg: bool) -> None:
    """Attach side/left.png to unlinked left_image sockets (three-view sheets)."""
    left_name = None
    if os.path.isfile(os.path.join(GRACE, "left.png")) or os.path.isfile(
        os.path.join(COMFY, "input", "left.png")
    ):
        left_name = "left.png"
    elif os.path.isfile(os.path.join(GRACE, "side.png")) or os.path.isfile(
        os.path.join(COMFY, "input", "side.png")
    ):
        left_name = "side.png"
    right_name = "right.png" if os.path.isfile(os.path.join(GRACE, "right.png")) else None
    wanted = [("left_image", "Left Image", left_name), ("right_image", "Right Image", right_name)]
    existing_titles = {(n.get("title") or "").lower() for n in wf.get("nodes") or []}
    max_n, max_l = _max_ids(wf)
    for node in list(wf.get("nodes") or []):
        inputs = node.get("inputs") or []
        by_name = {i.get("name"): i for i in inputs}
        for sock, title, fname in wanted:
            inp = by_name.get(sock)
            if not inp or inp.get("link") is not None or not fname:
                continue
            if title.lower() in existing_titles:
                continue
            slot = next((i for i, s in enumerate(inputs) if s.get("name") == sock), None)
            if slot is None:
                continue
            max_n += 1
            load_id = max_n
            max_n += 1
            prep_id = max_n
            max_l += 1
            link_load = max_l
            max_l += 1
            link_prep = max_l
            load_node = {
                "id": load_id,
                "type": "Trellis2LoadImageWithTransparency",
                "pos": [30, 1900],
                "size": [360, 200],
                "flags": {},
                "order": 20,
                "mode": 0,
                "inputs": [],
                "outputs": [
                    {"name": "image", "type": "IMAGE", "links": []},
                    {"name": "mask", "type": "MASK", "links": []},
                    {"name": "image_with_alpha", "type": "IMAGE", "links": [link_load]},
                ],
                "title": title,
                "properties": {"Node name for S&R": "Trellis2LoadImageWithTransparency"},
                "widgets_values": [fname, "image"],
            }
            prep_node = {
                "id": prep_id,
                "type": "Trellis2PreProcessImage",
                "pos": [420, 1900],
                "size": [247, 106],
                "flags": {},
                "order": 21,
                "mode": 0,
                "inputs": [{"name": "image", "type": "IMAGE", "link": link_load}],
                "outputs": [{"name": "image", "type": "IMAGE", "links": [link_prep]}],
                "properties": {"Node name for S&R": "Trellis2PreProcessImage"},
                "widgets_values": [10, bool(rembg), int(resolution)],
            }
            inp["link"] = link_prep
            wf["nodes"].append(load_node)
            wf["nodes"].append(prep_node)
            wf.setdefault("links", []).append(
                [link_load, load_id, 2, prep_id, 0, "IMAGE"]
            )
            wf["links"].append([link_prep, prep_id, 0, node.get("id"), slot, "IMAGE"])
            existing_titles.add(title.lower())
            print("wired", title, fname, "->", node.get("type"), sock)
    wf["last_node_id"] = max_n
    wf["last_link_id"] = max_l


def _patch_node(
    node: dict,
    prefix: str,
    resolution: int,
    texture_size: int,
    steps: int,
    rembg: bool,
) -> None:
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
            wv[0] = "front.png"
        elif "back" in title:
            wv[0] = "back.png"
        elif "left" in title or "side" in title:
            wv[0] = _view_file("left.png", "side.png")
        elif "right" in title:
            wv[0] = "right.png"
        elif isinstance(wv[0], str) and wv[0].endswith(".png"):
            # untitled loaders handled in a second pass
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
    elif ntype == "Trellis2PreProcessImage":
        if len(wv) >= 2:
            wv[1] = bool(rembg)
        if len(wv) >= 3 and isinstance(wv[2], int):
            wv[2] = resolution

    node["widgets_values"] = wv


def _bypass_passthrough_nodes(wf: dict, ntypes: tuple[str, ...]) -> None:
    """Rewire around mesh pass-through nodes (MeshLib hole-fill OOMs on 17k holes)."""
    links = wf.get("links") or []
    drop_nodes: set = set()
    drop_links: set = set()
    for node in wf.get("nodes") or []:
        if node.get("type") not in ntypes:
            continue
        nid = node.get("id")
        mesh_in = next((i for i in (node.get("inputs") or []) if i.get("name") == "mesh"), None)
        if not mesh_in or mesh_in.get("link") is None:
            continue
        src = next((L for L in links if L[0] == mesh_in["link"]), None)
        if not src:
            continue
        src_node, src_slot = src[1], src[2]
        drop_links.add(src[0])
        drop_nodes.add(nid)
        for L in links:
            if L[1] == nid:
                L[1] = src_node
                L[2] = src_slot
        print("bypassed", node.get("type"), "id", nid, "via", src_node)
    if drop_nodes:
        wf["nodes"] = [n for n in wf["nodes"] if n.get("id") not in drop_nodes]
        wf["links"] = [L for L in links if L[0] not in drop_links]


def patch_workflow(
    src: str,
    dst: str,
    prefix: str,
    resolution: int,
    texture_size: int,
    steps: int,
    rembg: bool,
) -> dict:
    data = json.load(open(src, encoding="utf-8"))
    out = copy.deepcopy(data)
    unknown_uuid = []
    for node in out.get("nodes") or []:
        ntype = node.get("type") or ""
        if len(ntype) >= 32 and "-" in ntype:
            unknown_uuid.append(ntype)
        _patch_node(node, prefix, resolution, texture_size, steps, rembg)
    _assign_untitled_image_loaders(out)
    _wire_optional_views(out, resolution, rembg)
    _bypass_passthrough_nodes(out, ("Trellis2FillHolesNicelyWithMeshlib",))
    os.makedirs(os.path.dirname(dst) or ".", exist_ok=True)
    with open(dst, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)
        f.write("\n")
    return {"src": src, "dst": dst, "subgraph_uuids": unknown_uuid}


def main() -> int:
    parser = argparse.ArgumentParser(description="Patch official ComfyUI-Trellis2 workflows for Grace")
    parser.add_argument("--resolution", type=int, default=1024)
    parser.add_argument("--texture-size", type=int, default=2048)
    parser.add_argument("--steps", type=int, default=12)
    parser.add_argument(
        "--rembg",
        action=argparse.BooleanOptionalAction,
        default=True,
        help="Trellis2PreProcessImage.remove_background (neural matting, not white chroma-key)",
    )
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
        info = patch_workflow(
            src, dst, prefix, args.resolution, args.texture_size, args.steps, args.rembg
        )
        info["key"] = key
        info["ok"] = True
        report.append(info)
        print("patched", dst)
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
