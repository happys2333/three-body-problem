# 火把火焰替换与第一人称修正

交付入口为 `Builds/ActOneTraveller.app`，与本目录 `NativeFinal` 通过验收的运行包逐文件一致。Unity 正常从 `01_GobiJourney` 开始游玩；本轮没有设置全局 Play 起始场景覆盖。

## 画面与持握

- 替换原来用正弦轮廓绘制的红黄条带火焰，采用 Unity 官方样例中的 60 帧火焰和 80 帧烟雾贴图。火焰每秒播放 30 帧，并混合相邻帧；每把火把有不同的起始相位。
- 主火焰网格的原点位于底端，底端固定在火把头内侧。原效果绕平面中心转动，第一人称实测火焰底部比燃烧端偏离约 6 厘米。
- 木柄跟随手腕，火焰在手腕放置完成后只绕世界 Y 轴朝向镜头；烟和少量余烬从燃烧端发射，速度按世界坐标向上。火把倾斜时不会沿着柄的局部 Z 轴喷出火星。
- 第一人称燃烧端保持在视野左下方，抬头、低头时一同移动。根据真实手臂长度调整木柄倾角，使手腕可达，同时保留燃烧端的位置；单独钳制手腕会再次把火把推出画面，已通过实机回归排除。
- 木柄的粗细缩放移至各 LOD 子物体，火把根节点使用均匀缩放，避免父物体的非均匀缩放把朝向镜头的火焰压斜。原火把、材质、脚本和 Shader 的 GUID 均保留。
- 共享火把预制体应用于玩家与 NPC；保留夜间携带、白天收起、手动点燃/熄灭以及原有 NPC 光源距离预算。照明改为较柔和的暖色和小幅噪声闪烁。

![替换前后](Before_After.jpg)

## 素材与来源

来自 [Unity Visual Effect Graph Samples](https://github.com/Unity-Technologies/VisualEffectGraph-Samples)，固定版本 `bcd800405c1b019654a0af3b4b8bd68fec590b4b`：

- `SmallFlame01_15x4.png`：火焰动画。
- `CandleSmoke01_20x4.png`：烟雾动画。

原始文件、版权说明、许可链接、下载地址及 SHA-256 保存在 `ArtSource/TorchVfx/2026-11-02/UnityVfxSamples/`。两个导入贴图与归档源文件逐字节一致，记录见 `Unity/source-verification.json`。这些样例采用 [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license)，版权说明也随运行包保存在 `StreamingAssets/ThirdPartyNotices/UnityVfxSamples.txt`。

本项目使用 Built-in 渲染管线，因此只导入这两张贴图并编写匹配管线的 Shader，没有引入样例中的 HDRP/VFX Graph 依赖。

## 可编辑资产和复用

- 共享预制体：`Assets/_Project/Art/ActOne/Prefabs/Torch.prefab`。
- 贴图：`Assets/ThirdParty/UnityVfxSamples/Textures/`。
- 主火焰材质及烟雾材质：`Assets/_Project/Art/ActOne/Materials/TorchFlame.mat`、`TorchSmoke.mat`。
- Shader：`Assets/_Project/Art/Shaders/TorchFlame.shader`、`TorchSmoke.shader`。
- 朝向与持握：`Assets/_Project/Scripts/TorchFlameBillboard.cs`、`TorchBearer.cs`。
- 编辑器更新入口：`Tools > Three Body > Update Torch Fire VFX`。`ActOneProduction` 的火把构建也调用同一个配置入口，重新制作第一幕不会退回旧火焰。

每把火把使用一个主火焰面和两组少量粒子；烟和余烬的粒子上限合计 22，材质及贴图共享，运行时不逐帧创建材质或查找 Renderer。

## 验证记录

| 范围 | 结果 | 证据 |
| --- | --- | --- |
| 原缺陷回归 | 旧预制体 3 个持握角度全部复现失败；修正后 7 项火把专用测试通过 | `Before/regression.json`、`Unity/torch-tests.json` |
| 最终项目 EditMode 检查 | 109 / 109 通过 | `Unity/editor-tests-final.json` |
| 最终独立 macOS 包 | 57 项功能检查通过，无运行错误 | `NativeFinal/report.json` |
| 第一人称角度与行走 | 正视、抬头 35°/80°、低头 45°/80°、转身、真实控制器行走约 4.05 米均通过 | `NativeFinal/FirstPerson_*.png` |
| 日夜与开关 | 夜间点燃、白天自动收起、手动点燃/熄灭、熄灭后粒子清空通过 | `NativeFinal/report.json` |
| 构建 | macOS Development，0 错误、0 警告 | `Unity/native-build.json` |
| 默认运行包 | 与已验收包逐文件 SHA-256 一致 | `Unity/playable-copy-verification.json` |

所有已采样姿态中火焰世界向上点积为 1；火焰底端位于燃烧端内侧约 7.5 毫米，手部 IK 的最大位置误差低于 0.004 毫米。正视与上下极限角度的燃烧端都保持在视口约 `(0.242, 0.243)`。

性能采样条件为 Apple M5、1600×900、VSync 关闭、60 帧限速、预热后 180 帧。平均帧时间 16.85 ms，P95 为 17.58 ms，包含限速等待；该采样没有达到 P95 16.67 ms 的严格 60 FPS 门槛，也不代表宫殿场景的性能结果。

`Native` 与 `NativePoseReview` 保留了中间验收结果：前者暴露旧持握位置及一条过严的远处 NPC 灯光断言，后者复现了手腕单独限位导致抬头时火把离开视野。最终交付以 `NativeFinal` 为准。Mac 锁屏时 Editor 游戏视图未正常推进，该次没有作为通过证据；最终截图和检查均来自实际独立运行包。

复测命令：

```sh
python3 Tools/run_torch_vfx_checks.py --output /absolute/path/to/a/new/report-folder
```

默认运行包的更新前版本保留为 `Builds/ActOneTraveller.before-torch-vfx.app`。本轮没有执行 Git 提交或推送。
