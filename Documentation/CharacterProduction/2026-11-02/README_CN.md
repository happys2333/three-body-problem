# 角色与关键道具补齐 · 2026-11-02

本轮生成、导出、加工及场景接入已完成，使用 **19 / 20 次提交**，保留 1 次额度。概念图、几何、贴图、绑定和重试全部计数。旧 Studio 起始显示 30 次，但本轮按用户给定的 20 次上限执行；没有购买或订阅。完整台账见 [generation-manifest.json](generation-manifest.json)。

## 已交付

| 角色 / 道具 | 完成内容 | 场景 |
| --- | --- | --- |
| PopeV3 | 为已有短袍教皇补齐绑定、23 骨骼 Humanoid、完整 PBR 材质，替换旧身体；保持原身份与坐姿剧情 | 03 中世纪 |
| ShangCourtLady | 一套专用侍女模型用于原有两名执扇岗位；重新校准双手与握持形变，修复长裙行走时的裂面和穿插 | 02 朝歌 |
| MedievalCleric | 新增教廷书记官，独立对话身份和记忆，站在台阶前的可行走区域 | 03 中世纪 |
| PapalThrone | 雕花木高背座椅，按教皇腿长修整座面以下比例，脚部落地；3 级 LOD 为 49,994 / 11,998 / 2,998 面 | 03 中世纪 |
| OpenManuscript | 打开的羊皮纸手稿；讲台上 1 件，书记官手持 1 件；3 级 LOD 为 50,000 / 12,000 / 3,000 面 | 03 中世纪 |

现有第一人称玩家、姬昌、纣王、学者、达芬奇、时代卫兵、99 条动作库、羽扇、表章和沙漏继续复用。原来未使用的精细中世纪讲台已放入第三幕，书本按实际斜面接触点放置。

## 角色系统

`NpcHeldBook` 支持阅读、行走降低持书位置、双手 IK、暂停冻结、禁用释放、重新启用恢复，以及交谈时朝向玩家。手持书本缩至约 36 厘米宽，按角色的实际臂长放置；持书时抑制火把。

书记官使用支持角色 AI 协议。对话请求包含自己的姓名、当前持书活动、可感知场景和自己的历史，沿用已有麦克风与文字输入；不会继承教皇的私人对话。此次没有向外部 LLM 发送测试请求。

人物身体替换保留根节点、身份、剧情引用和宫廷岗位；两名女性仍在原位摇扇，其他两名男性官员及传奏流程保留。换身体后重新制作手掌接触数据和握持形变，避免使用旧身体的手部网格。

`CharacterCompletionCloth` 从原始绑定 FBX 派生连续裙摆权重，保留手臂、鞋与露出的小腿，保持原拓扑、UV 和骨骼。原始 FBX 不改写。场景使用的裙摆修正网格与握扇网格分开保存。

## 验证结果

- 五件素材导入检查全部通过；三个人形均为有效 23 骨骼 Humanoid，角色身高分别为 1.84 / 1.65 / 1.76 米。
- 24 组动作、120 个姿态采样通过，涵盖待机、行走、跑步、坐姿、说话、托物、鞠躬、遮眼。另检查了单体和实际场景截图。
- [NativeFinal/report.json](NativeFinal/report.json)：39 项通过，涵盖握扇、持书、走路、暂停、选择对话、独立记忆、台阶穿插检查、教皇坐姿及材质。握持目标最大误差约 0.004 毫米；这衡量手骨目标，不代表逐指接触精度。
- [Regression/Interior/report.json](Regression/Interior/report.json)：172 项宫廷功能回归通过，包括表章呈递、卫兵握矛、侍从、暂停和四种分辨率。
- 本次中世纪采样均值 6.12ms、P95 11.82ms；宫廷采样均值 12.41ms、P95 20.90ms。**宫廷仍未达到 16.67ms 的 P95 目标，不宣称全地图稳定 60fps。**
- `Unity/editor-tests-final.json`：102 项 EditMode 回归通过，包含新增书记官后的实际人物数量与骨骼检查；`Unity/source-hashes.json` 校验 14 条归档记录的原始文件与项目文件。
- `Unity/native-build.json`：macOS 开发包构建成功，0 错误、0 警告。测试包位于 `Builds/CharacterCompletion20261102/Traveller.app`。

默认试玩包 `Builds/ActOneTraveller.app` 已更新；226 个包内文件与通过原生测试的构建逐一校验相同。旧包保存在 `Builds/ActOneTraveller.previous.app`，复制校验见 `Unity/playable-copy-verification.json`。Unity 编辑器停在已保存的 `01_GobiJourney`，按 Play 从戈壁开局进入。

`NativeFirst` 保留发现持书超出臂长时的失败报告；`NativeFinal` 为修正后的通过报告。`NativePopePilot` 是先完成教皇后的局部检查，不以它替代整体验证。

## 额度与导出记录

实际提交：教皇绑定 1 次、侍女 6 次、书吏 6 次、座椅 3 次、手稿 3 次，共 19 次。侍女宽袖和书吏长披风概念稿在几何制作前拒绝，修订稿及理由保留。第 11 次页面初始化复位输入，实际生成另一份书吏几何，已纠正并只留作 ArtSource 备用。

跨标签页导出曾下载到前台侍女绑定资产，造成一次手稿错误归档。在创建书本预制体前通过骨骼检查发现并纠正，改为单一前台页逐件选中、导出、核对哈希和模型类型。重复下载已清理，错误文件对应的原始数据保留在正确的侍女目录，无错误模型留在场景中。此过程没有新增生成请求。

原始模型位于 `ArtSource/CharacterCompletion/2026-11-02`；Unity 资源位于 `Assets/_Project/Art/CharacterCompletion20261102`。场景与预制体通过 Editor API 修改，场景备份在 `Logs/character-completion-backups`。

## 可重复加工

在正确项目的已运行 Unity Editor 中执行 `CharacterCompletionProduction.ImportAll()`。已通过检查且源文件未改变的资源会跳过贴图重加工；确需重导入时使用 `ImportAll(true)`。此入口会重新应用裙摆修正。

道具 LOD 使用本地 Blender 运行 `Tools/prepare_character_completion_lods.py -- PapalThrone OpenManuscript`（作为 Blender 的脚本参数），然后依次调用：

```csharp
ThreeBodyProblem.Editor.CharacterCompletionProduction.PreparePropLods();
ThreeBodyProblem.Editor.CharacterCompletionProduction.Apply();
ThreeBodyProblem.Editor.CharacterCompletionValidation.Run();
ThreeBodyProblem.Editor.CharacterCompletionValidation.QueueBuild();
```

原生检查使用 `Tools/run_character_completion_checks.py`。重新加工后应复核图片、手部接触、裙摆、坐姿和表章流程，而不只检查编译结果。生成角色没有逐指关节，握持使用形变补偿；手稿是静态模型，本轮没有制作翻页或完整布料物理。
