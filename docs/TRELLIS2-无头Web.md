# TRELLIS.2 无头 Web / 多视图 / SkinToken

脚本在 `scripts/trellis2-headless/`。需要 GPU 弹性 IP SSH：`ubuntu@18.180.160.51`。

安装（在 GPU 上）：

```bash
bash /home/ubuntu/trellis2/app/scripts/install_on_gpu.sh
```

素材到位后：

```bash
bash /home/ubuntu/trellis2/app/scripts/run_multiview.sh texturing
bash /home/ubuntu/trellis2/app/scripts/run_multiview.sh full
bash /home/ubuntu/trellis2/app/scripts/run_projection.sh
bash /home/ubuntu/trellis2/app/scripts/run_rig.sh
```
