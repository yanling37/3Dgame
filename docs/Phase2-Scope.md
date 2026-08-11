# Phase 2 范围：世界模拟基础设施升级

| 项 | 内容 |
|----|------|
| 目标 | 季节、数据驱动资源、事件状态、地图可视化、数学 FastForward |
| 不做 | NPC / 神之注视 / 英雄 / 神格 / 自走棋 |
| 入口 | `SimulationBootstrap` → `Boot.unity` |

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

- `MapVisualizationController`：人口采样点（≤40/区）、资源图腾、事件色点
