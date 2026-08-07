# 3Dgame

用 **Unity** 做主开发、**Blender** 做建模的 3D 游戏尝试项目。

## 快速开始（本机）

1. 安装 **Unity Hub**，使用已安装的 Editor **`2022.3.62f3c1` LTS**（或同系列 2022.3 LTS）
2. 安装 **Blender 4.2 LTS**
3. 拉取分支 `cursor/unity-blender-requirements-55cc`
4. Unity Hub → **Open** → 选择本仓库根目录
5. 若提示版本，选 **使用 2022.3.62f3c1 打开**
6. 打开 `Assets/Scenes/Level_01.unity` 开始灰盒

首次打开若提示 Input System / URP，按编辑器提示重启或创建 URP Asset 即可。

## 文档

- [需求文档](docs/需求文档.md)
- [开发环境](docs/开发环境.md)
- [ArtSource 说明](ArtSource/README.md)

## 技术选型（已锁定）

| 用途 | 软件 |
|------|------|
| 游戏引擎 | Unity **2022.3.62f3c1 LTS** + URP + C# |
| 建模 | Blender **4.2 LTS** |
| 版本管理 | Git + Git LFS |

## 仓库结构

```text
Assets/           Unity 资源与脚本
Packages/         Unity 包清单
ProjectSettings/  Unity 工程设置
ArtSource/        Blender 源文件与 FBX 导出
docs/             需求与环境文档
scripts/setup/    环境检查 / 安装脚本
scripts/blender/  Blender 导出与示例脚本
```

## 当前状态

开发环境骨架已就绪：Unity 工程可打开、示例道具 FBX 已导入、基础脚本与场景已就位。请在本机安装 Unity Editor 后继续灰盒玩法。
