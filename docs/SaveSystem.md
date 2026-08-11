# Phase 1 存档系统

本地 JSON 快照，保存世界数值与观察者微调。关掉 Play 后可从槽位读回继续。

## 路径

```
Application.persistentDataPath/DivineWorld/saves/
  autosave.json
  slot1.json
  slot2.json
  slot3.json
```

Windows 常见位置示例（用户名因机器而异）：

`%USERPROFILE%\AppData\LocalLow\<CompanyName>\3Dgame\DivineWorld\saves\`

## 槽位

| 文件 | 用途 |
|------|------|
| `autosave.json` | 每推进 **30** 日自动写入；观察仪点「暂停」时再写一次 |
| `slot1.json` ~ `slot3.json` | 观察仪手动「存 N / 读 N」 |

## 文件内容（schemaVersion = 1）

| 字段 | 说明 |
|------|------|
| `schemaVersion` | 存档格式版本；不匹配则拒绝加载 |
| `savedUtc` | 保存时间（UTC ISO） |
| `seed` | 世界随机种子 |
| `secondsPerDay` | 模拟速度 |
| `autoRun` | 是否自动推进 |
| `world` | `WorldState` 全量快照（年/日/地区人口与资源等） |
| `fertilityBlessing` 等 | 观察者四个倍率 |
| `hasFocusRegion` / `focusRegion` | 注视地区（无焦点时 `hasFocusRegion=false`） |

**不写入存档：** 种族定义表（读档后由 `DefaultWorldFactory` 重建）、场景物体与 HUD。

**RNG：** 读档后用 `seed ^ TotalDays` 重播种；保证世界数值与存档一致，不保证与「从未存档的连续游玩」比特级相同。

## 代码入口

| 类型 | 路径 |
|------|------|
| DTO | `Assets/Scripts/Simulation/Save/SaveGameDto.cs` |
| 读写 | `Assets/Scripts/Simulation/Save/SaveService.cs` |
| 应用 | `SimulationWorld.ToSaveDto` / `ApplySaveDto` |
| UI | `WorldObserverHud` 存读按钮 |

## 版本策略

- 当前：`schemaVersion = 1`
- 未来不兼容变更时递增版本；旧档 Phase 1 直接拒绝并提示，不做自动迁移
