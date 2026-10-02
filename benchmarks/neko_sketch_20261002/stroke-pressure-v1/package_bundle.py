"""Build a standalone cloud brush benchmark, excluding runtime/model downloads."""
import hashlib
import json
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parent
dest = root.parents[1] / "neko-variable-stroke-cloud-test-20261002.zip"
if dest.exists():
    raise SystemExit(f"Refusing to replace existing bundle: {dest}")
excluded_dirs = {"node_modules", "local-results", "__pycache__"}
files = sorted(p for p in root.rglob("*") if p.is_file()
               and not any(part in excluded_dirs for part in p.relative_to(root).parts)
               and not p.name.endswith(".log")
               and not any(part.startswith("cloud-results") for part in p.relative_to(root).parts)
               and not any(part.startswith("local-results-") and part != "local-results-v2"
                           for part in p.relative_to(root).parts))
manifest = {"scope": "standalone synthetic-pressure rendering benchmark; no Neko/model integration",
            "local_result_directory": "local-results-v2", "files": {}}
with zipfile.ZipFile(dest, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for file in files:
        name = (Path(root.name) / file.relative_to(root)).as_posix()
        archive.write(file, name)
        manifest["files"][name] = hashlib.sha256(file.read_bytes()).hexdigest()
    archive.writestr(root.name + "/bundle-manifest.json",
                     json.dumps(manifest, ensure_ascii=False, indent=2))
with zipfile.ZipFile(dest) as archive:
    assert archive.testzip() is None
    names = archive.namelist()
    assert not any("node_modules/" in n or n.endswith(".log") or "weights/" in n for n in names)
    assert "stroke-pressure-v1/CURSOR任务.txt" in names
    assert "stroke-pressure-v1/local-results-v2/summary.json" in names
    assert "stroke-pressure-v1/blind-review.html" in names
digest = hashlib.sha256(dest.read_bytes()).hexdigest()
dest.with_suffix(".sha256.txt").write_text(digest + "  " + dest.name + "\n", encoding="utf-8")
print(json.dumps({"bundle": str(dest), "files": len(names), "bytes": dest.stat().st_size,
                  "sha256": digest, "crc_check": "passed", "runtime_and_models": "excluded"}, ensure_ascii=False))
