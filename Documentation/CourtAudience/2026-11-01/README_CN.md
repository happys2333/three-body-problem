# 御前执扇、阶下官员与表章递奏

已写入正式朝歌场景 `Assets/_Project/Scenes/Traveller/02_ChaogeCourt.unity`。四名侍从改为两名执扇侍女、两名袍服官员；原有持矛卫兵继续驻守。

## 人物与站位

| 人物 | 位置 | 行为 |
| --- | --- | --- |
| 左执扇侍女 | 御座左后方 | 双手执长柄大羽扇，轻缓摇动 |
| 右执扇侍女 | 御座右后方 | 同样执扇，摆动相位错开 |
| 殿前礼官 | 阶下右侧 | 长袍、宽袖、冠帽，持笏板，觐见时行礼 |
| 传奏官 | 阶下左侧 | 接取竹简表章、登阶呈送纣王、退回岗位 |

构图参考故宫博物院[《阎立本步辇图卷》](https://intl.dpm.org.cn/Ceramics/64087.html)中宫女执扇、袍服官员引见的关系。此为唐代图像的构图参考，服装与器物为本项目设计，不宣称是商代服饰考据复原。

![御座两侧执扇侍女与持矛亲卫](Native/Interior/06_Royal_Retinue.png)

[原生摇扇动作预览（约 4 秒，无音轨）](Previews/Royal_Fan_Motion.mp4)

## 递奏与剧情衔接

递奏接入现有的姬昌献历剧情：姬昌与传奏官走近，双方伸手接递竹简；传奏官沿台阶真实行走，将同一份表章送到纣王手中，随后按原路返回阶下。纣王接过后继续持简阅览。人物没有在递送路点间传送。

表章演出完成之前，历法验证计时、天窗开启、沙漏与占卜圆阵等待。完成后姬昌走入原来的占卜位置，才启动原有演出。玩家保持第一人称自由活动；可用现有 F 对话入口向侍从提问。与递奏参与者交谈时暂停其交接推进，关闭对话后继续；暂停菜单冻结人物与摇扇。

![在阶下接取表章](Native/Interior/Audience_01_ReceivingPetition.png)

![传奏官登阶呈送](Native/Interior/Audience_03_PresentedToKing.png)

## 素材与动作

复用已绑定骨骼的混元女性人物与现有男性人物，新增本地制作的蒙皮长袍、宽袖与冠帽。道具均有可编辑 Blender 源文件和三级 LOD，本轮未消耗混元生成次数。

| 道具 | LOD0 / LOD1 / LOD2 三角面数 | 制作 |
| --- | --- | --- |
| 长柄大羽扇 | 11052 / 5304 / 1988 | 实体羽片、羽轴、木柄、青铜箍 |
| 竹简表章 | 10092 / 4844 / 1816 | 十二枚竹片、绑绳、小铜扣与装饰刻痕 |
| 官员笏板 | 564 / 270 / 100 | 木板与端部铜饰 |

双手通过同一个握持 IK 系统跟随道具；原扫描骨骼没有手指关节，因此使用独立的手部握持变形，保留原始模型。扇子周期约 4.8 秒，左右侍女的相位不同。官服跟随躯干、双腿、双臂和头部蒙皮，不是固定在人物外面的静态外壳。

本轮没有新增在线 LLM 或真实麦克风测试。侍从继续使用现有旁人对话协议，传奏阶段与表章持有者加入对话上下文；舞台动作由游戏控制，旁人不能越权推进剧情。

## 验证与复现

构建及功能结果见 `native-build.json`、`editmode-tests.json`、`Native/*/report.json` 和 `delivery.json`。性能预算单独记录，不能用功能检查通过推断全游戏稳定 60 FPS。

最终 48 项 EditMode、21 项网关检查通过；原生内殿与人物 172 项、连续行走 36 项、复苏 39 项均通过。连续行走约 369.01 米，最终构建 0 错误、0 警告。场景没有丢失脚本、丢失材质或不支持的材质。戈壁、中世纪场景、复苏配置、对话配置和原卫兵预制体与配置已作哈希对照。

Apple M5、1600×900、120 FPS 上限，活动占卜视角预热 2 秒后采样 240 帧，平均 7.25 ms、P95 25.90 ms；P95 尚未通过 16.67 ms 预算。全岗位握持检查与截图放在正常性能采样之后，保留既有可见性裁剪。

编辑器停在已保存的朝歌场景，退出 Play 后，可依次执行菜单 `Tools / Three Body Traveller / Capital / Prepare Court Retinue` 和 `Apply Court Retinue`。修改脚本后应先等待编译完成，再执行资源制作；应用前会备份场景到 `Logs/court-retinue-backups/`。正常体验仍从戈壁场景的标题开始。

```sh
python3 Tools/run_palace_interior_checks.py --app Builds/CourtAudience20261101/Traveller.app --output-root Documentation/CourtAudience/2026-11-01/Native --case interior --check-retinue --capture-court
python3 Tools/run_palace_interior_checks.py --app Builds/CourtAudience20261101/Traveller.app --output-root Documentation/CourtAudience/2026-11-01/Native --case walk
python3 Tools/run_palace_interior_checks.py --app Builds/CourtAudience20261101/Traveller.app --output-root Documentation/CourtAudience/2026-11-01/Native --case revival
```

初次运行发现下方握持点超出侍女的手臂可达范围，已把扇柄抬高并移近身体。传奏官返回使用更精确的到达容差，恢复固定站岗碰撞；记录见 `Native/Attempts/`。

复苏验证的旧“街头平民”集合包含了宫内四名侍从，现已单独区分室外夜间举火把与室内保持执扇、持笏板；新增室内侍从检查，未为通过测试而更改运行时火把策略。演出与行走使用的构建、仅调整开发验证脚本后的复苏构建，以及刷新编译后的最终构建分别存档于 `native-build-*.json`，对应关系见 `delivery.json`。
