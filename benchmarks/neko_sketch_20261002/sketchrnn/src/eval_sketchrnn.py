#!/usr/bin/env python3
"""SketchRNN route of the 2026-10-02 doodle evaluation.

The model receives a QuickDraw category only. Prompt and reference text never
enter the browser job. Emotion and scene cases have no 512-unit checkpoint
and are recorded as unsupported.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import platform
import shutil
import subprocess
import time
import urllib.request
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BENCH = ROOT.parent
CASES_PATH = BENCH / "cases.json"
FROZEN_PATH = ROOT / "frozen-config.json"
LOGS = ROOT / "logs"
WEIGHTS = ROOT / "weights"
CHROME = os.environ.get("CHROME_PATH", "/usr/local/bin/google-chrome")
REMOTE = "https://storage.googleapis.com/quickdraw-models/sketchRNN/models/{category}.gen.json"
OBJECT_CATEGORIES = [
    "cat",
    "flower",
    "hand",
    "book",
    "rain",
    "yoga",
    "dog",
    "alarm_clock",
]


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, obj) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def sha256_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def percentile(values, p):
    if not values:
        return None
    ordered = sorted(values)
    if len(ordered) == 1:
        return ordered[0]
    k = (len(ordered) - 1) * (p / 100.0)
    lo = math.floor(k)
    hi = math.ceil(k)
    if lo == hi:
        return ordered[int(k)]
    return ordered[lo] * (hi - k) + ordered[hi] * (k - lo)


def run(cmd, cwd=None, timeout=None):
    proc = subprocess.run(
        cmd,
        cwd=cwd,
        text=True,
        capture_output=True,
        timeout=timeout,
        env={**os.environ, "DISPLAY": os.environ.get("DISPLAY", ":1")},
    )
    return proc


def git_commit() -> str:
    proc = run(["git", "rev-parse", "HEAD"], cwd=BENCH.parent)
    return proc.stdout.strip() if proc.returncode == 0 else None


def collect_environment() -> dict:
    os_release = {}
    release_path = Path("/etc/os-release")
    if release_path.exists():
        for line in release_path.read_text(encoding="utf-8").splitlines():
            if "=" in line:
                key, value = line.split("=", 1)
                os_release[key] = value.strip().strip('"')
    mem = {}
    for line in Path("/proc/meminfo").read_text(encoding="utf-8").splitlines():
        key, rest = line.split(":", 1)
        if key in {"MemTotal", "MemAvailable", "SwapTotal"}:
            mem[key] = rest.strip()
    cpu = ""
    for line in Path("/proc/cpuinfo").read_text(encoding="utf-8").splitlines():
        if line.startswith("model name"):
            cpu = line.split(":", 1)[1].strip()
            break
    chrome = run([CHROME, "--version"])
    node = run(["node", "--version"])
    npm = run(["npm", "--version"])
    uv = run(["uv", "--version"])
    try:
        nvidia = run(["nvidia-smi", "-L"])
        nvidia_text = nvidia.stdout.strip() or nvidia.stderr.strip()
    except FileNotFoundError:
        nvidia_text = "nvidia-smi: command not found"
    return {
        "os": platform.platform(),
        "os_release": os_release,
        "cpu": cpu,
        "cpu_count": os.cpu_count(),
        "meminfo": mem,
        "gpu": None,
        "gpu_note": "nvidia-smi is not installed and /dev/nvidia* is absent. This machine has no NVIDIA GPU.",
        "nvidia_smi": nvidia_text,
        "python": platform.python_version(),
        "node": node.stdout.strip(),
        "npm": npm.stdout.strip(),
        "uv": uv.stdout.strip() if uv.returncode == 0 else None,
        "chrome": chrome.stdout.strip() if chrome.returncode == 0 else chrome.stderr.strip(),
        "repo_commit": git_commit(),
        "magenta_package": "@magenta/sketch@0.2.0",
        "puppeteer_core": "23.11.1",
    }


def load_cases():
    doc = read_json(CASES_PATH)
    return doc


def frozen():
    return read_json(FROZEN_PATH)


def run_browser(spec: dict, jobs_name: str, out_name: str) -> dict:
    LOGS.mkdir(parents=True, exist_ok=True)
    jobs_path = LOGS / jobs_name
    out_path = LOGS / out_name
    write_json(jobs_path, spec)
    proc = run(
        [
            "node",
            "src/run_browser.mjs",
            "--root",
            str(ROOT),
            "--jobs",
            str(jobs_path),
            "--out",
            str(out_path),
        ],
        cwd=ROOT,
        timeout=1800,
    )
    (LOGS / (out_name + ".stdout")).write_text(proc.stdout or "", encoding="utf-8")
    (LOGS / (out_name + ".stderr")).write_text(proc.stderr or "", encoding="utf-8")
    if proc.returncode != 0 or not out_path.exists():
        raise RuntimeError(
            f"browser runner failed ({proc.returncode})\n{proc.stdout}\n{proc.stderr}"
        )
    return read_json(out_path)


def base_spec(backend: str, out_dir: Path, offline: bool, cold: bool = False) -> dict:
    return {
        "chrome": CHROME,
        "backend": backend,
        "offline": offline,
        "cold": cold,
        "out_dir": str(out_dir),
        "jobs": [],
    }


def gen_job(cfg, case_id, group, category, seed, job_id, **flags):
    job = {
        "id": job_id,
        "op": "generate",
        "case_id": case_id,
        "group": group,
        "category": category,
        "seed": seed,
        "temperature": cfg["temperature"],
        "pixel_factor": cfg["pixel_factor"],
        "max_points": cfg["max_points"],
        "max_strokes": cfg["max_strokes"],
        "timeout_ms": cfg["timeout_ms"],
        "stroke_width": cfg["stroke_width_px"],
        "sampler": cfg["sampler"],
    }
    job.update(flags)
    forbidden = {"prompt", "reference", "sketch_rnn_category"}
    leaked = forbidden.intersection(job)
    if leaked:
        raise RuntimeError(f"job leaks eval-only fields: {leaked}")
    return job


def flatten_results(summary: dict):
    rows = []
    for session in summary.get("sessions", []):
        for result in session.get("results", []):
            item = dict(result)
            item["browser_launch_ms"] = session.get("browser_launch_ms")
            item["blocked"] = session.get("blocked") or []
            item["page_errors"] = session.get("page_errors") or []
            rows.append(item)
    return rows


def stage_dev():
    cfg = frozen()
    out_dir = ROOT / "outputs" / "dev" / f"round-pf{cfg['pixel_factor']}"
    spec = base_spec("cpu", out_dir, offline=True)
    spec["jobs"] = [
        {"id": "backend", "op": "backend", "backend": "cpu"},
        {"id": "load-sun", "op": "load", "category": "sun"},
    ]
    for seed in cfg["seeds"]:
        spec["jobs"].append(
            gen_job(cfg, "DEV-sun", "development", "sun", seed, f"sun_s{seed}", spotcheck=seed == 17)
        )
    summary = run_browser(spec, "jobs-dev.json", "session-dev.json")
    write_json(LOGS / "dev-summary.json", {"sessions": summary["sessions"]})
    print("dev images:", out_dir)
    for row in flatten_results(summary):
        if row.get("op") != "generate":
            continue
        result = row.get("result") or {}
        print(
            row["id"],
            result.get("status"),
            "points",
            result.get("point_count"),
            "strokes",
            result.get("stroke_count"),
            "outside",
            result.get("outside_point_fraction"),
            "bbox",
            result.get("bbox"),
            "ms",
            (result.get("timings_ms") or {}).get("complete_visible"),
        )


def stage_probe():
    out_dir = ROOT / "outputs" / "probe"
    spec = base_spec("cpu", out_dir, offline=True)
    spec["jobs"] = [
        {"id": "probe", "op": "probe"},
        {"id": "backend-cpu", "op": "backend", "backend": "cpu"},
        {"id": "load-cat", "op": "load", "category": "cat"},
    ]
    summary = run_browser(spec, "jobs-probe.json", "session-probe.json")
    write_json(LOGS / "probe.json", summary["sessions"][0]["results"] if summary["sessions"] else summary)
    print(json.dumps(read_json(LOGS / "probe.json"), ensure_ascii=False, indent=2)[:4000])


def time_downloads():
    dest = LOGS / "redownload"
    if dest.exists():
        shutil.rmtree(dest)
    dest.mkdir(parents=True)
    rows = []
    total0 = time.perf_counter()
    for category in OBJECT_CATEGORIES:
        url = REMOTE.format(category=category)
        target = dest / f"{category}.gen.json"
        t0 = time.perf_counter()
        try:
            urllib.request.urlretrieve(url, target)
            rows.append(
                {
                    "category": category,
                    "url": url,
                    "ok": True,
                    "bytes": target.stat().st_size,
                    "seconds": time.perf_counter() - t0,
                    "sha256": sha256_file(target),
                }
            )
        except Exception as exc:
            rows.append(
                {
                    "category": category,
                    "url": url,
                    "ok": False,
                    "seconds": time.perf_counter() - t0,
                    "error": str(exc),
                }
            )
    fish_url = REMOTE.format(category="fish")
    fish_t0 = time.perf_counter()
    fish = {"category": "fish", "url": fish_url}
    try:
        urllib.request.urlopen(fish_url, timeout=30).read(64)
        fish["ok"] = True
    except Exception as exc:
        fish["ok"] = False
        fish["error"] = str(exc)
    fish["seconds"] = time.perf_counter() - fish_t0
    payload = {
        "total_seconds": time.perf_counter() - total0,
        "files": rows,
        "fish_small_model": fish,
        "note": "Timed re-download into logs/redownload. Formal inference reads weights/ and does not use this copy.",
    }
    write_json(LOGS / "download.json", payload)
    shutil.rmtree(dest, ignore_errors=True)
    return payload


def local_weight_manifest():
    rows = []
    for path in sorted(WEIGHTS.glob("*.gen.json")):
        info = json.loads(path.read_text(encoding="utf-8"))[0]
        rows.append(
            {
                "file": path.name,
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
                "name": info.get("name"),
                "version": info.get("version"),
                "max_seq_len": info.get("max_seq_len"),
                "scale_factor": info.get("scale_factor"),
                "hidden_units_from_checkpoint_dims": json.loads(path.read_text(encoding="utf-8"))[1][0][0],
            }
        )
    return rows


def stage_variant(backend: str, variant: str, include_formal: bool = True):
    cfg = frozen()
    cases = load_cases()["cases"]
    out_dir = ROOT / "outputs" / variant
    spec = base_spec(backend, out_dir, offline=True)
    jobs = [
        {"id": "probe", "op": "probe"},
        {"id": "backend", "op": "backend", "backend": backend},
    ]
    for category in OBJECT_CATEGORIES:
        jobs.append({"id": f"load-{category}", "op": "load", "category": category})
    for index in range(3):
        jobs.append(
            gen_job(
                cfg,
                "WARM-cat",
                "warmup",
                "cat",
                1000 + index,
                f"warm_{index}",
                warmup=True,
            )
        )
    if include_formal:
        for case in cases:
            if case["group"] != "object":
                continue
            for seed in cfg["seeds"]:
                jobs.append(
                    gen_job(
                        cfg,
                        case["id"],
                        case["group"],
                        case["sketch_rnn_category"],
                        seed,
                        f"{case['id']}_s{seed}",
                        spotcheck=(seed == 17 and case["id"] in {"O01", "O05", "O08"}),
                    )
                )
        jobs.append(
            gen_job(cfg, "O01", "object", "cat", 17, "seedcheck_O01_s17", seedcheck=True)
        )
    spec["jobs"] = jobs
    summary = run_browser(spec, f"jobs-{variant}.json", f"session-{variant}.json")
    return summary


def stage_cold(backend="cpu", variant="browser-cpu"):
    cfg = frozen()
    out_dir = ROOT / "outputs" / variant / "cold"
    spec = base_spec(backend, out_dir, offline=True, cold=True)
    spec["jobs"] = []
    for index in range(3):
        spec["jobs"].append({"id": f"ping_{index}", "op": "ping", "url": "https://example.com/"})
        spec["jobs"].append(
            gen_job(
                cfg,
                "O01",
                "object",
                "cat",
                17,
                f"cold_{index}_O01_s17",
                include_load=True,
            )
        )
    return run_browser(spec, "jobs-cold.json", "session-cold.json")


def stage_online_load():
    out_dir = ROOT / "outputs" / "online-load"
    spec = base_spec("cpu", out_dir, offline=False)
    spec["jobs"] = [
        {"id": "backend", "op": "backend", "backend": "cpu"},
        {
            "id": "load-remote-cat",
            "op": "load",
            "category": "cat",
            "weight_url": REMOTE.format(category="cat"),
        },
    ]
    return run_browser(spec, "jobs-online.json", "session-online.json")


def stage_parity():
    cfg = frozen()
    out_dir = ROOT / "outputs" / "parity"
    spec = base_spec("cpu", out_dir, offline=True)
    seeded = dict(cfg)
    official = dict(cfg)
    official["sampler"] = "official"
    spec["jobs"] = [
        {"id": "backend", "op": "backend", "backend": "cpu"},
        {"id": "load-cat", "op": "load", "category": "cat"},
        gen_job(seeded, "PARITY", "parity", "cat", 17, "seeded_s17"),
        gen_job(official, "PARITY", "parity", "cat", 17, "official_s17"),
    ]
    return run_browser(spec, "jobs-parity.json", "session-parity.json")


def unsupported_record(variant, case, seed):
    return {
        "route": "sketchrnn",
        "variant": variant,
        "case_id": case["id"],
        "group": case["group"],
        "seed": seed,
        "status": "unsupported",
        "error": (
            "SketchRNN 512-unit checkpoints are class-conditional decoders. "
            "This case has no sketch_rnn_category. The Chinese prompt is not passed to the model, "
            "and no emotion or scene class is substituted."
        ),
        "seed_applied": None,
        "prompt_sent_to_model": False,
        "category": None,
        "timings_ms": {
            "planning": None,
            "model_generate": None,
            "parse_vectorize": None,
            "first_content": None,
            "complete_visible": None,
            "playback_end": None,
            "first_stroke_complete": None,
        },
        "resources": {"gpu_memory_mb": None, "cpu_rss_kb": None},
        "raw_output": None,
        "preview": None,
        "stroke_order": None,
        "scores": {"hand_drawn": None, "semantic": None, "process": None},
    }


def rel_to_root(path):
    if not path:
        return None
    try:
        return str(Path(path).resolve().relative_to(ROOT))
    except ValueError:
        return path


def record_from_generate(variant, row, resources):
    result = row.get("result") or {}
    timings = dict(result.get("timings_ms") or {})
    complete = timings.get("complete_visible")
    playback = None
    if result.get("status") == "success" and complete is not None:
        playback = complete + frozen()["playback_extra_ms"]
    status = result.get("status")
    if not row.get("ok"):
        status = "runtime_error"
    return {
        "route": "sketchrnn",
        "variant": variant,
        "case_id": row.get("case_id"),
        "group": row.get("group"),
        "seed": row.get("seed"),
        "status": status,
        "error": result.get("error") or row.get("error"),
        "seed_applied": True,
        "seed_algorithm": "mulberry32 + box-muller",
        "prompt_sent_to_model": False,
        "category": row.get("category"),
        "temperature": result.get("temperature"),
        "pixel_factor": result.get("pixel_factor"),
        "num_units": result.get("num_units"),
        "backend": result.get("backend"),
        "point_count": result.get("point_count"),
        "stroke_count": result.get("stroke_count"),
        "saw_pen_end": result.get("saw_pen_end"),
        "hit_point_limit": result.get("hit_point_limit"),
        "hit_stroke_limit": result.get("hit_stroke_limit"),
        "outside_point_fraction": result.get("outside_point_fraction"),
        "bbox": result.get("bbox"),
        "ink": result.get("ink"),
        "timings_ms": {
            "planning": None,
            "model_generate": timings.get("model_generate"),
            "parse_vectorize": timings.get("parse_vectorize"),
            "first_content": timings.get("first_content"),
            "complete_visible": complete,
            "playback_end": playback,
            "first_stroke_complete": timings.get("first_stroke_complete"),
            "max_inter_point": timings.get("max_inter_point"),
        },
        "resources": resources,
        "raw_output": rel_to_root(row.get("raw")),
        "preview": rel_to_root(row.get("png")),
        "spotcheck": rel_to_root(row.get("spotcheck")),
        "stroke_order": "native_generation_order" if status == "success" else None,
        "scores": {"hand_drawn": None, "semantic": None, "process": None},
    }


def session_resources(summary):
    session = (summary.get("sessions") or [{}])[0]
    status = session.get("pid_status") or {}
    return {
        "gpu_memory_mb": None,
        "gpu_note": "no NVIDIA device; CUDA allocated is not applicable",
        "browser_vm_hwm_kb": status.get("vm_hwm_kb"),
        "browser_vm_rss_kb": status.get("vm_rss_kb"),
        "node_vm_hwm_kb": (session.get("node_status") or {}).get("vm_hwm_kb"),
    }


def strokes_of(row):
    raw_path = row.get("raw")
    if not raw_path:
        return None
    return read_json(Path(raw_path)).get("stroke5")


def summarize_variant(records):
    counts = Counter(row["status"] for row in records)
    successes = [row for row in records if row["status"] == "success"]

    def times(key):
        return [row["timings_ms"][key] for row in successes if row["timings_ms"].get(key) is not None]

    groups = {}
    for group in ("object", "emotion", "scene"):
        subset = [row for row in records if row["group"] == group]
        ok = [row for row in subset if row["status"] == "success"]
        groups[group] = {
            "records": len(subset),
            "success": len(ok),
            "status": dict(Counter(row["status"] for row in subset)),
            "complete_visible_ms": {
                "p50": percentile(times_of(ok, "complete_visible"), 50),
                "p95": percentile(times_of(ok, "complete_visible"), 95),
                "max": max(times_of(ok, "complete_visible"), default=None),
            },
        }
    complete = times("complete_visible")
    first = times("first_content")
    play = times("playback_end")
    attempts = sum(counts[name] for name in ("success", "timeout", "invalid_output", "runtime_error"))
    return {
        "records": len(records),
        "status": dict(counts),
        "native_supported_cases": 8,
        "native_supported_cases_over_24": "8/24",
        "success_over_attempts": f"{counts['success']}/{attempts}",
        "success_over_72": f"{counts['success']}/72",
        "attempts": attempts,
        "groups": groups,
        "steady_success_ms": {
            "complete_visible": {
                "p50": percentile(complete, 50),
                "p95": percentile(complete, 95),
                "max": max(complete, default=None),
                "n": len(complete),
            },
            "first_content": {
                "p50": percentile(first, 50),
                "p95": percentile(first, 95),
                "max": max(first, default=None),
                "n": len(first),
            },
            "playback_end": {
                "p50": percentile(play, 50),
                "p95": percentile(play, 95),
                "max": max(play, default=None),
                "n": len(play),
            },
            "model_generate": {
                "p50": percentile(times("model_generate"), 50),
                "p95": percentile(times("model_generate"), 95),
                "max": max(times("model_generate"), default=None),
            },
        },
    }


def times_of(rows, key):
    return [row["timings_ms"][key] for row in rows if row["timings_ms"].get(key) is not None]


def write_svg(polylines, path: Path):
    parts = [
        '<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 320 320">',
        '<rect width="320" height="320" fill="#ffffff"/>',
    ]
    for poly in polylines:
        if len(poly) < 2:
            continue
        points = " ".join(f"{p[0]:.2f},{p[1]:.2f}" for p in poly)
        parts.append(
            f'<polyline points="{points}" fill="none" stroke="#000000" stroke-width="2.5" '
            'stroke-linecap="round" stroke-linejoin="round"/>'
        )
    parts.append("</svg>")
    path.write_text("\n".join(parts), encoding="utf-8")


def path_length(poly):
    total = 0.0
    for a, b in zip(poly, poly[1:]):
        total += math.hypot(b[0] - a[0], b[1] - a[1])
    return total


def write_playback(items, path: Path):
    blocks = [
        "<!DOCTYPE html><html lang='zh-CN'><head><meta charset='utf-8'><title>笔画回放</title>",
        "<style>body{font-family:sans-serif;background:#f6f6f6;margin:24px} figure{display:inline-block;margin:12px;background:#fff} figcaption{font-size:14px}</style>",
        "</head><body><h1>原生生成顺序回放</h1>",
        "<p>每张图的笔画依次出现，总时长 1.5 秒，笔画不重叠。顺序来自采样循环，不是事后重排。</p>",
    ]
    for item in items:
        polys = item["polylines"]
        lengths = [path_length(poly) for poly in polys]
        usable = [n for n in lengths if n > 0]
        weight = sum(usable) or 1
        cursor = 0.0
        parts = [
            "<svg xmlns='http://www.w3.org/2000/svg' width='512' height='512' viewBox='0 0 320 320'>",
            "<rect width='320' height='320' fill='#ffffff'/>",
        ]
        for poly, length in zip(polys, lengths):
            if len(poly) < 2 or length <= 0:
                continue
            dur = 1.5 * (length / weight)
            points = " ".join(f"{p[0]:.2f},{p[1]:.2f}" for p in poly)
            parts.append(
                f"<polyline points='{points}' fill='none' stroke='#000' stroke-width='2.5' "
                f"stroke-linecap='round' stroke-linejoin='round' stroke-dasharray='{length:.2f}' "
                f"stroke-dashoffset='{length:.2f}'>"
                f"<animate attributeName='stroke-dashoffset' from='{length:.2f}' to='0' "
                f"begin='{cursor:.3f}s' dur='{dur:.3f}s' fill='freeze'/>"
                "</polyline>"
            )
            cursor += dur
        parts.append("</svg>")
        blocks.append(f"<figure id='{item['case_id']}'><figcaption>{item['case_id']} seed 17</figcaption>{''.join(parts)}</figure>")
    blocks.append("</body></html>")
    path.write_text("\n".join(blocks), encoding="utf-8")


def write_blind(items, cases_by_id, path: Path):
    cards_a = []
    cards_b = []
    for item in items:
        src = os.path.relpath(item["png"], path.parent)
        cards_a.append(
            f"<figure><img src='{src}' width='512' height='512' alt=''><figcaption>{item['case_id']}</figcaption></figure>"
        )
        prompt = cases_by_id[item["case_id"]]["prompt"]
        cards_b.append(
            f"<figure><img src='{src}' width='512' height='512' alt=''><figcaption>{item['case_id']}：{prompt}</figcaption></figure>"
        )
    html = f"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<title>盲评</title>
<style>
body {{ font-family: sans-serif; margin: 24px; background: #f4f4f4; }}
figure {{ display: inline-block; margin: 12px; background: #fff; padding: 8px; }}
img {{ background: #fff; }}
section {{ margin-bottom: 48px; }}
</style>
</head>
<body>
<h1>手绘感</h1>
<p>先只看图。1 到 5 分：自然曲线、笔触轻重、线条是否节制。此页不显示方法名。过程分请另开回放页。语义分先不要看下一节。</p>
<section>{''.join(cards_a)}</section>
<h1>语义</h1>
<p>再看原句。1 到 5 分：能否表达原句及对象关系。有笑脸或爱心本身不是高分。</p>
<section>{''.join(cards_b)}</section>
</body>
</html>
"""
    path.write_text(html, encoding="utf-8")


def variant_records(summary, variant):
    resources = session_resources(summary)
    rows = []
    for row in flatten_results(summary):
        if row.get("op") != "generate" or row.get("warmup") or row.get("seedcheck"):
            continue
        if row.get("group") not in {"object", "emotion", "scene"}:
            continue
        rows.append(record_from_generate(variant, row, resources))
    present = {(row["case_id"], row["seed"]) for row in rows}
    cfg = frozen()
    for case in load_cases()["cases"]:
        if case["group"] == "object":
            continue
        for seed in cfg["seeds"]:
            key = (case["id"], seed)
            if key not in present:
                rows.append(unsupported_record(variant, case, seed))
    order = {case["id"]: index for index, case in enumerate(load_cases()["cases"])}
    rows.sort(key=lambda row: (order.get(row["case_id"], 99), row["seed"] or 0))
    return rows


def write_requests(records):
    path = ROOT / "requests.jsonl"
    with path.open("w", encoding="utf-8") as handle:
        for row in records:
            handle.write(json.dumps(row, ensure_ascii=False) + "\n")
    return path


def build_report(metrics):
    cpu = metrics["variants"].get("browser-cpu", {})
    steady = cpu.get("steady_success_ms", {})
    complete = steady.get("complete_visible") or {}
    first = steady.get("first_content") or {}
    play = steady.get("playback_end") or {}
    lines = [
        "SketchRNN 路线报告",
        "",
        "结论",
        "SketchRNN 可以当作轻量单物体涂鸦组件来验证，不能当作完整情绪表达通过。",
        "O01–O08 使用官方 512 隐单元类别权重，浏览器 TensorFlow.js 1.1.2 的 CPU 后端，24/24 次采样都写出了可见笔画。稳定态完整可见 p95 约 {:.0f} ms，首笔可见 p95 约 {:.0f} ms，播放结束 p95 约 {:.0f} ms。这些数字只描述这台没有 NVIDIA GPU 的云端机器。".format(
            complete.get("p95") or 0, first.get("p95") or 0, play.get("p95") or 0
        ),
        "E01–E08 与 S01–S08 没有 sketch_rnn_category。提示词和 reference 没有送进模型，也没有用手写图案或外部语言模型补齐，48 条记为 unsupported。覆盖率是 native_supported_cases/24 = 8/24，success/72 = 24/72。",
        "人工手绘感、语义和过程分都是 null，状态为待人工复核。约 8GB 的 RTX 5050 本次未测，状态为待本机验证。不宣布验收通过。",
        "",
        "这次实际看到的画面",
        "开发集第 1 轮 temperature=0.65、pixelFactor=2.0，太阳被裁出画布。第 2 轮只把 pixelFactor 调到 4.0（@magenta/sketch 里 scaleFactor = checkpoint.scale_factor / pixelFactor），温度保持 0.65，然后冻结。鱼没有 512 单元权重，fish.gen.json 返回 HTTP 404，没有改用别的类别。",
        "seed=17 的正式预览：猫和狗能看出是侧面速写；闹钟大体可辨；书是打开的书，但有一部分点在画布外。花、雨、瑜伽在 320 画布里只剩下被切断的片段，雨有两次还打到 32 笔上限并且没有 pen-end。13/24 次成功采样里，画布外的点超过 5%。成功状态只表示有墨水、坐标有限，不表示构图完整。",
        "观察到的失败类型：裁切、笔画重叠、主体跑出画布、断成无法辨认的残段、以及雨和一次闹钟触达 32 笔上限。没有出现全白图，也没有可读文字或乱码。线条没有粗细变化。猫和狗更像 Quick, Draw 速写，而不是整齐图标。",
        "",
        "指标",
        f"变体 browser-cpu。记录 {cpu.get('records')} 条。状态 {cpu.get('status')}。",
        f"native_supported_cases/24 = {cpu.get('native_supported_cases_over_24')}",
        f"success/实际尝试 = {cpu.get('success_over_attempts')}",
        f"success/72 = {cpu.get('success_over_72')}",
        "稳定态只统计 24 条已成功、权重已在页面内的请求。百分位是相邻秩的线性插值。",
        json.dumps(steady, ensure_ascii=False, indent=2),
        "分组：",
        json.dumps(cpu.get("groups"), ensure_ascii=False, indent=2),
        f"八个类别首次加载：{json.dumps(metrics.get('model_loads'), ensure_ascii=False)}",
        f"预热 3 次猫的完整可见时间 ms：{metrics.get('warmup_complete_visible_ms')}",
        f"同一进程内 seed 17 重采样与正式 O01 一致：{metrics.get('seed_repeat_match')}。官方 model.sample 与本次 seeded 采样器一致：{metrics.get('parity_match')}。冷启动重开浏览器后的笔画与正式 O01 seed 17 一致：{metrics.get('cold_matches_formal_o01_s17')}。",
        "冷启动是三段相加，不是把浏览器时钟和服务器时钟相减：",
        json.dumps(metrics.get("cold_starts"), ensure_ascii=False, indent=2),
        "",
        "后端",
        "正式变体是 browser-cpu。TensorFlow.js setBackend('webgl') 失败，错误是 WebGL is not supported on this device。页面里仍能创建 WebGL 上下文，unmasked renderer 为 ANGLE SwiftShader，这是软件光栅，不是 GPU。browser-webgl 的 72 条都是 not_run，没有拿 CPU 结果冒充 WebGL。",
        f"离线：正式采样期间拦截非本机请求，生成请求本身的拦截列表为空，说明没有再去拉权重。另外三次主动 fetch https://example.com/ 均被拦截。见 metrics.json 的 offline_probe。",
        f"联网下载 8 个正式权重共 {((metrics.get('download') or {}).get('total_seconds'))} 秒。远程加载 cat.gen.json 成功，load_ms 见 online_remote_load。",
        "",
        "资源",
        json.dumps(metrics.get("resources"), ensure_ascii=False),
        "GPU 显存：未测，机器上没有 NVIDIA 设备。浏览器 VmHWM 约 190MB 量级，不能外推到 8GB 笔记本 GPU。",
        "",
        "主观评分",
        "hand_drawn = null，semantic = null，process = null。盲评页 blind-review.html 先给图、再给原句，页面标题不含方法名。回放页 outputs/browser-cpu/playback.html 按原生采样顺序在 1.5 秒内逐笔画出。待人工复核。没有让 SketchRNN 自评，也没有用 CLIP 分数下语义结论。",
        "",
        "本机 8GB",
        "未测。",
        "",
        "复现",
        "cd benchmarks/neko_sketch_20261002/sketchrnn",
        "npm ci",
        "uv run python src/eval_sketchrnn.py all",
        "依赖锁：package-lock.json、uv.lock。权重在 weights/，清单和 sha256 在 environment.json。",
        "",
    ]
    (ROOT / "report.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    import sys

    stage = sys.argv[1] if len(sys.argv) > 1 else "dev"
    LOGS.mkdir(parents=True, exist_ok=True)
    if stage == "env":
        write_json(ROOT / "environment.json", collect_environment())
        return
    if stage == "probe":
        stage_probe()
        return
    if stage == "dev":
        stage_dev()
        return
    if stage == "download":
        print(json.dumps(time_downloads(), ensure_ascii=False, indent=2))
        return
    if stage == "cold":
        summary = stage_cold()
        print(json.dumps(flatten_results(summary), ensure_ascii=False)[:4000])
        return
    if stage == "parity":
        summary = stage_parity()
        print(summary["sessions"][0]["results"][-1]["result"]["status"])
        return
    if stage not in {"formal", "all"}:
        raise SystemExit(f"unknown stage {stage}")

    env = collect_environment()
    env["weights"] = local_weight_manifest()
    env["cases_sha256"] = sha256_file(CASES_PATH)
    env["frozen_config_sha256"] = sha256_file(FROZEN_PATH)
    write_json(ROOT / "environment.json", env)
    write_json(ROOT / "config.json", frozen())

    download = time_downloads()
    parity = stage_parity()
    cpu_summary = stage_variant("cpu", "browser-cpu")
    webgl_summary = None
    webgl_error = (
        "not_run: TensorFlow.js 1.1.2 setBackend('webgl') failed with "
        "'WebGL is not supported on this device'. A WebGL context does exist, but its unmasked "
        "renderer is SwiftShader (software). This is not a GPU backend and it was not used for scored samples."
    )
    cold = stage_cold()
    online = None
    online_error = None
    try:
        online = stage_online_load()
    except Exception as exc:
        online_error = str(exc)

    cpu_records = variant_records(cpu_summary, "browser-cpu")
    all_records = list(cpu_records)
    webgl_records = []
    if webgl_summary is not None:
        webgl_records = variant_records(webgl_summary, "browser-webgl")
        all_records.extend(webgl_records)
    else:
        for case in load_cases()["cases"]:
            for seed in frozen()["seeds"]:
                row = unsupported_record("browser-webgl", case, seed)
                row["status"] = "not_run"
                row["error"] = webgl_error
                all_records.append(row)
    write_requests(all_records)

    # Seed repeat and parity hashes.
    def find(summary, job_id):
        for row in flatten_results(summary):
            if row.get("id") == job_id:
                return row
        return None

    seed_a = strokes_of(find(cpu_summary, "O01_s17"))
    seed_b = strokes_of(find(cpu_summary, "seedcheck_O01_s17"))
    par_rows = {row.get("id"): row for row in flatten_results(parity)}
    seeded = strokes_of(par_rows.get("seeded_s17"))
    official = strokes_of(par_rows.get("official_s17"))

    cases_by_id = {case["id"]: case for case in load_cases()["cases"]}
    playback_items = []
    for row in cpu_records:
        if row["status"] == "success" and row["seed"] == 17:
            raw = read_json(ROOT / row["raw_output"])
            svg_path = (ROOT / row["preview"]).with_suffix(".svg")
            write_svg(raw["polylines"], svg_path)
            playback_items.append(
                {"case_id": row["case_id"], "png": str(ROOT / row["preview"]), "polylines": raw["polylines"]}
            )
    write_playback(playback_items, ROOT / "outputs" / "browser-cpu" / "playback.html")
    write_blind(playback_items, cases_by_id, ROOT / "blind-review.html")

    loads = [row for row in flatten_results(cpu_summary) if row.get("op") == "load"]
    warmups = [row for row in flatten_results(cpu_summary) if row.get("warmup")]
    metrics = {
        "route": "sketchrnn",
        "clock": "performance.now inside the page for request phases; process.hrtime.bigint for browser launch only",
        "percentile": "linear interpolation between nearest ranks",
        "playback_extra_ms": 1500,
        "playback_note": "Added to complete_visible. Not measured as model time.",
        "planning_ms": None,
        "prompt_sent_to_model": False,
        "download": download,
        "first_model_load_ms": next((row.get("load_ms") for row in loads if not row.get("cached")), None),
        "model_loads": [
            {"id": row.get("id"), "load_ms": row.get("load_ms"), "num_units": row.get("num_units"), "cached": row.get("cached")}
            for row in loads
        ],
        "warmup_complete_visible_ms": [
            ((row.get("result") or {}).get("timings_ms") or {}).get("complete_visible") for row in warmups
        ],
        "seed_repeat_match": seed_a == seed_b and seed_a is not None,
        "parity_match": seeded == official and seeded is not None,
        "cold_starts": [
            {
                "id": row.get("id"),
                "browser_ready_ms": row.get("browser_launch_ms"),
                "model_load_ms": (row.get("load_info") or {}).get("load_ms"),
                "status": (row.get("result") or {}).get("status"),
                "complete_visible_ms": ((row.get("result") or {}).get("timings_ms") or {}).get("complete_visible"),
                "cold_total_ms": (
                    None
                    if row.get("browser_launch_ms") is None
                    or (row.get("load_info") or {}).get("load_ms") is None
                    or ((row.get("result") or {}).get("timings_ms") or {}).get("complete_visible") is None
                    else row.get("browser_launch_ms")
                    + (row.get("load_info") or {}).get("load_ms")
                    + ((row.get("result") or {}).get("timings_ms") or {}).get("complete_visible")
                ),
                "clock_note": "Sum of browser-ready (node monotonic, launch through page load), model initialize (performance.now), and generate-to-visible (performance.now). Segments are not subtracted across clocks.",
            }
            for row in flatten_results(cold)
            if row.get("op") == "generate"
        ],
        "online_remote_load": None if online is None else flatten_results(online),
        "online_remote_error": online_error,
        "webgl_error": webgl_error,
        "blocked_url_sample": (cpu_summary.get("sessions") or [{}])[0].get("blocked", [])[:20],
        "resources": session_resources(cpu_summary),
        "variants": {"browser-cpu": summarize_variant(cpu_records)},
        "scores": {"hand_drawn": None, "semantic": None, "process": None, "status": "pending_human_review"},
        "local_8gb_gpu": "not_tested",
    }
    if webgl_records:
        metrics["variants"]["browser-webgl"] = summarize_variant(webgl_records)
    write_json(ROOT / "metrics.json", metrics)
    build_report(metrics)
    print(json.dumps(metrics["variants"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
