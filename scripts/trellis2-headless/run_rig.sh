#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "$SCRIPT_DIR/common.sh"

usage() {
  cat <<'EOF'
Usage:
  run_rig.sh [glb路径]
  Default GLB = newest file in output/multiview or output/projection
  SkinToken: use_transfer=True, bottom_center_origin=True, Mixamo (fallback UE5)
  Needs blender on PATH or SKINTOKEN_BLENDER_BIN, and ComfyUI :8188
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

code=$(curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:8188/ || true)
if [[ "$code" == "000" ]]; then
  echo "ComfyUI 8188 is down" >&2
  exit 1
fi
if ! command -v blender >/dev/null 2>&1 && [[ -z "${SKINTOKEN_BLENDER_BIN:-}" ]]; then
  echo "blender not found; set SKINTOKEN_BLENDER_BIN" >&2
  exit 1
fi
# shellcheck disable=SC1091
source /home/ubuntu/miniconda3/etc/profile.d/conda.sh
conda activate trellis2
python "$SCRIPT_DIR/run_rig.py" "$@"
echo "preview $PREVIEW_URL"
