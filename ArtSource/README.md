# ArtSource

Blender 源文件与导出中间产物（不经过 Unity `Library`）。

## 目录

| 路径 | 用途 |
|------|------|
| `Characters/` | 角色 `.blend` |
| `Environments/` | 场景模块 `.blend` |
| `Props/` | 道具 `.blend` |
| `Exports/` | 导出的 FBX（可再复制到 `Assets/Art/`） |

## 导出到 Unity

```bash
blender --background ArtSource/Props/SM_Crate.blend \
  --python scripts/blender/export_fbx.py -- \
  --out ArtSource/Exports/SM_Crate.fbx
```

然后将 FBX 放到 `Assets/Art/...`，回到 Unity 等待导入。

约定：单位米、Apply Scale、Forward -Z / Up Y（脚本已按 Unity 轴向导出）。
