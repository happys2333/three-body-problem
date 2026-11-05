# Three Body Problem

依据《三体旅人演示策划》制作的 Unity 3D 引导式演示，包含资源库、三个可漫游空间和按行动条件推进的主线。

## 开发环境

- Unity **6000.3.27f2**（Unity 6.3），请使用 Unity Hub 安装并打开仓库根目录。
- 使用 **Built-in Render Pipeline**，颜色空间为 **Linear**。
- 默认桌面窗口为 **1920 × 1080**，支持调整窗口大小。

## 开始运行

1. 在 Unity Hub 中添加当前目录，使用上述版本打开项目。
2. 打开 `Assets/_Project/Scenes/Traveller/01_GobiJourney.unity`。
3. 点击 **Play**，在标题页点击 **开始旅程**。
4. 在荒漠中醒来，使用 WASD 行走、直接移动鼠标环顾，沿绿色箭头调查四周并接近火光。巨日出现后跟随旅人进入巨岩阴影。
5. 看向人物按住 F 说话，松开后自动转写并提问；按下时锁定本次接收者，转头不改选。Enter 打开文字交谈，T 切换火把，E 记录观察，J 打开观察簿，Esc 打开菜单并释放鼠标。
6. 标题页或暂停菜单选择“设置”。“AI 对话”提供 32K／128K／512K／1M 上下文；“操作与声音”可调整鼠标灵敏度和选择麦克风；“显示与画质”可切换窗口、全屏、分辨率和画质。地址、模型和设备选择保存在本机，Key 仅在本次运行中使用。[设置说明](Documentation/Settings/2026-11-01/README_CN.md)、[麦克风设置](Documentation/Settings/2026-11-02/README_CN.md)与[角色记忆及长上下文验证](Documentation/Dialogue/2026-11-02/README_CN.md)。

主线顺序：荒漠苏醒与调查 → 火光相遇、最多三次简短回答 → AI 决定巨日升起与晒伤避险 → 岩下认识姬昌、交流与植被燃烧 → AI 决定日落、原路退回地平线 → 同行一段夜路后自动抵达朝歌 → 穿城进入金字塔石殿 → 献历验证 → 复苏池苑 → 历法失验、长夜与三颗飞星 → 第 137 号文明毁灭 → 再次登录进入中世纪质询 → 三日凌空 → 回顾。夜路上的两次飞星观察为可选，不阻止第一幕结束。当前第一幕完整说明、语音服务启动和验收见 [夜色中的戈壁旅人](Documentation/ActOne/README.md)，独立开发包为 `Builds/ActOneTraveller.app`。

Apple Silicon 可运行 `python3 Tools/setup_qwen_tts.py`，为实时 NPC 回复选择 **Qwen3-TTS 0.6B CustomVoice Q5_K**，再用 `python3 Tools/play_act_one.py` 启动游戏。预生成剧情台词仍由游戏播放，语音识别可沿用 Vosk 或 Qwen3-ASR。安装与切换见 [本地语音说明](Documentation/ActOne/README.md#本机语音服务)。

戈壁和首都已分成两个独立场景。首都从城门外进入，包含夯土城垣、王道、宗庙院落、民居市场、作坊粮仓、巨摆环绕的石砌金字塔王宫与复苏池苑。布局与预览见 [殷商王都](Documentation/SHANG_CAPITAL.md)。

场景时刻、跳转与传送仅用于调试：编辑器或 Development Build 中按 **F8** 打开，普通游戏默认隐藏。

三个旅人场景已加入 Build Profiles 的 Scene List，戈壁为构建入口。通过 **File → Build Profiles** 选择目标平台后即可构建。旧的空白初始化场景已移除。

场景包含巨日避险、一颗与两颗飞星、朝歌大厅与复苏水边、长夜、中世纪大厅和三日凌空。全部人物已使用带贴图与骨架的 Humanoid 模型：六套主要人物，以及卫兵、男女居民和复苏者。玩家采用第一人称，低头可见身体和脚，视线不跟随头部骨骼摇晃。周文王带路、等候与交谈，纣王和教皇保留坐姿手势，学者与达芬奇在警报后走出大厅。卫兵持盾矛站岗，居民短途走动并在寒冷时发抖。人物已装配持火把能力；主要人物使用独立 AI 角色，普通人物共用旁人角色，对话保留独立记忆、文字输入和本机语音转写。默认使用离线剧情回复，也可在设置中填写 DeepSeek 或兼容接口的地址、Key 与模型；朝歌的质疑、警告、拘捕与撤回已用真实 DeepSeek 对话验证。

- [场景、操作与制作结构](Documentation/SCENE_GUIDE.md)
- [Unity CLI、共享技能与跨会话制作流程](Documentation/UNITY_CLI_WORKFLOW.md)
- [素材来源、授权与用途](Documentation/ASSET_SOURCES.md)
- [中世纪城镇与中式建筑素材库、实际预览](Documentation/ARCHITECTURE_ASSETS.md)
- [建筑模型目录与三角形数](Documentation/architecture-model-catalog.csv)
- [逐文件资源清单与校验值](Documentation/asset-manifest.json)
- [场景验证与本机性能采样](Documentation/QA_SUMMARY.md)
- [项目清理与恢复记录](Documentation/ProjectCleanup/README.md)
- [荒漠苏醒：第一幕开场设计](Documentation/ActOne/OPENING_DESIGN.md)：调查、绿色指引、初谈上限、AI 日出日落与麦克风流程。
- [统一 F 键提问与 LLM 转交](Documentation/F_DIALOGUE.md)：视线选人、开始时锁定、单人回答，以及 LLM 接入与验收边界。
- [角色扮演、人物记忆与 1M 上下文](Documentation/Dialogue/2026-11-02/README_CN.md)：主要人物跨场景延续本轮交流，按各自亲历提供现场事实，支持按模型窗口发送长记录。
- [AI 角色决策与剧情推进](Documentation/AI_NPC_DECISIONS.md)：已接通日出、日落、语义推进、警告、卫兵拘捕／致命攻击与召回；[实现及验收](Documentation/Dialogue/DecisionRuntime20261102/README_CN.md)记录权限校验、失败重试和实际测试边界。
- [角色生成记录](Documentation/CharacterProduction/hunyuan-production-manifest.json)与[实际动作覆盖表](Documentation/CharacterProduction/ACTION_COVERAGE.csv)：六套主要角色已接入三个剧情场景。动作库共 99 个片段／版本，594 种角色与动作组合通过骨架和蒙皮检查；剧情按需要使用其中的待机、移动、交谈、手势和环境反应。
- [完整人物替换记录](Documentation/CharacterProduction/full-cast-integration.json)：27 个配角由九套造型替换，三个场景共 36 个人物实例、零人物占位体；配角另外验证了 45 组常用动作组合。九套造型复用四个人体源模型，并非 27 个独立生成的人体。
- [历史混元后续素材批次](Documentation/CharacterProduction/HUNYUAN_EXPANSION_20261028.md)：四套独立配角、历法箱、沙漏、皮面书与早期教皇服装候选的来源记录。
- [11 月 2 日角色与道具补齐](Documentation/CharacterProduction/2026-11-02/README_CN.md)：19/20 次提交完成短袍教皇绑定、两名执扇侍女的专用身体、中世纪书记官、教廷座椅和打开的手稿；已接入朝歌与中世纪场景，含持书交互、裙摆修正与原生验证。
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
