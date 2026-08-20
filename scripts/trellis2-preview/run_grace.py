#!/usr/bin/env python3
"""Generate a uniquely named grace GLB and official snapshots. Never overwrites existing GLBs."""
import os

os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["TRANSFORMERS_OFFLINE"] = "1"
os.environ["PYTORCH_CUDA_ALLOC_CONF"] = "expandable_segments:True"
os.environ.setdefault("HF_HOME", "/home/ubuntu/trellis2/cache/huggingface")
os.environ.setdefault("OPENCV_IO_ENABLE_OPENEXR", "1")

from datetime import datetime, timezone
from PIL import Image
from trellis2.pipelines import Trellis2ImageTo3DPipeline
import o_voxel

from snapshot_official import save_snapshots_from_mesh

stamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
out = f"/home/ubuntu/trellis2/app/grace_{stamp}.glb"
if os.path.exists(out):
    raise SystemExit(f"refusing to overwrite {out}")
print("glb_name", out, flush=True)

print("load_pipeline", flush=True)
pipe = Trellis2ImageTo3DPipeline.from_pretrained("microsoft/TRELLIS.2-4B")
pipe.cuda()
print("pipeline_ready", flush=True)

img = Image.open("assets/example_image/grace.png").convert("RGB")
print("image", img.size, flush=True)
mesh = pipe.run(img)[0]
print("run_ok", type(mesh), flush=True)
mesh.simplify(16777216)
print("simplify_ok", flush=True)

prefix = out[:-4]
print("snapshot_start", prefix, flush=True)
save_snapshots_from_mesh(mesh, prefix)
print("snapshot_ok", flush=True)

glb = o_voxel.postprocess.to_glb(
    vertices=mesh.vertices,
    faces=mesh.faces,
    attr_volume=mesh.attrs,
    coords=mesh.coords,
    attr_layout=mesh.layout,
    voxel_size=mesh.voxel_size,
    aabb=[[-0.5, -0.5, -0.5], [0.5, 0.5, 0.5]],
    decimation_target=1000000,
    texture_size=4096,
    remesh=True,
    remesh_band=1,
    remesh_project=0,
    verbose=True,
)
glb.export(out, extension_webp=True)
print("glb_ok", out, flush=True)
