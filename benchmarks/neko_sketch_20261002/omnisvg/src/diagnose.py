#!/usr/bin/env python3
"""OmniSVG blocking diagnosis. Does not download weights or offload to RAM."""

from __future__ import annotations

import json
import os
import platform
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOGS = ROOT / "logs"


def main():
    LOGS.mkdir(parents=True, exist_ok=True)
    cuda_rows = []
    for attempt in range(1, 4):
        t0 = time.perf_counter()
        devices = list(Path("/dev").glob("nvidia*"))
        cuda_rows.append(
            {
                "attempt": attempt,
                "nvidia_devices": [str(p) for p in devices],
                "nvidia_smi": "command not found" if os.system("command -v nvidia-smi >/dev/null 2>&1") else "present",
                "seconds": time.perf_counter() - t0,
                "status": "blocked_no_gpu",
            }
        )
    head = []
    url = "https://huggingface.co/OmniSVG/OmniSVG1.1_4B/resolve/main/pytorch_model.bin"
    # HEAD only, three times, to record size. The file is not saved.
    for attempt in range(1, 4):
        req = urllib.request.Request(url, method="HEAD")
        t0 = time.perf_counter()
        try:
            with urllib.request.urlopen(req, timeout=60) as response:
                head.append(
                    {
                        "attempt": attempt,
                        "status": response.status,
                        "bytes": response.headers.get("Content-Length"),
                        "seconds": time.perf_counter() - t0,
                    }
                )
        except Exception as exc:
            head.append({"attempt": attempt, "error": str(exc), "seconds": time.perf_counter() - t0})
    doc = {
        "route": "omnisvg",
        "model": "OmniSVG/OmniSVG1.1_4B",
        "revision_seen_on_hub_api": "117d4c4541839e029435",
        "official_gpu_memory": "16G for OmniSVG1.1-4B, from the model card inference table",
        "official_time_note": "Model card lists about 4.08/8.68/18.07 seconds per 256/512/1024 SVG tokens on that GPU reference, not on this machine.",
        "weight_file_bytes": 7694002862,
        "environment": {
            "os": platform.platform(),
            "cpu_count": os.cpu_count(),
            "gpu": None,
        },
        "cuda_probe": cuda_rows,
        "weight_head": head,
        "quantized_8gb_variant": "not_run",
        "quantized_8gb_reason": "No quantization that is shown to support this model's custom tokenizer and decoder was run. 8GB is not verified.",
        "base_variant": "not_run",
        "base_reason": "No NVIDIA GPU. Official inference memory is about 16GB. Weights were not loaded, and system RAM was not used as an offload path.",
        "samples": {"status": "not_run", "count": 72},
        "human_scores": None,
        "local_8gb_gpu": "not_tested",
    }
    (LOGS / "diagnosis.json").write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    report = """OmniSVG 路线报告

结论
这条路线没有进入采样。云端机器没有 NVIDIA GPU，官方模型卡把 OmniSVG1.1-4B 的推理显存写成约 16GB。按方案要求，资源不够时提交阻塞报告，不用系统内存卸载后再说成 GPU 上几秒跑完。

未测
- 基础精度 72 条：not_run。没有加载 pytorch_model.bin，也没有跑开发集或正式集。
- 8GB 量化变体：not_run。没有找到并实际跑通一套支持其自定义 tokenizer / decoder 的量化。不能把权重大小 7.6GB 写成 8GB 通过。
- 人工手绘感、语义、过程分：null，待人工复核。没有画面。
- 本机 RTX 5050 约 8GB：未测。
- 首笔、稳定态 p50/p95、冷启动、播放：null。
- 后处理逐笔播放：未做，因为没有 SVG 输出。

已核对
- Hub 模型 OmniSVG/OmniSVG1.1_4B，API 上看到的 sha 前缀 117d4c4541839e029435。没有改用旧的 3B 权重。
- pytorch_model.bin 的 HEAD 大小约 7694002862 字节。三次无 GPU 检查都是同一阻塞。
- 模型卡表格写的是 GPU Memory Usage 16G，以及 256/512/1024 token 约 4.08/8.68/18.07 秒。那是官方参考，不是这次机器的成绩。

复现诊断
uv run python src/diagnose.py
"""
    (ROOT / "report.txt").write_text(report, encoding="utf-8")
    print(report)


if __name__ == "__main__":
    main()
