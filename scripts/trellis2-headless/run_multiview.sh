#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "$SCRIPT_DIR/common.sh"

MODE="${1:-}"
usage() {
  cat <<'EOF'
Usage:
  run_multiview.sh <texturing|full|mesh_only>

texturing : needs front.png + back.png + mesh_from_singleview.glb
full      : needs front.png + back.png
mesh_only : needs front.png + back.png

Views live in $TRELLIS2_APP/assets/multiview/grace/
First-run defaults: resolution=1024 texture_size=2048 steps=12
On OOM the script retries texture_size=2048 resolution=512.
EOF
}

if [[ -z "$MODE" || "$MODE" == "-h" || "$MODE" == "--help" ]]; then
  usage
  [[ "$MODE" == "-h" || "$MODE" == "--help" ]] && exit 0
  exit 1
fi

GRACE="$TRELLIS2_APP/assets/multiview/grace"
need_files=(front.png back.png)
if [[ "$MODE" == "texturing" ]]; then
  need_files+=(mesh_from_singleview.glb)
fi
missing=()
for f in "${need_files[@]}"; do
  [[ -f "$GRACE/$f" ]] || missing+=("$GRACE/$f")
done
if [[ ${#missing[@]} -gt 0 ]]; then
  echo "run_multiview.sh: missing required views/mesh (will not start ComfyUI job):" >&2
  printf '  %s\n' "${missing[@]}" >&2
  echo "Upload those files then re-run." >&2
  exit 1
fi

WF=""
case "$MODE" in
  texturing) WF="$TRELLIS2_APP/workflows/grace_mv_texturing.json" ;;
  full) WF="$TRELLIS2_APP/workflows/grace_mv_full.json" ;;
  mesh_only) WF="$TRELLIS2_APP/workflows/grace_mv_mesh_only.json" ;;
  *) usage; exit 1 ;;
esac
if [[ ! -f "$WF" ]]; then
  echo "missing workflow $WF (run install_on_gpu.sh first)" >&2
  exit 1
fi

code=$(curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:8188/ || true)
if [[ "$code" == "000" ]]; then
  echo "ComfyUI 8188 is down. Start with: bash $SCRIPT_DIR/launch_web.sh comfy" >&2
  exit 1
fi

# shellcheck disable=SC1091
source /home/ubuntu/miniconda3/etc/profile.d/conda.sh
conda activate trellis2
cd "$TRELLIS2_APP"
STAMP="$(date -u +%Y%m%d_%H%M%S)"
OUT_DIR="$TRELLIS2_APP/output/multiview"
mkdir -p "$OUT_DIR" "$TRELLIS2_APP/logs"
JSON_OUT="$TRELLIS2_APP/logs/comfy_${MODE}_${STAMP}.json"

run_once() {
  python "$SCRIPT_DIR/comfy_run.py" "$WF" --json-out "$JSON_OUT"
}

set +e
run_once
rc=$?
set -e
if [[ $rc -ne 0 ]]; then
  echo "first attempt failed (rc=$rc); retrying resolution=512 if OOM is likely" >&2
  python "$SCRIPT_DIR/patch_workflows.py" --resolution 512 --texture-size 2048 --steps 12
  run_once
fi

GLB="$(python3 - << PY
import json
p=json.load(open("$JSON_OUT"))
glbs=p.get("glbs") or []
print(glbs[0] if glbs else "")
PY
)"
if [[ -z "$GLB" || ! -f "$GLB" ]]; then
  echo "no GLB from ComfyUI" >&2
  exit 2
fi
DEST="$OUT_DIR/grace_mv_${MODE}_${STAMP}.glb"
cp -n "$GLB" "$DEST" || cp "$GLB" "$DEST"
echo "glb_ok $DEST"
if [[ -x "$TRELLIS2_APP/publish_preview.sh" ]]; then
  bash "$TRELLIS2_APP/publish_preview.sh" "$DEST"
else
  echo "publish_skip: $TRELLIS2_APP/publish_preview.sh missing"
fi
echo "preview $PREVIEW_URL"
