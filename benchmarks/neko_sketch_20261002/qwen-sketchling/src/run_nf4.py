#!/usr/bin/env python3
"""NF4 Qwen3-4B stroke run. CPU bitsandbytes, not an 8GB GPU result."""

from __future__ import annotations

import json
import math
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BENCH = ROOT.parent
CASES = json.loads((BENCH / "cases.json").read_text(encoding="utf-8"))
WEIGHTS = ROOT / "weights"
OUT = ROOT / "outputs" / "nf4-complete"
LOGS = ROOT / "logs"

# Frozen after two development rounds. Round 2 added a 6-stroke cap and made every
# development prompt time out, so the formal prompt is round 1.
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


def validate(text: str):
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
            return None, "unexpected keys"
        points = obj["points"]
        if not isinstance(points, list) or not 1 <= len(points) <= 12:
            return None, "point count"
        for point in points:
            if not isinstance(point, list) or len(point) != 2:
                return None, "point shape"
            x, y = point
            if isinstance(x, bool) or isinstance(y, bool) or not isinstance(x, (int, float)) or not isinstance(y, (int, float)):
                return None, "coordinate type"
            if not math.isfinite(float(x)) or not math.isfinite(float(y)) or x < 0 or y < 0 or x > 320 or y > 320:
                return None, "coordinate out of range"
        if obj["weight"] not in WEIGHTS_OK or obj["energy"] not in ENERGY_OK:
            return None, "style not in whitelist"
        looseness = obj["looseness"]
        if isinstance(looseness, bool) or not isinstance(looseness, (int, float)) or not math.isfinite(float(looseness)) or looseness < 0 or looseness > 1:
            return None, "looseness"
        if not isinstance(obj["closed"], bool):
            return None, "closed"
        strokes.append(obj)
        if len(strokes) > 24:
            return None, "more than 24 strokes"
    if not saw_done:
        return None, "missing done"
    if not strokes:
        return None, "no strokes"
    return strokes, None


def hwm_kb():
    for line in Path("/proc/self/status").read_text(encoding="utf-8").splitlines():
        if line.startswith("VmHWM:"):
            return int(line.split()[1])
    return None


def load_model():
    import torch
    from transformers import AutoModelForCausalLM, AutoTokenizer, BitsAndBytesConfig

    config = BitsAndBytesConfig(
        load_in_4bit=True,
        bnb_4bit_quant_type="nf4",
        bnb_4bit_compute_dtype=torch.bfloat16,
    )
    t0 = time.perf_counter()
    model = AutoModelForCausalLM.from_pretrained(WEIGHTS, quantization_config=config, device_map="cpu")
    tokenizer = AutoTokenizer.from_pretrained(WEIGHTS)
    model.eval()
    return model, tokenizer, time.perf_counter() - t0


def generate(model, tokenizer, prompt: str, seed: int):
    import torch
    from transformers import StoppingCriteria, StoppingCriteriaList, set_seed

    class TimeLimit(StoppingCriteria):
        def __init__(self, deadline):
            self.deadline = deadline
            self.hit = False

        def __call__(self, input_ids, scores, **kwargs):
            if time.perf_counter() >= self.deadline:
                self.hit = True
                return True
            return False

    messages = [{"role": "user", "content": TASK + prompt}]
    t0 = time.perf_counter()
    encoded = tokenizer.apply_chat_template(
        messages, add_generation_prompt=True, return_tensors="pt", return_dict=True
    )
    plan_s = time.perf_counter() - t0
    set_seed(seed)
    limiter = TimeLimit(time.perf_counter() + 30.0)
    gen_t0 = time.perf_counter()
    with torch.no_grad():
        output = model.generate(
            **encoded,
            max_new_tokens=1800,
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
    }


def write_svg(strokes, path: Path):
    width = {"light": 1.5, "confident": 2.5, "bold": 4.0}
    parts = [
        '<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 320 320">',
        '<rect width="320" height="320" fill="#ffffff"/>',
    ]
    for stroke in strokes:
        points = " ".join(f"{p[0]},{p[1]}" for p in stroke["points"])
        tag = "polygon" if stroke["closed"] else "polyline"
        parts.append(
            f'<{tag} points="{points}" fill="none" stroke="#000000" '
            f'stroke-width="{width[stroke["weight"]]}" stroke-linecap="round" stroke-linejoin="round"/>'
        )
    parts.append("</svg>")
    path.write_text("\n".join(parts), encoding="utf-8")


def one(model, tokenizer, case_id, group, prompt, seed, folder: Path):
    raw = generate(model, tokenizer, prompt, seed)
    strokes, error = validate(raw["text"])
    if raw["timed_out"]:
        status = "timeout"
    elif error:
        status = "invalid_output"
    else:
        status = "success"
    folder.mkdir(parents=True, exist_ok=True)
    stem = f"{case_id}_s{seed}"
    text_path = folder / f"{stem}.txt"
    text_path.write_text(raw["text"], encoding="utf-8")
    preview = None
    if strokes:
        preview = folder / f"{stem}.svg"
        write_svg(strokes, preview)
        (folder / f"{stem}.strokes.json").write_text(json.dumps(strokes, ensure_ascii=False), encoding="utf-8")
    record = {
        "route": "qwen-sketchling",
        "variant": "qwen3-4b-nf4-complete",
        "case_id": case_id,
        "group": group,
        "seed": seed,
        "status": status,
        "error": "exceeded 30s" if status == "timeout" else error,
        "prompt_sent_to_model": True,
        "reference_sent_to_model": False,
        "seed_algorithm": "transformers.set_seed + generation_config do_sample temperature 0.7 top_p 0.8 top_k 20",
        "new_token_count": raw["new_token_count"],
        "timings_ms": {
            "planning": raw["planning_s"] * 1000,
            "model_generate": raw["generate_s"] * 1000,
            "parse_vectorize": None,
            "first_content": raw["generate_s"] * 1000 if status == "success" else None,
            "complete_visible": raw["generate_s"] * 1000 if status == "success" else None,
            "playback_end": None,
            "first_stroke_complete": None,
        },
        "stroke_order": "model_line_order" if status == "success" else None,
        "renderer": "plain-svg-coordinates" if status == "success" else None,
        "renderer_note": "Preview draws the validated coordinates. Sketchling mountRenderable was not used for this file.",
        "raw_output": str(text_path),
        "preview": None if preview is None else str(preview),
        "stroke_count": None if strokes is None else len(strokes),
        "scores": {"hand_drawn": None, "semantic": None, "process": None},
        "text_excerpt": raw["text"][:1500],
    }
    print(case_id, seed, status, raw["new_token_count"], round(raw["generate_s"], 2), error or "", flush=True)
    return record


def main():
    stage = sys.argv[1] if len(sys.argv) > 1 else "dev"
    model, tokenizer, load_s = load_model()
    print("loaded", round(load_s, 2), "hwm_kb", hwm_kb(), flush=True)
    LOGS.mkdir(parents=True, exist_ok=True)
    (LOGS / "nf4-load.json").write_text(
        json.dumps({"load_s": load_s, "hwm_kb": hwm_kb(), "quant": "bitsandbytes nf4 cpu"}, indent=2),
        encoding="utf-8",
    )
    if stage == "dev":
        rows = []
        for index, prompt in enumerate(CASES["development_prompts"], start=1):
            rows.append(one(model, tokenizer, f"DEV{index:02d}", "development", prompt, 17, OUT / "dev"))
        (LOGS / "dev.jsonl").write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")
        return
    if stage != "formal":
        raise SystemExit(stage)
    rows = []
    for case in CASES["cases"]:
        for seed in CASES["seeds"]:
            rows.append(one(model, tokenizer, case["id"], case["group"], case["prompt"], seed, OUT))
    (ROOT / "requests.jsonl").write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8")
    print("records", len(rows), "hwm_kb", hwm_kb(), flush=True)


if __name__ == "__main__":
    main()
