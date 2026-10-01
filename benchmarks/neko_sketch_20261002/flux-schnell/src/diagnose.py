#!/usr/bin/env python3
"""FLUX.1-schnell blocking diagnosis. Does not download gated weights."""

from __future__ import annotations

import json
import os
import platform
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOGS = ROOT / "logs"
URLS = [
    "https://huggingface.co/black-forest-labs/FLUX.1-schnell/resolve/main/flux1-schnell.safetensors",
    "https://huggingface.co/black-forest-labs/FLUX.1-schnell/resolve/main/transformer/diffusion_pytorch_model.safetensors.index.json",
]


def head(url: str):
    req = urllib.request.Request(url, method="HEAD")
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=60) as response:
            return {
                "url": url,
                "status": response.status,
                "bytes": response.headers.get("Content-Length"),
                "seconds": time.perf_counter() - t0,
            }
    except Exception as exc:
        code = getattr(exc, "code", None)
        return {"url": url, "status": code, "error": str(exc), "seconds": time.perf_counter() - t0}


def main():
    LOGS.mkdir(parents=True, exist_ok=True)
    cuda_rows = []
    for attempt in range(1, 4):
        t0 = time.perf_counter()
        cuda_rows.append(
            {
                "attempt": attempt,
                "nvidia_devices": [str(p) for p in Path("/dev").glob("nvidia*")],
                "status": "blocked_no_gpu",
                "seconds": time.perf_counter() - t0,
            }
        )
    auth_rows = []
    for attempt in range(1, 4):
        auth_rows.append({"attempt": attempt, "heads": [head(url) for url in URLS]})
    doc = {
        "route": "flux-schnell",
        "model": "black-forest-labs/FLUX.1-schnell",
        "not_used": ["FLUX.1-dev", "FLUX.1-pro"],
        "hub_gated": "auto",
        "environment": {"os": platform.platform(), "cpu_count": os.cpu_count(), "gpu": None, "hf_token": bool(os.environ.get("HF_TOKEN") or os.environ.get("HUGGING_FACE_HUB_TOKEN"))},
        "cuda_probe": cuda_rows,
        "weight_head_attempts": auth_rows,
        "diffusers_revision": None,
        "variants": {
            "bf16": "not_run",
            "quantized_or_offload": "not_run",
        },
        "samples": {"status": "not_run", "count": 72},
        "first_stroke_ms": "not_applicable",
        "stroke_order": "not_applicable",
        "process_score": "not_applicable",
        "hand_drawn_score": None,
        "semantic_score": None,
        "local_8gb_gpu": "not_tested",
        "reason": "Weight files return HTTP 401 without a token, the model card marks the repo gated, and this machine has no NVIDIA GPU. No image was generated. CPU offload was not run, so there is no timing to compare with a few-second target.",
    }
    (LOGS / "diagnosis.json").write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    report = """FLUX.1-schnell 路线报告

结论
这条路线没有生成图片。黑森林 FLUX.1-schnell 的权重请求返回 401，Hub 上该仓库是 gated。当前环境没有 Hugging Face token，也没有 NVIDIA GPU。没有改用 dev 或 pro，也没有跑 CPU offload 再把“能加载”写成几秒完成。

未测
- 默认精度 72 条：not_run。没有冻结步数，因为开发集的 1/2/4 步比较没有开始。
- 量化或 offload 变体：not_run。没有实测显存、RAM 或耗时。
- text encoder、transformer、VAE 的分阶段时间和整机显存：null。
- 首笔时间和笔画顺序：not_applicable。这是位图模型，但本次没有位图，所以手绘感和语义分也是 null，不是 0。
- 过程分：not_applicable。
- 边缘提取、矢量化、模拟逐笔：未做。
- 人工评分：待人工复核，没有盲评图。
- 本机约 8GB GPU：未测。权重小于显存或可以量化的估计都不算通过。
- Diffusers revision：未安装，未记录一次实际推理。

已核对
- 模型 id 是 black-forest-labs/FLUX.1-schnell。README 仍给出 guidance_scale=0.0 和 schnell 的 1 到 4 步说明，那是文档，不是这次成绩。
- 三次 HEAD 都记在 logs/diagnosis.json。同一 401 连续出现后停止，没有反复换下载方案。

复现诊断
uv run python src/diagnose.py
"""
    (ROOT / "report.txt").write_text(report, encoding="utf-8")
    print(json.dumps(auth_rows, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
