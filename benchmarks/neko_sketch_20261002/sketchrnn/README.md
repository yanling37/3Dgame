# SketchRNN 路线

浏览器里的 `@magenta/sketch@0.2.0`，权重是官方 512 隐单元 `*.gen.json`。正式采样不把 prompt 或 reference 送进模型。

## 复现

```bash
cd benchmarks/neko_sketch_20261002/sketchrnn
npm ci
uv run python src/eval_sketchrnn.py all
```

`all` 会重新下载一次权重做计时（写到临时目录后删除）、跑奇偶校验、CPU 正式 24 次、冷启动和一次远程加载。WebGL 后端在这台机器上初始化失败，对应 72 条保持 `not_run`。

只重跑开发集：

```bash
uv run python src/eval_sketchrnn.py dev
```

冻结参数在 `frozen-config.json`。开发阶段已用完两轮：temperature 保持 0.65，pixelFactor 从 2.0 调到 4.0。

## 结果

`report.txt`、`metrics.json`、`requests.jsonl`、`environment.json`、`blind-review.html`、`outputs/`。
