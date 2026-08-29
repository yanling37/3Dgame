#!/bin/bash
set -euo pipefail
# Back-compat wrapper. Unique GLB names only; does not write sample.glb.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$SCRIPT_DIR/publish_preview.sh" "$@"
