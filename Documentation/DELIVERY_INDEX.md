# 分类交付索引

整理日期：2026-11-06。开发分支：`codex/chaoge-pendulum`。

## 文件分类

| 分类 | 入口 | 内容与状态 |
| --- | --- | --- |
| 游戏与 AI 修复 | [游戏脚本](../Assets/_Project/Scripts/)、[模型连接](../Tools/llm_connection.py)、[对话服务](../Tools/dialogue_gateway.py) | Windows 中文编码、模型短回复、故障冷却重试、退场火把清理、三日观察指引 |
| 游戏模型素材 | [中世纪素材库](../Assets/_Project/Art/Medieval20261106/)、[制作与预览](MedievalProduction/2026-11-06/README.md) | 已接入的门廊、束柱、石砌民居；预制体、三级 LOD、PBR 材质与纹理 |
| 场景与导航 | [中世纪场景](../Assets/_Project/Scenes/Traveller/03_MedievalTribunal.unity)、[导航数据](../Assets/_Project/Dialogue/Decisions/03_MedievalTribunal_Navigation.asset) | 1 处门廊、10 根柱、4 栋民居；中央通道、卫兵出门与三日视线检查通过 |
| 制作工作流与工具 | [Comfy 工作流与减面处理](../Tools/MedievalProduction/)、[素材导入](../Assets/_Project/Editor/MedievalAssetProduction.cs)、[场景放置](../Assets/_Project/Editor/MedievalSceneProduction.cs) | 保留三份采用的混元工作流、处理脚本和 Unity 编辑器入口 |
| 原件与待加工素材 | [中世纪采用原件](../ArtSource/Environment/Hunyuan/2026-11-06/)、[道具候选归档](../ArtSource/HunyuanExpansion/2026-11-03/README.md) | 采用原件可复现；四份旧道具候选仅归档，尚未进入游戏；青铜摆锤复用既有原件 |
| 自动化测试与验收 | [Windows 回归](WindowsStoryQA/README.md)、[测试工作流](../.github/workflows/windows-story-qa.yml)、[制作清单](MedievalProduction/2026-11-06/production.json) | 原生 Windows 剧情记录、Unity 回归、构建体积与清理记录 |

模型原件与纹理通过 Git LFS 保存，首次克隆后需执行 `git lfs pull`。`ArtSource/` 位于 Unity 的 `Assets/` 之外，新增候选归档不会进入游戏构建。

## 按类别查阅提交

| 类别 | 主要提交 |
| --- | --- |
| 游戏与 AI | [Windows UTF-8 修复](https://github.com/happys2333/three-body-problem/commit/21bc897)、[叙事重试](https://github.com/happys2333/three-body-problem/commit/9c1442d)、[火把与故障状态](https://github.com/happys2333/three-body-problem/commit/3cdf750)、[三日观察指引](https://github.com/happys2333/three-body-problem/commit/11e7d58)、[DeepSeek 短回复](https://github.com/happys2333/three-body-problem/commit/f6e714b) |
| 中世纪模型、场景与制作工具 | [模型生成与场景接入](https://github.com/happys2333/three-body-problem/commit/1bf107a)、[导出格式与体积统计](https://github.com/happys2333/three-body-problem/commit/1ce6cd3) |
| 待加工道具原件 | [四份混元候选归档与摆锤去重](https://github.com/happys2333/three-body-problem/commit/6b4a068) |
| Windows 测试 | [真实 AI 全剧情检查](https://github.com/happys2333/three-body-problem/commit/a51b8e9)、[最终回归配置](https://github.com/happys2333/three-body-problem/commit/cf6264d)、[通过结果](https://github.com/happys2333/three-body-problem/commit/2d27ebf) |
| 文档与清理 | [中世纪验收和临时产物清理](https://github.com/happys2333/three-body-problem/commit/25dbbaa) |

## 验证版本与试玩包

| 对象 | 验证结果 | 记录 |
| --- | --- | --- |
| Windows R2 试玩包，源码 `f6e714b` | 83 项剧情检查、7 次真实模型回复，最终进入 `Summary`；试玩者必须配置 AI | [Windows 通过记录及 ZIP 校验值](WindowsStoryQA/verified-20261106.json) |
| 后续中世纪模型更新 | 141 项 Unity 编辑器回归通过；正式 Windows 构建成功，体积增加 14,308,886 字节（1.529%） | [场景检查](MedievalProduction/2026-11-06/scene-checks.json)、[构建检查](MedievalProduction/2026-11-06/resource-build.json) |
| 四份道具候选归档 | 源文件哈希复核通过，保留网格、UV 与内嵌纹理检查结果；Unity 导入与场景效果待验证 | [候选清单](../ArtSource/HunyuanExpansion/2026-11-03/manifest.json) |

`ThreeBodyTraveller-Windows-x64-20261105-r2.zip` 尚未包含后续中世纪模型。新版模型完成的是构建与场景检查，尚未重新执行完整的 Windows 真实 AI 剧情回归。构建与打包方法见 [Windows 试玩说明](WindowsPlaytest.md)。
