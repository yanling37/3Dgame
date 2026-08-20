# TRELLIS.2 官方预览挂到快机中转站

页面：https://yanling3d.duckdns.org/trellis/

原先网上那页是 Google model-viewer 拖转 GLB，只适合看网格/PBR，**不是**官方预览。现已改成 microsoft/TRELLIS.2 `app.py` 的 6 模式 snapshot，model-viewer 仍作为折叠附加项保留。已有 `grace_20260820_0754.glb` 与 `T_20260819_1631.glb` 不会被覆盖。

实现与运维脚本在 `scripts/trellis2-preview/`。GPU 工作目录是 `/home/ubuntu/trellis2/app`（conda `trellis2`），发布目标是内网 `ec2-user@172.31.29.43` 的 `/var/www/trellis/`。
