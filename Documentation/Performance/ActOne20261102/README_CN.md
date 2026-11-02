# 第一幕戈壁渲染优化 · 2026-11-02

当前可运行入口：`Builds/ActOneTraveller.app`。它与本目录 `Final` 验收所用的 `Builds/ActOnePerformance20261102/Traveller.app` 逐文件一致，核验记录见 `Unity/playable-copy-verification.json`。

## 实现

- 对 23 组不燃烧的大型静态岩石生成专用阴影网格：原近景阴影源合计约 117.4 万面，简化后约 7.4 万面，减少约 93.7%。它们不参与可见颜色绘制。
- 将这些阴影网格按 64 米空间网格与物体 Layer 合为 10 组，保留分区剔除和原来的 Layer。23 个单独阴影节点作为编辑源保留，Renderer 关闭，实际运行只提交合批后的阴影。
- 阴影使用专门的深度 Shader，不再需要可见表面材质。近景可见模型与摄影材质仍保留；掩体顶盖仍使用原 LOD0 可见网格，阴影使用较细的 LOD1。
- 单独调整 11 组远山、天际线与远处岩台的 LOD 切换；接近时仍可以回到高细节档。没有强制把全场景固定在 LOD1。
- 燃烧灌木、枯木、透明植被、角色和火把沿用各自的渲染与生命周期。燃烧植物不带独立阴影副本，以免成为灰烬后留下树影。
- 126 个碰撞体的形状、网格引用、启用状态、触发设置与完整变换矩阵核对一致，详见 `Unity/collision-verification.json`。原画质参数、第一幕状态控制器及共享岩石/植被 Shader 的哈希保持一致，见 `Unity/shared-source-verification.json`。

## 同条件实测

硬件 Apple M5，macOS 独立 Development 包，实际窗口 1600×900，VSync 关闭，90 帧上限；每个视角预热 240 帧、采样 360 帧。燃烧视角确实有 32 组活动火焰，所有视角的 GPU 时间均取得 360 个样本。采样时没有同时构建、导入或运行 Editor 测试。

| 视角 | P95 ms：优化前 / 后 | 绘制面数（万）：前 / 后 | 面数减少 | 绘制批次：前 / 后 |
| --- | --- | --- | --- | --- |
| 夜间开场 | 11.58 / 11.38 | 291.8 / 253.6 | 13.1% | 291 / 296 |
| 旅人附近 | 11.51 / 11.42 | 246.9 / 199.1 | 19.4% | 245 / 243 |
| 日出后接近掩体 | 11.48 / 11.46 | 289.3 / 247.5 | 14.4% | 262 / 265 |
| 掩体岩石近景 | 11.51 / 11.25 | 351.7 / 248.2 | 29.4% | 217 / 215 |
| 32 组灌木燃烧 | 11.43 / 11.54 | 235.1 / 160.4 | 31.8% | 182 / 180 |
| 夜间离开掩体 | 11.47 / 11.58 | 316.9 / 231.4 | 27.0% | 232 / 234 |

最终 6 个视角全部低于 P95 16.67 ms 的 60 FPS 预算。90 帧限速使帧时间接近限速等待下限，优化前后 P95 总体变化较小；本轮的主要收益是降低几何绘制负载，不能将面数下降幅度当成帧率提升幅度。批次在不同视角略有增减，GPU 与渲染线程时间也存在调度波动，完整数值保存在 `Before/Comparable/report.json`、`Final/Comparable/report.json` 和 `comparison.json`。

以上数据仅覆盖第一幕戈壁；宫廷的 20.90 ms 来自其他场景与测试条件，不能与这里的帧耗时直接计算改善比例。测试没有并行压满本地语音模型或调用外部 LLM，未把模型推理负载包含在渲染对照中。

## 功能与美术验收

- 最终 Editor 检查 112 / 112 通过，包含阴影不可见、所有子网格都进入合批、近景掩体网格和物理遮蔽保持原样等约束：`Unity/editor-tests-final.json`。
- 最终独立运行包 447 项功能检查通过：`Final/Mechanics/report.json`。真实控制器累计行走约 83.30 米，完成夜晚醒来、日晒十点伤害、进入掩体恢复、88 株受晒植物烧成灰烬、53 株阴影植物保留、日落后举火把行进以及切换到独立朝歌场景。
- 已复核最终夜景、近景岩棚、阴影、燃烧与灰烬截图；素材仍使用原近景摄影纹理，太阳与火把效果保持。截图在 `Final/Comparable/` 和 `Final/Mechanics/`。
- 最终构建 0 错误、0 警告：`Unity/native-build.json`。

## 维护与复测

编辑器菜单 `Tools > Three Body Traveller > Gobi > Optimize Rendering` 可以重建本轮优化。`GobiEnvironmentDressing.Arrange()` 也已接入同一入口。

**手动移动或旋转静态岩石后，应重新执行此菜单。** 空间合批网格保存了制作时的变换；各岩石下面关闭的 `Efficient rock shadow` 是编辑源，`Environment/Gobi shadow batches` 是生成结果，不应单独手工修改。

实现文件：

- `Assets/_Project/Editor/ActOnePerformanceProduction.cs`
- `Assets/_Project/Art/Shaders/GobiRockShadow.shader`
- `Assets/_Project/Art/ActOne/Meshes/GobiShadows/`
- `Assets/_Project/Art/ActOne/Materials/GobiRockShadow.mat`
- `Assets/_Project/Tests/Editor/GobiRenderBudgetTests.cs`
- `Assets/_Project/Scripts/Development/ActOneRenderBenchmark.cs`
- `Assets/_Project/Scripts/Development/GobiRenderRegressionCheck.cs`

所有诊断分支仅由开发包 CLI 参数启用，普通启动不会运行。基准的可复现入口：

```sh
python3 Tools/run_act_one_render_benchmark.py --output /absolute/path/to/a/new/folder
```

功能检查使用同一包的 `-gobi-render-regression /absolute/path/to/report.json` 参数。旧运行包保留在 `Builds/ActOneTraveller.before-gobi-performance.app`；本轮没有执行 Git 提交或推送。

## 调试证据与测量边界

`InitialOpening`、`Before/Views` 为早期探索。早期程序请求 1920×1080，但 macOS 将窗口约束为实际 1920×939；它们没有用于最终同比。`Before/Views` 的植物计时器还会从前一个视角延续，因此该次燃烧采样不作为压力测试证据。正式前后对照统一使用 1600×900，并明确记录活动火焰数。

`Probes` 保存单变量诊断：强制全部 LOD1 反而增加远处几何；关闭所有阴影会显著减少阴影面数和批次；去掉三向表面采样带来有限收益。正式方案保留可见材质与掩体阴影。`After/Comparable` 与 `NativeMechanics` 是合批之前的中间验收，最终交付以 `Final` 为准。

计时 API 采用 Unity [Frame Timing Manager](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/manager) 与 ProfilerRecorder；GPU 采样与 CPU 工作时间分开记录，不把限速等待算作脚本耗时。
