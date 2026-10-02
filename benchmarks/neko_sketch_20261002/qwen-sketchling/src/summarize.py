#!/usr/bin/env python3
"""Build metrics.json and report.txt from the NF4 requests.jsonl."""

from __future__ import annotations

import json
import math
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


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


def main():
    rows = [json.loads(line) for line in (ROOT / "requests.jsonl").read_text(encoding="utf-8").splitlines() if line.strip()]
    counts = Counter(row["status"] for row in rows)
    successes = [row for row in rows if row["status"] == "success"]

    def times(key):
        return [row["timings_ms"][key] for row in successes if (row.get("timings_ms") or {}).get(key) is not None]

    groups = {}
    for group in ("object", "emotion", "scene"):
        subset = [row for row in rows if row.get("group") == group]
        ok = [row for row in subset if row["status"] == "success"]
        groups[group] = {
            "records": len(subset),
            "success": len(ok),
            "status": dict(Counter(row["status"] for row in subset)),
        }
    attempts = sum(counts[name] for name in ("success", "timeout", "invalid_output", "runtime_error"))
    load = {}
    load_path = ROOT / "logs" / "nf4-load.json"
    if load_path.exists():
        load = json.loads(load_path.read_text(encoding="utf-8"))
    metrics = {
        "route": "qwen-sketchling",
        "variant": "qwen3-4b-nf4-complete",
        "device": "cpu",
        "quantization": "bitsandbytes NF4",
        "local_8gb_gpu": "not_tested",
        "load": load,
        "records": len(rows),
        "status": dict(counts),
        "native_supported_cases_over_24": "24/24",
        "success_over_attempts": f"{counts['success']}/{attempts}",
        "success_over_72": f"{counts['success']}/72",
        "groups": groups,
        "steady_success_ms": {
            "complete_visible": {
                "p50": percentile(times("complete_visible"), 50),
                "p95": percentile(times("complete_visible"), 95),
                "max": max(times("complete_visible"), default=None),
                "n": len(times("complete_visible")),
            }
        },
        "scores": {"hand_drawn": None, "semantic": None, "process": None, "status": "pending_human_review"},
        "variants_not_run": {
            "qwen3-4b-bf16-complete": "not_run. Peak RSS of the NF4 process was already about 9–10GB. A second full-precision run was not started.",
            "qwen3-4b-bf16-stream": "not_run",
            "qwen3-4b-nf4-stream": "not_run. Streaming was not given its own 72 requests. This variant waits for the full text, so first_content equals complete_visible.",
        },
        "renderer": "plain-svg-coordinates",
        "sketchling_mount": "not_used",
    }
    (ROOT / "metrics.json").write_text(json.dumps(metrics, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    report = f"""Qwen3-4B 笔画路线报告

结论
这次只跑了 CPU 上的 bitsandbytes NF4 完整输出变体，不能写成 8GB 笔记本通过。模型能按行吐 JSON 笔画，但 30 秒里经常在半行 JSON 处被切断。开发集里“画一个太阳”有一次在 28 秒内结束，画的是重复三次的同一条折线，不像太阳。加“最多 6 笔”的第二轮提示让 6 个开发题全部超时，正式评测因此冻回第一轮提示。

未测
- bf16 完整输出、bf16 流式、NF4 流式：not_run。没有把它们和 NF4 完整输出混成一组指标。
- Sketchling 的 sketch.stroke / sketch.loop 和 mountRenderable：没有接到预览。成功样本用普通 SVG 画校验后的坐标，渲染器名是 plain-svg-coordinates。
- 人工手绘感、语义、过程分：null，待人工复核。
- 本机 RTX 5050 约 8GB：未测。NF4 进程 VmHWM 大约 9–10GB 系统内存，这不是显存读数。
- 冷启动三次重开进程：未单列。加载本身大约 35 秒，已写在 logs/nf4-load.json，不计入下面的生成时间。

正式计数
变体 qwen3-4b-nf4-complete。记录 {len(rows)} 条。状态 {dict(counts)}。
native_supported_cases/24 = 24/24。模型接受任意中文 prompt，所以 24 题都算它的输入范围；这不等于画得出来。
success/实际尝试 = {counts['success']}/{attempts}
success/72 = {counts['success']}/72
成功样本的完整可见时间只在有成功时才有 p50/p95，失败不放进这个分布。

分组
{json.dumps(groups, ensure_ascii=False, indent=2)}

复现
cd benchmarks/neko_sketch_20261002/qwen-sketchling
uv sync
uv run python src/run_nf4.py formal
"""
    (ROOT / "report.txt").write_text(report, encoding="utf-8")
    print(json.dumps({"status": metrics["status"], "success_over_72": metrics["success_over_72"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
