# 第一场景：戈壁旅人

> 此页保留上一批静态布景的历史验收。当前游戏已改为夜间相遇、岩下等待十分钟、夜行自动抵达朝歌，入口包为 `Builds/ActOneTraveller.app`；当前操作和验证见 [第一幕说明](../ActOne/README.md)。下文旧时序、旧报告及旧包不用于验收新流程。

2026-10-29，将修整后的荒漠素材正式布置进 `01_GobiJourney`。本轮改变第一幕的地形和静态布景，保留现有第一人称、周文王、沙漏、剧情与天象系统。

## 运行

Unity 打开 `Assets/_Project/Scenes/Traveller/01_GobiJourney.unity`，点击 Play，再点击标题页的开始按钮。独立运行包为 `Builds/GobiDressed.app`，包含原来的三个独立剧情场景。

WASD 行走，按住鼠标右键环顾，Shift 快走，F 交谈或查看方向，E 记录观察，J 打开观察簿。路线为：遇见旅人 → 巨岩避日与交谈 → 第一处飞星观测 → 第二处飞星观测 → 出发石标。到石标后仍需按 F 启程前往朝歌。

## 布置

共放置 **103 个实例、10 类修整素材**，均保留三级 LOD。主线附近的岩体和小物件保留独立碰撞；远处 11 处背景山体关闭碰撞和投影。

| 类型 | 数量 | 布景用途 |
| --- | ---: | --- |
| GobiMountainPeak / GobiRidge | 3 / 6 | 东西两侧峰群、远山轮廓和南侧回望 |
| WindErodedCliff / YardangMesa | 6 / 4 | 谷肩、层叠岩壁和驿路出口 |
| RockOverhang | 2 | 西侧岩体与避日岩盖 |
| GobiRockCluster | 16 | 坡脚碎岩、岩下过渡与观测站旁岩群 |
| DeadTamarisk / DesertThornBush | 12 / 48 | 道路两侧疏密变化的枯树和荒漠灌木 |
| GobiWaystone / CaravanSupplies | 4 / 2 | 入口、观测点、出发路标及旅人物件 |

北侧地形切出通向远方的山口，中央驿路保持连续。地表减少草地权重，并沿行进路线绘制磨损泥砾。新增 TerrainData 独立保存在 `Art/EnvironmentReady/GobiDressing/`，原始地形与素材库模型保持不变。

避日岩盖从现有悬挑巨岩裁切、封底后派生，保留三级细节和独立碰撞。岩下净空覆盖交谈位置，石桌旁留出绕行空间。原来的巨石、石柱式支撑和方块路标留在场景中停用，便于对照和回退。替换前备份路径见 [基线](baseline.json)。

## 实际画面

![戈壁开场](Native/dressing/Moment_0.png)

[第一人称开场与 HUD](Native/dressing/Opening.png) · [岩下交谈](Native/journey/conversation.png) · [巨日](Native/dressing/Moment_1.png) · [两颗飞星](Native/dressing/Moment_4.png) · [驿路出发](Native/dressing/Departure.png) · [布景俯视](Previews/Layout.jpg)

## 验证

- 保存后重新加载：103 个 LODGroup、材质、派生地形和第一人称引用完整，场景停止播放、无未保存改动。见 [场景引用检查](saved-scene-audit.json)。
- 现有 EditMode **36/36 通过**。见 [结果](editmode.json)。
- macOS 开发包 **48 项戈壁检查通过**：使用现有玩家的 `MoveInput` 和 CharacterController，从出生点连续行走 **168.86 米**；完成岩下交谈、两次飞星记录与离场提示。没有在主线步行段之间传送；完成路线后，另测东侧岩壁阻挡，并用六个预设视角采样。见 [戈壁运行报告](Native/dressing/report.json)。
- 原有完整主线 **51 项运行回归通过**，覆盖四种窗口尺寸、戈壁到朝歌的显式切场、后续剧情及结尾。该套检查使用传送到目标点，不替代上述实际步行。见 [主线报告](Native/journey/report.json)。
- Apple M5、1920×1080、120 FPS 测试上限、沿用游戏画质设置，六个戈壁视角各预热 120 帧、采样 360 帧，最差 P95 **8.61 ms**，均低于 16.67 ms。该结果适用于本轮戈壁场景，其他两幕的性能结论沿用原记录。
- 原生构建成功，**0 错误、0 警告**。朝歌、中世纪场景、三个剧情配置、UI 配置和 Build Settings 的哈希均与基线一致。见 [构建报告](build.json) 和 [范围审计](delivery.json)。

## 复现

1. 如需重新派生岩盖，运行 `Blender --background --python Tools/prepare_gobi_canopy.py`。源网格取自修整库，工作文件与三档中间网格保存在 `ArtSource/Environment/GobiDressing/`。
2. Unity 停止播放并保存当前场景，调用 `ThreeBodyProblem.Editor.GobiEnvironmentDressing.Arrange()`，或使用菜单 `Tools → Three Body Traveller → Gobi → Arrange Prepared Landscape`。该入口只布置第一幕，不调用重建三幕的旧工具；会重置自己管理的布景根节点。
3. `GobiEnvironmentDressing.CaptureViews()` 更新六个编辑器视角和俯视图，结束后重新打开已保存场景。
4. `GobiEnvironmentDressing.BuildPlayer()` 构建专用 macOS 开发包；执行 `python3 Tools/run_gobi_integration_checks.py` 顺序运行真实步行与主线回归。
5. 保存重新加载的场景引用检查及 EditMode 报告后，执行 `python3 Tools/audit_gobi_integration.py` 对照基线核实范围。此前素材库阶段的审计记录保留在原目录，不用它要求本次获准修改的第一幕保持旧哈希。
