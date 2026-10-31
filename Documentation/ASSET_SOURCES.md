# 资源来源与授权

采集日期：2026-10-26。下表资源已下载到 `Assets/ThirdParty/`；原始授权随包保留。逐文件 SHA-256、下载地址与文件列表见 [asset-manifest.json](asset-manifest.json)。文末另列用户补充的候选资源，尚未导入。

| 资源 | 原作者 / 来源 | 授权 | 工程用途 |
| --- | --- | --- | --- |
| Castle Kit 2.0，76 个 FBX 模型及色板 | [Kenney](https://kenney.nl/assets/castle-kit) | CC0 1.0 | 中世纪钟楼、城墙、观测台矮墙；保留完整模型库供继续搭建 |
| Nature Kit，16 个选用 FBX 模型 | [Kenney](https://kenney.nl/assets/nature-kit) | CC0 1.0 | 保留为道具素材库；戈壁主要岩壁、草丛与王都树木已由 2026-10-27 的扫描环境素材替换 |
| Dry Riverbed Rock，1K 漫反射、法线、粗糙度 | [Poly Haven](https://polyhaven.com/a/dry_riverbed_rock) | CC0 1.0 | 王道路面与早期地表底材；漫反射与 OpenGL 法线已配置材质 |
| Sandstone Blocks 05，1K 漫反射、法线、粗糙度 | [Poly Haven](https://polyhaven.com/a/sandstone_blocks_05) | CC0 1.0 | 朝歌与中世纪石壁、铺装 |
| Namaqualand Boulder 02，FBX 及 1K 贴图 | [Poly Haven](https://polyhaven.com/a/namaqualand_boulder_02) | CC0 1.0 | 巨岩庇护所，岩石材质 |
| Noto Sans CJK SC Regular | [Noto Project](https://github.com/notofonts/noto-cjk) | SIL Open Font License 1.1 | 可分发的中文界面字体，原文件未修改 |

Poly Haven 的资源授权说明：[Asset License](https://polyhaven.com/license)。资源准备脚本使用其公开 API 获取下载地址，感谢 / Powered by Poly Haven。

## 工程原创内容

- 朝歌大厅、门楼、曲面屋顶、巨摆、王座、青铜沙漏、观测路标与中世纪尖拱：编辑器工具生成的原创模块。
- 主线六套主要人物来自混元生成；卫兵与平民已使用带贴图和骨架的完整人体，来源分别为已有混元底模及下文的 Mixamo 人物。原人物占位体已替换。
- 星空、三个太阳、草火、烟尘、雪、裂隙、UI 布局为本工程制作；没有使用来源不明的图片或商业模型。
- `DesertWind.wav` 是本地程序合成的原创循环风声，没有使用第三方录音。
- 本轮殷商王都的城垣门塾、四坡屋顶、木土民居、宗庙与高台王庭、青铜礼器造型、道路和通行坡道为本工程生成的原创网格。结构、独立场景与验证见 [SHANG_CAPITAL.md](SHANG_CAPITAL.md)。

## 新增建筑资源库

建筑素材首轮取得 18 组资源；王都建设时新增 [Reed Roof 03](https://polyhaven.com/a/reed_roof_03) 茅草屋面（Poly Haven／Rob Tuytel，CC0，1K），现共 19 组。实际内容、使用位置、来源授权和预览见 [建筑素材库](ARCHITECTURE_ASSETS.md)，逐文件记录见 [architecture-asset-manifest.json](architecture-asset-manifest.json)。

其中 CS Studio 的五栋中式建筑允许项目使用，但限制素材再分发，已单独放入 Git 忽略的 `Assets/ThirdParty/LocalOnly/`；其余新增入库原素材使用 CC0。下面原有的资源建议继续作为人物、声音和戈壁细节的候选。

## 高质量环境素材（2026-10-27 已导入）

新增 10 组 CC0 资源、60 个模型或贴图文件，原素材约 192.6 MiB。获取脚本为 `Tools/fetch_environment_assets.py`，来源、逐文件 SHA-256 与下载路径记录于 [environment-asset-manifest.json](environment-asset-manifest.json)。

| 资源 | 使用位置 |
| --- | --- |
| [Rock Face 01](https://polyhaven.com/a/rock_face_01)、[Rock Face 02](https://polyhaven.com/a/rock_face_02) | 戈壁侵蚀岩壁、远景岩层、首都河谷出露岩石 |
| [Namaqualand Boulders 01](https://polyhaven.com/a/namaqualand_boulders_01) | 戈壁近景碎石与石群 |
| [Shrub 03](https://polyhaven.com/a/shrub_03)、[Grass Medium 01](https://polyhaven.com/a/grass_medium_01) | 枯灌木、路旁草丛、农田和河岸植被 |
| [Tree Small 02](https://polyhaven.com/a/tree_small_02) | 王都池苑、河岸树木；保留树干与枝条，用原摄影贴图制作完整叶片卡与三级 LOD |
| [Aerial Rocks 02](https://polyhaven.com/a/aerial_rocks_02)、[Aerial Grass Rock](https://polyhaven.com/a/aerial_grass_rock)、[Brown Mud Dry](https://polyhaven.com/a/brown_mud_dry)、[Rocky Terrain 02](https://polyhaven.com/a/rocky_terrain_02) | 两个独立 Terrain 的 2K 地表层，根据坡度、河岸与噪声混合 |

同时核验 [Mountain Environment – Dynamic Nature](https://assetstore.unity.com/packages/3d/vegetation/mountain-environment-dynamic-nature-191851) 商店页面：页面列出 Unity 6000.3.9f1 的 Built-in 兼容性。该包目前为付费候选，未下载或并入工程；本轮场景实际使用上表已取得的 CC0 素材。

## 用户补充资源：选用顺序

依据用户于 2026-10-26 提供的资源整理文本，并与当前工程逐项对照。下列选用项已检查来源页面；尚未下载、试听、导入或做项目兼容测试。原文提到的完整 Excel 未随本轮附件提供，本次没有修改该表，也未将原文所称的 89 项全部视为已核验资源。

| 顺序 | 候选资源与来源 | 对应当前缺口 | 接入要求与页面核验结果 |
| --- | --- | --- | --- |
| 1：旅人外观 | [MPFB](https://static.makehumancommunity.org/mpfb/downloads.html) + [Suits 02](https://static.makehumancommunity.org/assets/assetpacks/suits02.html) | 替换周文王的几何占位造型，再复用身体制作纣王、教皇与学者 | MPFB 官方推荐从 Blender 扩展平台安装；服装包需另装。Suits 02 列有 CC0 的 Donitz 僧袍，仅作改造底材，交领、袖口、冠与背架需制作。整理骨架后导出 FBX。 |
| 2：人物动作 | [Human Basic Motions FREE — Kevin Iglesias](https://assetstore.unity.com/packages/3d/animations/human-basic-motions-free-154271) | 周文王目前移动身体而没有正式行走动画 | 商店页面列为免费、Standard Unity Asset Store EULA，并列出 Unity 6 的 Built-in 兼容性。须核对实际片段，完成 Humanoid 重定向与步速匹配；不能只导入动画就视为角色完成。 |
| 3：基本声音 | [Fantozzi’s Footsteps](https://opengameart.org/content/fantozzis-footsteps-grasssand-stone)、[Fire Crackling](https://opengameart.org/content/fire-crackling)、[RPG Sound Pack](https://opengameart.org/content/rpg-sound-pack) | 目前以合成风声为主，缺少脚步、火源与衣料声音 | 页面标注 CC0。脚步包提供 12 个单步和 OGG 版本；RPG 包提供 WAV。先试听沙地、石地、衣料与燃烧子集，接入行走节奏和火源距离衰减，对话期间降低环境声。 |
| 4：戈壁近景 | [Wild Rooibos Bush](https://polyhaven.com/a/wild_rooibos_bush) + [Namaqualand Boulder 03](https://polyhaven.com/a/namaqualand_boulder_03) | 改善干灌木形态与岩石重复感 | Poly Haven CC0。现有 Boulder 02 已导入，03 用作变化；先检查网格预算、透明叶片与 LOD，选 1K／2K 贴图。替换外观时保留巨岩安全区、可走通道和剧情目标坐标。 |
| 5：地表变化 | [Sand 01](https://polyhaven.com/a/sand_01) + [Mud Cracked Dry 03](https://polyhaven.com/a/mud_cracked_dry_03) | 普通戈壁与高温干裂地表之间缺少细节变化 | Poly Haven CC0。作为现有地表材质的补充；由已有巨日、灾变事件控制混合。裂纹贴图只提供表面细节，地形动态裂开仍需另做。 |
| 6：太阳细节备选 | [Solar System Scope](https://www.solarsystemscope.com/textures/) | 现有三个太阳为程序生成，可按最终画风补表面细节 | 页面使用 CC BY 4.0，采用时加入来源与修改说明。现有飞星计数、三日观察窗口和天象时序继续由剧情控制器管理。 |

Poly Haven 上述模型与贴图的授权依据为其 [Asset License](https://polyhaven.com/license)。表中顺序是结合本工程现状的制作安排，网页上的兼容性说明不等同于本项目实测。

### 渲染管线适配

当前工程使用 **Built-in Render Pipeline**。清单中的 [Free Fire VFX — URP](https://assetstore.unity.com/packages/vfx/particles/fire-explosions/free-fire-vfx-urp-266226) 页面明确列出 Built-in 不兼容，因此暂列备选。清单里的其他 URP 扭曲、溶解资源也应先单独核验，不能按名字相近就直接替换现有材质。

现有火焰、热浪和天空可继续承担演出；后续先补齐人物、动作和基本声音。朝歌主体、巨摆与背负式沙漏仍需按剧情设定制作，通用城堡、僧袍和普通沙漏仅提供底材。卫兵处置与火刑分支的素材需求先记录，具体演出随对应剧情功能接入。

## 更新与复现

在仓库根目录运行 `python3 Tools/fetch_scene_assets.py` 可以重新获取上述第三方资源并更新清单。下载缓存位于已忽略的 `Logs/asset-downloads/`；FBX、贴图、字体和各自授权保存在工程内。

外部资产沿用各自授权；仓库自身的 LICENSE 不改变第三方文件的授权。

## 原著风格金字塔王宫（2026-10-27）

金字塔外壳、倒角砌石、厚墙甬道、内殿、竖井与滑动石板由项目生成工具制作，使用已有 Poly Haven CC0 `sandstone_blocks_05` 与 `stone_wall` 扫描贴图。未为这次改造新增第三方付费资产。小说的建筑特征与项目自行设计的尺寸分别说明于 [王都制作说明](SHANG_CAPITAL.md)。

## 混元生成角色与动画（2026-10-27）

来源为用户已登录的[腾讯混元 3D Studio](https://3d.hunyuan.tencent.com/studio/creation/ae)，通过 Chrome 界面完成生成和下载。本轮取得六张角色概念图、六个带 PBR 贴图及骨架的角色 FBX、六个动画 FBX。使用平台当日 30 次免费生成额度，没有购买、订阅或创建新账号。

原始下载文件名、概念提示词、SHA-256、Unity 路径及实际完成状态见 [生成清单](CharacterProduction/hunyuan-production-manifest.json)。这些平台生成资产单独记录来源，不沿用 Poly Haven 的 CC0 标识。Unity 材质中将原金属度与反向粗糙度打包为 Standard Metallic/Smoothness；完整原始文件保存在 `ArtSource/Characters/Hunyuan/`。

卫兵、平民及未取得的动作仅在需求清单中列为待制作；没有把生成目标计入已交付资产。详细使用方式见[场景制作说明](SCENE_GUIDE.md)。

## Mixamo 人物动作补充（2026-10-27）

在用户明确授权、并由用户完成 Adobe 登录后，通过其 Chrome 中的 [Mixamo](https://www.mixamo.com/#/) 下载动作，没有上传六个角色模型。下载 Y Bot 的 T-pose 作为共同参考骨架，动画使用不带角色的 FBX、30 FPS、不简化关键帧；`MX_Fear` 曾请求 Unity 和 Binary 两种格式重试，最终取得文件的具体成功预设未能唯一对应，文件已通过实际 Humanoid 导入和六角色重定向检查。

四包为 Gestures Pack Basic（15）、Locomotion Pack（12）、Action Adventure Pack（22）、Male Injured Pack（20），另有 20 个单独动作，共 89 个源片段。原下载保存在 `ArtSource/Characters/Mixamo/Downloads/`，拆分后的源文件在 `ArtSource/Characters/Mixamo/Motions/`，Unity 动作在 `Assets/_Project/Characters/Mixamo/Animations/`。下载页、原文件名、逐文件 SHA-256 及验证状态见 [mixamo-assets.json](CharacterProduction/mixamo-assets.json)。`archive_mixamo_download.py` 只归档用户浏览器实际取得的本地文件，不调用服务的隐藏接口。

Mixamo 是 Adobe 的服务，原始角色与动画不标为 CC0，仓库自身的 LICENSE 不替代其服务与内容条款。项目没有购买或订阅。四条派生动画的来源和改动见[制作说明](SCENE_GUIDE.md)，混元跌倒校正也保留了原始 FBX。主要人物的剧情动作已经接入，实际触发范围与仍待完成的道具、衣料工作见[覆盖表](CharacterProduction/ACTION_COVERAGE.csv)。

第一人称身体网格由已有 `PlayerTraveller` 角色派生，只删除会挡眼的头颈及上缘三角形，保留原始模型、UV、PBR 材质和蒙皮。完整模型在剧情中仅投影，人物图库保留原外观。剧情控制器和坐姿遮罩由项目制作，背部沙漏复用已有项目预制体。

## 完整配角模型（2026-10-28）

通过用户 Chrome 的 [Mixamo](https://www.mixamo.com/#/?type=Character) 下载 `Peasant Girl`、`Peasant Man`、`Knight D Pelegrini`，格式为带皮肤和贴图的 Unity FBX、T-pose。模型分别为 5,036、4,625、13,124 个三角形，不能描述为新增的高面数扫描人物；两套农民源模型也保留较明显的风格化比例。商朝卫兵和部分男性平民复用已有约 50,000 三角形的 `PlayerTraveller`。来源标题、实际文件与 SHA-256 见 [crowd-model-sources.json](CharacterProduction/crowd-model-sources.json)。未购买素材、订阅或上传用户模型。

原始 FBX 在 `ArtSource/Characters/Crowd/Mixamo/`，Unity 源模型与导出的贴图在 `Assets/_Project/Characters/Crowd/Sources/`。共制作九套带骨架的配角造型，用于替换六名商朝卫兵、两名教廷卫兵、十二名王都居民和七名复苏者。服装色调通过顶点遮罩着色器调整，头脸与手部保留原贴图；商朝札甲、青铜盔、矛盾为本项目制作的装备网格，随人体骨骼挂接，属于游戏美术设计，不声称考古复原。

Mixamo 源模型沿用 Adobe 服务与内容条款，**不是 CC0**。本仓库 LICENSE 不替代这些条款。九套造型复用四个人体源模型，不能算作九个新生成的人体，更不能算作二十七个独立角色源模型。

## 混元后续人物与剧情道具（2026-10-28）

通过用户 Chrome 中已登录的混元 3D Studio，使用恢复的 30 次免费额度生成素材：四套带骨架配角、三件静态道具，以及一个未绑骨的教皇服装修订候选。生成提示、下载文件、逐文件 SHA-256、弃用概念图和错误绑定的复核记录保存在 [hunyuan-expansion-2026-10-28.json](CharacterProduction/hunyuan-expansion-2026-10-28.json)，原始文件在 `ArtSource/HunyuanExpansion/2026-10-28/`，Unity 文件在 `Assets/_Project/Art/HunyuanExpansion/`。

这些模型为腾讯平台生成输出，不标为 CC0；项目 LICENSE 不替代平台适用的生成与内容条款。没有购买、订阅、创建账号或向其他模型服务上传本批文件。新增资源的用途、验证及仍待处理的骨架、玻璃材质与交互细节见 [制作说明](CharacterProduction/HUNYUAN_EXPANSION_20261028.md)。

## 混元建筑与自然环境（2026-10-28）

通过用户 Chrome 中的[混元图／文生 3D](https://3d.hunyuan.tencent.com/creation) V3.1 提交二十组建筑与环境生成，使用首页界面显示的二十次每日免费额度。该额度与本日已用完的 Studio 三十次分开；本批没有购买、订阅、新建账号或调用隐藏 API。

逐项提示词、请求面数、下载与导入状态、源文件 SHA-256 见 [环境制作清单](EnvironmentProduction/hunyuan-environment-2026-10-28.json)。原始 PBR FBX 在 `ArtSource/Environment/Hunyuan/2026-10-28/`，Unity 资源在 `Assets/_Project/Art/HunyuanEnvironment/`。这些是平台生成内容，不沿用第三方扫描资源的 CC0 标签。技术检查与美术筛选分开记录，当前素材库没有自动替换三个剧情场景；说明与预览见 [环境素材库](EnvironmentProduction/README.md)。

## 环境素材修整与摄影表面（2026-10-28）

20 件生成候选经形体修整、屋顶与构件重建、材质重配后，另存为 `Assets/_Project/Art/EnvironmentReady/`。原始 FBX 及其 Unity 副本保持不变，可编辑 Blender 工作文件在 `ArtSource/Environment/Prepared/2026-10-28/`。具体改动、使用范围、预览和验证见 [修整版说明](EnvironmentProduction/Ready/README.md)。

| 新增摄影材质 | 授权 | 用途 |
| --- | --- | --- |
| [Rock Face](https://polyhaven.com/a/rock_face) | Poly Haven，CC0 | 山体、岩群与石标的自然岩面 |
| [Thatch Roof Angled](https://polyhaven.com/a/thatch_roof_angled) | Poly Haven，CC0 | 重建的直坡茅草屋面 |
| [Pine Bark](https://polyhaven.com/a/pine_bark) | Poly Haven，CC0 | 荒漠枯树和灌木的粗糙树皮表面 |

每套包含反照率、OpenGL 法线、粗糙度、遮蔽四张 2K JPG，共 12 张，放在 `Assets/ThirdParty/EnvironmentReady/PolyHaven/`。获取脚本为 `Tools/fetch_environment_ready_surfaces.py`；[逐文件来源与 SHA-256](EnvironmentProduction/Ready/surface-sources.json) 保留官方页面和公开下载地址。石砌、抹灰、木板、瓦面与试走场地面复用本项目已有 Poly Haven 资源。修整版将粗糙度和遮蔽合并到同一张线性纹理的 R/G 通道以减少采样；原图和授权记录仍完整保留。摄影贴图的 CC0 标识不扩展到混元网格及行囊原图集。

## 本地语音模型与临时配音（2026-10-29）

识别使用 [Qwen3-ASR-0.6B 的 MLX 8bit 转换](https://huggingface.co/mlx-community/Qwen3-ASR-0.6B-8bit/tree/89e96d92ba34aca20b3e29fb10cc284097d1219f)。合成使用 [MOSS-TTS-Nano-100M ONNX](https://huggingface.co/OpenMOSS-Team/MOSS-TTS-Nano-100M-ONNX/tree/f52645cb467506d8e18e746ddd59482685b74e58) 与 [MOSS 音频编解码器](https://huggingface.co/OpenMOSS-Team/MOSS-Audio-Tokenizer-Nano-ONNX/tree/ceff0d0749bfb3fa2d61149794ec6feef0d1e1ae)，底层 CPU 运行时代码固定为 [MOSS-TTS-Nano 8b7bcc9](https://github.com/OpenMOSS/MOSS-TTS-Nano/tree/8b7bcc9341b3b4ef3a3a58ba1338a7d85ff133eb)。模型与代码均从公开仓库取得；本地安装记录见 [model-installation.json](ActOne/LocalVoice/model-installation.json)，权重没有提交到游戏仓库。

六条剧情 WAV 与二十条试听由这些模型的内置 Weiguo、Zhiming、Junhao 声线生成，没有采集用户或其他真实人物录音来克隆。它们是临时模型配音，不标为 CC0；模型、预设声线及生成输出分别遵循对应上游的适用说明，项目 LICENSE 不替代这些条件。脚本文字、实际生成参数和转写对照保存在 `Tools/scripted_voice_lines.json`、`Tools/voice_profiles.json` 与 [LocalVoice](ActOne/LocalVoice/) 内。

## 混元环境与道具续批（2026-10-30）

通过用户 Chrome 中的混元首页 V3.1 使用恢复的二十次额度，生成十组荒漠及旅人道具、六组朝歌与宫殿构件、四组中世纪构件与室内道具。二十组均已生成，并于 2026-10-31 补齐全部二十件 PBR FBX 的正常下载与哈希归档。其中六件完成预制体技术加工，新增十四件待加工，全批尚无模型通过最终美术验收。续下载确认过网络中断，另有操作等待超时；此前的客户端阻止没有复现，未认定唯一根因。没有购买、订阅、上传个人文件、关闭安全检查或绕过安全警告。

原档在 `ArtSource/Environment/Hunyuan/2026-10-30/`，新候选库在 `Assets/_Project/Art/HunyuanEnvironment20261030/`。实际文件、提示词、哈希、待修整问题和剩余导出见 [批次说明](EnvironmentProduction/2026-10-30/README.md) 与 [清单](EnvironmentProduction/2026-10-30/manifest.json)。这些是腾讯平台生成内容，不标为 CC0，也未替换主线场景。
