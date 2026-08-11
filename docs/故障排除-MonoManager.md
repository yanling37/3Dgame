# Unity 打开失败：MonoManager is NULL

出现  
`GetManagerFromContext: pointer to object of manager 'MonoManager' is NULL (table index 5)`  
时，工程无法进入编辑器。

## 本机已确认的根因（2026-08-11）

在 **Unity 2022.3.62f3c1** 下复现并排查：

1. 仅删除 `Library` / `Temp` / `Logs` **不够**，错误仍会出现。
2. 日志栈落在 `PackageManager` → `ReloadSingletonAssets` → `GetManagerFromContext(MonoManager)`。
3. **根因**：仓库里早期由云端生成的 `ProjectSettings` 对本机编辑器无效（骨架不完整 / 与本机 China LTS 不匹配）。
4. **修复**：用本机 `2022.3.62f3c1` 新建空工程，将其有效的 `ProjectSettings` 换入本仓库，并保留 `EditorBuildSettings`、`InputSystem.settings.json`；随后用正确版本打开即可。  
   相关提交已在分支 `cursor/fix-monomanager-projectsettings`。

打开工程时请务必选择：**2022.3.62f3c1**（不要用 2020.x 打开本仓库）。

## 方案 A（清缓存，先试）

1. **完全退出 Unity**（托盘里也不要留着）
2. 打开本地工程根目录（推荐 `D:\MyProject\3Dgame`）
3. **删除**这些目录（有就删，没有跳过）：
   - `Library`
   - `Temp`
   - `Obj`
   - `Logs`
   - `UserSettings`（可选）
4. Unity Hub → 用 **2022.3.62f3c1** 重新 Open  
5. 第一次会重新导入，多等几分钟

若方案 A 无效，多半是 `ProjectSettings` 问题，见上文「根因」；当前仓库主修复分支已包含可再生的本机配置。

## 方案 B（Hub 新建 URP 工程 + 同步脚本）

若仍打不开仓库根目录，可用 Hub 新建可打开的 URP 工程，再把游戏内容拷进去：

1. 确认目标目录（如 `D:\MyProject\3dgame2`）是 Hub 新建的 **3D (URP)**，且能用 **2022.3.62f3c1** 打开  
2. 在本仓库目录执行：

```powershell
cd D:\MyProject\3Dgame
git pull
powershell -ExecutionPolicy Bypass -File scripts\setup\sync-from-repo-to-3dgame2.ps1 -Dest "D:\MyProject\3dgame2" -SkipPush
```

3. 之后用 Hub 打开目标工程目录

> 云端 Agent 访问不到你电脑的 `D:\`，同步脚本必须在本机运行。

## 日常打开

- 工程路径：`D:\MyProject\3Dgame`
- 编辑器：`2022.3.62f3c1`
- 场景：`Assets/Scenes/Boot.unity` → Play  
- 有 GitHub 更新时：在工程目录执行 `git pull`
