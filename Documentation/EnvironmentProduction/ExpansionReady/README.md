# 可导入的环境资源库 · 2026-10-31

**24 件通过技术和美术筛选的预制体**，位于 `Assets/_Project/Art/EnvironmentExpansionReady/Prefabs/`。导入包为 `Builds/EnvironmentExpansionReady_20261031.unitypackage`，Unity 使用 Assets → Import Package → Custom Package。根缩放保持 1；三级 LOD、独立碰撞、米制落地尺寸、PBR 贴图、法线 / UV / 切线与零缺失脚本已从保存后的资产检查。

- 独立试走场：`Assets/_Project/Scenes/Environment/Environment_ExpansionYard.unity`。
- Mac 预览：`Builds/EnvironmentExpansionYard.app`。WASD 行走、右键环顾、Shift 快走。
- 可编辑源：`ArtSource/Environment/Prepared/2026-10-31/Output/<ID>/<ID>.blend`。
- [逐件目录](CATALOG.csv)、[保存后检查](saved-prefab-audit.json)、[美术筛选](art-review.json)、[当前运行记录](Native/final/qa.json)、[包检查](package-audit.json)、[交付记录](delivery.json)。

## 内容与使用边界

包括戈壁岩脊、岩柱、巨石、石堆、遮阴岩棚、干 / 炭化灌木、枯 / 焦树；夯土厚墙、木门楼、石柱、石阶、金字塔研究样件、青铜壁灯、甲骨、方鼎、火盆和货担；中世纪石拱门、长椅、讲台和铁火盆；本地重做的扎口皮水囊。

中央宫殿使用[自行砌筑的 MonumentalMasonry 结构](../../Architecture/MonumentalMasonry/README.md)：基底约 120×156 米，塔体 78 米。本环境库中的 `PyramidEntryPassage` 是宽约 48 米的可进入研究样件，含 14×18 米内室、约 8 米净高和王台；NPC、剧情和移动天窗机关不在该样件中。

`NaturalRidgeV2` 仅用于远景，不是可攀爬近景高山。石堆不替代细砾石地表。静态非凸碰撞不能直接用于动态刚体。火盆和壁灯带实时火焰、火星与光源；甲骨和水囊带拾取触发范围，玩家手部持握仍需接入。

## 本轮补齐下载与拒绝候选

今日七组、每组四个候选均已生成，页面显示剩余 13 次。七件选定源文件现已全部完整下载并归档；20 件旧源与 7 件新源的原档 / Unity 输入哈希均核对一致。

重新下载的 `GobiShadeCanopyV3` 在反向美术复核中暴露折叠薄片和屋顶破洞，**已从可用库及交付包移除**。它通过过技术 / 通行检查，但这些检查不足以证明美术可交付。继续使用 `ShadeOverhangV2`。拒绝依据见[反向截图](ReviewAttempts/RejectedCanopy/GobiShadeCanopyV3_Reverse.jpg)，修整尝试保存在 `ArtSource/Environment/Prepared/2026-10-31/RejectedOutput/`，Unity 研究副本位于 `EnvironmentExpansionRejected`。

`TravellerWaterskinV2` 已下载归档，因现代拉链拒绝使用；交付的是本地重做的 `TravellerLeatherFlask`，不计作额外生成或下载。坏的炭化灌木 / 焦树源也被拒绝，烧焦状态从干净植被派生。生成与下载过程见[本轮记录](../2026-10-31/README.md)。正常浏览器重试成功，没有关闭安全保护；一次成功不能证明此前失败的具体原因。

## 当前运行与性能

**最新 `Native/attempt10` 已同步到 `Native/final`：24 件资源加宫殿内室，共 25 个视角。** 实际 TravellerPlayer 控制器通过门洞、侧墙阻挡、岩棚进入、石阶攀登和宫殿内室检查，视线检查全部通过，无运行异常。

**最新性能预算未通过。** Apple M5、1600×900、120 FPS 上限、垂直同步关闭、60 米阴影距离；每个视角预热 240 帧、采样 360 帧。四个视角 P95 超过 16.67 ms，最慢约 38.11 ms。不能承诺稳定 60 FPS。`attempt09` 的 26 个视角曾全部通过帧时间检查，但它包含后来被美术拒绝的新岩棚，且随后重导入改变了预制体序列化；它保留为历史记录，不替代当前版本证明。

导出器强制技术、物理、视线和全部非性能运行检查通过，并将性能结果单独记录；没有改变 FPS 阈值或把失败报告改成通过。限制随包保存在 `Documentation/VALIDATION.txt`。主游戏另有[砌筑宫殿的功能 / 性能记录](../../Architecture/MonumentalMasonry/delivery.json)，不使用本试走场证明其帧率。

## 来源与重做

混元生成网格和原图集适用平台内容条件，**不是 CC0**；项目 LICENSE 不替代第三方条款。摄影表面来自项目已有的 Poly Haven Rock Face、Pine Bark、Thatch Roof Angled、Sandstone Blocks 05、Clay Plaster、Wood Planks，适用 CC0。来源随包保存在 `Assets/_Project/Art/EnvironmentExpansionReady/Documentation/`。

FBX 导出不稳定时使用平台正常 GLB 导出后本地转换，原档 / SHA-256 保留，金属度与粗糙度分别提取 glTF B / G 通道。制作入口为 `Tools/prepare_environment_expansion.py` 和 Editor 中 `EnvironmentExpansionLibrary`，导入保持 GUID 并显式更新网格数据。包适用于 Built-in 管线；检查了载荷和 GUID 依赖闭合，没有声称完成独立空工程导入或 URP / HDRP 转换。
