#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "$SCRIPT_DIR/common.sh"

usage() {
  cat <<'EOF'
Usage:
  run_and_publish.sh <single|texturing|full|mesh_only|projection|rig> [args...]

Prints https://yanling3d.duckdns.org/trellis/ after a successful publish.
EOF
}

CMD="${1:-}"
if [[ -z "$CMD" || "$CMD" == "-h" || "$CMD" == "--help" ]]; then
  usage
  [[ "$CMD" == "-h" || "$CMD" == "--help" ]] && exit 0
  exit 1
fi
shift || true
case "$CMD" in
  single) bash "$SCRIPT_DIR/run_single.sh" "$@" ;;
  texturing|full|mesh_only) bash "$SCRIPT_DIR/run_multiview.sh" "$CMD" "$@" ;;
  projection) bash "$SCRIPT_DIR/run_projection.sh" "$@" ;;
  rig) bash "$SCRIPT_DIR/run_rig.sh" "$@" ;;
  *) usage; exit 1 ;;
esac
echo "preview $PREVIEW_URL"
