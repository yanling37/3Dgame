#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "$SCRIPT_DIR/common.sh"

usage() {
  cat <<'EOF'
Usage:
  launch_web.sh [all|app|tex|comfy]
  Starts Gradio 7860, Gradio texturing 7861, and/or ComfyUI 8188.
  Logs: $TRELLIS2_APP/logs/app_7860.log app_7861.log comfyui_8188.log
EOF
}

WHAT="${1:-all}"
if [[ "$WHAT" == "-h" || "$WHAT" == "--help" ]]; then
  usage
  exit 0
fi

mkdir -p "$TRELLIS2_APP/logs"
# shellcheck disable=SC1091
source /home/ubuntu/miniconda3/etc/profile.d/conda.sh
conda activate trellis2

start_one() {
  local name="$1" log="$2" pidfile="$3"
  shift 3
  if [[ -f "$pidfile" ]] && kill -0 "$(cat "$pidfile")" 2>/dev/null; then
    echo "$name already running pid=$(cat "$pidfile")"
    return 0
  fi
  nohup "$@" >"$log" 2>&1 &
  echo $! >"$pidfile"
  echo "started $name pid=$! log=$log"
}

cd "$TRELLIS2_APP"
export PYTHONUNBUFFERED=1

if [[ "$WHAT" == "all" || "$WHAT" == "app" ]]; then
  start_one app "$TRELLIS2_APP/logs/app_7860.log" "$TRELLIS2_APP/logs/app_7860.pid" \
    python "$SCRIPT_DIR/launch_gradio.py" --listen 0.0.0.0 --port 7860 --app app.py
fi
if [[ "$WHAT" == "all" || "$WHAT" == "tex" ]]; then
  start_one tex "$TRELLIS2_APP/logs/app_7861.log" "$TRELLIS2_APP/logs/app_7861.pid" \
    python "$SCRIPT_DIR/launch_gradio.py" --listen 0.0.0.0 --port 7861 --app app_texturing.py
fi
if [[ "$WHAT" == "all" || "$WHAT" == "comfy" ]]; then
  if [[ ! -d "$COMFYUI_DIR" ]]; then
    echo "missing $COMFYUI_DIR; run install_on_gpu.sh" >&2
    exit 1
  fi
  cd "$COMFYUI_DIR"
  start_one comfy "$TRELLIS2_APP/logs/comfyui_8188.log" "$TRELLIS2_APP/logs/comfyui_8188.pid" \
    python main.py --listen 0.0.0.0 --port 8188 --disable-auto-launch
  cd "$TRELLIS2_APP"
fi

sleep 2
echo "curl 7860=$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:7860/ || echo 000)"
echo "curl 7861=$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:7861/ || echo 000)"
echo "curl 8188=$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8188/ || echo 000)"
echo "listen 0.0.0.0:7860 0.0.0.0:7861 0.0.0.0:8188 (open SG or use Pixel Bite reverse proxy; do not use ephemeral public IPs)"
