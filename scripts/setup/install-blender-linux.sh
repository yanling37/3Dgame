#!/usr/bin/env bash
# Install Blender 4.2 LTS into ~/tools and link ~/bin/blender
set -euo pipefail

VERSION="4.2.9"
URL="https://download.blender.org/release/Blender4.2/blender-${VERSION}-linux-x64.tar.xz"
TOOLS_DIR="${HOME}/tools"
BIN_DIR="${HOME}/bin"
TARGET="${TOOLS_DIR}/blender-${VERSION}-linux-x64"
ARCHIVE="/tmp/blender-${VERSION}-linux-x64.tar.xz"

mkdir -p "$TOOLS_DIR" "$BIN_DIR"

if [[ -x "${TARGET}/blender" ]]; then
  echo "Blender ${VERSION} already installed at ${TARGET}"
else
  echo "Downloading Blender ${VERSION} ..."
  curl -L --fail -o "$ARCHIVE" "$URL"
  echo "Extracting ..."
  tar -xJf "$ARCHIVE" -C "$TOOLS_DIR"
fi

ln -sfn "$TARGET" "${TOOLS_DIR}/blender"
ln -sfn "${TOOLS_DIR}/blender/blender" "${BIN_DIR}/blender"

if ! grep -q 'export PATH="$HOME/bin:$PATH"' "${HOME}/.bashrc" 2>/dev/null; then
  echo 'export PATH="$HOME/bin:$PATH"' >> "${HOME}/.bashrc"
fi

export PATH="${BIN_DIR}:$PATH"
blender --version | head -n 1
echo "Done. Use: blender"
