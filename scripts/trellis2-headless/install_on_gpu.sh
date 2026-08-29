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
if [[ ! -d comfyui/.git ]]; then
  git clone --depth 1 https://github.com/comfyanonymous/ComfyUI.git comfyui
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

echo "== pip (constraint: do not change torch) =="
CONSTRAINT="$TRELLIS2_APP/logs/pip-torch-constraint.txt"
python - <<PY
import torch, pathlib
p=pathlib.Path("$CONSTRAINT")
p.write_text(f"torch=={torch.__version__.split('+')[0]}\n")
print("constraint", p.read_text())
PY
if [[ "$SKIP_COMFY_PIP" != "1" ]]; then
  pip install -c "$CONSTRAINT" -r requirements.txt
  pip install -c "$CONSTRAINT" -r custom_nodes/ComfyUI-Trellis2/requirements.txt || \
    echo "WARN: ComfyUI-Trellis2 requirements incomplete; reuse trellis2 env packages"
  pip install -c "$CONSTRAINT" -r custom_nodes/ComfyUI-SkinToken/requirements.txt || \
    echo "WARN: SkinToken requirements incomplete"
  python - <<'PY'
import torch, sys
print("torch_after_pip", torch.__version__)
if "2.6.0" not in torch.__version__:
    sys.exit("PyTorch changed; aborting")
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
export HF_HUB_OFFLINE=1 TRANSFORMERS_OFFLINE=1 HF_HOME="$HF_HOME"
set +e
python - <<'PY'
from transformers.utils.hub import cached_file
import os
print("HF_HOME", os.environ.get("HF_HOME"))
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

if [[ "$SKIP_WEB" != "1" ]]; then
  bash "$TRELLIS2_APP/scripts/launch_web.sh" all || true
fi

echo "== install done =="
echo "scripts: $TRELLIS2_APP/scripts"
echo "comfy:   $COMFYUI_DIR"
echo "logs:    $TRELLIS2_APP/logs"
