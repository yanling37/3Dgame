# P2-A 冻结协议 / P2-B 开工前基线

| 项 | 内容 |
|----|------|
| 状态 | **P2-A 已完成并冻结** |
| 生效 | 即日起（P2-B 开发全程） |
| 目的 | P2-B 的 UI / 地图 / 统计 / 报告 / 可视化 **不得擅自改动** 已验收的 P2-A 模拟数学 |

---

## 一、P2-A 冻结范围（Baseline）

以下系统全部视为 **P2-A Baseline**：

1. **世界时间** — 1 年 = 360 天；4 季；每季 90 天  
2. **季节系统** — 春 / 夏 / 秋 / 冬  
3. **天气系统**  
4. **人口系统** — 出生、自然死亡、疾病死亡、超载死亡（本实现为 logistic 出生抑制，无独立超载死亡项）、人口与承载力关系  
5. **承载力系统**  
6. **水资源系统** — 水储量、水容量、水消耗、水可用性  
7. **粮食系统** — 生产、消耗、腐烂、季节影响、水对农业生产的影响  
8. **疾病系统**  
9. **社会稳定**  
10. **教育**  
11. **信仰**  
12. **知识**  
13. **魔力**  
14. **区域神明微调** — Fertility / Harvest / Disease / Stability  
15. **地区独立事件**  
16. **世界级事件**  
17. **天灾持续时间与结束**  
18. **FastForward**  
19. **NumericGuard**

### 冻结的核心代码（数学层）

未经批准，**禁止改核心数学**（含默认系数与公式结构）：

| 系统 | 路径 |
|------|------|
| Population | `Assets/Scripts/Simulation/Systems/PopulationSystem.cs` |
| Resources | `Assets/Scripts/Simulation/Systems/ResourceSystem.cs` |
| Season | `Assets/Scripts/Simulation/Systems/SeasonSystem.cs` |
| Weather | `Assets/Scripts/Simulation/Systems/WeatherSystem.cs` |
| Events | `Assets/Scripts/Simulation/Systems/EventSystem.cs` |
| FastForward | `Assets/Scripts/Simulation/Systems/FastForwardSystem.cs` |
| Society | `Assets/Scripts/Simulation/Systems/SocietySystem.cs` |
| Config defaults | `Assets/Scripts/Simulation/Data/SimulationConfig.cs` |
| Daily pipeline | `Assets/Scripts/Simulation/Core/DailySimulation.cs` |

只读诊断 / 无头回归（允许新增**测试与报告**，不得借机改公式）：

- `Tools/HeadlessSimTests/`
- `Assets/Scripts/Simulation/Testing/`

---

## 二、已通过的验收基线

P2-A2 诊断已完成。已确认：

| 项 | 结论 |
|----|------|
| FertilityModifier | 正常（进入出生公式；0.70→1.00→1.30 单调） |
| Logistic | 方向正确 |
| Water → Food | 方向正确 |
| Water → CarryingCapacity | 方向正确 |
| Disease modifier | 方向正确 |
| Regional Event | 地区独立 |
| NaturalDisaster | 有 Start/End，不会永久残留 |
| Numeric Stability | 100 年测试无 NaN / Infinity |
| FastForward 360 天 | Population ≈0.4%；Food ≈1.5%；Stability ≈0.4% |
| FastForward 720 天 | 误差仍然很低 |
| FastForward 3600 天 | 未发现明显误差爆炸 |

**因此：P2-A 现视为稳定 Baseline。**

---

## 三、P2-B 期间禁止事项

禁止为方便 UI / 地图 / 统计而修改上述系统的**核心数学**。

明确禁止：

1. 改出生率  
2. 改死亡率  
3. 改承载力公式  
4. 改水消耗公式  
5. 改粮食生产公式  
6. 改粮食腐烂公式  
7. 改疾病公式  
8. 改事件概率  
9. 改事件持续时间  
10. 改季节长度  
11. 改 FastForward 数学  

除非确认为下方 **P0 / P1**（且须报告决策者批准）。

---

## 四、何时可以突破冻结

### P0（可紧急修，仍须说明）

- NaN / Infinity  
- 模拟完全停止  
- 数据永久损坏  
- FastForward 灾难性错误  

### P1（明确违反已定义数学关系）

- Daily 与 FastForward 严重失真  
- 水 = 0 但粮食仍正常生产  
- Fertility / Disease modifier **方向反转**  
- 地区事件错误复制  
- 事件永久残留  
- 时间系统错误  

### 不属于突破冻结（P2 Balance / Design）

下列现象**不得自行改公式**，必须报告由决策者决定：

- 人口偏低 / 粮食太多 / 水太少  
- 承载力下降太快 / 死亡率太高  
- 超载死亡（或出生抑制）太强  
- 稳定度上限不理想  

---

## 五、P2-B 正确开发原则

### 允许

- **读取** P2-A 数据  
- **显示** P2-A 数据  
- **记录 / 统计 / 可视化** P2-A 数据  
- 让玩家**观察** P2-A 数据  

### 禁止

- 重新实现一套人口 / 资源模拟  
- UI 自己计算一套人口  
- 地图自己维护另一套粮食  
- 折线图使用独立伪造数据  
- 报告系统生成与 Simulation State 不一致的数据  

### 正确架构

```text
Simulation
    ↓
Simulation State
    ↓
 ┌──────────────┬──────────────┬──────────────┐
 Map            HUD            Statistics
 │              │              │
 Population     Population     Population
 Food           Food           Food
 Water          Water          Water
 Events         Events         Events
 Resources      Resources      Resources
```

**所有显示数据必须来自 Simulation State。**

---

## 六、P2-B 变更自检清单

提交前确认：

- [ ] 未改 `PopulationSystem` / `ResourceSystem` / `SeasonSystem` / `WeatherSystem` / `EventSystem` / `FastForwardSystem` 核心公式  
- [ ] 未改 `SimulationConfig` 默认数学系数（除非已批准的 P0/P1）  
- [ ] UI / Map / Stats 只读 `WorldState` / `RegionState`（或现有只读 API）  
- [ ] 无独立平行模拟或伪造时间序列  
- [ ] 若触及冻结文件：PR / 说明中写明 P0/P1 理由，并等待决策者确认  
- [ ] 无头回归仍可通过：`dotnet run --project Tools/HeadlessSimTests/HeadlessSimTests/HeadlessSimTests.csproj -c Release`

---

## 七、相关文档

- [Phase 2 范围](Phase2-Scope.md)  
- [Phase 2 报告](Phase2-Report.md)  
- [GDD](GameDesignDocument.md)  
