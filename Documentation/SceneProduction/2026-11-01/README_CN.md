# 朝歌生活街区与分时代卫兵

本轮使用用户 Chrome 中的混元 3D Studio 生成 9 件素材，累计使用 29 次免费请求，页面最后显示余量 1 次。概念、几何、纹理、绑定分别计次。原始下载、提示词、SHA-256 与 Unity 路径保存在 [生成清单](generation-manifest.json)。生成来源不等于成品：进入场景的是经过尺寸、材质、碰撞和外形修订的版本。

## 本轮内容

| 内容 | 实际处理与用途 |
| --- | --- |
| 商朝宫廷卫兵 V2 | 新蒙皮人物，23 骨骼有效 Humanoid，身高 1.83 米；替换 4 名宫廷卫兵 |
| 商朝城门卫兵 | 复用已有独立蒙皮人物，2 名；青铜矛、木皮盾、轻微换重心及短程巡逻 |
| 中世纪教会卫兵 | 复用已有教会卫兵，2 名；钢矛、红色鸢盾，修正盾面朝向 |
| 木制瞭望台 | 7 米高；4 处城门与外墙哨位 |
| 三开间土木民居 | 清除生成模型的绿色弯檐，重做直坡厚茅草屋面 |
| 泥质铸造炉 | 5 座；保留炉体贴图，添加运行时火焰、火星与局部灯光 |
| 木车、灰陶罐、粮筐 | 布置市集、作坊、仓储；替换 24 个球形粮罐 |
| 取水井 | 3 处；保留石井圈，重建木柱、横梁、麻绳与木桶，清除生成的现代金属曲柄 |
| 木桥 | 生成结果的凸起结构未通过实机外形检查；另行制作独立倒角木板、承重梁、栏杆、立柱及绳结，设连续行走面与栏杆碰撞 |

卫兵保留原预制体 GUID、场景角色身份和对话角色。商朝宫廷持戈，城门持矛，中世纪持钢矛；所有武器与盾牌各有三级 LOD。人物身体当前保留完整蒙皮网格，尚未制作身体减面 LOD。没有手指骨的生成角色使用局部握持形变；手臂用 Humanoid IK，校准 Avatar 手部旋转，并以真实手腕位置检查握持距离。夜间盾移到背后、左手举火把；白天恢复持盾。城门卫兵短程巡逻，玩家靠近或对话时停步。

朝歌增量替换 71 栋住宅、后舍、粮仓和工坊，补齐屋顶两端山墙。西侧居住区移离宗庙台基，6 组市集摊位改用已加工素材。工坊与宗庙的 11 处简易铜鼎换成既有细节模型；宫殿内另外两处合批铜鼎使用派生网格剔除旧器物后替换，原始合批网格和砌块结构保留。木车避开水渠与桥面。复用的铜鼎、摊位、货物、房舍等来自项目已有资源库。

## 文件与重制入口

- `ArtSource/ShangCapital/2026-11-01/`：不可变原始概念、FBX 与绑定下载。
- `Assets/_Project/Art/HunyuanCapital20261101/`：Unity 原始输入和候选库。
- `ArtSource/ShangCapital/Prepared/2026-11-01/`：加工输入、可编辑 Blender 文件、分级网格和制作记录。
- `Assets/_Project/Art/ShangCapitalReady/`：正式静态预制体与派生网格。
- `Assets/_Project/Characters/Guards/`：分时代装备、握持网格和配置。
- [资产目录](asset-catalog.csv)、[Unity 输入检查](import-validation.json)、[成品检查](ready-validation.json)、[场景替换统计](scene-integration.json)。

Unity 菜单：`Tools → Three Body Traveller → Guards → Prepare Era Guards`，随后 `Apply Era Guards To Scenes`。场景增量布置入口：`Capital → Apply Prepared Civic Districts`，要求先保存并打开 `02_ChaogeCourt`。这些入口仅用于本项目；不会重建戈壁、宫殿结构或复苏系统。场景写入前的备份位于 `Logs/era-guard-backups/` 和 `Logs/civic-backups/`。

离线处理脚本为 `Tools/prepare_shang_capital.py` 与 `Tools/prepare_era_guard_weapons.py`，使用 Blender 后台执行。Unity 导入入口 `ShangCivicAssetProduction.ImportReady()` 会保存并重新导入网格，避免原位替换后 GPU 仍显示旧顶点缓冲。

## 运行与验证

正常游玩继续从 `01_GobiJourney` 开始，保持第一人称。直接检查本轮美术可打开 `02_ChaogeCourt`，通过 F8 预览切换时刻。macOS 开发构建为 `Builds/ShangCapital20261101/Traveller.app`，正常启动不会运行检查脚本。

检查脚本需要显式命令行开关：

| 参数 | 检查范围 |
| --- | --- |
| `-shang-civic-check /绝对路径/report.json` | 保存场景中的卫兵、建筑、道具、日夜装备、双手握持、巡逻与真实控制器过桥；10 个视角截图和帧时 |
| `-traveller-capital-check /绝对路径/report.json` | 真实 CharacterController 连续走过城门、宫殿前坡、内殿、侧面甬道至复苏池 |
| `-capital-revival-check /绝对路径/report.json` | 七人的纸片、吸水、恢复体积、起身与复原；平民日夜火把、玩家手动火把；结束后恢复原观察簿 |

最终构建、功能与性能结果分别见 [构建记录](native-build.json)、[EditMode](editmode-tests.json) 与 [交付记录](delivery.json)。`Native/` 保留原生截图、日志和报告；早期尝试单独标注，最终目录为 `Civic`、`CapitalWalk`、`Revival`。功能通过和帧时预算分开统计。短时本机采样不代表所有机器或完整剧情性能；此前宫殿砌块版本也存在未达预算的视角，参见 `Documentation/Architecture/MonumentalMasonry/delivery.json`。

最终 9 项 EditMode、57 项街区与卫兵检查、36 项约 369 米连续行走、38 项复苏流程检查通过；macOS 构建 0 错误、0 警告。最后一轮 10 个街区视角的稳定段 P95 为 3.61–13.08 ms，但换视角预热段仍有卡顿；复苏过程 P95 为 25.41 ms，整场稳定 60 FPS 尚未通过验证。`Previews/` 为制作过程截图，其中木桥重建前的素材库照片已标为 `BeforeBridgeRebuild`，最终外形以 `Native/Civic` 为准。

## 下一批模型的优先级

制作重点已调整为宫殿内部陈设。下一批优先生成御座、兽面雷纹浮雕板、青铜爵和觚；五层木箱沙漏与卦象地面采用精确自建结构。火盆、铜鼎、历箱与甲骨已有可复用素材。[内殿缺项、布置图与提示词](../../InteriorProduction/2026-11-01/README_CN.md) 已整理。

上一批结束时观测剩余 1 次请求，内殿研究未消耗新额度；该数值不作为持续更新的实时余额。待生成的模型和待完成的陈设不会计入本轮已交付资产。整体为小说启发的殷商王都美术，材料和轮廓参考考古资料，不宣称严格遗址复原。

![宫廷卫兵与细节铜鼎](Native/Civic/10_Palace_Guard_Day.png)

![西侧居民区](Native/Civic/07_Western_Residential_Ward.png)

![水渠木桥](Native/Civic/06_Watercourse.png)
