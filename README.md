# Three Body Problem

依据《三体旅人演示策划》制作的 Unity 3D 引导式演示，包含资源库、三个可漫游空间和按行动条件推进的主线。

## 开发环境

- Unity **6000.3.26f1**（Unity 6.3），请使用 Unity Hub 安装并打开仓库根目录。
- 使用 **Built-in Render Pipeline**，颜色空间为 **Linear**。
- 默认桌面窗口为 **1920 × 1080**，支持调整窗口大小。

## 开始运行

1. 在 Unity Hub 中添加当前目录，使用上述版本打开项目。
2. 打开 `Assets/_Project/Scenes/Traveller/01_GobiJourney.unity`。
3. 点击 **Play**，在标题页点击 **开始旅程**。
4. 夜间与旅人相遇，使用 WASD 行走、按住鼠标右键环顾。巨日出现后跟随旅人进入巨岩阴影；靠近后按 F 交谈。
5. T 切换火把，交谈时按住 V 录音，核对转写后发送，也可打字。E 记录观察，J 打开观察簿，Esc 暂停。

主线顺序：戈壁夜间初遇 → 巨日与晒伤避险 → 岩下交谈十分钟、暴晒植被烧成灰烬 → 突然入夜 → 同行一段夜路后自动抵达朝歌 → 穿城进入金字塔石殿 → 献历验证 → 复苏池苑 → 长夜 → 中世纪质询 → 三日凌空 → 回顾。夜路上的两次飞星观察为可选，不阻止第一幕结束。当前第一幕完整说明、语音服务启动和验收见 [夜色中的戈壁旅人](Documentation/ActOne/README.md)，独立开发包为 `Builds/ActOneTraveller.app`。

戈壁和首都已分成两个独立场景。首都从城门外进入，包含夯土城垣、王道、宗庙院落、民居市场、作坊粮仓、巨摆环绕的石砌金字塔王宫与复苏池苑。布局与预览见 [殷商王都](Documentation/SHANG_CAPITAL.md)。

场景时刻、跳转与传送仅用于调试：编辑器或 Development Build 中按 **F8** 打开，普通游戏默认隐藏。

三个旅人场景已加入 Build Profiles 的 Scene List，戈壁为构建入口。通过 **File → Build Profiles** 选择目标平台后即可构建。原 `Main.unity` 保留为初始化样例。

场景包含巨日避险、一颗与两颗飞星、朝歌大厅与复苏水边、长夜、中世纪大厅和三日凌空。全部人物已使用带贴图与骨架的 Humanoid 模型：六套主要人物，以及卫兵、男女居民和复苏者。玩家采用第一人称，低头可见身体和脚，视线不跟随头部骨骼摇晃。周文王带路、等候与交谈，纣王和教皇保留坐姿手势，学者与达芬奇在警报后走出大厅。卫兵持盾矛站岗，居民短途走动并在寒冷时发抖。36 个角色已装配持火把能力；交互角色有独立会话、八类提示词、文字输入和本机语音转写。默认显示离线剧情回复，未来 DeepSeek 接口已适配，真实在线生成仍需服务端密钥验证。

- [场景、操作与制作结构](Documentation/SCENE_GUIDE.md)
- [Unity CLI、共享技能与跨会话制作流程](Documentation/UNITY_CLI_WORKFLOW.md)
- [素材来源、授权与用途](Documentation/ASSET_SOURCES.md)
- [中世纪城镇与中式建筑素材库、实际预览](Documentation/ARCHITECTURE_ASSETS.md)
- [建筑模型目录与三角形数](Documentation/architecture-model-catalog.csv)
- [逐文件资源清单与校验值](Documentation/asset-manifest.json)
- [场景验证与本机性能采样](Documentation/QA_SUMMARY.md)
- [角色生成记录](Documentation/CharacterProduction/hunyuan-production-manifest.json)与[实际动作覆盖表](Documentation/CharacterProduction/ACTION_COVERAGE.csv)：六套主要角色已接入三个剧情场景。动作库共 99 个片段／版本，594 种角色与动作组合通过骨架和蒙皮检查；剧情按需要使用其中的待机、移动、交谈、手势和环境反应。
- [完整人物替换记录](Documentation/CharacterProduction/full-cast-integration.json)：27 个配角由九套造型替换，三个场景共 36 个人物实例、零人物占位体；配角另外验证了 45 组常用动作组合。九套造型复用四个人体源模型，并非 27 个独立生成的人体。
- [混元后续素材](Documentation/CharacterProduction/HUNYUAN_EXPANSION_20261028.md)：新增四套独立配角、历法箱、沙漏和皮面书，以及尚未绑骨的教皇服装修订候选；下载、Unity 预制体、复核与额度记录单独保存，尚未自动覆盖主线场景。
- [修整后的环境素材库与第一人称试走场](Documentation/EnvironmentProduction/Ready/README.md)：20 件山体、荒漠装饰、中式与中世纪建筑预制体，含摄影材质、碰撞和三级 LOD；[原生成记录](Documentation/EnvironmentProduction/README.md) 单独保留。
- 打开 `Assets/_Project/Scenes/CharacterPreview/CharacterMotionGallery.unity` 并 Play，可按中文名称搜索、切换六位角色、暂停和拖动进度；完整文件见 [动作目录](Documentation/CharacterProduction/MOTION_CATALOG.csv)。主线仍从戈壁场景启动。

## 资源目录

项目内容统一放在 `Assets/_Project/` 下；第三方资源可单独放在 `Assets/` 的其他目录。

| 目录 | 用途 |
| --- | --- |
| `Scenes/Traveller/` | 三组旅人场景，入口为 `01_GobiJourney.unity` |
| `Scenes/Architecture/` | 建筑素材图库，供美术预览，不是游戏入口 |
| `Scripts/` | 运行时代码，默认命名空间 `ThreeBodyProblem` |
| `Editor/` | 仅在编辑器中使用的工具脚本 |
| `Prefabs/Traveller/` | 巨岩庇护所、青铜沙漏等可复用预制体 |
| `Prefabs/Architecture/` | 新采集的中世纪、中式亭与陈设 Prefab |
| `Characters/Hunyuan/` | 六套 Humanoid 角色、PBR 材质、预制体和共享动作库 |
| `Characters/Mixamo/` | Y Bot 参考骨架、89 条共享动作和四条项目派生动作 |
| `Characters/Story/` | 剧情动画控制器、坐姿上半身遮罩与第一人称身体网格 |
| `Characters/Crowd/` | 配角完整人体源模型、贴图、九套卫兵和平民造型及装备网格 |
| `Art/HunyuanExpansion/` | 2026-10-28 新生成的人物与道具源文件、PBR 材质和米制预制体 |
| `Art/HunyuanEnvironment/` | 混元生成的建筑与自然环境模型、PBR 材质和三级 LOD 预制体 |
| `Art/EnvironmentReady/` | 修整后的 20 件静态环境预制体、摄影材质、碰撞和三级 LOD |
| `Art/ActOne/`、`Settings/ActOne/` | 火把、灰烬、燃烧特效与第一幕时序、伤害配置 |
| `Dialogue/` | 八类角色提示词、身份和独立会话配置 |
| `Scenes/CharacterPreview/` | 独立角色动作预览场景 |
| `Materials/` | 材质 |
| `Art/Models/`、`Art/Textures/` | 模型与贴图 |
| `Audio/` | 音乐与音效 |
| `UI/` | 界面资源 |
| `Settings/` | 项目配置资源 |

## 版本管理

- 提交 `Assets/`（包括 `.meta`）、`Packages/` 和 `ProjectSettings/`。
- 模型、贴图、音频、Blender 源文件与生成网格通过 Git LFS 保存。首次克隆前安装 Git LFS 并运行 `git lfs install`，克隆后运行 `git lfs pull`；若只取得很小的指针文件，先拉取 LFS 内容再打开 Unity。
- 使用 Force Text 序列化和 Visible Meta Files，便于协作与审查。
- `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`Builds/` 和 IDE 生成文件由 `.gitignore` 排除。
- `Assets/ThirdParty/LocalOnly/` 保存禁止素材再分发的本地资源，已单独排除；按建筑素材库说明重新获取。
- `.editorconfig` 与 `.gitattributes` 统一文本格式和 LF 换行。

2026-10-27：中央王宫已按《三体》朝歌描写改为厚墙金字塔，加入低入口、甬道、内殿与可开启天窗；戈壁与首都河谷换用连续地形、扫描岩石与摄影植被，见 [王都布局与美术](Documentation/SHANG_CAPITAL.md) 和 [环境素材来源](Documentation/ASSET_SOURCES.md)。
