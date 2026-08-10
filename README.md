# 3Dgame / Divine World Simulation

用 **Unity** 做主开发、**Blender** 做建模的动态文明模拟项目。

## 设计文档（优先阅读）

- [**Game Design Document**](docs/GameDesignDocument.md) — 《Divine World Simulation》游戏设计需求说明书（v1.0）
- [开发环境](docs/开发环境.md)
- [故障排除（MonoManager）](docs/故障排除-MonoManager.md)
- [早期工程需求（已过时，仅作技术骨架参考）](docs/需求文档.md)

> 后续将另拆《程序设计文档》《算法设计文档》，与 GDD 分离。

## 快速开始（本机）

1. 安装 **Unity Hub**，使用已安装的 Editor **`2022.3.62f3c1` LTS**（或同系列 2022.3 LTS）
2. 安装 **Blender 4.2 LTS**
3. 拉取分支 `cursor/unity-blender-requirements-55cc`
4. Unity Hub → **Open** → 选择本仓库根目录
5. 若提示版本，选 **使用 2022.3.62f3c1 打开**
6. 当前工程仍是技术骨架；模拟玩法按 GDD **第一阶段（纯模拟）**推进

## 本机推荐路径（MonoManager 无法打开旧骨架时）

若你已用 Hub 新建 URP 项目在 `D:\MyProject\3dgame2`：

```powershell
# 先 clone/pull 本仓库，再执行：
powershell -ExecutionPolicy Bypass -File scripts\setup\sync-from-repo-to-3dgame2.ps1 -Dest "D:\MyProject\3dgame2"
```

之后只用 Hub 打开 `D:\MyProject\3dgame2`。

## 技术选型（已锁定）

| 用途 | 软件 |
|------|------|
| 游戏引擎 | Unity **2022.3.62f3c1 LTS** + URP + C# |
| 建模 | Blender **4.2 LTS** |
| 版本管理 | Git + Git LFS |

## 仓库结构

```text
Assets/           Unity 资源与脚本
Packages/         Unity 包清单
ProjectSettings/  Unity 工程设置
ArtSource/        Blender 源文件与 FBX 导出
docs/             GDD / 环境 / 故障排除
scripts/setup/    环境检查 / 安装脚本
scripts/blender/  Blender 导出与示例脚本
```

## 当前状态

- GDD v1.0 已入库：`docs/GameDesignDocument.md`
- Unity 工程骨架可用于后续模拟与表现层接入
- 下一步建议：按 GDD 第一阶段实现纯模拟（时间 / 资源 / 人口 / 地区）
