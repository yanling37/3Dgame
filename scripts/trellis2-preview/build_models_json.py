#!/usr/bin/env python3
"""Build models.json for the official TRELLIS.2 snapshot preview page."""
from __future__ import annotations

import argparse
import json
import os
import re
from typing import List

MODE_DEFS = [
    {"key": "normal", "name": "Normal", "icon": "icons/normal.png", "suffix": "normal", "hdri": False},
    {"key": "clay", "name": "Clay render", "icon": "icons/clay.png", "suffix": "clay", "hdri": False},
    {"key": "base_color", "name": "Base color", "icon": "icons/basecolor.png", "suffix": "base_color", "hdri": False},
    {"key": "shaded_forest", "name": "HDRI forest", "icon": "icons/hdri_forest.png", "suffix": "hdri_forest", "hdri": True},
    {"key": "shaded_sunset", "name": "HDRI sunset", "icon": "icons/hdri_sunset.png", "suffix": "hdri_sunset", "hdri": True},
    {"key": "shaded_courtyard", "name": "HDRI courtyard", "icon": "icons/hdri_courtyard.png", "suffix": "hdri_courtyard", "hdri": True},
]
STAMP_RE = re.compile(r"_(\d{8})_(\d{4,6})$")


def _views(prefix: str, suffix: str, search_dirs: List[str], nviews: int = 8) -> List[str]:
    out = []
    for i in range(nviews):
        name = f"{prefix}_{suffix}_s{i}.png"
        if any(os.path.isfile(os.path.join(d, name)) for d in search_dirs):
            out.append(name)
    return out


def _exists(name: str, search_dirs: List[str]) -> bool:
    return any(os.path.isfile(os.path.join(d, name)) for d in search_dirs)


def build(search_dirs: List[str]) -> dict:
    glbs = {}
    for d in search_dirs:
        if not os.path.isdir(d):
            continue
        for name in os.listdir(d):
            if not name.endswith(".glb") or name == "sample.glb":
                continue
            glbs[name] = os.path.join(d, name)
    models = []
    for name in glbs:
        prefix = name[:-4]
        snapshots = {}
        hdri_ok = True
        hdri_missing = []
        for mode in MODE_DEFS:
            suffix = mode["suffix"]
            main = f"{prefix}_{suffix}.png"
            views = _views(prefix, suffix, search_dirs)
            if _exists(main, search_dirs):
                snapshots[mode["key"]] = {"file": main, "views": views, "ready": True}
            else:
                snapshots[mode["key"]] = {
                    "file": None,
                    "views": views,
                    "ready": False,
                    "reason": "EXR 未就绪" if mode["hdri"] else "snapshot 未生成",
                }
                if mode["hdri"]:
                    hdri_ok = False
                    hdri_missing.append(mode["name"])
                # non-hdri missing is fine to list
        models.append({
            "id": prefix,
            "label": prefix,
            "file": name,
            "glb": name,
            "snapshots": snapshots,
            "hdri_ready": hdri_ok and all(snapshots[m["key"]]["ready"] for m in MODE_DEFS if m["hdri"]),
            "hdri_missing": hdri_missing,
        })

    def sort_key(m):
        match = STAMP_RE.search(m["id"])
        if not match:
            return ("", m["id"])
        date, tod = match.group(1), match.group(2).ljust(6, "0")
        return (date + tod, m["id"])

    models.sort(key=sort_key, reverse=True)
    return {
        "default_mode": "shaded_forest",
        "fallback_mode": "normal",
        "default_step": 3,
        "modes": MODE_DEFS,
        "models": models,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dir", action="append", dest="dirs", help="Scan directory (repeatable)")
    parser.add_argument("--out", required=True)
    args = parser.parse_args()
    dirs = args.dirs or ["/home/ubuntu/trellis2/app"]
    data = build(dirs)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)) or ".", exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print("models", len(data["models"]), "->", args.out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
