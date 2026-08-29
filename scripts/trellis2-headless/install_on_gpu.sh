#!/usr/bin/env bash
# Idempotent TRELLIS.2 GPU install: cache mount, ComfyUI+nodes, workflows, scripts, blender.
# Does not mkfs, does not upgrade PyTorch/CUDA, does not touch DINOv3 layer patch.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "$SCRIPT_DIR/common.sh"

usage() {
  cat <<'EOF'
Usage:
  install_on_gpu.sh [--skip-comfy-pip] [--skip-web]
Install ComfyUI into Instance Store cache, symlink DINOv3, patch workflows, copy scripts.
EOF
}
if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then usage; exit 0; fi

SKIP_COMFY_PIP=0
SKIP_WEB=0
for a in "$@"; do
  case "$a" in
    --skip-comfy-pip) SKIP_COMFY_PIP=1 ;;
    --skip-web) SKIP_WEB=1 ;;
  esac
done

echo "== mount cache =="
if ! mount | grep -q "on /home/ubuntu/trellis2/cache "; then
  sudo mount /dev/nvme1n1 /home/ubuntu/trellis2/cache || {
    echo "WARN: could not mount /dev/nvme1n1 (Instance Store empty after Stop/Start?)" >&2
    mkdir -p /home/ubuntu/trellis2/cache
  }
fi
df -h /home/ubuntu/trellis2/cache / || true

mkdir -p "$TRELLIS2_APP"/{logs,workflows,scripts} \
  "$TRELLIS2_APP/assets/multiview/grace" \
  "$TRELLIS2_APP/output"/{multiview,rigged,projection} \
  "$TRELLIS2_CACHE"

# shellcheck disable=SC1091
source /home/ubuntu/miniconda3/etc/profile.d/conda.sh
conda activate trellis2
python - <<'PY'
import torch, sys
print("torch", torch.__version__, "cuda", torch.version.cuda)
if "2.6.0" not in torch.__version__:
    sys.exit("refusing to continue: torch is not 2.6.0+cu124")
PY

echo "== copy scripts into app =="
rsync -a --exclude '*.pyc' "$SCRIPT_DIR/" "$TRELLIS2_APP/scripts/"
chmod +x "$TRELLIS2_APP/scripts/"*.sh "$TRELLIS2_APP/scripts/"*.py || true
# preview helpers used by launch_gradio / snapshots
if [[ -d "$SCRIPT_DIR/../trellis2-preview" ]]; then
  cp -n "$SCRIPT_DIR/../trellis2-preview/"*.py "$TRELLIS2_APP/" 2>/dev/null || \
    cp "$SCRIPT_DIR/../trellis2-preview/"hdri_utils.py "$TRELLIS2_APP/" || true
  if [[ -f "$SCRIPT_DIR/../trellis2-preview/publish_preview.sh" ]]; then
    cp "$SCRIPT_DIR/../trellis2-preview/publish_preview.sh" "$TRELLIS2_APP/publish_preview.sh"
    cp "$SCRIPT_DIR/../trellis2-preview/publish_glb.sh" "$TRELLIS2_APP/publish_glb.sh"
    chmod +x "$TRELLIS2_APP/publish_preview.sh" "$TRELLIS2_APP/publish_glb.sh"
    mkdir -p "$TRELLIS2_APP/web"
    cp "$SCRIPT_DIR/../trellis2-preview/web/index.html" "$TRELLIS2_APP/web/index.html"
  fi
fi

echo "== grace mesh placeholder =="
shopt -s nullglob
latest=$(ls -1t "$TRELLIS2_APP"/grace_*.glb 2>/dev/null | head -1 || true)
shopt -u nullglob
if [[ -n "${latest:-}" ]]; then
  cp -n "$latest" "$TRELLIS2_APP/assets/multiview/grace/mesh_from_singleview.glb" || true
  echo "mesh_from_singleview <- $latest"
else
  echo "no grace_*.glb yet; texturing will wait for upload"
fi

echo "== ComfyUI clone =="
cd "$TRELLIS2_CACHE"
# Pin v0.30.2: later tags require comfy-kitchen APIs that need torch>2.6 (list[int] infer_schema / CUDA 13).
if [[ ! -d comfyui/.git ]]; then
  git clone --depth 1 --branch v0.30.2 https://github.com/comfyanonymous/ComfyUI.git comfyui
else
  git -C comfyui fetch --depth 1 origin tag v0.30.2 || git -C comfyui fetch --depth 1 origin v0.30.2
  git -C comfyui checkout -f v0.30.2 || git -C comfyui checkout -f FETCH_HEAD
fi
cd comfyui
if [[ ! -d custom_nodes/ComfyUI-Trellis2/.git ]]; then
  git clone --depth 1 https://github.com/visualbruno/ComfyUI-Trellis2.git custom_nodes/ComfyUI-Trellis2
fi
if [[ ! -d custom_nodes/ComfyUI-SkinToken/.git ]]; then
  git clone --depth 1 https://github.com/Rizzlord/ComfyUI-SkinToken.git custom_nodes/ComfyUI-SkinToken
fi

echo "== DINOv3 symlink =="
mkdir -p models/facebook
ln -sfn /home/ubuntu/trellis2/app/checkpoints/dinov3-vitl16-pretrain-lvd1689m \
  models/facebook/dinov3-vitl16-pretrain-lvd1689m
readlink -f models/facebook/dinov3-vitl16-pretrain-lvd1689m
test -f models/facebook/dinov3-vitl16-pretrain-lvd1689m/config.json
ln -sfn "$TRELLIS2_APP/assets/multiview/grace" "$COMFYUI_DIR/input/grace"

echo "== ComfyUI + torch 2.6: kitchen import must not crash =="
# Latest comfy-kitchen uses list[int] which torch 2.6 infer_schema rejects (ValueError).
# Do not upgrade PyTorch. Treat kitchen as optional (fp8/fp4 only).
python - <<'PY'
from pathlib import Path
p = Path("/home/ubuntu/trellis2/cache/comfyui/comfy/quant_ops.py")
t = p.read_text(encoding="utf-8")
old = 'except ImportError as e:\n    logging.error(f"Failed to import comfy_kitchen'
new = 'except Exception as e:\n    logging.error(f"Failed to import comfy_kitchen'
if old not in t:
    print("quant_ops kitchen except already patched or missing")
else:
    p.write_text(t.replace(old, new, 1), encoding="utf-8")
    print("patched", p, "except Exception for comfy_kitchen")
PY

echo "== keep trellis2 torch: drop CUDA13 torchaudio if a previous pip pulled it =="
CONSTRAINT="$TRELLIS2_APP/logs/pip-torch-constraint.txt"
mkdir -p "$TRELLIS2_APP/logs"
printf '%s\n' "torch==2.6.0" "torchvision==0.21.0" "torchaudio==2.6.0" > "$CONSTRAINT"
python - <<'PY'
import importlib.metadata as m, subprocess, sys
try:
    v = m.version("torchaudio")
except Exception:
    print("trellis2_no_torchaudio")
    raise SystemExit(0)
print("trellis2_torchaudio", v)
if not str(v).startswith("2.6"):
    subprocess.check_call([sys.executable, "-m", "pip", "uninstall", "-y", "torchaudio"])
    print("uninstalled_mismatched_torchaudio", v)
PY
# Matching cu124 torchaudio so transformers 5 can import DINOv3 without libcudart.so.13.
python -m pip install -c "$CONSTRAINT" --index-url https://download.pytorch.org/whl/cu124 torchaudio==2.6.0
python - <<'PY'
import torch, sys
print("trellis2_torch", torch.__version__, "cuda", torch.version.cuda)
if "2.6.0" not in torch.__version__:
    sys.exit("trellis2 PyTorch changed; aborting")
PY

echo "== ComfyUI pip into conda env skintoken (Python 3.11); do not touch trellis2 torch =="
SKIN_ENV="/home/ubuntu/miniconda3/envs/skintoken"
echo "constraint $(tr '\n' ' ' < "$CONSTRAINT")"
if [[ "$SKIP_COMFY_PIP" != "1" ]]; then
  if [[ ! -x "$SKIN_ENV/bin/python" ]]; then
    conda create -y -n skintoken python=3.11 pip
  fi
  "$SKIN_ENV/bin/pip" install torch==2.6.0 torchvision==0.21.0 torchaudio==2.6.0 \
    --index-url https://download.pytorch.org/whl/cu124
  "$SKIN_ENV/bin/python" - <<'PY'
import torch, sys
print("skintoken_torch", torch.__version__, "cuda", torch.version.cuda)
if "2.6.0" not in torch.__version__ or "cu124" not in torch.__version__:
    sys.exit("skintoken torch is not 2.6.0+cu124")
PY
  CU124="--extra-index-url https://download.pytorch.org/whl/cu124"
  "$SKIN_ENV/bin/pip" install -c "$CONSTRAINT" $CU124 -r "$COMFYUI_DIR/requirements.txt"
  "$SKIN_ENV/bin/pip" install -c "$CONSTRAINT" $CU124 -r "$COMFYUI_DIR/custom_nodes/ComfyUI-Trellis2/requirements.txt" || \
    echo "WARN: ComfyUI-Trellis2 requirements incomplete"
  "$SKIN_ENV/bin/pip" install -c "$CONSTRAINT" $CU124 -r "$COMFYUI_DIR/custom_nodes/ComfyUI-SkinToken/requirements.txt" || \
    echo "WARN: SkinToken requirements incomplete"
  # ComfyUI itself runs in trellis2 (flash_attn/o_voxel). Install node-light deps there too.
  pip install -c "$CONSTRAINT" $CU124 -r "$COMFYUI_DIR/requirements.txt" || true
  pip install -c "$CONSTRAINT" $CU124 -r "$COMFYUI_DIR/custom_nodes/ComfyUI-Trellis2/requirements.txt" || true
  pip install -c "$CONSTRAINT" $CU124 -r "$COMFYUI_DIR/custom_nodes/ComfyUI-SkinToken/requirements.txt" || true
  python - <<'PY'
import torch, sys
print("trellis2_torch_after_comfy_pip", torch.__version__, "cuda", torch.version.cuda)
if "2.6.0" not in torch.__version__:
    sys.exit("trellis2 PyTorch changed; aborting")
PY
  "$SKIN_ENV/bin/python" - <<'PY'
import torch, sys
print("skintoken_torch_after_pip", torch.__version__, "cuda", torch.version.cuda)
if "2.6.0" not in torch.__version__ or "cu124" not in torch.__version__:
    sys.exit("skintoken PyTorch changed; aborting")
try:
    import torchaudio
    print("skintoken_torchaudio", torchaudio.__version__)
except Exception as e:
    print("skintoken_torchaudio_skip", type(e).__name__, e)
PY
fi

echo "== blender =="
if ! command -v blender >/dev/null 2>&1; then
  sudo apt-get update -qq
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y blender || true
fi
command -v blender && blender --version | head -1 || echo "blender missing; set SKINTOKEN_BLENDER_BIN"

echo "== patch workflows =="
python "$TRELLIS2_APP/scripts/patch_workflows.py" --resolution 1024 --texture-size 2048 --steps 12

echo "== 4B offline check (Instance Store may be empty after Stop) =="
# Official package lives at TRELLIS2_APP (not pip-installed as trellis2).
cd "$TRELLIS2_APP"
export PYTHONPATH="$TRELLIS2_APP${PYTHONPATH:+:$PYTHONPATH}"
export HF_HUB_OFFLINE=1 TRANSFORMERS_OFFLINE=1 HF_HOME="$HF_HOME"
set +e
python - <<'PY'
import os, sys
print("HF_HOME", os.environ.get("HF_HOME"))
print("cwd", os.getcwd(), "path0", sys.path[0])
try:
    from trellis2.pipelines import Trellis2ImageTo3DPipeline
    p=Trellis2ImageTo3DPipeline.from_pretrained("microsoft/TRELLIS.2-4B")
    print("4B_offline_ok", type(p))
except Exception as e:
    print("4B_offline_fail", type(e).__name__, e)
    raise SystemExit(7)
PY
rc=$?
set -e
if [[ $rc -eq 7 ]]; then
  echo "4B missing from cache (expected after Stop/Start wipes Instance Store)."
  echo "Briefly allowing HF download into $HF_HOME then restoring offline."
  export HF_HUB_OFFLINE=0 TRANSFORMERS_OFFLINE=0
  python - <<'PY'
from trellis2.pipelines import Trellis2ImageTo3DPipeline
p=Trellis2ImageTo3DPipeline.from_pretrained("microsoft/TRELLIS.2-4B")
print("4B_redownload_ok")
PY
  export HF_HUB_OFFLINE=1 TRANSFORMERS_OFFLINE=1
fi

echo "== texturing_pipeline.json (small; ImageTo3D from_pretrained does not pull it) =="
python - <<'PY'
import os
from pathlib import Path
from huggingface_hub import hf_hub_download, try_to_load_from_cache
need = "texturing_pipeline.json"
cached = try_to_load_from_cache("microsoft/TRELLIS.2-4B", need)
if cached:
    print("texturing_pipeline_cached", cached)
else:
    os.environ["HF_HUB_OFFLINE"] = "0"
    os.environ["TRANSFORMERS_OFFLINE"] = "0"
    p = hf_hub_download("microsoft/TRELLIS.2-4B", need)
    print("texturing_pipeline_downloaded", p)
    os.environ["HF_HUB_OFFLINE"] = "1"
    os.environ["TRANSFORMERS_OFFLINE"] = "1"
PY

echo "== symlink HF snapshots into ComfyUI models (no second 4B copy) =="
python - <<'PY'
import os, pathlib
hub = pathlib.Path(os.environ["HF_HOME"]) / "hub"
comfy = pathlib.Path(os.environ["COMFYUI_DIR"]) / "models"
mapping = {
    "models--microsoft--TRELLIS.2-4B": comfy / "microsoft" / "TRELLIS.2-4B",
    "models--microsoft--TRELLIS-image-large": comfy / "microsoft" / "TRELLIS-image-large",
    "models--briaai--RMBG-2.0": comfy / "briaai" / "RMBG-2.0",
}
for repo, dest in mapping.items():
    refs = hub / repo / "refs" / "main"
    snap_dir = hub / repo / "snapshots"
    if refs.is_file():
        rev = refs.read_text().strip()
        src = snap_dir / rev
    else:
        snaps = sorted(snap_dir.glob("*")) if snap_dir.is_dir() else []
        src = snaps[-1] if snaps else None
    if src is None or not src.is_dir():
        print("symlink_skip", repo)
        continue
    dest.parent.mkdir(parents=True, exist_ok=True)
    if dest.is_symlink() or dest.exists():
        if dest.is_symlink() and os.path.realpath(dest) == os.path.realpath(src):
            print("symlink_ok", dest, "->", src)
            continue
    dest.unlink(missing_ok=True) if dest.is_symlink() else None
    if dest.exists():
        print("symlink_keep_existing_dir", dest)
        continue
    os.symlink(src, dest)
    print("symlink", dest, "->", src)
PY

if [[ "$SKIP_WEB" != "1" ]]; then
  bash "$TRELLIS2_APP/scripts/launch_web.sh" all || true
fi

echo "== install done =="
echo "scripts: $TRELLIS2_APP/scripts"
echo "comfy:   $COMFYUI_DIR"
echo "logs:    $TRELLIS2_APP/logs"
