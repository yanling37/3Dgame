# 3Dgame / Divine World Simulation

用 **Unity** 做主开发、**Blender** 做建模的动态文明模拟项目。

## 设计文档（优先阅读）

- [**Game Design Document**](docs/GameDesignDocument.md) — 《Divine World Simulation》GDD v1.0
- [**Phase 1 范围**](docs/Phase1-Scope.md) — 当前实现锁定
- [开发环境](docs/开发环境.md)
- [故障排除（MonoManager）](docs/故障排除-MonoManager.md) — **本机反复打不开时必读；云端勿提交骨架 ProjectSettings**

## 现在能玩什么（Phase 2 / 2-A）

打开场景 **`Assets/Scenes/Boot.unity`**：

- 季节（春夏秋冬，90 日/季）进入模拟公式
- 数据驱动资源（粮食易腐 / Mana 持久）与地区产能
- 承载力人口 + 地区独立 ObserverInfluence
- 正式事件状态 + 地图色点
- 人口采样点密度（非 NPC）
- **+1年/+10年数学快进**（非逐日循环）
- HUD「一致性测试 1年」

设计文档：

- [GDD](docs/GameDesignDocument.md)
- [Phase 2 范围](docs/Phase2-Scope.md)
- [Phase 2 报告](docs/Phase2-Report.md)

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
- **Phase 2-A**：360 天四季、季节天气、资源生命周期（Food/Water/Magic）、承载力人口、地区独立 ObserverInfluence
- **Phase 2**：事件系统、地图可视化、数学快进与一致性测试
- 无头测试：`dotnet run --project Tools/HeadlessSimTests/HeadlessSimTests/HeadlessSimTests.csproj -c Release`
- 下一步（P2-B / P2-C）：饥荒/流民；历史数据/图表
