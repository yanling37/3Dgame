#!/bin/bash
set -euo pipefail
# Publish uniquely named GLBs + official snapshots + preview page to Pixel Bite.
# GPU → ec2-user@172.31.29.43 → sudo /var/www/trellis/
# Does not touch game site / or /api/, does not rsync --delete, does not serve from GPU EIP.

APP="${TRELLIS2_APP:-/home/ubuntu/trellis2/app}"
KEY="${PIXELBITE_KEY:-/home/ubuntu/.ssh/pixelbite.pem}"
HOST="${PIXELBITE_HOST:-ec2-user@172.31.29.43}"
DEST="/var/www/trellis"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

ssh_pb() { ssh -i "$KEY" -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new "$HOST" "$@"; }
scp_pb() { scp -i "$KEY" -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new "$@"; }

python3 "$SCRIPT_DIR/build_models_json.py" \
  --dir "$APP" \
  --dir "$APP/output/multiview" \
  --dir "$APP/output/rigged" \
  --dir "$APP/output/projection" \
  --out "$APP/models.json"

STAGE="$(mktemp -d /tmp/trellis-pub.XXXXXX)"
mkdir -p "$STAGE/icons"

HTML_SRC="$SCRIPT_DIR/web/index.html"
if [[ ! -f "$HTML_SRC" && -f "$APP/web/index.html" ]]; then
  HTML_SRC="$APP/web/index.html"
fi
cp "$HTML_SRC" "$STAGE/index.html"
cp "$APP/models.json" "$STAGE/models.json"

for icon in normal.png clay.png basecolor.png hdri_forest.png hdri_sunset.png hdri_courtyard.png; do
  cp "$APP/assets/app/$icon" "$STAGE/icons/$icon"
done

copy_model_assets() {
  local src="$1"
  local base
  base="$(basename "$src")"
  if [[ "$base" == "sample.glb" ]]; then
    echo "skip sample.glb" >&2
    return
  fi
  if [[ -f "$src" ]]; then
    cp -n "$src" "$STAGE/$base" 2>/dev/null || cp "$src" "$STAGE/$base"
  fi
  local prefix="${base%.glb}"
  local dir
  dir="$(cd "$(dirname "$src")" && pwd)"
  shopt -s nullglob
  for f in "$dir/$prefix"_*.png "$dir/${prefix}_snapshot_meta.json"; do
    [[ -e "$f" ]] || continue
    cp "$f" "$STAGE/$(basename "$f")"
  done
  shopt -u nullglob
}

if [[ $# -eq 0 ]]; then
  shopt -s nullglob
  for g in "$APP"/*.glb; do
    copy_model_assets "$g"
  done
  shopt -u nullglob
else
  for arg in "$@"; do
    if [[ -f "$arg" ]]; then
      copy_model_assets "$arg"
    elif [[ -f "$APP/$arg" ]]; then
      copy_model_assets "$APP/$arg"
    else
      echo "missing $arg" >&2
      exit 1
    fi
  done
fi

# Never clobber unique GLBs that already exist on the relay with a different file
# of the same name: scp into /tmp then sudo cp -n for GLBs, regular cp for html/png.
REMOTE_TMP="/tmp/trellis-pub.$$"
ssh_pb "mkdir -p $REMOTE_TMP"
scp_pb -r "$STAGE/." "$HOST:$REMOTE_TMP/"
ssh_pb "sudo mkdir -p $DEST/icons && \
  sudo cp -a $REMOTE_TMP/index.html $REMOTE_TMP/models.json $DEST/ && \
  sudo cp -a $REMOTE_TMP/icons/. $DEST/icons/ && \
  sudo find $REMOTE_TMP -maxdepth 1 -type f \\( -name '*.png' -o -name '*_snapshot_meta.json' \\) -exec sudo cp -a {} $DEST/ \\; && \
  sudo find $REMOTE_TMP -maxdepth 1 -type f -name '*.glb' -exec sudo cp -n {} $DEST/ \\; && \
  sudo chmod -R a+rX $DEST && \
  rm -rf $REMOTE_TMP"
rm -rf "$STAGE"
echo "published https://yanling3d.duckdns.org/trellis/"
