# TRELLIS.2 官方预览挂到快机中转站

页面：https://yanling3d.duckdns.org/trellis/

原先网上那页是 Google model-viewer 拖转 GLB，只适合看网格/PBR，**不是**官方预览。现已改成 microsoft/TRELLIS.2 `app.py` 的 6 模式 snapshot，model-viewer 仍作为折叠附加项保留。已有 `grace_20260820_0754.glb` 与 `T_20260819_1631.glb` 不会被覆盖。

实现与运维脚本在 `scripts/trellis2-preview/`。GPU 工作目录是 `/home/ubuntu/trellis2/app`（conda `trellis2`），发布目标是内网 `ec2-user@172.31.29.43` 的 `/var/www/trellis/`。

## 验收（2026-08-20）

### EXR

三个官方 EXR 都是真 OpenEXR（magic `v/1\\x01\\x02`），不是 Git LFS 指针。体积小于文档里的 ~994247，因为仓内是 DWAB 压缩的 1k latlong：

| 文件 | 字节 |
| --- | --- |
| `assets/hdri/forest.exr` | 552641 |
| `assets/hdri/sunset.exr` | 171164 |
| `assets/hdri/courtyard.exr` | 255126 |

OpenCV 5.0.0：`cv2.imread(exr)` 返回空（即使 `OPENCV_IO_ENABLE_OPENEXR=1`）。未重装 CUDA/PyTorch。conda `trellis2` 里装了 `OpenEXR==3.4.6`，读出 `(512,1024,3) float32`，并成功 `EnvMap` → `EnvironmentLight` cubemap。PyTorch 仍是 `2.6.0+cu124`。

### 6 模式

现有两份 GLB 无法还原 `MeshWithVoxel`，因此用烘焙贴图走 `MeshWithPbrMaterial` + `render_utils.render_snapshot`（官方同一套 `render_key`）。没有重跑 4B，也没有覆盖 GLB。下次 `run_grace.py` 会在 `to_glb` 之前对内存 mesh 出图。

默认视角是官方 `DEFAULT_STEP=3`（8 个方位滑条）。

### URL

https://yanling3d.duckdns.org/trellis/

`models.json` 新的在前：`grace_20260820_0754` 然后 `T_20260819_1631`。HDRI 三条已就绪，按钮未灰。
