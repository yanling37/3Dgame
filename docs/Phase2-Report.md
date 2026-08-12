# Phase 2 完成报告

## 1. 修改了哪些文件

| 文件 | 修改内容 |
|------|----------|
| `Assets/Scripts/Simulation/Data/Enums.cs` | 增加 `SeasonId`、`SimEventType` |
| `Assets/Scripts/Simulation/Data/WorldModels.cs` | 季节字段、`RegionEvent`、`Clone`、人口趋势 |
| `Assets/Scripts/Simulation/Data/ResourceDefinition.cs` | **新** 资源定义与 Catalog |
| `Assets/Scripts/Simulation/Data/DefaultWorldFactory.cs` | 初始化季节 |
| `Assets/Scripts/Simulation/Systems/SeasonSystem.cs` | **新** 季节时钟/修正/天气区间 |
| `Assets/Scripts/Simulation/Systems/SimulationSystems.cs` | 数据驱动资源+腐烂；人口吃季节修正 |
| `Assets/Scripts/Simulation/Systems/EventSystem.cs` | **新** 事件状态/预报/影响 |
| `Assets/Scripts/Simulation/Systems/FastForwardSystem.cs` | **新** 季节宏观投影+断点 |
| `Assets/Scripts/Simulation/Testing/FastForwardConsistencyTest.cs` | **新** Daily vs Fast 对比 |
| `Assets/Scripts/Simulation/Core/SimulationWorld.cs` | 新日序、快进 API、一致性入口 |
| `Assets/Scripts/Simulation/Presentation/MapVisualizationController.cs` | **新** 地图数据绑定可视化 |
| `Assets/Scripts/Simulation/UI/WorldObserverHud.cs` | 季节显示、+1年/+10年、一致性按钮 |
| `Assets/Scripts/Simulation/SimulationBootstrap.cs` | 接线 MapVisualization |
| `docs/Phase2-Scope.md` | 范围说明 |
| `docs/Phase2-Report.md` | 本报告 |

## 2. 当前实际执行流程

### 一天开始 → 结束

1. `SeasonSystem.SyncFromCalendar`：由 `DayOfYear` 写 `CurrentSeason/SeasonIndex/SeasonProgress`
2. 每个 Region：
   - `ResourceSystem.TickDay`：按 Catalog 生产→消耗→腐烂；缺粮反馈；季节天气游走
   - `PopulationSystem.TickDay`：出生/死亡（含季节修正）；教育/信仰缓变
3. `DayOfYear++`、`TotalDays++`；跨年则 `Year++` + `YearTurn` 事件
4. `EventSystem.EvaluateAndApply`：刷新正式事件列表与 `LastEvent` 摘要
5. `OnDayAdvanced` → HUD / 地图刷新

### FastForward(1 year)

1. Clone `WorldState`（不影响对比用的原始快照）
2. 按季（或断点前）调用 `ProjectSeasonChunk`：**不**循环 `TickDay`
3. `EventForecast` 若在窗口内命中重大事件 → 先投影到断点日 → `ApplyEventImpact` → 继续
4. 用结果替换 live `State` 并刷新表现

## 3. 数学公式（当前实现）

### 季节

- `SeasonIndex = (DayOfYear-1) / 90`，`SeasonProgress = (dayInSeason)/90`
- 出生修正：春1.15 / 夏1.05 / 秋0.95 / 冬0.8
- 死亡修正：春0.95 / 夏1.0 / 秋1.05 / 冬1.25
- 疫病修正：春0.9 / 夏1.15 / 秋1.0 / 冬1.2
- 天气在季节 `[min,max]` 内随机游走（快进用中位基线）

### 资源（日）

```text
Prod = EstimateDailyProduction(def, season, labor, tech, weather, harvest, race)
Stock' = Stock + Prod - Pop*ConsumePerCapita - (CanSpoil ? Stock*SpoilRate : 0)
```

- Food 产能：`max(80, labor*400) * 0.02 * 50 * season * tech * weather * harvest * Growth`（**不**随库存指数放大）
- Mana：`Pop * 0.0001 * MagicAffinity * seaMul * season`
- Food 腐烂：`SpoilRate = 0.008 / day`

### 人口（日）

```text
birth = Pop * 0.00035 * Fertility * BirthSeason * blessing * (0.5 + foodRatio)
naturalDeath = Pop * (0.00022 / Lifespan) * DeathSeason
diseaseDeath = Pop * Disease * 0.0015 * DiseaseSeason * curse
Pop' = max(100, Pop + birth - naturalDeath - diseaseDeath)
```

### 快进（季）

- 易腐 Food：`S_inf=(P-C)/s`，`S(t)=S_inf+(S0-S_inf)*e^{-s t}`
- 其他资源：`Δ = (P-C)*days`
- 人口 logistic：`ΔP = r * P * (1 - P/K) * days`，`K=CarryingCapacity(food产能,水)`

## 4. 快速模拟测试（1 年）

同初始世界、seed=`20260810`，镜像公式离线对比（与 C# 同结构；Unity 内请点 HUD「一致性测试 1年」跑正式结果）：

| 指标 | Daily 1y | FastForward 1y | Error |
|------|----------|----------------|-------|
| PopTotal | 138937 | 132026 | **5.0%** |
| FoodTotal | 119394 | 152018 | 27.3%（略超 25% soft） |
| ManaTotal | 7915 | 7829 | **1.1%** |
| WaterTotal | 23943 | 24367 | **1.8%** |
| DiseaseAvg | 0.01 | 0.14 | 13.1% |
| StabilityAvg | 1.25 | 0.96 | 23.6% |
| EducationAvg | 0.31 | 0.32 | **0.5%** |
| FaithAvg | 0.37 | 0.38 | **0.8%** |

结论：快进是**稳定宏观近似**；人口/Mana/水接近，Food/稳定有可见偏差（可接受 soft warn）。正式数值以 Play Mode 按钮输出为准。

## 5. 编译/运行问题

- 云端无 Unity Editor，**未能在本环境实际点 Play**；代码为 Unity 2022.3 风格 C#，需本机打开工程验证。
- 若本机仍用 `D:\MyProject\3dgame2`：请 `git pull` 后 sync `Assets/Scripts/Simulation`。
- 旧 `SimpleRegionMarkers` 仍保留但 Bootstrap 已改用 `MapVisualizationController`。

## 6. 下一步建议（不进入 NPC）

1. 收紧 Food 快进稳定态参数，使 Food error ≤15%
2. JSON 导出/读档 `WorldState`
3. 事件预报表数据化（可调阈值，而不是写死在 EventSystem）
4. 地图相机/地区点击查看详情面板

## 7. P2-A 冻结状态

**P2-A 已验收并冻结。** 完整协议与突破条件见 [P2-A 冻结协议](P2-A-Freeze-Protocol.md)。

P2-B 起：Map / HUD / Statistics 只能消费 Simulation State；不得改人口/资源/季节/天气/事件/FastForward 核心数学。观感类平衡问题属 Design，须上报决策。
