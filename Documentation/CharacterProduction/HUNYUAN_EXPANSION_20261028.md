# 2026-10-28 混元素材补齐

本轮通过用户 Chrome 中已登录的混元 3D Studio 生成并下载素材，使用当日恢复的 30 次免费额度，没有购买或订阅。实际文件、提示词、哈希、弃用记录见 [制作清单](hunyuan-expansion-2026-10-28.json)。

## 文件与用途

| 资源 | 类型 | 用途与边界 |
| --- | --- | --- |
| ShangGuard | 带骨架人物 | 独立商朝卫兵，皮革札甲、青铜盔；空手，盾矛仍需另外挂接 |
| ChurchGuard | 带骨架人物 | 独立教廷卫兵，锁甲、暗红罩衣与金属护具；空手 |
| CitizenMale | 带骨架人物 | 东方成年男性平民，交领粗麻短衣、布裤、绑腿 |
| CitizenFemale | 带骨架人物 | 东方成年女性平民，交领衣、简朴发髻与分片裙摆 |
| CalendarChest | 静态道具 | 木制历法箱、青铜包角、天象盘与算筹造型；未制作开盖或递交交互 |
| BronzeHourglass | 静态道具 | 木质、青铜和沙漏造型；玻璃仍是生成材质外观，真实透明与沙流动画需单独制作 |
| ScholarlyBook | 静态道具 | 合上的皮面书；未制作翻页、书写或手指接触 |
| PopeV3 | 未绑骨人物候选 | 无长披风、裤腿分离的教皇服装修订；有模型与贴图，尚不能作为已完成的动画人物 |

原始导出在 `ArtSource/HunyuanExpansion/2026-10-28/`；Unity 模型、提取的贴图、Standard PBR 材质与米制预制体在 `Assets/_Project/Art/HunyuanExpansion/`，预制体子目录为 `Prefabs/`。人物和道具分别按使用尺寸规范化，贴图导入上限为 2K。

这些是资源库交付；本轮没有将新模型自动覆盖到三个剧情场景。既有第一人称玩家、人物布置、剧情和动作库继续使用已有配置。新人物预制体带 Humanoid Animator，可在后续场景整合时替换身体并复用动作；这不代表本轮已经做了新角色的完整实机主线检查。

## 检查与复现

编辑器菜单 `Tools → Three Body Traveller → Prepare Downloaded Hunyuan Expansion` 从清单中实际存在的 FBX 提取贴图、配置材质和 Avatar、生成预制体，输出 [Unity 导入检查](hunyuan-expansion-unity-validation.json)。`HunyuanExpansionLibrary.CheckMotionsAndCapture()` 检查已绑骨人物的待机、重心变化、行走、发抖和疲惫，每种动作采样五个姿势，并输出 [动作检查](hunyuan-expansion-motion-validation.json) 和实际 Unity 预览。

动作检查覆盖骨骼运动、烘焙蒙皮、有限数值与整体尺寸，不保证衣料没有穿插。新的四套混元人物仍使用 23 骨架，没有独立手指或面部骨骼；精细持物、口型、行礼、押送等不能算作已完成。已有 99 个片段／版本继续保留，本轮没有为重复的动作模板消耗额度。

最终结果：八个资源的网格与三组 PBR 贴图引用检查通过；四个人物的 20 组动作组合、共 100 个姿势通过采样检查。商朝卫兵、中世纪卫兵、男性平民、女性平民的待机身高分别为 1.83、1.85、1.73、1.65 米。项目现有 [36 项 EditMode 回归检查](hunyuan-expansion-editmode-validation.json) 全部通过，[35 次源文件核对](hunyuan-expansion-files-validation.json) 无哈希差异，三个剧情场景文件与上一批交付完全相同。本轮没有重新构建独立运行包或测试性能。

实际 Unity 预览：[人物与未绑骨候选](Previews/Expansion_Characters_Unity.jpg)、[四个人物行走](Previews/Expansion_Walking_Unity.jpg)、[三件道具](Previews/Expansion_Props_Unity.jpg)。人物图从左至右为教皇候选、女性平民、男性平民、中世纪卫兵、商朝卫兵；教皇保持源姿势，没有参与动作检查。

## 质量复核与额度

- 一张教皇概念图仍有长披风且明显偏卡通，已弃用，保留源文件与原因；后续候选改用无披风造型。
- 初次女性绑定因提交发生在模型切换完成之前而错用了上一份卫兵。实际 Unity 外观检查发现此问题；错件与派生贴图已保存在 `Rejected/`，重新绑定时先等待并核实女性源模型。以清单中的最终导出及哈希为准。
- 替换女性 FBX 后清除了旧身体的 Avatar 配置，并刷新了重映射材质的金属／光滑度引用；复核修正了重复缩放的预览测量，最终待机与行走图均使用纠正后的模型和材质。
- 额度分配为九次概念图、八次几何、八次贴图、五次绑定，共三十次；其中包括一张弃用概念和一次重绑。最后一次优先用于纠正必需的女性角色，所以教皇修订版明确保留为未绑骨候选。

当前验证汇总、场景是否改动及后续限制见制作清单末尾；网页截图 [混元完成状态](Previews/Expansion_Hunyuan_Completed.png) 展示了纠正后的女性模型、骨架和零剩余额度。
