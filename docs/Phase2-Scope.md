# Phase 2 范围：世界模拟基础设施升级

| 项 | 内容 |
|----|------|
| 目标 | 季节、数据驱动资源、事件状态、地图可视化、数学 FastForward |
| 不做 | NPC / 神之注视 / 英雄 / 神格 / 自走棋 |
| 入口 | `SimulationBootstrap` → `Boot.unity` |
| **P2-A 状态** | **已冻结** — 见 [P2-A 冻结协议](P2-A-Freeze-Protocol.md) |

> **P2-B 开工约束**：可读取 / 显示 / 统计 / 可视化 Simulation State；**不得**为 UI 便利改动人口、资源、季节、天气、事件、FastForward 核心数学。平衡类观感问题属 Design，须上报决策，不得自行改公式。

## 日流程

```text
SyncSeason
 → ResourceSystem.TickDay (produce → consume → spoil)
 → PopulationSystem.TickDay
 → Calendar +1
 → EventSystem.EvaluateAndApply
 → OnDayAdvanced (HUD / Map)
```

## 快进流程

```text
FastForwardYears(n)
 → clone state
 → while not at target:
      forecast breakpoints in season chunk
      if breakpoint: ProjectDays(to break) → ApplyEventImpact → continue
      else: ProjectSeasonChunk(days)
 → replace live State
```

**禁止** `for days: TickDay()`。

## 关键

- 360 日/年，4×90 日
- 影响：粮/水产量、出生、死亡、疫病、天气区间

## 资源

- `ResourceCatalog` 数据驱动
- Food：易腐（spoil）
- Mana(`Magic`)：持久无腐烂

## 地图

- `ObservationHost` → `RegionObservationSnapshot` → `PopulationVisualizer`：参数化人口占位点（每区最多 `MaxMarkersPerRegion`）
- `MapVisualizationController`：资源图腾、事件色点
