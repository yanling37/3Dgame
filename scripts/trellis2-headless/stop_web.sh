#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "$SCRIPT_DIR/common.sh"
usage() { echo "Usage: stop_web.sh  # stops 7860/7861/8188 pidfiles"; }
if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then usage; exit 0; fi
for pf in app_7860.pid app_7861.pid comfyui_8188.pid; do
  f="$TRELLIS2_APP/logs/$pf"
  if [[ -f "$f" ]]; then
    pid="$(cat "$f")"
    if kill -0 "$pid" 2>/dev/null; then
      kill "$pid" || true
      echo "stopped $pf pid=$pid"
    fi
    rm -f "$f"
  fi
done
