# 修整后的环境素材库

本目录交付 20 个用于静态布景和建筑外观的 Unity 预制体。生成源档继续保存在原素材库；实际使用请选择 `Assets/_Project/Art/EnvironmentReady/Prefabs/` 中的修整版。

2026-10-29，荒漠类 10 种素材已另行整合到第一幕，共 103 处布置；当前剧情场景的画面、实际步行与性能结果见 [戈壁场景整合](../../GobiIntegration/README.md)。下方“三个剧情场景保持不变”的记录对应此前独立素材库的交付阶段。

## 如何使用

1. 在 Unity 的 Project 面板进入上述 `Prefabs` 目录，将所需预制体拖入场景。单位为米，根节点缩放为 1，底部落地；材质、三级 LOD 和静态碰撞已包含在内。
2. 打开 `Assets/_Project/Scenes/Environment/Environment_ArtYard.unity`，点击 Play 即可第一人称试走。也可通过菜单 `Tools → Three Body Traveller → Assets → Open Environment Art Yard` 打开。
3. 按住鼠标右键环顾，WASD 行走，Shift 快走。前方石门和远处门楼均留出真实通道，两侧摆放建筑和荒漠物件。此场景是素材试走场；游戏的三个剧情场景仍独立保留在 Build Settings 中。
4. `Builds/EnvironmentArtYard.app` 是本机独立预览包，可直接启动试走。该构建只包含试走场，不是剧情游戏的正式运行包。

这轮工作完成素材准备和试走验证。大范围地面继续使用 Terrain，山体与岩块用于布景和地形层次。放置山体时应将平整底部埋入地形并补地面过渡，避免裸露切面；不要把展示底面当作可平铺地表。建筑保持等比缩放，可通过 Prefab Variant 调色和调整尺寸。

## 具体修整

| 类别 | 资源 | 已处理内容 |
| --- | --- | --- |
| 荒漠山体 | GobiMountainPeak、GobiRidge、WindErodedCliff、YardangMesa、GobiRockCluster、RockOverhang | 裁切展示底座并封底，修整轮廓、平滑与多尺度位移；换用岩面摄影贴图。岩脊由修整后的山体重新组合，排除了原候选混入的栅格和人工门洞。 |
| 荒漠装饰 | DeadTamarisk、DesertThornBush、GobiWaystone、CaravanSupplies | 植物去土座、重做粗糙树皮；灌木重组枝条；石标削去凸起五金并改为岩石表面。行囊保留原有 UV 和三个已验证网格层级，调整皮革色调和粗糙度，小型扣件作为装饰保留。 |
| 殷商外围建筑 | ShangDwelling、ShangWorkshop、ShangGranary、ShangMarketStall、RammedEarthGatehouse | 移除绿色弯檐，重建厚茅草直坡屋顶、木脊与檐梁，统一夯土、木材和石基质感；作坊去掉高处固化烟柱区域；门楼贯通中央门洞。建筑外观服务于项目设定，不作为考古复原模型。 |
| 厚墙宫殿构件 | PyramidPortal、PyramidStonePanel | 石门贯通；墙饰面厚度从约 2.66 米削至 0.65 米，正面约 6 米宽。保留其厚重石构轮廓，可作为既有金字塔宫殿的装饰构件。 |
| 中世纪建筑 | MedievalTownhouse、GothicWindowWall、MedievalButtress | 民居重新分配抹灰、木框、石基和瓦面；石窗墙去顶端雕像并重建素面山墙压顶，保留窗洞；错误的神龛塔改建为分层退台石扶壁。 |

民居、作坊和粮仓用于封闭的建筑外观，没有制作完整室内、可开启门扇或室内交互。石墙饰面、哥特窗墙和扶壁也不宣称无缝覆盖任意建筑尺寸。岩下空间只带岩体碰撞；实际主线路径仍需结合所放置的地面复核。

## 细节层与材质

全库 LOD0 / LOD1 / LOD2 合计分别为 **1,025,844 / 266,692 / 61,330 个三角形**，每件资源都递减。LOD 根据屏幕占比切换，最远层关闭投影；静态非凸 MeshCollider 使用中档网格，大山体使用远档网格。碰撞独立于渲染 LOD，切换远近不会使门墙突然失去碰撞。大量复制植被或小物件时，可在不需要接触的实例上关闭碰撞。

修整版采用 Built-in 管线的 `Traveller/Environment Surface` 材质：岩面、树皮和建筑表面使用米制三向投射，茅草屋顶使用单独铺设的 UV。包含反照率、法线、粗糙度和遮蔽图；粗糙度与遮蔽打包为同一线性贴图的 R/G 通道，使三向投射从 12 次采样减至 9 次，UV 表面从 4 次减至 3 次。打包后通道数值逐像素核验，见 [打包检查](packed-surfaces.json)。行囊使用保留原图集的 Standard 材质。不要将这套材质直接用于尚未适配的 URP/HDRP 工程。

新增 [Rock Face](https://polyhaven.com/a/rock_face)、[Thatch Roof Angled](https://polyhaven.com/a/thatch_roof_angled)、[Pine Bark](https://polyhaven.com/a/pine_bark) 共 12 张 2K 摄影贴图，沿用 Poly Haven 的 CC0 授权；逐文件来源和 SHA-256 见 [surface-sources.json](surface-sources.json)。石砌、夯土、木板、瓦面和试走场地面复用项目已有的 Poly Haven 材质。混元生成网格继续单独记录平台来源，不继承贴图的 CC0 标识。

## 实际画面

下列棚拍预览使用最高细节层并将资源缩放到相近展示尺寸，因此不表达实际物件之间的比例。米制尺寸和面数见 [逐件目录](READY_CATALOG.csv)。

| 预览 | 从左到右 |
| --- | --- |
| [01](Previews/Ready_01.jpg) | 主峰、岩脊、断崖、雅丹孤丘 |
| [02](Previews/Ready_02.jpg) | 岩群、悬挑巨岩、枯死柽柳、干枯灌木 |
| [03](Previews/Ready_03.jpg) | 石标、行囊、民居、作坊 |
| [04](Previews/Ready_04.jpg) | 粮仓、摊棚、门楼、石门 |
| [05](Previews/Ready_05.jpg) | 墙饰面、中世纪民居、哥特石窗墙、扶壁 |

独立程序原始截图：[门楼](Native/RammedEarthGatehouse.png)、[民居](Native/ShangDwelling.png)、[山脊](Native/GobiRidge.png)、[石门](Native/PyramidPortal.png)、[石窗墙](Native/GothicWindowWall.png)、[行囊](Native/CaravanSupplies.png)。试走场地面平整，以便单独检查资源的轮廓、碰撞和表面；它没有进行剧情关卡的地形美术组合。

## 验证记录

<!-- READY_VERIFICATION_START -->
- **20/20 个预制体通过**保存后重新载入检查：三个 LOD 的网格、法线、UV、材质、递减面数与静态碰撞均完整；8 组摄影表面材质的打包贴图引用正常。见 [保存后引用检查](saved-prefab-audit.json)。
- **6 项运行碰撞检查通过**：两处门洞的胶囊通道、两侧实体墙，以及现有第一人称控制器实际穿越石门和门楼。
- **20/20 个运行视角通过**：目标正常渲染，镜头不在其他素材内部，中心视线无遮挡。每个视角预热 240 帧，采样 360 帧；本次 20 件资源均使用 LOD0，保留原始 1080p 截图。
- **本机性能采样通过**：Apple M5、1920×1080、4×MSAA、120 FPS 上限、60 米双级联阴影；最差 P95 帧时间为 **8.79 ms**，全部低于 16.67 ms。该结果只描述这次独立试走场，不能推导未改动的剧情场景性能。完整 CPU、GPU、呈现等待与批次数见 [运行报告](Native/qa.json)。
- **既有 EditMode 回归 36/36 通过**，0 失败、0 跳过，约 51.48 秒；任务 `071dd575ea27432b8bb2990a653000f2`。见 [完整结果](editmode.json)。
- **独立预览构建成功**，0 错误、0 警告。40 份原始源文件副本、12 张新增摄影原图的 SHA-256 一致，修整素材库 99 个文件均有 Unity 元数据；三个剧情场景哈希及 Build Settings 保持不变。见 [交付审计](delivery.json) 与 [构建报告](preview-build.json)。

早期采样暴露了无限帧率下的帧时间尖峰，以及大型山体遮挡检查镜头的问题。现已给试走场设置帧率上限、合并材质通道、补足预热，并检查取景遮挡；早期原始数据保存在 [性能调整记录](Performance/README.md)，不作为当前交付结果。
<!-- READY_VERIFICATION_END -->

## 可编辑来源与复现

`ArtSource/Environment/Prepared/2026-10-28/Output/<ID>/` 保存每件资源的 `.blend` 工作文件、三档米制网格和 `processing.json`。Blender 文件中的分类材质用于区分表面，最终摄影材质配置保存在 Unity；行囊的 Unity 网格特意采用原有图集版本，不采用 Blender 减面版本，避免 UV 裂缝。

处理步骤依次为：

1. 编辑器 `EnvironmentProduction.ExportWorkingMeshes()` 导出原候选的米制工作网格。
2. 运行 `Blender --background --python Tools/prepare_environment_meshes.py -- <ID...>` 修整选定资源；不传 ID 时处理全库。
3. 运行 `python3 Tools/fetch_environment_ready_surfaces.py` 获取并核验新增摄影材质。
4. 运行 `Blender --background --python Tools/pack_environment_surface_maps.py` 打包粗糙度/遮蔽通道并检查数值。随后使用 Unity 菜单 `Tools → Three Body Traveller → Assets → Import Repaired Environment` 导入修整网格、材质、LOD 和碰撞。
5. `EnvironmentProduction.CaptureReadyLibrary()` 更新全库预览；`Build Environment Art Yard` 重建独立试走场；`Build Standalone Environment Preview` 构建本机运行包。
6. 运行包使用参数 `-environment-qa <绝对路径/qa.json> -screen-width 1920 -screen-height 1080 -screen-fullscreen 0` 执行门洞与 20 个视角采样，截图写入报告所在目录。基准采样结束后自动退出。
7. `python3 Tools/reconcile_environment_ready.py` 核对原档、场景哈希、元数据、贴图来源、保存后引用及运行结果，生成交付审计和目录。

重建试走场前需要停止 Play 并保存当前场景；工具在完成后恢复之前打开的场景。修整工作不会覆写原始 FBX，也不会把试走场加入剧情游戏的 Build Settings。
