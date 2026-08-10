#!/usr/bin/env bash
# Verify / prepare Linux-side tooling for this repo (Blender + Git LFS).
# Unity Editor should be installed on your Windows/macOS machine via Unity Hub.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

echo "== 3Dgame env check =="
echo "Repo: $ROOT"

ok=0
fail=0

check() {
  local name="$1"
  shift
  if "$@" >/dev/null 2>&1; then
    echo "[OK] $name"
    ok=$((ok + 1))
  else
    echo "[MISSING] $name"
    fail=$((fail + 1))
  fi
}

check "git" git --version
check "git-lfs" git lfs version

if command -v blender >/dev/null 2>&1; then
  echo "[OK] blender ($(blender --version | head -n1))"
  ok=$((ok + 1))
elif [[ -x "$HOME/bin/blender" ]]; then
  echo "[OK] blender ($("$HOME/bin/blender" --version | head -n1))"
  ok=$((ok + 1))
else
  echo "[MISSING] blender"
  fail=$((fail + 1))
fi

if [[ -f ProjectSettings/ProjectVersion.txt ]]; then
  echo "[OK] Unity project scaffold (ProjectSettings/ProjectVersion.txt)"
  ok=$((ok + 1))
else
  echo "[MISSING] Unity project scaffold"
  fail=$((fail + 1))
fi

if [[ -f Packages/manifest.json ]]; then
  echo "[OK] Packages/manifest.json"
  ok=$((ok + 1))
else
  echo "[MISSING] Packages/manifest.json"
  fail=$((fail + 1))
fi

if [[ -f Assets/Art/Props/SM_Crate.fbx ]]; then
  echo "[OK] Sample FBX Assets/Art/Props/SM_Crate.fbx"
  ok=$((ok + 1))
else
  echo "[WARN] Sample FBX not found (run scripts/setup/install-blender-linux.sh then create sample)"
fi

echo
echo "Result: $ok checks passed, $fail missing"
echo
echo "Next on your PC:"
echo "  1) Use Unity Hub with installed Editor 2022.3.62f3c1 LTS"
echo "  2) Install Blender 4.2 LTS"
echo "  3) Unity Hub -> Open -> select this repo folder"
echo "  4) When prompted, open with 2022.3.62f3c1"
echo "  5) Open Assets/Scenes/Level_01.unity and start greyboxing"
echo "See docs/开发环境.md for details."

exit "$fail"
