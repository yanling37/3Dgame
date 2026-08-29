#!/usr/bin/env python3
"""Official Trellis2ImageTo3DPipeline → unique timestamped GLB. Never writes sample.glb."""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
from datetime import datetime, timezone

os.environ.setdefault("HF_HUB_OFFLINE", "1")
os.environ.setdefault("TRANSFORMERS_OFFLINE", "1")
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")
os.environ.setdefault("HF_HOME", "/home/ubuntu/trellis2/cache/huggingface")
os.environ.setdefault("OPENCV_IO_ENABLE_OPENEXR", "1")

APP = os.environ.get("TRELLIS2_APP", "/home/ubuntu/trellis2/app")


def utc_stamp() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")


def main() -> int:
    parser = argparse.ArgumentParser(description="Single-view TRELLIS.2 image→GLB")
    parser.add_argument("--image", default=os.path.join(APP, "assets/example_image/grace.png"))
    parser.add_argument("--stem", default="grace")
    parser.add_argument("--texture-size", type=int, default=2048)
    parser.add_argument("--no-publish", action="store_true")
    args = parser.parse_args()
    if not os.path.isfile(args.image):
        raise SystemExit(f"missing image: {args.image}")
    os.chdir(APP)
    sys.path.insert(0, APP)
    from PIL import Image
    from trellis2.pipelines import Trellis2ImageTo3DPipeline
    import o_voxel

    stamp = utc_stamp()
    out_dir = os.path.join(APP, "output")
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, f"{args.stem}_{stamp}.glb")
    if os.path.basename(out) == "sample.glb":
        raise SystemExit("refusing sample.glb")
    print("glb_name", out, flush=True)
    pipe = Trellis2ImageTo3DPipeline.from_pretrained("microsoft/TRELLIS.2-4B")
    pipe.cuda()
    img = Image.open(args.image).convert("RGB")
    mesh = pipe.run(img)[0]
    mesh.simplify(16777216)
    try:
        from snapshot_official import save_snapshots_from_mesh
        save_snapshots_from_mesh(mesh, out[:-4])
    except Exception as e:
        print("snapshot_skip", e, flush=True)
    glb = o_voxel.postprocess.to_glb(
        vertices=mesh.vertices,
        faces=mesh.faces,
        attr_volume=mesh.attrs,
        coords=mesh.coords,
        attr_layout=mesh.layout,
        voxel_size=mesh.voxel_size,
        aabb=[[-0.5, -0.5, -0.5], [0.5, 0.5, 0.5]],
        decimation_target=1000000,
        texture_size=args.texture_size,
        remesh=True,
        remesh_band=1,
        remesh_project=0,
        verbose=True,
    )
    glb.export(out, extension_webp=True)
    print("glb_ok", out, flush=True)
    if not args.no_publish:
        pub = os.path.join(APP, "publish_preview.sh")
        if os.path.isfile(pub):
            subprocess.check_call(["bash", pub, out])
        else:
            print("publish_skip missing", pub, flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
