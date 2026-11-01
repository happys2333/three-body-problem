# 纣王的侍从与持矛卫兵

已保存到正式场景 `Assets/_Project/Scenes/Traveller/02_ChaogeCourt.unity`。

## 布置

- 御座后侧两名持矛亲卫，台阶两侧两名卫兵，殿门两侧两名卫兵；殿内合计六人。
- 金字塔入口原有两名卫兵也换用竖直青铜矛并留岗。全朝歌另有两名城门卫兵，共十名卫兵。
- 新增御前内侍、奉礼侍从、献历侍从、值殿侍从四人，分布在御座两侧与献历、礼仪区。
- 侍从在觐见时行礼，随后留岗待命。看向人物按 F 可以进入现有旁人对话；显示称呼与别名独立保存。

![原生版本：御座、侍从与亲卫](Native/Interior/06_Royal_Retinue.png)

![原生版本：站立持矛近景](Native/Interior/07_Spear_Grip.png)

## 模型、动作与资源来源

本次复用已完成的真实蒙皮模型和贴图，没有新增混元生成请求。亲卫使用专用 `ShangGuardV2` 身体及已修整的青铜矛、盾；侍从使用已有男性衣饰变体与混元女性居民模型。男性衣饰调整保留皮肤颜色，所有人物沿用 Humanoid 动作控制器。

新预制体位于 `Assets/_Project/Characters/Court/Prefabs/`：

| 预制体 | 用途 | 来源 |
| --- | --- | --- |
| ShangSpearSentry | 竖直青铜矛、盾、手部握持、站岗 | 现有专用商朝卫兵、BronzeSpear、ShangLeatherShield |
| CourtSteward | 御前内侍 | CitizenMale_Indigo 的派生衣饰 |
| RitualAttendant | 奉礼侍从 | 已导入混元 CitizenFemale |
| CalendarAttendant | 献历侍从 | CitizenMale_Hemp 的派生衣饰 |
| ChamberAttendant | 值殿侍从 | 已导入混元 CitizenFemale |

原有四名宫殿及入口卫兵保留根对象，覆盖武器网格、岗位配置及位置。武器继续使用三级 LOD；身体沿用原有网格、动作与可见性裁剪。本轮没有把原身体档位数量描述成新增的三级角色 LOD。

右手 IK 配合已有手指握持变形；准备新武器时只重新校准握持目标，不再次写入同名手指变形，也不覆盖原卫兵网格。殿内卫兵不启用巡逻。夜间右手仍持矛、盾移到背后、左手可举火把；侍从默认空手，保留火把组件的调用能力。

侍从使用原有 `citizen` 旁人协议及语音配置，按人物显示名区分职分；旁人提示词补充宫中礼仪和职守，不代替纣王决策、不擅自推进剧情。此次没有新增独立声线或调用在线模型。

## 验证

36 项 EditMode 检查、21 项网关序列化/适配检查已通过。原生内殿与人物 163 项、真实连续行走 36 项也已通过，行走约 369.01 米；最后构建 0 错误、0 警告。原生人物、握持、夜间装备、占卜、字幕与真实控制器通路的最终结果，见 `Native/*/report.json`；构建记录见 `native-build.json`。性能采样与功能结果分别记录，短镜头测量不代表全城稳定帧率。

初次碰撞检查发现御座旁侍从的胶囊碰到旧石座边缘，现已外移并复验。原生全岗位握持检查需要在性能采样之后暂时唤醒画面外的骨骼动画，再恢复正常裁剪；过程记录见 `clearance-fix.json`、`pose-check-method.json` 和 `Native/Attempts/`。

## 编辑与复现

保持当前工程的朝歌场景已保存、退出 Play，依次使用 Unity 菜单 `Tools / Three Body Traveller / Capital / Prepare Court Retinue` 和 `Apply Court Retinue`。只重建本功能的派生预制体与独立场景分组，修改前备份至 `Logs/court-retinue-backups/`。

原生包：`Builds/CourtRetinue20261101/Traveller.app`。正常入口仍从戈壁标题开始。可复验：

```sh
python3 Tools/run_palace_interior_checks.py --app Builds/CourtRetinue20261101/Traveller.app --output-root Documentation/CourtRetinue/2026-11-01/Native --case interior --check-retinue
python3 Tools/run_palace_interior_checks.py --app Builds/CourtRetinue20261101/Traveller.app --output-root Documentation/CourtRetinue/2026-11-01/Native --case walk
```

原戈壁、中世纪场景、原卫兵预制体与配置、复苏及网关设置均有 SHA-256 对照，见 `preservation-audit.json`。

本机 Apple M5、1600×900、120 FPS 上限，活动占卜视角预热 2 秒后采样 240 帧，平均 6.46 ms、P95 24.49 ms；本次 P95 未通过 16.67 ms 预算，不能承诺全游戏稳定 60 FPS。正式性能采样使用正常动画裁剪；全岗位握持检查随后才暂时唤醒画面外骨骼。完整机器可读交付见 `delivery.json`。
