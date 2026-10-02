#!/usr/bin/env python3
"""Qwen3-4B-Instruct-2507 stroke probe.

Default-precision and NF4 are separate variants. This machine has no CUDA,
so NF4 via bitsandbytes is attempted and then marked not_run. Default
precision is loaded on CPU only when the local bf16 weights are present.
Prompt text from cases.json is the only case text sent to the model.
"""

from __future__ import annotations

import json
import math
import os
import platform
import subprocess
import sys
import time
import traceback
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BENCH = ROOT.parent
CASES_PATH = BENCH / "cases.json"
WEIGHTS = ROOT / "weights"
LOGS = ROOT / "logs"
MODEL_ID = "Qwen/Qwen3-4B-Instruct-2507"

TASK = (
    "用无文字、黑色少量笔画的简笔画表达输入意思；输出画布坐标和笔画，不输出解释。\n"
    "每行一个 JSON 笔画："
    '{"points":[[x,y],...],"weight":"light|confident|bold","looseness":0.2,"energy":"calm|quick","closed":false}\n'
    '最后一行输出 {"done":true}。坐标必须在 0 到 320。不要输出解释。\n'
    "输入："
)
WEIGHTS_OK = {"light", "confident", "bold"}
ENERGY_OK = {"calm", "quick"}
KEYS = {"points", "weight", "looseness", "energy", "closed"}


def write_json(path: Path, obj) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def environment() -> dict:
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
    return {
        "os": platform.platform(),
        "cpu": cpu,
        "cpu_count": os.cpu_count(),
        "meminfo": mem,
        "gpu": None,
        "python": platform.python_version(),
        "nvidia_devices": [str(p) for p in Path("/dev").glob("nvidia*")],
    }


def cuda_probe(torch_module):
    rows = []
    for attempt in range(1, 4):
        t0 = time.perf_counter()
        row = {
            "attempt": attempt,
            "cuda_available": bool(torch_module.cuda.is_available()),
            "device_count": int(torch_module.cuda.device_count()),
            "seconds": time.perf_counter() - t0,
        }
        rows.append(row)
    return rows


def nf4_load_subprocess():
    code = r"""
import json, traceback
from pathlib import Path
root = Path(%r)
try:
    import torch
    from transformers import AutoModelForCausalLM, BitsAndBytesConfig
    cfg = BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_quant_type="nf4", bnb_4bit_compute_dtype=torch.bfloat16)
    model = AutoModelForCausalLM.from_pretrained(root, quantization_config=cfg, device_map="auto")
    print(json.dumps({"status": "loaded", "param0_dtype": str(next(model.parameters()).dtype)}))
except Exception:
    print(json.dumps({"status": "runtime_error", "traceback": traceback.format_exc()[-2500:]}))
""" % str(WEIGHTS)
    rows = []
    for attempt in range(1, 4):
        t0 = time.perf_counter()
        proc = subprocess.run(
            [sys.executable, "-c", code],
            text=True,
            capture_output=True,
            timeout=180,
        )
        parsed = None
        if proc.stdout.strip():
            try:
                parsed = json.loads(proc.stdout.strip().splitlines()[-1])
            except json.JSONDecodeError:
                parsed = None
        rows.append(
            {
                "attempt": attempt,
                "returncode": proc.returncode,
                "seconds": time.perf_counter() - t0,
                "parsed": parsed,
                "stderr_tail": (proc.stderr or "")[-1500:],
                "stdout_tail": (proc.stdout or "")[-1500:],
            }
        )
        if parsed and parsed.get("status") == "loaded":
            break
    return rows


def bitsandbytes_probe():
    rows = []
    for attempt in range(1, 4):
        t0 = time.perf_counter()
        row = {"attempt": attempt}
        try:
            import bitsandbytes as bnb  # type: ignore

            row["imported"] = True
            row["version"] = getattr(bnb, "__version__", None)
            from transformers import BitsAndBytesConfig

            cfg = BitsAndBytesConfig(load_in_4bit=True, bnb_4bit_quant_type="nf4")
            row["config"] = str(cfg)
            row["status"] = "config_constructed"
        except Exception as exc:
            row["imported"] = False
            row["status"] = "runtime_error"
            row["error"] = f"{type(exc).__name__}: {exc}"
        row["seconds"] = time.perf_counter() - t0
        rows.append(row)
        if row.get("status") != "config_constructed":
            # Same failure mode. The caller stops this variant after three attempts.
            continue
    return rows


def weights_ready() -> bool:
    needed = [
        "config.json",
        "model.safetensors.index.json",
        "model-00001-of-00003.safetensors",
        "model-00002-of-00003.safetensors",
        "model-00003-of-00003.safetensors",
        "tokenizer.json",
    ]
    return all((WEIGHTS / name).exists() for name in needed)


def validate_strokes(text: str):
    strokes = []
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    if not lines:
        return None, "empty output"
    saw_done = False
    for line in lines:
        if saw_done:
            return None, "tokens after done"
        try:
            obj = json.loads(line)
        except json.JSONDecodeError as exc:
            return None, f"invalid json: {exc.msg}"
        if not isinstance(obj, dict):
            return None, "line is not an object"
        if obj == {"done": True}:
            saw_done = True
            continue
        if set(obj.keys()) != KEYS:
            return None, f"unexpected keys {sorted(obj.keys())}"
        points = obj["points"]
        if not isinstance(points, list) or not 1 <= len(points) <= 12:
            return None, "point count"
        for point in points:
            if not isinstance(point, list) or len(point) != 2:
                return None, "point shape"
            x, y = point
            if isinstance(x, bool) or isinstance(y, bool):
                return None, "bool coordinate"
            if not isinstance(x, (int, float)) or not isinstance(y, (int, float)):
                return None, "coordinate type"
            if not math.isfinite(x) or not math.isfinite(y) or x < 0 or x > 320 or y < 0 or y > 320:
                return None, "coordinate out of range"
        if obj["weight"] not in WEIGHTS_OK or obj["energy"] not in ENERGY_OK:
            return None, "style not in whitelist"
        looseness = obj["looseness"]
        if isinstance(looseness, bool) or not isinstance(looseness, (int, float)):
            return None, "looseness type"
        if not math.isfinite(looseness) or looseness < 0 or looseness > 1:
            return None, "looseness range"
        if not isinstance(obj["closed"], bool):
            return None, "closed type"
        strokes.append(obj)
        if len(strokes) > 24:
            return None, "more than 24 strokes"
    if not saw_done:
        return None, "missing done"
    if not strokes:
        return None, "no strokes"
    return strokes, None


def rss_kb() -> int | None:
    for line in Path("/proc/self/status").read_text(encoding="utf-8").splitlines():
        if line.startswith("VmRSS:"):
            return int(line.split()[1])
        if line.startswith("VmHWM:"):
            hwm = int(line.split()[1])
    return None


def hwm_kb() -> int | None:
    for line in Path("/proc/self/status").read_text(encoding="utf-8").splitlines():
        if line.startswith("VmHWM:"):
            return int(line.split()[1])
    return None


def try_load_default():
    import torch
    from transformers import AutoModelForCausalLM, AutoTokenizer

    attempts = []
    model = None
    tokenizer = None
    for attempt in range(1, 4):
        t0 = time.perf_counter()
        row = {"attempt": attempt, "dtype": "bfloat16", "device": "cpu"}
        try:
            if tokenizer is None:
                tokenizer = AutoTokenizer.from_pretrained(WEIGHTS)
            model = AutoModelForCausalLM.from_pretrained(
                WEIGHTS,
                torch_dtype=torch.bfloat16,
                low_cpu_mem_usage=True,
            )
            model.eval()
            row["status"] = "loaded"
            row["seconds"] = time.perf_counter() - t0
            row["rss_kb"] = rss_kb()
            row["hwm_kb"] = hwm_kb()
            attempts.append(row)
            return model, tokenizer, attempts
        except Exception as exc:
            row["status"] = "runtime_error"
            row["error"] = f"{type(exc).__name__}: {exc}"
            row["traceback"] = traceback.format_exc()[-2000:]
            row["seconds"] = time.perf_counter() - t0
            row["rss_kb"] = rss_kb()
            attempts.append(row)
            model = None
    return None, tokenizer, attempts


def generate_once(model, tokenizer, prompt: str, seed: int, max_new_tokens: int, timeout_s: float = 30.0):
    import torch
    from transformers import StoppingCriteria, StoppingCriteriaList

    class TimeLimit(StoppingCriteria):
        def __init__(self, deadline):
            self.deadline = deadline
            self.hit = False

        def __call__(self, input_ids, scores, **kwargs):
            if time.perf_counter() >= self.deadline:
                self.hit = True
                return True
            return False

    user = TASK + prompt
    messages = [{"role": "user", "content": user}]
    device = next(model.parameters()).device
    t0 = time.perf_counter()
    encoded = tokenizer.apply_chat_template(
        messages, add_generation_prompt=True, return_tensors="pt", return_dict=True
    )
    encoded = {k: v.to(device) for k, v in encoded.items()}
    plan_s = time.perf_counter() - t0
    torch.manual_seed(seed)
    limiter = TimeLimit(time.perf_counter() + timeout_s)
    gen_t0 = time.perf_counter()
    with torch.no_grad():
        output = model.generate(
            **encoded,
            max_new_tokens=max_new_tokens,
            do_sample=True,
            temperature=0.7,
            top_p=0.8,
            top_k=20,
            stopping_criteria=StoppingCriteriaList([limiter]),
        )
    gen_s = time.perf_counter() - gen_t0
    new_tokens = output[0, encoded["input_ids"].shape[1] :]
    text = tokenizer.decode(new_tokens, skip_special_tokens=True)
    return {
        "planning_s": plan_s,
        "generate_s": gen_s,
        "timed_out": limiter.hit,
        "new_token_count": int(new_tokens.shape[0]),
        "text": text,
        "seed": seed,
        "max_new_tokens": max_new_tokens,
    }


def main():
    LOGS.mkdir(parents=True, exist_ok=True)
    env = environment()
    diag = {"environment": env, "model_id": MODEL_ID, "weights_ready": weights_ready()}
    try:
        import torch

        env["torch"] = torch.__version__
        diag["cuda_probe"] = cuda_probe(torch)
    except Exception as exc:
        diag["torch_import_error"] = f"{type(exc).__name__}: {exc}"
        diag["cuda_probe"] = None
    diag["bitsandbytes_probe"] = bitsandbytes_probe()
    cuda_ok = bool(diag.get("cuda_probe")) and any(row["cuda_available"] for row in diag["cuda_probe"])
    if weights_ready():
        diag["nf4_load_attempts"] = nf4_load_subprocess()
        loaded = any((row.get("parsed") or {}).get("status") == "loaded" for row in diag["nf4_load_attempts"])
        diag["nf4_variant"] = {
            "name": "qwen3-4b-nf4",
            "status": "loaded_but_not_scored" if loaded else "not_run",
            "cuda_available": cuda_ok,
            "reason": None
            if loaded
            else "Three NF4 load attempts failed. No NF4 samples were generated. This does not show that 4-bit fits in 8GB.",
            "records": "72 not_run",
        }
    else:
        diag["nf4_variant"] = {
            "name": "qwen3-4b-nf4",
            "status": "not_run",
            "cuda_available": cuda_ok,
            "reason": "Weights were not local yet, so NF4 from_pretrained was not started.",
            "records": "72 not_run",
        }
    if not weights_ready():
        diag["bf16_variant"] = {
            "name": "qwen3-4b-bf16-complete",
            "status": "not_run",
            "reason": "Local bf16 weights are incomplete, so default-precision generation was not started.",
        }
        write_json(LOGS / "diagnosis.json", diag)
        print(json.dumps(diag, ensure_ascii=False, indent=2)[:4000])
        return
    model, tokenizer, attempts = try_load_default()
    diag["bf16_load_attempts"] = attempts
    if model is None:
        diag["bf16_variant"] = {
            "name": "qwen3-4b-bf16-complete",
            "status": "not_run",
            "reason": "Three load attempts failed with the same class of error. Formal samples were not started.",
        }
        write_json(LOGS / "diagnosis.json", diag)
        print(json.dumps({k: diag[k] for k in diag if k != "environment"}, ensure_ascii=False, indent=2)[:4000])
        return
    # Development smoke: first development prompt, three seeds, 30s cap.
    cases = json.loads(CASES_PATH.read_text(encoding="utf-8"))
    dev_prompts = cases["development_prompts"]
    smokes = []
    consecutive_timeouts = 0
    for seed in (17, 29, 43):
        row = generate_once(model, tokenizer, dev_prompts[0], seed, 1800, 30.0)
        strokes, error = validate_strokes(row["text"])
        if row["timed_out"]:
            status = "timeout"
            consecutive_timeouts += 1
        elif error:
            status = "invalid_output"
            consecutive_timeouts = 0
        else:
            status = "success"
            consecutive_timeouts = 0
        smokes.append({
            "prompt": dev_prompts[0],
            "seed": seed,
            "status": status,
            "error": error,
            "timed_out": row["timed_out"],
            "new_token_count": row["new_token_count"],
            "generate_s": row["generate_s"],
            "planning_s": row["planning_s"],
            "text": row["text"][:2000],
            "stroke_count": None if strokes is None else len(strokes),
        })
        if consecutive_timeouts >= 3:
            break
    diag["bf16_dev_smoke"] = smokes
    diag["bf16_variant"] = {
        "name": "qwen3-4b-bf16-complete",
        "status": "smoke_only" if any(row["status"] != "success" for row in smokes) else "smoke_passed",
        "formal_72": "not_run",
        "reason": "Stopped after the development smoke. Formal 72-row run starts only when a smoke returns a valid done line inside 30 seconds.",
    }
    diag["streaming_variant"] = {
        "name": "qwen3-4b-bf16-stream",
        "status": "not_run",
        "reason": "Not started. Streaming is a separate variant and was not measured in this pass.",
    }
    write_json(LOGS / "diagnosis.json", diag)
    (LOGS / "smoke.jsonl").write_text(
        "\n".join(json.dumps(row, ensure_ascii=False) for row in smokes) + "\n", encoding="utf-8"
    )
    print(json.dumps(smokes, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
