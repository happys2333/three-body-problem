# 混元道具源素材（2026-11-03）

本批通过腾讯混元 3D V3.1 网页生成，2026-11-06 补齐 Git LFS 归档。四份候选原模共 232,665,776 字节，保留在 `ArtSource` 中，尚未导入 Unity 或加入游戏构建。

| 素材 | 原件 | 当前状态 |
| --- | --- | --- |
| 青铜沙漏 | [FBX](BronzeHourglassCandidate/BronzeHourglassCandidate_PBR.fbx) | 候选；需分离沙漏芯、制作玻璃与流沙 |
| 历法竹简 | [FBX](BambooCalendarSlips/BambooCalendarSlips_PBR.fbx) | 候选；约 150 万三角面，需先减面 |
| 粗陶储粮罐 | [FBX](ShangEarthenwareJar/ShangEarthenwareJar_PBR.fbx) | 候选；需检查罐口、材质并制作 LOD |
| 木架芦苇担架 | [FBX](RevivalReedStretcher/RevivalReedStretcher_PBR.fbx) | 候选；需核对人物尺寸与接触位置 |
| 青铜摆锤 | [既有原件](../../Pendulum/2026-11-03/BronzePendulumBob_PBR.fbx) | 已通过[既有制作流程](../../../Tools/prepare_pendulum_model.py)接入；复用原件，避免重复存储 |

[清单](manifest.json)保留下载时的网格、UV、内嵌纹理与 SHA-256 检查结果；其中 `path` 相对于清单所在目录。摆锤路径指向现有归档，四份候选的后续 Unity 导入、减面、碰撞和场景效果仍待验证。
