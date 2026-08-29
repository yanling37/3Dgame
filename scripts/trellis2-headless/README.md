# TRELLIS.2 无头 Web + 多视图 + Projection + SkinToken

运行位置：GPU `ubuntu@18.180.160.51`（只用弹性 IP），代码 `/home/ubuntu/trellis2/app`。

- Gradio `:7860` / `:7861`：conda `trellis2`（PyTorch `2.6.0+cu124`，不要改）
- ComfyUI `:8188`：conda `trellis2`，**ComfyUI 钉在 `v0.30.2`**。更新的 ComfyUI 依赖 `comfy-kitchen`（`list[int]` / 要 CUDA 13 的 torch），和冻结的 `2.6.0+cu124` 不兼容。
- conda `skintoken`（3.11）仍会创建，并用同一套 `2.6.0+cu124` 跑 SkinToken 的 pip，避免再把需要 `libcudart.so.13` 的 `torchaudio 2.11` 装进 `trellis2`。ComfyUI 进程本身仍走 `trellis2`，因为 `flash_attn` / `o_voxel` 是 3.10 扩展。

本目录脚本会复制到 GPU 的 `$TRELLIS2_APP/scripts/`。

## 用法（一行一条）

```bash
bash /home/ubuntu/trellis2/app/scripts/install_on_gpu.sh
bash /home/ubuntu/trellis2/app/scripts/launch_web.sh all
bash /home/ubuntu/trellis2/app/scripts/run_single.sh
bash /home/ubuntu/trellis2/app/scripts/run_multiview.sh texturing
bash /home/ubuntu/trellis2/app/scripts/run_multiview.sh full
bash /home/ubuntu/trellis2/app/scripts/run_multiview.sh mesh_only
bash /home/ubuntu/trellis2/app/scripts/run_projection.sh
bash /home/ubuntu/trellis2/app/scripts/run_rig.sh
bash /home/ubuntu/trellis2/app/scripts/run_and_publish.sh texturing
bash /home/ubuntu/trellis2/app/scripts/stop_web.sh
```

无参或 `--help` 会打印用法。三视图不齐时 `run_multiview.sh texturing` **直接报缺文件并退出 1**，不会假装成功。

## 端口与日志

| 服务 | 监听 | 日志 | PID |
| --- | --- | --- | --- |
| Gradio 图生3D | `0.0.0.0:7860` | `logs/app_7860.log` | `logs/app_7860.pid` |
| Gradio 只刷贴图 | `0.0.0.0:7861` | `logs/app_7861.log` | `logs/app_7861.pid` |
| ComfyUI | `0.0.0.0:8188` | `logs/comfyui_8188.log` | `logs/comfyui_8188.pid` |

官方 `app.py` 没有 `--listen`（那是 ComfyUI 参数）。`launch_gradio.py` 把 `--listen/--port` 转成 Gradio `server_name/server_port`。OpenCV 5 读不了 EXR，启动时用已有 `OpenEXR` 直接读（不要再走 `cv2.imread`，会递归）。

三个口都绑 `0.0.0.0`。安全组默认不放行 7860/7861/8188 时，本机 `127.0.0.1` 可用；公网要用 SG 或 Pixel Bite 反代，不要用临时公网 IP。

重启：`stop_web.sh` 然后 `launch_web.sh all`。

三视图放到 `assets/multiview/grace/`（`front.png` / `back.png` / `side.png`）。不要把白底抠成透明（裙子是白的）。`run_multiview.sh` 会转成不透明 RGBA、复制到 ComfyUI `input/` 根目录，并用 rembg（神经网络抠图，不是按白色色键）预处理。texturing 工作流会把 `side.png` 接到 `left_image`。

```bash
bash /home/ubuntu/trellis2/app/scripts/run_multiview.sh texturing
```

可选 systemd user：把 `ExecStart=` 指到上述 launch 命令；需要 `loginctl enable-linger ubuntu`。

## ComfyUI API

工作流是 UI 格式（`nodes`/`links`）。`comfy_run.py` 用 `/object_info` 转成 `/prompt` API。若拒收，日志里会有 `/prompt rejected` 原文。

## Projection / Qwen

`Projection_NvDiffrast_Qwen_XViews.json` 含 UUID 子图节点（Qwen 图像包）。**不擅自下大模型**。`run_projection.sh` 在仍有这些节点时 exit 2，状态为「待 Qwen」。SkinToken 用的 `Qwen3-0.6B` tokenizer 是另一回事，体积小、无 gate，允许下载到 `comfyui/models/skintoken/`。

## 素材

上传到：

- `assets/multiview/grace/front.png`
- `assets/multiview/grace/back.png`
- `assets/multiview/grace/side.png`（接到 texturing 的 `left_image`）
- 形状 OK 的 mesh：`assets/multiview/grace/mesh_from_singleview.glb`（install 会从最新 `grace_*.glb` 复制，不覆盖已有）

`Trellis2PreProcessImage` 官方实现按 RGBA 读第 4 通道。RGB 设定图会 `IndexError`。`patch_trellis2_nodes.py` 在缺 alpha 时补不透明通道，并把 ComfyUI-Trellis2 对 `tiled_flexible_dual_grid_to_mesh` 的导入改成可回退（当前 `o_voxel 0.0.1` / torch 2.6 没有该符号，与官方 TRELLIS.2 一致）。OOM 以外的错误不会把 resolution 降到 512。
