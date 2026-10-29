# Unity CLI 与跨会话素材制作

用户级技能已准备：`unity-cli` 保存本机 CLI 随附的官方命令说明，`unity-asset-production` 保存素材生产、连接判断和验收方法。其他本机 Codex 会话可按任务自动选择，也可直接提出：

```text
使用 $unity-cli 检查当前 Unity 项目的连接和可用命令。
使用 $unity-asset-production 处理当前项目的新建筑素材，并验证材质、LOD、碰撞和实际画面。
```

官方文件在 `~/.agents/skills/unity-cli/`；补充 skill 在 `~/.agents/skills/unity-asset-production/`。维护官方副本可按需使用 `unity skill refresh`，项目经验不写入官方文件。Codex 会检测技能变化；如果当前会话的技能选择器尚未显示，可新开会话，必要时重启应用。

## 先做只读检测

在仓库根目录执行：

```sh
python3 "$HOME/.agents/skills/unity-asset-production/scripts/probe_project.py" --project "$PWD" --live
```

2026-10-28 首次核验时，项目 Editor 为 `6000.3.26f1`，本机 CLI 为 `1.0.0-beta.13`；已有 `com.coplaydev.unity-mcp`，未声明官方 `com.unity.pipeline`。这些不是后续会话可直接沿用的连接状态，后续以自检为准。

两份 skill 的格式校验及四项只读自检通过，见 [验证记录](SkillChecks/unity-skills-20261028.json)：从无关目录检测当前工程、拒绝非 Unity 目录、报告 CLI 缺失、排除未打开的同名工作区。检查前后 26 个项目配置、主线场景和修整预制体文件保持一致。检测还发现 beta.13 有时会给未打开目录返回无 PID 的“运行中”候选，自检已将其标为未核实，要求进一步确认实际进程路径。本轮没有另建会话或执行 Pipeline 制作命令。

CLI 版本管理、帮助和包内容检查可独立使用。通过 CLI 控制运行中的 Editor 需要 Pipeline；这次创建技能没有安装 Pipeline 或替换已有 MCP。任务包括 CLI 接入时再按官方说明安装、等待包解析和编译，并精确匹配本工作区路径。不要向另一个工作区的同名项目发命令。

## 复用本项目现有入口

实现位于 `Assets/_Project/Editor/EnvironmentProduction*.cs`，命名空间为 `ThreeBodyProblem.Editor`。通过已连接 Editor 的 C# 执行工具按需调用，不要每次重做全部素材。

| 方法 | 用途 |
| --- | --- |
| `EnvironmentProduction.ExportWorkingMeshes()` | 从原候选导出米制工作网格 |
| `EnvironmentProduction.ImportAvailable()` | 导入已完成修整的网格、PBR、三级 LOD 与静态碰撞 |
| `EnvironmentProduction.CaptureReadyLibrary()` | 更新独立棚拍预览 |
| `EnvironmentProduction.BuildArtYard()` | 构建独立第一人称试走场，结束后恢复原场景 |
| `EnvironmentProduction.BuildStandalonePreview()` | 仅构建试走场的 macOS 开发包 |
| `EnvironmentProduction.OpenArtYard()` | 打开试走场供检查 |

本机 CLI 连接后先通过 `unity command --project-path <绝对路径> --format json` 发现实际命令及 C# 参数格式，再调用以上方法；不能把 C# 方法名误当成已注册 CLI 子命令。交互式方法中的非 `void` 返回值也不能直接套用到批处理 `-executeMethod`，如需批处理应添加适配入口并验证输出。

Blender 与 Python 工具：

- `Tools/prepare_environment_meshes.py`：由 Blender Python 执行，按 ID 修整素材；不传 ID 会处理全库。
- `Tools/fetch_environment_ready_surfaces.py`：获取并核验摄影材质。
- `Tools/pack_environment_surface_maps.py`：由 Blender 执行，合并粗糙度/遮蔽并逐像素核验。
- `Tools/reconcile_environment_ready.py`：汇总当前源文件、引用、场景哈希与运行证据。它是本批 20 件资源的专用审计器，扩展清单时需同步调整，不能当成任意素材库的通用检查。

Blender 已安装在 `/Applications/Blender.app/Contents/MacOS/Blender`，运行前仍需检查该位置。外部进程完成后再让 Unity 导入；大文件用临时目录写完后原子移动，避免编辑器读到半写文件。

## 验证与运行边界

修整素材使用 `Assets/_Project/Art/EnvironmentReady/Prefabs/`，试走场为 `Assets/_Project/Scenes/Environment/Environment_ArtYard.unity`，完整说明见 [修整版指南](EnvironmentProduction/Ready/README.md)。原 FBX 及其归档保持不变。

游戏入口仍为 `01_GobiJourney`，剧情三场景保持独立。素材准备不自动改 Build Settings；正式集成须另外验证主线路径、飞星/三日视线、人物交互与运行开销。

原始运行结果和范围见 [交付审计](EnvironmentProduction/Ready/delivery.json) 与 [QA 汇总](QA_SUMMARY.md)。技能文件只保存方法，不把某次性能通过当成以后所有修改都通过。已有包由 Editor API 构建，不保证存在 `unity build run` 所需的 CLI 构建记录；可根据 `.app/Contents/Info.plist` 中的 `CFBundleExecutable` 定位实际运行文件。

## 来源

- [Unity CLI reference](https://docs.unity.com/en-us/unity-cli/unity-cli-reference)
- [Unity Pipeline 安装与连接](https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package)
- [Codex 技能发现和用户级目录](https://learn.chatgpt.com/docs/build-skills)
