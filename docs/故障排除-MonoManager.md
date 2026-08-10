# Unity 打开失败：MonoManager is NULL

出现  
`GetManagerFromContext: pointer to object of manager 'MonoManager' is NULL`  
通常是 **Library 缓存损坏**，或上次用错误版本打开过工程。

## 方案 A（先试这个）

1. **完全退出 Unity**（托盘里也不要留着）
2. 打开本地 `3Dgame` 文件夹
3. **删除**这些目录（有就删，没有跳过）：
   - `Library`
   - `Temp`
   - `Obj`
   - `Logs`
4. Unity Hub → 用 **2022.3.62f3c1** 重新 Open 该文件夹  
5. 第一次会重新导入，多等几分钟

## 方案 B（推荐：你已新建 `D:\MyProject\3dgame2`）

1. 确认 `D:\MyProject\3dgame2` 是 Hub 新建的 **3D (URP)** 项目，且能正常打开  
2. 先把本仓库拉到任意目录，例如 `D:\MyProject\3Dgame-repo`  
3. 在仓库里执行（会复制脚本/场景到 3dgame2，并推 GitHub）：

```powershell
cd D:\MyProject\3Dgame-repo
git checkout cursor/unity-blender-requirements-55cc
git pull
powershell -ExecutionPolicy Bypass -File scripts\setup\sync-from-repo-to-3dgame2.ps1 -Dest "D:\MyProject\3dgame2"
```

4. 之后用 Hub 只打开 **`D:\MyProject\3dgame2`**

> 云端 Agent 访问不到你电脑的 `D:\`，所以复制必须在你本机跑上面的脚本。
