#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "$SCRIPT_DIR/common.sh"

usage() {
  cat <<'EOF'
Usage:
  run_single.sh [--image PATH] [--stem grace]
  Single-view TRELLIS.2 (official pipeline) → output/grace_{UTC}.glb → publish
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

if [[ ! -f /home/ubuntu/miniconda3/etc/profile.d/conda.sh ]]; then
  echo "missing conda" >&2
  exit 1
fi
# shellcheck disable=SC1091
source /home/ubuntu/miniconda3/etc/profile.d/conda.sh
conda activate trellis2
cd "$TRELLIS2_APP"
python "$SCRIPT_DIR/run_single.py" "$@"
