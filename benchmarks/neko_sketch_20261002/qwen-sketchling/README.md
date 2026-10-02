# Qwen3-4B + 笔画 JSON

正式变体是 CPU 上的 bitsandbytes NF4，不是 8GB GPU 成绩。权重在 `weights/`，体积约 8GB，不纳入 git。

```bash
cd benchmarks/neko_sketch_20261002/qwen-sketchling
uv sync
# 把 Qwen/Qwen3-4B-Instruct-2507 的 safetensors 和 tokenizer 放到 weights/
uv run python src/run_nf4.py dev
uv run python src/run_nf4.py formal
```

`src/eval_qwen.py` 会再探一次 CUDA 和 NF4。Sketchling 的 `sketch.stroke` / `sketch.loop` 与 `mountRenderable` 没有接到这次预览上；成功样本的 SVG 只画校验后的坐标。
