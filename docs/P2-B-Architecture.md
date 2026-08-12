# P2-B Architecture：世界观察与信息表现层

| 项 | 内容 |
|----|------|
| 分支 | `cursor/p2b-observation-interface-2738` |
| 基线 main | `a7b5f91e31243881deef65ea20244f880d42dcdc`（P2-A squash merge） |
| 冻结协议 | [P2-A Freeze Protocol](P2-A-Freeze-Protocol.md) |
| 本阶段 | **骨架**：调查 + 只读观察接口 + 历史缓冲结构。不实现完整 UI / 折线图 / 报告 |

---

## 1. P2-A vs P2-B

| | P2-A | P2-B |
|--|------|------|
| 角色 | 世界如何运行（数学） | 玩家如何观察这个世界 |
| 状态 | **已冻结** | 表现层开发中 |
| 可改 | 仅批准的 P0/P1 | Map / HUD / Statistics / Graph / Report（只读 State） |

---

## 2. 统一 Simulation State（已存在）

**唯一真相源**：`SimulationWorld.State : WorldState`

| 类型 | 路径 | 职责 |
|------|------|------|
| `SimulationWorld` | `Assets/Scripts/Simulation/Core/SimulationWorld.cs` | 拥有 State；日推进 / 快进 / Reset；`OnDayAdvanced` |
| `WorldState` | `Assets/Scripts/Simulation/Data/WorldModels.cs` | 日历、季节、地区数组、数值停机标志 |
| `RegionState` | 同上 | 人口、资源、社会、事件、诊断 `Last*` |
| `RegionEvent` | 同上 | 事件类型 / 严重度 / StartDay / Duration；`EndDay` 为派生属性 |
| `ObserverInfluence` | `Player/ObserverInfluence.cs` | HUD 焦点写入各地区 `RegionObserverInfluence` |

**结论**：已有统一 Simulation State。P2-B **不得**再复制一套平行状态。

---

## 3. 数据入口调查（本阶段结论）

### 3.1 Population

- **字段**：`RegionState.Population`、`PopulationDelta`、`LastCarryingCapacity`、`LastNaturalDeath`、`LastDiseaseDeath`、`DiseasePressure`
- **日写入口**：`PopulationSystem.TickDay` ← `DailySimulation.SimulateDay`
- **快进入口**：`FastForwardSystem.ProjectChunk`（离散投影，非逐日 `TickDay`）

### 3.2 Resource

- **字段**：`RegionState.Resources[ResourceId]`、`ProductionCapacity[]`；诊断 `LastFoodProduction` / `LastFoodSpoilage` / `LastWaterFactor` / …
- **目录**：`ResourceCatalog` / `ResourceId`（Food 可腐；Magic=魔力，不可腐）
- **日写入口**：`ResourceSystem.TickDay`
- **快进入口**：`FastForwardSystem` 内 perishable / capacity / persistent 投影

### 3.3 Season

| 名称 | 存储或派生 | 来源 |
|------|------------|------|
| `CurrentSeason` | 存储 | `WorldState.SyncSeasonFromDay` / `SeasonSystem` |
| `SeasonIndex` | 存储（= `(int)CurrentSeason`） | 同上 |
| `DayInSeason` | **派生** getter | `((DayOfYear-1) % 90) + 1` |
| `SeasonProgress` | **派生** getter | `(DayInSeason-1)/90` |
| `Year` / `DayOfYear` / `TotalDays` | 存储 | `SeasonSystem.AdvanceCalendar` |

UI **禁止**自行按日期重算季节；读取上述字段即可。

### 3.4 Event

- **结构**：`RegionState.ActiveEvents : List<RegionEvent>`
- **字段**：`EventId`、`EventType`、`RegionId`、`Scope`、`StartDay`、`Duration`、`Severity`；`EndDay` / `IsActiveOn(totalDay)` 派生
- **日写入口**：`EventSystem.EvaluateAndApply`（日末）；年界 `ApplyYearTurn`
- **快进**：断点预报 + `ApplyBreakpoint`；过期清理

UI **禁止**重新判定事件条件；只读 `ActiveEvents`。

### 3.5 Daily Tick 生命周期

```text
SimulationWorld.AdvanceDay
  → DailySimulation.SimulateDay(State, …)
       SeasonSystem.UpdateSeason
       per region: Weather → Resource → Population → Society
       SeasonSystem.AdvanceCalendar
       [year roll] EventSystem.ApplyYearTurn
       EventSystem.EvaluateAndApply
  → OnDayAdvanced(State)
```

创建 / 重置：`ResetWorld` → `DefaultWorldFactory.CreateWorld()` + `Influence.Bind` + `OnDayAdvanced`。

### 3.6 FastForward 入口

```text
SimulationWorld.FastForwardYears(n)
  → FastForwardSystem.FastForwardYears(State, …)   // 内部 Clone + Project，无 TickDay 循环
  → State = result.State
  → Influence.Bind(State)
  → SeasonSystem.SyncFromCalendar(State)
  → OnDayAdvanced(State)
```

注意：快进会**替换** `State` 引用；观察层必须通过 `OnDayAdvanced`（或显式 Refresh）刷新，历史记录需对跳跃日做策略（见 §5）。

### 3.7 MapVisualization 当前读法

`MapVisualizationController`：

- 订阅 `OnDayAdvanced` → `Refresh()`
- 直接读 `world.State.Regions[i]`
- 人口：`Population` → sqrt 映射点数，**上限 40/区**（不是 1:1 GameObject）
- 资源：Totem 用 Food + Magic
- 事件：`ActiveEvents` 中 Severity 最高者着色

### 3.8 HUD 当前读法

`WorldObserverHud`：

- 订阅 `OnDayAdvanced` → `BuildStatusReport()` / 季节头信息
- 控制：`AdvanceDay` / `AdvanceDays` / `FastForwardYears` / `ResetWorld`
- Influence 滑条写入 State（玩家输入，非平行模拟）

---

## 4. 推荐的 P2-B 观察架构

```text
SimulationWorld (P2-A, frozen math)
    │  owns WorldState
    │  emits OnDayAdvanced(WorldState)
    ▼
Observation layer (P2-B, NEW — read only)
    ├── SimulationObservation.Capture(world) → immutable snapshots
    └── SimulationHistoryBuffer.Record / Sample
            │
            ├── Map Visualization
            ├── HUD
            ├── Statistics
            ├── Graph series
            └── Report export
```

### 原则

1. **只读**：观察层从 `WorldState` 拷贝快照字段；不调用人口/资源公式“重算显示值”。
2. **单源**：Map / HUD / Stats / Graph / Report 都消费同一快照或同一 HistoryBuffer。
3. **聚合可视化**：人口点继续 capped / density；禁止为每人创建一个 GameObject。
4. **事件 / 季节**：只读现有字段；不在 UI 重判。
5. **资源**：继续 `ResourceCatalog`；Food 可腐、Mana(`Magic`) 不可腐仅作显示语义。

### 本阶段已落地的类型

| 类型 | 路径 | 作用 |
|------|------|------|
| `WorldObservationSnapshot` / `RegionObservationSnapshot` / `EventObservation` | `Observation/ObservationModels.cs` | 不可变 DTO |
| `SimulationObservation` | `Observation/SimulationObservation.cs` | `Capture(WorldState)` 从 State 投影 |
| `SimulationHistoryBuffer` | `Observation/SimulationHistoryBuffer.cs` | 按 TotalDays 记录 / 采样接口 |

后续接线（下阶段）：在 `SimulationBootstrap` 或独立 `ObservationHost` 订阅 `OnDayAdvanced` → `history.Record(Capture(state))`。本阶段**不改** Bootstrap / Map / HUD，避免半成品耦合。

---

## 5. 历史记录方向（接口已预留）

每日至少记录（World 合计 + 每 Region）：

Population, Food, Water, Mana(`Magic`), DiseasePressure, Stability, Education, Faith, active Events

支持查询：Day 1 / 30 / 90 / 180 / 270 / 360 及任意 `TotalDays`。

**FastForward 注意**：

- 快进后 State 已是终点；中间日若未采样则 History 无点。
- 策略（后续实现）：(A) 快进前后各记一点并标记 gap；或 (B) 在 FF 内按断点/季末可选采样（仍只读结果，不改 FF 数学）。本阶段仅预留 API。

---

## 6. 发现的架构问题 / 缺口

1. **无历史缓冲**（直至本分支新增空实现）— 折线图/报告尚不能回放。
2. **Map/HUD 直接摸可变 `WorldState`**— 应逐步改为读 Snapshot，减少误写风险。
3. **`ResourceState` 类若存在但未用于 live stocks** — 显示层继续用 `Resources[]` + `ResourceId`。
4. **Mana 命名**：模拟 ID 为 `Magic`；UI 显示「魔力」即可，勿另造库存。
5. **快进替换 State 引用** — 观察缓存必须失效/重绑。
6. **地图尚未表现**：季节全局氛围、Water/多资源、事件 Severity/剩余时长等 — 属后续表现，不属本骨架。

---

## 7. 明确非目标（本任务）

- 不改 P2-A 核心数学 / Config 默认系数 / ProjectSettings / Unity 版本
- 不实现完整地图视觉、完整报告、完整折线图、复杂事件动画
- 不开始饥荒/流民等玩法扩展

---

## 8. 自检

- [x] 分支自 `a7b5f91…` 分出  
- [x] 架构调查完成并文档化  
- [x] 只读观察 DTO + Capture + HistoryBuffer  
- [x] 无头测试：Capture 与 State 一致  
- [x] **未修改** Population / Resource / Season / Weather / Event / FastForward 数学  
