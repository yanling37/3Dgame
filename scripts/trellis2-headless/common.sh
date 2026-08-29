#!/usr/bin/env bash
# Shared env for TRELLIS.2 GPU scripts. Source this; do not execute.
export TRELLIS2_APP="${TRELLIS2_APP:-/home/ubuntu/trellis2/app}"
export TRELLIS2_CACHE="${TRELLIS2_CACHE:-/home/ubuntu/trellis2/cache}"
export HF_HOME="${HF_HOME:-/home/ubuntu/trellis2/cache/huggingface}"
export HF_HUB_OFFLINE="${HF_HUB_OFFLINE:-1}"
export TRANSFORMERS_OFFLINE="${TRANSFORMERS_OFFLINE:-1}"
export PYTORCH_CUDA_ALLOC_CONF="${PYTORCH_CUDA_ALLOC_CONF:-expandable_segments:True}"
export OPENCV_IO_ENABLE_OPENEXR="${OPENCV_IO_ENABLE_OPENEXR:-1}"
export COMFYUI_DIR="${COMFYUI_DIR:-/home/ubuntu/trellis2/cache/comfyui}"
export SKINTOKEN_FORCE_HEADLESS="${SKINTOKEN_FORCE_HEADLESS:-1}"
if [[ -n "${SKINTOKEN_BLENDER_BIN:-}" ]]; then
  export SKINTOKEN_BLENDER_BIN
elif command -v blender >/dev/null 2>&1; then
  export SKINTOKEN_BLENDER_BIN="$(command -v blender)"
fi
export PREVIEW_URL="https://yanling3d.duckdns.org/trellis/"
export PIXELBITE_HOST="${PIXELBITE_HOST:-ec2-user@172.31.29.43}"
export PIXELBITE_KEY="${PIXELBITE_KEY:-/home/ubuntu/.ssh/pixelbite.pem}"
