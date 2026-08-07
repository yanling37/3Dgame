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

## 方案 B（A 还不行就用这个，最稳）

手写工程设置偶发不兼容时，用 Hub 新建官方工程再拷资源：

1. Unity Hub → **新建项目**
2. 模板选 **3D (URP)**
3. 编辑器选 **2022.3.62f3c1**
4. 项目名例如 `3DgameLocal`，创建
5. 关掉 Unity 后，把仓库里的这些拷进新项目（覆盖同名即可）：
   - `Assets/Scripts`
   - `Assets/Scenes`
   - `Assets/Art`
   - `Assets/Input`
   - `ArtSource`（可选）
6. 再用 Hub 打开 `3DgameLocal`

脚本与场景就能用了。
