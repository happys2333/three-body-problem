# 混元建筑与戈壁环境素材

**修整版入口：** [20 件环境预制体与第一人称试走场](Ready/README.md)。新增的 `EnvironmentReady` 库包含重新修整的形体、摄影材质、碰撞和三级 LOD；本页及原清单保留生成候选的下载记录与初次美术问题，实际布景请选择修整版。

2026-10-28，按三个现有游戏场景的需要，在用户 Chrome 中使用混元首页的文生 3D V3.1 提交了 20 组生成。首页提示的每日 20 次与先前用完的 Studio 30 次是两套额度；本批使用首页免费额度，没有购买、订阅或新建账号。每组包含多个候选，本库优先导出带 PBR 标记的版本。

实际提示词、尺寸目标、导出文件、哈希与阶段见 [制作清单](hunyuan-environment-2026-10-28.json)。下载、导入与美术复核是不同阶段，生成完成不等于已放进游戏。

本批 20 个 PBR FBX 已全部下载、归档并导入 Unity，没有待导出文件。40 份源文件副本的 SHA-256 一致；20 个预制体的材质、尺寸和三级 LOD 检查通过，保存后的网格与材质引用也已重新载入检查，素材库 160 个文件都有 Unity 元数据。36 项现有 EditMode 回归通过。完整实际状态见 [交付记录](hunyuan-environment-delivery.json)、[保存后引用检查](hunyuan-environment-saved-prefab-audit.json) 与 [资源目录 CSV](ENVIRONMENT_CATALOG.csv)。

## 本批内容

| 场景 | 资源 |
| --- | --- |
| 戈壁 | 荒漠高山、破碎岩脊、风蚀断崖、雅丹孤丘、风化岩群、悬挑巨岩、枯死柽柳、干枯灌木、驿路石标、旅人行囊 |
| 殷商王都 | 夯土木构民居、铸铜作坊、粮仓、市集摊棚、城门楼候选、金字塔入口、厚墙石砌饰面 |
| 中世纪 | 石木城市民居、哥特石窗墙段、教堂扶壁候选 |

前景复杂山体与主要建筑请求 50 万三角形，其余请求 5 万三角形；实际面数以导入报告为准。原始 FBX 保存在 `ArtSource/Environment/Hunyuan/2026-10-28/`，Unity 文件在 `Assets/_Project/Art/HunyuanEnvironment/`，预制体在其 `Prefabs/` 子目录。

## Unity 处理

`Tools → Three Body Traveller → Assets → Prepare Hunyuan Environment Library` 提取 FBX 内嵌贴图，配置 Built-in Standard 材质与 2K 导入上限，将粗糙度转换成 Unity 的光滑度通道，统一使用尺寸、底部落地，并生成三层 LOD。原始高精度网格保留，远景使用保留 UV 分区的合并顶点版本；最远层关闭投影。

工具按资源写入 `hunyuan-environment-unity-validation.json`。长批次可以调用 `HunyuanEnvironmentLibrary.PrepareBatch(skip, take)` 分段处理，参数针对清单中已下载的资源；报告合并保留之前处理的结果。`CaptureLibrary()` 在隔离预览场景中使用最高细节层渲染，每张图最多四个资源；它不打开或保存剧情场景。

本库目前用于筛选和布景准备。大范围可行走地面继续由现有 Terrain 承载，生成的山体、断崖和岩群用于塑造山势及近景层次。可行走的门洞、岩下空间、地面、天窗和碰撞需要按现有路线单独检查。三个剧情场景、第一人称玩家与主线行为没有自动替换。

## 美术复核边界

技术检查通过只表示网格、材质引用、尺寸和 LOD 可用。模型是否写实、符合殷商设定、可无缝拼接或可穿行，需要另外复核。

初批 Unity 预览发现：门楼仍有弯曲檐口和绿色饰边；金字塔入口含模板化装饰；石墙模块带突出檐口，不是可直接平铺的完整墙段；教堂构件增加了未要求的雕像和山墙。这些已保留为候选，不能直接当作符合全部提示词的最终建筑。中世纪民居也需要与当前场景统一表面质感。

已逐件复核导入的 20 个模型，均标记为 `reviewed_requires_rework`。自然岩体普遍带绘制式裂纹和展示底座；柽柳需去底座，荆棘结果更像小树；石标多出金色圆钉；行囊的皮带扣需要年代复核。原始提示词明确请求写实风格，但实际结果没有全部遵循，不能以源网格面数代替美术验收。

山脊候选还混入了类似门洞、栅格的人工结构，已明确标为不直接用于自然山脊。后续应以自然山脊参考图重生成；主峰与断崖保留形体参考价值，仍需写实岩面与地形接缝处理。

石墙饰面已经绕 Y 轴旋转 90°，按正立面宽 6 米重新归一化，最终高约 3.84 米、厚约 2.66 米。当前只是建筑饰面候选，仍需削薄和处理接缝。

当前全部 20 份 Unity 预览（每行从左到右，最高细节层）：

| 图 | 从左到右 |
| --- | --- |
| [01](Previews/Environment_Unity_01.jpg) | 雅丹孤丘、风蚀断崖、山脊候选、荒漠主峰 |
| [02](Previews/Environment_Unity_02.jpg) | 荆棘灌木、枯死柽柳、悬挑巨岩、戈壁岩群 |
| [03](Previews/Environment_Unity_03.jpg) | 铸铜作坊、殷商民居、行囊、驿路石标 |
| [04](Previews/Environment_Unity_04.jpg) | 金字塔入口、夯土门楼、市集摊位、粮仓 |
| [05](Previews/Environment_Unity_05.jpg) | 带雕像的教堂装饰塔、哥特石窗墙、中世纪民居、石墙饰面 |

浏览器实证：[最终主峰预览](Previews/Hunyuan_Environment_Browser.jpg)、[最后三个山体的下载记录](Previews/Hunyuan_Environment_Downloads.jpg)。

初批美术复核图：[门楼、石门、墙段与民居](Previews/Initial_Architecture_Review_01.jpg)、[哥特构件](Previews/Initial_Architecture_Review_02.jpg)。最新全库预览及其资源顺序以交付记录为准。

尚未纳入剧情场景的素材不沿用上一轮实机检查与性能结论。后续实际放置时，还需复核遮挡、飞星与三日视线、主线路径和运行开销。
