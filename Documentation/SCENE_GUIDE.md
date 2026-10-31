# 三体旅人：场景制作说明

依据 `ThreeBody_Traveller_Demo_Plan_CN.docx`（第一版，2026-10-26）制作；第一幕已按 2026-10-30 荒漠苏醒要求更新，当前时序与语音说明见 [夜色中的戈壁旅人](ActOne/README.md)。默认入口为引导式主线，场景预览工具仅用于调试。

## 场景与方案对应

三个入口均位于 `Assets/_Project/Scenes/Traveller/`，已按顺序加入 Build Profiles 的 Scene List。

| 场景 | 方案空间 | 可选择的时刻 |
| --- | --- | --- |
| `01_GobiJourney.unity` | 独立戈壁：夜间初遇、巨岩、岩下交流、飞星夜路、离开戈壁的驿路；没有首都城门与巨摆 | 夜间相遇、巨日、骤然入夜、一颗飞星、两颗飞星、夜路朝歌在望，共 6 个 |
| `02_ChaogeCourt.unity` | 独立殷商王都：夯土城垣、门塾、王道、市场民居、宗庙、石砌金字塔王宫、作坊粮仓、池苑、环绕巨摆 | 王都白昼、复苏池苑、长夜，共 3 个 |
| `03_MedievalTribunal.unity` | 中世纪大厅、教皇与学者站位、入口、外侧观测台 | 最终质询、三日辨认窗口、高温灾变，共 3 个 |

旧的空白初始化场景已移除；游戏与预览的启动入口是 `Assets/_Project/Scenes/Traveller/01_GobiJourney.unity`。

## 运行与操作

使用 Unity 6000.3.26f1 打开工程，打开戈壁场景并进入 Play，在标题页点击“开始旅程”。

- WASD：行走；按住鼠标右键：环顾；Shift：快走；松开鼠标右键恢复界面操作。
- F：与附近人物交谈，或执行当前的明确选择。交谈选项也可直接点击。
- T：切换玩家火把；靠近人物直接按住 V 或点击麦克风录音，松开后自动转写发送（可在配置关闭自动发送），也可直接打字。默认使用明确标注的离线剧情回复，DeepSeek 在线模式需服务端密钥。
- R：看向人物或目标方向，保留玩家位置。
- E / “记录观察”：在给出观察内容的时刻保存记录；飞星和三日观测需要看向观测方向。
- J / “观察簿”：查看亲眼观察、人物判断，填写并保存自己的假说。文本编辑期间暂停移动输入。
- 场景按主线条件异步切换，观察记录与假说在本机保留。普通游玩不能用数字键或方向键跳过章节。
- H：隐藏 / 显示游戏 HUD；Esc：关闭交谈或观察簿，或打开暂停菜单。

## 主线推进

1. 在荒漠中睁眼醒来，沿绿色箭头调查。玩家可以自由游荡；靠近火光后依次听到“来这里！”和“快过来，现在是乱纪元，不要命吗？”。
2. 初谈最多三个简短回答，AI 可提前选择日出。巨日从地平线急速升起，暴晒造成隐藏伤害；跟随旅人进入真实实体阴影后恢复满值。
3. 双方进入岩下，暴晒植被点燃并成为灰烬，遮阴植被保留。旅人介绍姬昌并问玩家为什么独自在外，可继续自由交流；AI 决定何时结束，取代固定 600 秒等待。太阳沿原路线落回地平线。
4. 夜间任务改为“与姬昌一同前往朝歌”。经过至少 35 米与十二秒、到达终点且旅人在附近后自动加载朝歌入口，第一幕结束。两次飞星观察可选。
5. 从首都城门外开始，随周文王沿王道进入金字塔的低入口，经过狭长甬道抵达内殿；参与质询或旁听，再选择验证。石板开启，观察沙漏与天窗日光。从侧面甬道与东坡前往复苏池苑，记录应验，明确等待日出；长夜到来后记录反例。
6. 明确选择进入新的文明，前往中世纪大厅，提出解释或保留判断。
7. 警报打断质询，玩家步行到门外；三个太阳保留可辨认窗口，记录后才继续高温演出与个人回顾。

玩家沉默时可由叙事 AI 判断日出、日落节奏；不会替玩家编造回答或自动行走。旧观察簿也不会自动跳过本轮的两次观星。场景天象变化保留玩家当前位置。

## 场景调试

编辑器与 Development Build 中按 F8 打开调试模式，使用右侧列表、方向键、数字键切换时刻和场景，R 返回预设观察位。退出调试返回标题，不将自由跳转混入当前主线。

UI 使用持久化保存的 `ScaleWithScreenSize`、1600×900 参考分辨率与居中的缩放匹配。菜单、目标、字幕和选项采用分区布局，不按显示器物理 DPI 放大整套工具面板。

## 天象与空间约束

- 飞星为缓慢运动的亮点，不使用流星长尾。一颗与两颗分别有独立观察位和记录。
- 长夜的三颗飞星与第二幕三个太阳使用不同显示方式，不合并为一个天象。
- 三日凌空先给出三个太阳同时可辨的窗口，再切换高温、裂隙、灰烬和草火。没有混入三日连珠的悬浮与失重。
- 巨岩提供可走入的阴影与岩外观察窗口；没有把岩下设计为最终灾变的通用避难所。
- 观察簿分别保存事实、周文王的判断与玩家手写假说；切换文明保留玩家记录。
- 戈壁与王都使用两个独立场景，正常切换采用单场景加载，旧场景卸载；王都从城门外进入，不从王座前开始。两组地理空间的详细布局见 [殷商王都制作说明](SHANG_CAPITAL.md)。

## 制作结构

- `Settings/Traveller/`：三个 ScriptableObject 场景配置，保存时刻、灯光、天象、观察位、固定演出字幕及观察内容；王都配置还保存入城、金字塔甬道、内殿及侧门下坡的带路路径。
- `Prefabs/Traveller/`：巨岩庇护所和青铜沙漏。
- `Materials/Traveller/`：PBR 材质、天空、粒子及彩窗材质；旧人物占位材质保留供历史场景工具使用。
- `Art/GeneratedMeshes/`：已保存的地形与建筑网格，运行时无需重新生成。
- `Scripts/`：漫游控制、场景时刻、观察簿、HUD 与摆阵动画。
- `Editor/`：场景制作工具。`Tools → Three Body Traveller → Build All Scenes` 可重新制作三个生成场景；重建前将已有场景备份到 `Logs/scene-backups/`。手工编辑后请先另存副本，避免重建覆盖自己的布局。
- `Tools → Three Body Traveller → Rebuild Shang Capital Only` 只重建首都；`Tools → Three Body Traveller → Rebuild Gobi And Shang Capital` 只重建戈壁与王都并备份场景、配置，不重建中世纪场景；`Capture Shang Capital` 可重新输出王都与驿路的检查截图。

1K 地表贴图、共用材质、模型 LOD 距离裁剪、静态批处理标记与限制粒子数量用于控制预览开销；实际帧率以本机运行检查为准。

## 后续接入边界

当前交谈为预设选项与演出字幕，主要人物、卫兵、男女居民和复苏者均已使用带贴图的 Humanoid 模型；周文王可动态带路与等候，居民可短途走动。文字和本机 ASR/TTS、持物 IK、晒伤检查点恢复与第一幕叙事意图已接入；真实在线 DeepSeek 的角色表现、卫兵处置、精细手指表演与后续分支仍待验证或开发。

当前主线已实现位置、观察、身份交代、明确等待和解释机会的推进条件；第一幕 AI 可选择受约束的日出与日落；通用人物命令与更广泛的剧情分支仍待开发。

## 混元角色与动作库（2026-10-27）

通过用户 Chrome 中的混元 3D Studio 生成六个角色：玩家／海人／哥白尼、周文王、纣王、教皇、学者、达芬奇。原始 FBX 在 `ArtSource/Characters/Hunyuan/`，Unity 预制体在 `Assets/_Project/Characters/Hunyuan/Prefabs/`。各角色约 5 万三角形，独立 2K PBR 材质，已配置 Humanoid Avatar 与统一米制身高。

`Assets/_Project/Characters/Hunyuan/Animations/` 包含实际下载的六条共享 Humanoid 动作：`Idle01`、`Walk01`、`Run`、`Turn`、`Despair`、`FallForward`。待机、行走、奔跑循环；其余单次播放。`GeneratedActors.controller` 提供同名状态，默认待机；使用方可调用 Animator.Play，移动与剧情切换需要调用方控制。它没有自动接管当前剧情角色。

平台的跌倒 FBX 只转动身体而没有完整下落高度。工具从原始片段派生了 `FallForward.anim`，补齐 Humanoid 身体高度曲线，控制器使用这个校正版；原始 FBX 保留。教皇的长袍在行走及大幅度动作中仍会拉伸，需要修整蒙皮后再用于近景演出。

编辑器菜单 `Tools → Three Body Traveller → Prepare Generated Characters` 可重新配置贴图、动画和预制体；`Check Generated Character Motions` 在隔离预览场景中检查六个角色与当前已导入动作的组合，并输出烘焙蒙皮和骨骼运动数据及效果图。结果与来源记录于 `Documentation/CharacterProduction/`。检查不包含衣料碰撞、手指表演或口型。

混元批次用完平台当日 30 次免费额度：概念图、几何、贴图、绑定各六次，动作六次。后续已通过用户授权的 Mixamo 补充交谈、指引、坐起、下跪、持物、书写身体动作、恐惧与疲惫等；发抖由项目派生。完整结果见 [动作覆盖表](CharacterProduction/ACTION_COVERAGE.csv)。空手举臂指天、翻沙漏、读历法和精细笔尖轨迹仍待定制。骨架只有 23 根蒙皮骨骼，没有独立手指、面部骨骼。主线主要人物现已替换并接入下述表演逻辑。

## Mixamo 动作与独立预览（2026-10-27）

通过 Chrome 下载四个动作包与 20 个单独片段，共 **89 个 Mixamo FBX**。使用 `YBot_TPose.fbx` 的 Humanoid Avatar 导入，再重定向给六位混元角色。与原六条混元动作和四条项目派生动作合计 **99 个命名片段／版本**；动作包包含同一行为的变体，不能理解为 99 种独立表演。来源、哈希和实际文件见 [Mixamo 清单](CharacterProduction/mixamo-assets.json)及[动作目录](CharacterProduction/MOTION_CATALOG.csv)。

打开 `Assets/_Project/Scenes/CharacterPreview/CharacterMotionGallery.unity` 后点击 Play。左侧按中文名称或 `MX_` 编号搜索、选择动作，顶部按钮可看全部或单个人物；空格暂停，左右键切换，R 重播。底部可调速度、拖动进度，拖动后保持暂停。单人视图按住右键旋转、滚轮缩放。该场景没有加入主线构建入口。

四个项目派生版本保存在 `Characters/Mixamo/Derived/`：`MX_LookSky` 将持火把模板的头颈观天曲线与自然待机的身体、手臂结合；`MX_WalkBackward` 反向播放行走；`MX_PutDown` 反向播放单手拾取；`MX_Shiver` 在待机上叠加胸肩头颈微振动。发抖不是从 Mixamo 下载的同名成品，反向放物也没有解决双手放箱与接触约束。

`GeneratedActors.controller` 提供与动作编号一致的状态，默认 `MX_Idle`。预制体关闭 Root Motion，预览中的移动动作原地播放；剧情使用单独的 `StoryActors.controller`，由游戏代码驱动位移与朝向。王座已经匹配坐姿；历法箱、箱盖和纸笔尚未绑定到手。右转模板在玩家骨盆上的实测转角约 103.2°，不能直接当精确 90° 使用。

重建顺序为 `Prepare Generated Characters` → `Prepare Mixamo Motion Library` → `Build Character Motion Gallery`。检查可调用 `GeneratedCharacterChecks.CheckClipsAndCapture(prefix, skip, take)` 分批执行，再运行 `python3 Tools/reconcile_character_delivery.py` 汇总已有报告、核对文件哈希并更新覆盖表。报告 [character-motion-validation-summary.json](CharacterProduction/character-motion-validation-summary.json) 记录 594 种组合各五个姿势的骨架、蒙皮变形、有限顶点与尺寸检查；六角色的向前跌倒结束离地小于 15 厘米。该检查不证明衣料没有穿插或台词表演已完成，教皇的袍子仍需修整。

## 主线角色与第一人称玩家

三个 `Scenes/Traveller/` 场景已经保存实际替换：戈壁为玩家与周文王；朝歌增加纣王及 25 名卫兵、居民和复苏者；中世纪为玩家、教皇、学者、达芬奇及两名卫兵。三个场景共 36 个人物实例、零人物占位体。主要人物单个约 5 万三角形，配角人体为 4,625–50,000 三角形，另加卫兵装备；远处通过 LODGroup 距离裁剪。

玩家相机直接挂在 CharacterController 根节点，眼高 1.65 米，保持原右键环顾方式；普通行走每秒 3 米，Shift 快走每秒 5.4 米。相机不挂在头部骨骼上，不跟随待机和步行动画晃动。第一人称可见网格去除了头颈及会挡眼的上缘，并将身体放在眼睛后方 27 厘米，低头可见衣服、腿与鞋。完整原模型仅投射影子，原始角色和图库中的头部保留。

`TravellerActor` 根据实际位移选择行走、奔跑、后退与侧移，关闭 Root Motion，按源片段步速调整播放倍率。周文王由原路线逻辑带路，远离玩家时停步等候；停下后转向附近玩家。`TravellerCastDirector` 把剧情和交谈选择映射到交谈、点头、摇头、鞠躬、观天、发抖、沮丧与惊惧。行走途中触发的手势排到停步之后。警报后，学者和达芬奇沿中殿两侧路径走到室外观测点。

纣王与教皇使用坐姿基础层，手势使用 `Seated Gestures` 上半身遮罩；腿部与根节点继续保持坐姿。标题、暂停菜单和观察簿会暂停角色，交谈面板允许 NPC 表演、同时阻止玩家移动。周文王背部挂载已有沙漏预制体；这不代表已经完成双手翻沙漏。

`Tools → Three Body Traveller → Integrate Story Characters And First Person Player` 可重新配置并应用到三个现有场景，先备份到 `Logs/scene-backups/StoryCharacters_*`，不重建地形与建筑。原场景重建工具在资源齐备时也会接入同样的角色。控制器、遮罩、表演配置与第一人称网格位于 `Characters/Story/`；正式游玩仍从戈壁场景开始，人物动作图库只用于独立预览。

## 全部卫兵与平民（2026-10-28）

九套配角造型在 `Characters/Crowd/Prefabs/`：两套商朝卫兵、一套教廷卫兵、四套男性平民、两套女性平民。人体源模型为已有 `PlayerTraveller` 与新增三套 Mixamo 人物，保留完整皮肤网格、贴图和 Humanoid Avatar；男女农民源模型具有风格化比例。商朝卫兵有独立札甲与青铜盔；两类卫兵均带盾矛。服装配色、身高及循环动作起始时间作变化，九套造型复用四个人体，并非九个独立新建身体。

`TravellerCrowdMember` 驱动站岗、居民安全短途走动及寒冷／疲惫反应；`TravellerActor` 根据真实位移选取行走并匹配速度。卫兵保持站岗朝向，菜单、暂停与观察簿暂停所有人物。复苏者使用疲惫站姿，当前没有“从脱水身体泡水复原”的专用动画；卫兵阻拦、行礼、押送同样仍需另做。

`Tools → Three Body Traveller → Prepare And Replace All Crowd Characters` 重制配角预制体并应用到现有三场景，先备份到 `Logs/scene-backups/FullCrowd_*`，不重建建筑与地形。重建主场景及主要人物时也会在配角资源齐全后应用同一套替换。`TravellerCrowdChecks.CheckAndCapture()` 在隔离预览场景验证九造型与五条常用动作的 45 种组合，每种采样五个姿势；它检查骨骼运动、烘焙蒙皮、有限数值与尺寸，不保证衣料完全没有穿插。

完整配角实机检查参数为 `-traveller-crowd-check <报告绝对路径>`；`python3 Tools/run_full_cast_checks.py` 依次运行配角、主要人物、主线界面、王都通行与九视角性能检查。记录见 [full-cast-integration.json](CharacterProduction/full-cast-integration.json)，原始日志位于 `Logs/FullCrowdQA/final-native/`。

## 恢复额度后的混元补齐资源

2026-10-28 又生成了四套独立配角：商朝卫兵、教廷卫兵、东方男性平民、东方女性平民；另有历法箱、旅行沙漏、学者皮面书和未绑骨的教皇服装修订候选。文件位于 `Art/HunyuanExpansion/`，预制体在其 `Prefabs/` 子目录；本轮交付为资源库，没有自动覆盖主线人物或道具。制作、实际导入、动作与外观检查及限制见 [混元后续素材](CharacterProduction/HUNYUAN_EXPANSION_20261028.md)。

资源授权、来源及使用位置见 [ASSET_SOURCES.md](ASSET_SOURCES.md)。

## 2026-10-27 美术升级

中央主殿按原著改为石砌金字塔，七座巨摆环绕。玩家沿正面缓坡进入低矮入口，穿过封闭甬道到内殿献历法；验证时开启石板天窗，按 R 可以看向日光落点。验证后经侧面甬道与东坡前往复苏池苑。长夜观察位在池苑西侧，三颗飞星位于东北方，避开金字塔遮挡。具体尺寸属于游戏设计，见 [王都说明](SHANG_CAPITAL.md)。

戈壁与王都河谷改用独立 Unity Terrain（1025 高度图、512 地表混合图）、2K 扫描环境贴图与摄影建模岩石。池苑和河岸树木使用摄影叶片、三级 LOD，草丛使用完整纹理轮廓。新增资源由 `python3 Tools/fetch_environment_assets.py` 获取，来源见 `environment-asset-manifest.json`。重建仍使用 Rebuild Gobi And Shang Capital，截图使用 Capture Shang Capital。
