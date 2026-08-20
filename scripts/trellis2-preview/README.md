# TRELLIS.2 官方预览（快机中转站）

这不是 Google model-viewer。官方预览是 `microsoft/TRELLIS.2` `app.py` 的 6 个 `render_snapshot` 模式：

| 按钮 | `render_key` | 文件后缀 |
| --- | --- | --- |
| Normal | `normal` | `_normal.png` |
| Clay render | `clay` | `_clay.png` |
| Base color | `base_color` | `_base_color.png` |
| HDRI forest | `shaded_forest` | `_hdri_forest.png` |
| HDRI sunset | `shaded_sunset` | `_hdri_sunset.png` |
| HDRI courtyard | `shaded_courtyard` | `_hdri_courtyard.png` |

HDRI 只给 snapshot 打环境光，不是再生成一遍 3D。默认模式与官方一致：HDRI forest；若 EXR/EnvMap 未就绪则回退 Normal，HDRI 按钮灰掉并写「EXR 未就绪」。

## 运行位置

- GPU：`ubuntu@18.180.160.51`，目录 `/home/ubuntu/trellis2/app`，conda `trellis2`
- 中转：`ec2-user@172.31.29.43` → `https://yanling3d.duckdns.org/trellis/`
- 禁止把 Gradio `app.py` 挂到 GPU 弹性 IP；禁止覆盖已有 `grace_20260820_0754.glb` / `T_20260819_1631.glb`

## GPU 上的脚本

```bash
source /home/ubuntu/miniconda3/etc/profile.d/conda.sh
conda activate trellis2
cd /home/ubuntu/trellis2/app
python probe_hdri.py --wrap-envmap
python snapshot_official.py --glb /home/ubuntu/trellis2/app/grace_20260820_0754.glb
./publish_preview.sh
```

下次从图片生成会自动出 6 套图（`run_grace.py` 在 `to_glb` 之前对内存里的 `MeshWithVoxel` 调用 `render_snapshot`）。已有 GLB 没有体素缓存，快照走 `MeshWithPbrMaterial`（烘焙贴图），不会重跑 4B。

## OpenCV / EXR

机器上是 OpenCV 5.0.0，`cv2.imread(*.exr)` 经常返回空图。不要为此重装 CUDA/PyTorch。`hdri_utils.py` 用 `OpenEXR==3.4.6` 读官方 `assets/hdri/{forest,sunset,courtyard}.exr`（真文件，不是 LFS 指针；forest 552641 字节），再包 `trellis2.renderers.EnvMap`。

若新环境还没有该包：

```bash
conda activate trellis2
pip install OpenEXR==3.4.6
```

不要动 PyTorch / CUDA / Driver。

## 发布约束

`update-web.sh` 的 `rsync --delete` 会清游戏站 html 目录。预览文件必须继续放 `/var/www/trellis/`（nginx `location ^~ /trellis/`），不要放进 `/usr/share/nginx/html`。
