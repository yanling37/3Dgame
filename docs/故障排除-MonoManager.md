# Unity 打开失败：MonoManager is NULL

出现  

`GetManagerFromContext: pointer to object of manager 'MonoManager' is NULL (table index 5)`  

时，工程无法进入编辑器。

## 给云端 Agent 的强制约定（2026-08-12 再次确认）

本机已**多次**复现：换分支 / 拉取云端提交后再次 MonoManager 崩溃。

| 错误做法 | 正确做法 |
|----------|----------|
| 手写或模板拼一套 `ProjectSettings/` | 只用本机 **Unity 2022.3.62f3c1** 实际创建并能打开的工程配置 |
| `ProjectVersion.txt` 写成 `(china-lts)` | 写成真实修订号，例如 `(1623fc0bbb97)` |
| 为了“干净骨架”覆盖已修复的 ProjectSettings | **保留**已验证可打开的 ProjectSettings，只改 `Assets/Scripts` 等玩法代码 |
| 假 `productGUID`（如 `7a3c9e2f4b1d8046a5e6f708192a3b4c`） | 使用编辑器生成的 GUID |

仓库规则：`.cursor/rules/unity-projectsettings.mdc`（alwaysApply）。

参考可用配置来源：分支 `cursor/fix-monomanager-projectsettings` 中已验证的 `ProjectSettings/`（本机 batchmode 打开成功）。

## 本机已确认的根因

1. 仅删除 `Library` / `Temp` / `Logs` **往往不够**。
2. 日志栈：`PackageManager` → `ReloadSingletonAssets` → `GetManagerFromContext(MonoManager)`。
3. **根因**：云端骨架 `ProjectSettings` 对本机 China LTS 编辑器无效。
4. **修复**：用本机编辑器生成的有效 `ProjectSettings` 替换仓库中的坏配置；`ProjectVersion` 使用 `1623fc0bbb97`。

打开工程必须选：**2022.3.62f3c1**（不要用 2020.x）。

## 方案 A（本机清缓存）

1. **完全退出 Unity**
2. 删除工程下：`Library`、`Temp`、`Obj`、`Logs`、`UserSettings`（有则删）
3. Hub → **2022.3.62f3c1** → Open `D:\MyProject\3Dgame`
4. 等待重新导入

若仍 Fatal Error → 不是缓存问题，是 `ProjectSettings` 又被坏骨架覆盖，按上文「强制约定」恢复可用配置。

## 方案 B（Hub URP 工程 + 同步脚本）

1. Hub 新建可打开的 URP 工程（同一编辑器版本）
2. 只同步游戏内容，**保留 Hub 工程自己的 ProjectSettings**：

```powershell
cd D:\MyProject\3Dgame
git pull
powershell -ExecutionPolicy Bypass -File scripts\setup\sync-from-repo-to-3dgame2.ps1 -Dest "D:\MyProject\3dgame2" -SkipPush
```

## 日常打开

- 路径：`D:\MyProject\3Dgame`
- 编辑器：`2022.3.62f3c1`
- 场景：`Assets/Scenes/Boot.unity` → Play
