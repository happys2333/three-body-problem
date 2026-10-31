# 朝歌宫殿：实体砌块与厚墙套件

2026-10-31，Unity 6000.3.27f2 / Built-in。中央宫殿已在正式朝歌场景中改为自行搭建的实体砌石结构；外层逐块砌筑，错缝、倒角和深浅差形成真实接缝，内墙保留连续实体核心。建筑规模由模块数量决定。

宫殿基底约 **120×156 米**，基座 **4 米**，塔体 **78 米**；甬道、内殿与贯穿塔体的天窗保留。预制体还包含入口及东侧坡道，因此整体包围盒大于基底尺寸。数字属于本项目空间设计。

## 使用入口

- 正式场景：`Assets/_Project/Scenes/Traveller/02_ChaogeCourt.unity`，宫殿位于 `03 Stone pyramid palace/Pyramid masonry and enclosed palace`。
- 模块目录：`Assets/_Project/Art/MonumentalMasonry/Prefabs/`。
- 导入包：`Builds/MonumentalMasonryKit_20261031.unitypackage`。
- Mac 运行包：`Builds/MonumentalCapitalPreview.app`，包含三个剧情场景；正常入口为戈壁，F8 用于首都美术预览。
- 只升级宫殿：`Tools → Three Body Traveller → Upgrade Palace To Authored Masonry`。已有未保存的首都修改会阻止重建，原场景备份至 `Logs/masonry-backups/`。

| 预制体 | 用途 / 米制尺寸 |
| --- | --- |
| `GiantStone_2p8x1p4x1p8` | 单块巨石：2.8×1.4×1.8 |
| `ThickWall_8p4x4p2x4p2` | 厚墙：8.4 宽×4.2 高×4.2 厚 |
| `ThickCorner_8p4` | 8.4 米外轮廓转角 |
| `Doorway_5p6_Clear` | 净宽 5.6、净高 4.2，墙厚 4.2；上方过梁与两侧实体墙 |
| `Buttress_8p4_High` | 4.2×8.4×4.2 扶壁 |
| `Lintel_8p4` | 8.4×2.1×4.2 过梁 |
| `StoneFloor_8p4` | 8.4×1.4×8.4 基础 / 铺台 |
| `RoyalPalace_120x156x78` | 已搭建的大宫殿结构，含双向通路与室内 |

根缩放保持 1。墙段建议按 8.4 米水平、4.2 米高度拼接；单砖用于局部修补。墙体按空间分块保存三级 LOD，碰撞独立于渲染档位。外壳采用更大的约 5.5 米宽、2.6 米高砌石，以人物尺度体现体量。静态 MeshCollider 用于建筑，不配置动态刚体。

完整宫殿预制体仅含建筑及固定陈设；NPC、可移动天窗盖和剧情控制保留在正式场景父节点中。将该预制体放进另一场景时，需要另配人物、天窗机关与剧情。混元生成的 48 米 `PyramidEntryPassage` 保留为资源研究与独立试走样件，中央宫殿采用本套自行搭建的结构。

## 检查证据

- [集成记录](integration.json)：仅替换宫殿几何，核对几何以外对象身份。
- [保存后检查](saved-prefab-audit.json)：重新加载八个预制体，检查引用、顶点、法线、UV、切线、LOD、碰撞、根尺寸及实际门洞控制器通行。
- [正式场景预览](Previews/Palace_FromStreet.jpg)、[入口人物尺度](Previews/GiantBlocks_AtEyeLevel.jpg)、[内殿](Previews/RoyalChamber.jpg)。
- 原生连续试走、性能与 EditMode 结果以最终 `delivery.json` 为准；`Native/attempt01` 保留侧门被砌石层堵住的失败版本。修正后两片东墙各自止于门洞边界。

砌块几何、拼装工具为本项目制作；岩石摄影表面沿用项目已归档的 Poly Haven Rock Face（CC0），包中附来源。该美术方案是小说启发的游戏建筑，并非殷商遗址复原。

![街道中的宫殿](Previews/Palace_FromStreet.jpg)

![入口人物尺度](Previews/GiantBlocks_AtEyeLevel.jpg)

## 本轮最终结果

八个预制体保存后检查通过，入口模块实际控制器通行与侧墙阻挡通过；五项既有 CapitalSeparation EditMode 检查通过，包括完整路径胶囊空间、天窗遮光 / 开光和观星视线。正式运行包连续行走约 **369.0 米，36 项检查通过**。构建 0 错误、0 警告。宫殿以外 **4554 个对象身份保持**；场景配置、戈壁、中世纪场景和 Build Settings 与任务开始快照相同。

砌块资源包约 **33.4 MB**，八个预制体、1218 个依赖条目，序列化 GUID 闭合且载荷与当前文件匹配。[完整交付记录](delivery.json)。

**主游戏性能预算未通过。** 本机 Apple M5、1600×900、120 FPS 上限，九个视角每个预热 120 帧、采样 300 帧，P95 为 **17.61–62.95 ms**；首都七个视角为 **25.03–62.95 ms**。没有运行错误，但不能承诺稳定 60 FPS。本轮和前一轮失败采样均保留，不能把不同运行轮次当成同条件加速比。此限制不影响模型引用、碰撞和路线的已通过检查。

采样脚本的部分高位预设镜头会受玩家重力影响而下落；相应画面不能算作固定高位机位覆盖。当前性能结果仍按未通过保留，后续应先固定采样镜头再做专项优化。
