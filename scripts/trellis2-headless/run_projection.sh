#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "$SCRIPT_DIR/common.sh"

usage() {
  cat <<'EOF'
Usage:
  run_projection.sh
  Needs a GLB + Grace views (at least a edited back.png).
  Runs workflows/grace_projection_qwen.json via ComfyUI.
  Exits 2 if Qwen subgraph nodes/weights are missing.
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

WF="$TRELLIS2_APP/workflows/grace_projection_qwen.json"
GRACE="$TRELLIS2_APP/assets/multiview/grace"
if [[ ! -f "$WF" ]]; then
  echo "missing $WF" >&2
  exit 2
fi

# UUID subgraph (Qwen image pack) is the blocker; report 待 Qwen even before views exist.
# shellcheck disable=SC1091
source /home/ubuntu/miniconda3/etc/profile.d/conda.sh
conda activate trellis2
python3 - "$WF" <<'PY'
import json, sys
wf=json.load(open(sys.argv[1]))
uuids=[]
for n in wf.get("nodes") or []:
    t=n.get("type") or ""
    if len(t)>=32 and t.count("-")>=4:
        uuids.append(t)
if uuids:
    print("Projection workflow still contains subgraph/Qwen node ids:")
    print("\n".join(uuids))
    print("Do not download extra Qwen image weights. Status: 待 Qwen")
    sys.exit(2)
PY

if [[ ! -f "$GRACE/back.png" ]]; then
  echo "missing $GRACE/back.png (need at least a fixed back view)" >&2
  exit 1
fi
if [[ ! -f "$GRACE/mesh_from_singleview.glb" ]]; then
  echo "missing $GRACE/mesh_from_singleview.glb" >&2
  exit 1
fi

code=$(curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:8188/ || true)
if [[ "$code" == "000" ]]; then
  echo "ComfyUI 8188 is down" >&2
  exit 1
fi

STAMP="$(date -u +%Y%m%d_%H%M%S)"
OUT_DIR="$TRELLIS2_APP/output/projection"
mkdir -p "$OUT_DIR" "$TRELLIS2_APP/logs"
JSON_OUT="$TRELLIS2_APP/logs/comfy_proj_${STAMP}.json"
set +e
python "$SCRIPT_DIR/comfy_run.py" "$WF" --json-out "$JSON_OUT"
rc=$?
set -e
if [[ $rc -ne 0 ]]; then
  echo "Projection /prompt failed. Typical cause: missing Qwen custom nodes or weights." >&2
  echo "Status: 待 Qwen — do not download extra large Qwen checkpoints unless you ask." >&2
  exit 2
fi
GLB="$(python3 - << PY
import json
p=json.load(open("$JSON_OUT"))
glbs=p.get("glbs") or []
print(glbs[0] if glbs else "")
PY
)"
DEST="$OUT_DIR/grace_proj_${STAMP}.glb"
cp "$GLB" "$DEST"
echo "glb_ok $DEST"
if [[ -x "$TRELLIS2_APP/publish_preview.sh" ]]; then
  bash "$TRELLIS2_APP/publish_preview.sh" "$DEST"
fi
echo "preview $PREVIEW_URL"
