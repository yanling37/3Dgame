# 3Dgame / Divine World Simulation

用 **Unity** 做主开发、**Blender** 做建模的动态文明模拟项目。

## 设计文档（优先阅读）

- [**Game Design Document**](docs/GameDesignDocument.md) — 《Divine World Simulation》GDD v1.0
- [**Phase 1 范围**](docs/Phase1-Scope.md) — 当前实现锁定
- [开发环境](docs/开发环境.md)
- [故障排除（MonoManager）](docs/故障排除-MonoManager.md)

## 现在能玩什么（Phase 1）

打开场景 **`Assets/Scenes/Boot.unity`**（或任意空场景挂上 `SimulationBootstrap`）：

- 自动跑世界：教廷区 / 帝国区 / 海
- 种族：人类、人鱼
- 左侧观察仪：暂停、+日、微调生育/收成/疫病/稳定
- 场景里三个「圆柱+圆球」图腾随人口缩放

## 快速开始（本机）

1. Unity **2022.3.62f3c1** 打开工程（推荐 `D:\MyProject\3dgame2`，用 sync 脚本同步）
2. 打开 `Boot` 场景 → Play
3. 用观察仪微调概率，看三地人口与资源变化

```powershell
powershell -ExecutionPolicy Bypass -File scripts\setup\sync-from-repo-to-3dgame2.ps1 -Dest "D:\MyProject\3dgame2"
```

## 技术选型

| 用途 | 软件 |
|------|------|
| 游戏引擎 | Unity **2022.3.62f3c1 LTS** + URP + C# |
| 建模 | Blender **4.2 LTS** |
| 版本管理 | Git + Git LFS |

## 当前状态

- GDD + Phase1 范围已入库
- Phase 1 纯模拟核心 + 观察仪 UI + 极简 3D 标记已提交
- 下一步：存档 JSON、组织层（Level 1）、更明确的事件条件表
