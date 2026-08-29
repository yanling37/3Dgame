#!/usr/bin/env python3
"""Submit a ComfyUI UI-format workflow to /prompt and wait for GLB output."""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request
import uuid
from typing import Any, Dict, List, Optional, Tuple

COMFY = os.environ.get("COMFYUI_DIR", "/home/ubuntu/trellis2/cache/comfyui")
APP = os.environ.get("TRELLIS2_APP", "/home/ubuntu/trellis2/app")
SERVER = os.environ.get("COMFYUI_URL", "http://127.0.0.1:8188")


def _http_json(method: str, path: str, body: Optional[dict] = None, timeout: int = 60) -> Any:
    data = None if body is None else json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        SERVER + path,
        data=data,
        method=method,
        headers={"Content-Type": "application/json"} if data else {},
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        raw = resp.read()
        if not raw:
            return None
        return json.loads(raw.decode("utf-8"))


def object_info() -> dict:
    return _http_json("GET", "/object_info", timeout=120)


WIDGET_KINDS = {"INT", "FLOAT", "BOOLEAN", "STRING"}
SEED_CONTROL = {"fixed", "randomize", "increment", "decrement"}


def _is_widget_spec(typ: Any) -> bool:
    """True if this input is a UI widget, not a linked socket."""
    kind = typ[0] if isinstance(typ, (list, tuple)) else typ
    if isinstance(kind, (list, tuple)):
        return True  # combo or file list
    return kind in WIDGET_KINDS


def ui_to_api(workflow: dict, info: dict) -> Tuple[dict, List[str]]:
    """Convert ComfyUI UI graph to API prompt. Returns (prompt, missing_types)."""
    missing: List[str] = []
    links = {}
    for link in workflow.get("links") or []:
        # [id, from_node, from_slot, to_node, to_slot, type]
        links[link[0]] = link

    prompt: Dict[str, Any] = {}
    for node in workflow.get("nodes") or []:
        ntype = node.get("type") or ""
        nid = str(node.get("id"))
        if ntype in ("Note", "Reroute", "MarkdownNote"):
            continue
        if ntype in ("GetNode", "SetNode"):
            # subgraph helpers; skip if not in object_info
            if ntype not in info:
                continue
        if ntype not in info:
            missing.append(f"{nid}:{ntype}")
            continue
        spec = info[ntype]
        widget_names: List[str] = []
        for section in ("required", "optional"):
            items = spec.get("input", {}).get(section) or {}
            if not isinstance(items, dict):
                continue
            for name, typ in items.items():
                if _is_widget_spec(typ):
                    widget_names.append(name)

        inputs: Dict[str, Any] = {}
        wv = list(node.get("widgets_values") or [])
        # flatten seed [value, "fixed"] pairs some UIs nest
        flat: List[Any] = []
        for v in wv:
            if isinstance(v, list):
                flat.extend(v)
            else:
                flat.append(v)
        wv = flat
        wi = 0
        for name in widget_names:
            if wi >= len(wv):
                break
            val = wv[wi]
            wi += 1
            if isinstance(val, str) and val.lower() in SEED_CONTROL:
                if wi >= len(wv):
                    break
                val = wv[wi]
                wi += 1
            inputs[name] = val
            if name.lower() in ("seed", "noise_seed") and wi < len(wv) and isinstance(wv[wi], str) and wv[wi].lower() in SEED_CONTROL:
                wi += 1

        for inp in node.get("inputs") or []:
            name = inp.get("name")
            link_id = inp.get("link")
            if not name or name.startswith("_") or name == "":
                continue
            if link_id is None:
                continue
            link = links.get(link_id)
            if not link:
                continue
            from_node, from_slot = link[1], link[2]
            inputs[name] = [str(from_node), int(from_slot)]

        prompt[nid] = {"class_type": ntype, "inputs": inputs}
    return prompt, missing


def wait_history(prompt_id: str, timeout: int = 7200) -> dict:
    t0 = time.time()
    while time.time() - t0 < timeout:
        try:
            hist = _http_json("GET", f"/history/{prompt_id}", timeout=30)
        except Exception:
            time.sleep(2)
            continue
        if hist and prompt_id in hist:
            return hist[prompt_id]
        time.sleep(2)
    raise TimeoutError(f"ComfyUI prompt {prompt_id} timed out after {timeout}s")


def collect_output_files(entry: dict) -> List[str]:
    out_dir = os.path.join(COMFY, "output")
    files = []
    for node_out in (entry.get("outputs") or {}).values():
        for key, items in node_out.items():
            if not isinstance(items, list):
                continue
            for item in items:
                if not isinstance(item, dict):
                    continue
                name = item.get("filename") or item.get("3d") or item.get("file")
                sub = item.get("subfolder") or ""
                if not name:
                    continue
                path = os.path.join(out_dir, sub, name) if sub else os.path.join(out_dir, name)
                files.append(path)
    return files


def submit(workflow_path: str, timeout: int = 7200) -> dict:
    wf = json.load(open(workflow_path, encoding="utf-8"))
    info = object_info()
    prompt, missing = ui_to_api(wf, info)
    if missing:
        print("missing_nodes", missing, flush=True)
        raise SystemExit(
            "ComfyUI is missing node types: " + ", ".join(missing[:12])
            + (" …" if len(missing) > 12 else "")
        )
    client_id = str(uuid.uuid4())
    body = {"prompt": prompt, "client_id": client_id}
    try:
        res = _http_json("POST", "/prompt", body, timeout=120)
    except urllib.error.HTTPError as e:
        err = e.read().decode("utf-8", "replace")
        raise SystemExit(f"/prompt rejected ({e.code}): {err[:4000]}")
    pid = res.get("prompt_id")
    if not pid:
        raise SystemExit(f"no prompt_id: {res}")
    print("prompt_id", pid, flush=True)
    hist = wait_history(pid, timeout=timeout)
    status = (hist.get("status") or {}).get("status_str") or hist.get("status")
    print("status", status, flush=True)
    files = collect_output_files(hist)
    glbs = [f for f in files if f.lower().endswith(".glb")]
    return {"prompt_id": pid, "status": status, "files": files, "glbs": glbs, "history": hist}


def main() -> int:
    parser = argparse.ArgumentParser(description="Run a patched ComfyUI UI workflow via /prompt")
    parser.add_argument("workflow")
    parser.add_argument("--timeout", type=int, default=7200)
    parser.add_argument("--json-out")
    args = parser.parse_args()
    result = submit(args.workflow, timeout=args.timeout)
    slim = {k: result[k] for k in ("prompt_id", "status", "files", "glbs")}
    print(json.dumps(slim, indent=2))
    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as f:
            json.dump(slim, f, indent=2)
    if not result["glbs"]:
        print("no GLB in ComfyUI outputs; check the workflow export node", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
