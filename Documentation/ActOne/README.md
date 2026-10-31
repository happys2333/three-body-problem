# 第一幕：夜色中的戈壁旅人

本次以 2026-10-30 的荒漠苏醒要求为准；分镜、触发条件和 AI 知识边界见 [开场设计](OPENING_DESIGN.md)。第一幕现在在抵达朝歌入口时结束；夜行中的一颗、两颗飞星可观察记录，不再要求按键记录才能离场。

## 体验入口

Unity 打开 `Assets/_Project/Scenes/Traveller/01_GobiJourney.unity`，Play 后点击“开始旅程”。独立开发包是 `Builds/ActOneTraveller.app`。

macOS 可双击 `Tools/PlayActOne.command`，同时启动本机语音服务和试玩包；也可在项目根目录运行 `python3 Tools/play_act_one.py`。该入口默认使用离线剧情，不启用付费模型请求。

WASD 行走，Shift 快走，按住鼠标右键环顾，F 看向人物交谈，T 切换玩家火把。可输入文字发送，或看向人物直接按住 F 录音，松开后中文转写并自动发送，无需先按 F。配置中可关闭自动发送，恢复松开 F 后自动发送。游戏不在后台持续录音。首次使用麦克风时由 macOS 显示授权提示，拒绝或无设备时仍能打字。

## 第一幕时序

| 阶段 | 触发与表现 |
| --- | --- |
| 荒漠苏醒与调查 | 约 3.5 秒睁眼起身，任务为“调查四周，看看发生了什么事”。绿色箭头先指调查区域，再指向旅人火光；自由漫游不计日出倒计时。 |
| 火光相遇 | 玩家进入附近范围，旅人呼喊“来这里！”；走到身边后说“快过来，现在是乱纪元，不要命吗？”。 |
| 岩外简谈 | 旅人只回答眼前处境，最多三个简短回答；AI 可提前选择日出。此时不介绍身份或世界真相。 |
| 巨日升起与避险 | 太阳约 3 秒从地平线急速上升，旅人带路；升起后用真实遮挡判断日晒伤害，阴影恢复隐藏生命值。 |
| 岩下交谈 | 双方进入掩体后介绍姬昌并询问玩家来意，可继续自由聊天。AI 根据谈话选择结束；固定十分钟等待已移除。 |
| 日晒灌木燃烧 | 仅日晒植物点燃，逐渐焦黑，约 32 秒后变成灰烬；阴影中的植物保留。 |
| 原路日落 | 通过有效 AI 意图选择，太阳约 2 秒沿原路线退回地平线，火把重新照明。 |
| 夜行到朝歌 | 任务改为“与姬昌一同前往朝歌”。至少实走 35 米、经过 12 秒、抵达终点且旅人在附近，自动转场到朝歌城外，第一幕结束。 |

玩家生命值为隐藏的 100 点，首次进入日晒立即受伤 10 点，持续日晒每秒再受伤 10 点；进入阴影或日落后恢复满值。没有血条或数值显示。受伤使用红色屏幕边缘、轻微视角冲击；日晒中的 NPC 也有受热颜色反馈。晒伤至零时短暂淡出并回到巨岩检查点，保留本轮记录。

主要配置位于 `Assets/_Project/Settings/ActOne/GobiStory.asset`，包括探索距离、相遇位置、AI 节奏检查、演出、伤害、燃烧、夜行条件与首场景渲染预算。默认 90 FPS 上限、两倍 MSAA、70 米双级联阴影、LOD bias 1、Terrain 像素误差 15；退出第一幕后恢复之前的帧率、抗锯齿、阴影与地形设置。

## 准备的资源

- `Assets/_Project/Art/ActOne/Prefabs/Torch.prefab`：木柄、浸油布头、螺旋绑绳、焦黑顶端，三级 LOD；动态火焰、火星与点光源。使用现有摄影材质派生，Blender 源文件为 `ArtSource/ActOne/Output/Torch.blend`。
- `Ash.prefab`：散落灰烬、焦炭，三级 LOD；源文件与三档中间网格保留在同一源目录。
- `VegetationFire.prefab`：燃烧与烟雾粒子，供池化复用；植被具有可复位的燃烧状态，本次另补三株开场枯木。
- `SunDamage.shader`：第一人称晒伤反馈；`TorchFlame.shader`：随时间变化的火焰轮廓。
- `Assets/_Project/Audio/ActOne/`：呼喊、警告、巨日避险、岩下、身份、沙漏与出发共七条 MOSS 神经语音配音，24 kHz 单声道，和实时回复使用同一周文王声线。现有戈壁风声继续使用。
- 三个场景中的 **36 个角色**都装配了火把能力和 Humanoid 左手 IK。第一人称火把放在视野左侧；普通角色按手骨位置持火把。夜间点亮，白天熄灭火焰；主要玩家使用像素光，其他角色使用较便宜的顶点光。
- **8 组角色提示词**覆盖周文王、纣王、教皇、学者、达芬奇、两类守卫与居民；每个可交互实例持有独立会话历史。
- 当前本机模型：Qwen3-ASR-0.6B MLX 8-bit、MOSS-TTS-Nano ONNX 及配套音频编解码器，总下载约 1.77 GB，保存在用户缓存而非 Git 仓库。旧 Vosk 轻量、完整模型保留为回退资源。当前版本与哈希核验见 [模型安装记录](LocalVoice/model-installation.json)。

新火把和灰烬采用可编辑建模生成，没有额外消耗混元额度。原始环境模型和下载归档保持不变；布景继续复用上一轮戈壁整合。

## 本机语音服务

### 2026-10-29 本地神经语音已接入

当前 Apple M5 / 16 GB Mac 默认使用 **MOSS-TTS-Nano-100M 的 ONNX CPU 版负责 NPC 发声，Qwen3-ASR-0.6B 的 MLX 8-bit 版负责玩家语音转文字**，已安装并完成合成语音回环、Unity 实播、打断和前台性能验证。详细结果见 [本轮汇总](LocalVoice/summary.json)；真实麦克风、现场噪声和主观自然度仍需实际体验。

MOSS 官方提供独立 ONNX CPU 推理、参考音频克隆和流式解码路径，适合作为减少 GPU 竞争的首选候选；同时需要配套的 `MOSS-Audio-Tokenizer-Nano-ONNX`。不把小模型直接视为音质胜出，也不默认它支持 CosyVoice 那样的自然语言情绪指令。[官方部署说明](https://github.com/OpenMOSS/MOSS-TTS-Nano#onnx-cpu-inference)

本项目使用固定到提交 `8b7bcc9` 的底层 `OrtCpuRuntime`，以 NumPy / SciPy 处理音频，未加载 PyTorch。高层参考脚本仍有 torch / torchaudio 导入，因此没有直接安装其全套推理包。`Tools/voice_profiles.json` 固定八类角色配置，当前复用 Weiguo、Zhiming、Junhao 三种内置中文男声，属于集成用临时选音，不是八名独立演员，也尚未完成男女居民的细分选音。

ASR 使用 `mlx-community/Qwen3-ASR-0.6B-8bit` 与 `mlx-audio 0.6.3`，是上游 Qwen3-ASR 的社区 MLX 转换版本。继续采用按住 F 说话、松开后识别并自动发送；已核对该版本的 `hotwords` 参数，把朝歌、姬昌、纣王、乱纪元等作为上下文词表，不强行替换识别结果。模型文件大小不等于实际运行内存。[MLX 实现说明](https://github.com/Blaizzy/mlx-audio/blob/main/mlx_audio/stt/models/qwen3_asr/README.md)、[模型卡](https://huggingface.co/mlx-community/Qwen3-ASR-0.6B-8bit)

关键剧情台词提前合成并缓存，自由回复实时合成；同一角色配置保持相同声线。MOSS 内部使用增量解码，但当前 Unity HTTP 接口仍接收完整 WAV 后播放，尚未实现网络音频流缓冲；模型内部的首段 PCM 时间不能当成玩家实际听到首音的延迟。CosyVoice 3 留作后续听感比较候选。具体模型 ID 与限制见 [选型记录](voice-model-selection.json)。

### 当前可用实现

当前电脑已安装依赖和识别模型。其他电脑可先执行：

```sh
python3 Tools/setup_voice_gateway.py
python3 Tools/setup_local_voice_models.py --activate
python3 Tools/play_act_one.py
```

启动本机服务：

```sh
~/.cache/three-body-voice/venv/bin/python Tools/dialogue_gateway.py
```

服务监听 `127.0.0.1:8769`。Unity 上传 16 kHz 单声道 WAV 到本机 Qwen 识别，原始麦克风录音不保存，也不发给 DeepSeek。转写成功后自动作为一句对话处理。MOSS 输出 24 kHz 单声道 NPC 语音，字幕始终保留，生成音频缓存在用户目录，不依赖云端语音 API。

ASR 与 TTS 分别在独立常驻进程运行，复用权重。MOSS 限制为一个 CPU 推理线程并关闭空闲自旋；ASR 单并发。识别处理上限 28 秒，客户端等待上限 32 秒；语音合成客户端上限 35 秒。玩家开始录音、关闭交谈或关键剧情插话时，停止旧声音并丢弃晚到结果。自动检查使用合成语音，不启动硬件录音。

回退为旧 Vosk + macOS 临时语音：运行 `python3 Tools/setup_local_voice_models.py --legacy` 后再运行 `python3 Tools/play_act_one.py`。恢复神经语音则用 `--activate`。启动器会核对本项目的 PID 与命令路径，再刷新自己的旧服务，避免关闭不相关进程；双向切换已做服务级检查。六条预生成剧情配音仍可离线播放。

旧 Vosk 完整模型仅在回退模式下通过 `TRAVELLER_ASR_MODEL` 选择，来源见 [轻量版](voice-source.json)、[完整版](voice-full-model-source.json)。目前仍为按键录音，尚未接入持续监听、自动 VAD 或扬声器回声消除。

## AI 角色与 DeepSeek 接入

提示词位于 `Assets/_Project/Dialogue/Prompts/`。`shared.txt` 约束角色身份、可见事实、知识边界、短句回答和输出格式；各角色文件包含目的、语气、分阶段信息和行为限制。玩家提前提出正确解释时，角色应回应论据，不为拖延剧情捏造错误规律。

游戏发送角色 ID、当前阶段、已经见到的天象、观察记录、玩家假说和当前实例的有限历史。服务端组装提示词；角色不会共享其他实例的私下聊天。模型返回台词、五种表演意图及 `storyIntent`。第一幕可选择日出、日落；Unity 校验当前阶段、双方是否安全、身份介绍和交流时长后执行。伤害、燃烧、移动和转场仍由实际游戏条件驱动，模型不能任意修改。开场不发送旧轮次观察簿。

没有启用在线服务时，界面明确显示“离线剧情”，采用预设回复，主线仍可完整运行。**目前未使用真实 DeepSeek 密钥验证在线生成**；接口适配测试使用模拟上游，不能当成真人自由对话或模型角色表现验收。

需要在线时，在服务端环境中设置 `DEEPSEEK_API_KEY`，可选设置 `DEEPSEEK_MODEL`，然后运行：

```sh
~/.cache/three-body-voice/venv/bin/python Tools/dialogue_gateway.py --online
```

密钥不写进 Unity 资源、场景或安装包。默认模型名按当前官方接口配置为 `deepseek-flash`，可通过环境变量替换；使用 Chat Completions、JSON 输出和关闭思考模式的短回复请求。接口依据 [DeepSeek 官方文档](https://api-docs.deepseek.com/api/create-chat-completion/)。超时或异常回退为明确的剧情回复，不把网络失败当成人物拒绝玩家。

## 制作与验证入口

1. `Blender --background --python Tools/prepare_act_one_props.py` 生成火把和灰烬的三档米制网格。
2. Unity 调用 `ThreeBodyProblem.Editor.ActOneProduction.Prepare()`，或菜单 `Tools → Three Body Traveller → Act One → Prepare Story Resources And Integrate`，导入材质、预制体、配置和角色装配。它保留当前三场景布局，不调用旧的全场景重建工具。
3. `ActOneProduction.BuildPlayer()` 构建专用 macOS 开发包。
4. 启动本机语音服务后，`python3 Tools/run_act_one_checks.py` 验证实际控制器、受伤、掩体、聊天、燃烧、夜行自动转场和六个视角性能。`--opening` 验证当前苏醒、指引、三次回答、AI 天象、避险、烧尽与自动朝歌转场；自动检查不使用硬件麦克风。
5. `python3 Tools/run_act_one_checks.py --soak` 等待当前叙事回退自然选择日落；`--journey` 验证后续完整主线。自动检查使用合成语音测试同一转写接口，不请求硬件麦克风权限或录下用户声音。
6. `python3 Tools/run_act_one_checks.py --characters` 验证三幕人物的实际手部 IK、火把、动作与坐姿表演。
7. `python3 Tools/test_dialogue_gateway.py` 验证八角色提示词、历史隔离、JSON 适配、剧情意图、剧透过滤、超时回退和多句语音分段；`python3 Tools/check_voice_gateway.py` 用合成中文连续三次验证本机转写和临时配音。
8. `~/.cache/three-body-voice/local-venv/bin/python Tools/benchmark_local_voice.py` 生成二十条试听及转写对照；`python3 Tools/render_story_voice.py` 重渲染当前剧情配音，保留原 `.meta` / GUID。
9. `python3 Tools/run_act_one_checks.py --voice` 检查实际播放、打断和活跃推理时帧率；`--perf-only` 是只做一次切场与单视角采样的诊断工具，不替代完整主线。性能采样保持游戏在前台，报告同时记录实际帧率上限、焦点与 VSync。

验证结果以 `editmode.json`、`Native/*/report.json` 和最终交付审计为准。早期运行发现并修复了材质初始化时机、第一人称火把出画、场景光源开销与时序检查问题，相关原始记录保存在 `Diagnostics/`。

## 2026-10-30 苏醒版本验收

当前版本通过 49 项 Unity 编辑器测试、16 项网关与语音分段测试、56 项独立包实走检查；包括真实 ASR 协程的转写后自动发送。结果、截图、实际渲染尺寸与验证限制见 [本轮验收](Opening20261030/README.md)。

## 历史验收：2026-10-29 的固定等待版本

以下报告保留原始运行结果，不代表当前苏醒与 AI 日落版本。新版本设计与最新验收见 [开场设计](OPENING_DESIGN.md)。

### 第一幕初版验收

以下保留神经语音升级前的验收数字，升级前快照在 `LocalVoice/baseline-*.json`；当前语音验收见下节。

- 保存重载后：三个场景共 36 个有效 Humanoid、36 个火把组件、33 个 NPC 身份，零缺失脚本；8 套人物提示词、6 条临时语音和 138 处可燃植被引用完整。火把三档为 4,212 / 1,262 / 314 三角形，灰烬为 500 / 200 / 60。见 [资源审计](saved-resource-audit.json)。
- EditMode **44/44**、最终原生第一幕 **49/49**、后续完整主线 **50/50**、人物与持物 **55/55** 通过。第一幕使用真实 CharacterController 连续走了约 **83.07 米**，经过受伤、岩下恢复、交谈、烧尽、夜行和自动朝歌转场。近距离手骨与握点误差检查覆盖全部 36 个角色，包含坐姿人物。
- 138 处植被中 **85 处暴晒燃烧并成为灰烬，53 处遮阴存活**。最后一次快速运行向计时器注入时间验证 600 秒边界；另一次完整等待实际经过 **600.52 秒**，日落约 **2.02 秒**，随后夜行自动抵达朝歌。
- 完整等待那次的合并报告仍为失败，保留了当时语音超时和帧预算失败。之后只改动识别服务、持物和渲染设置，并通过最终快速运行复测；没有将早期报告改写为通过，也没有再重复整个十分钟。见 [真实等待原始报告](Native/soak/report.json)、[升级前运行报告](LocalVoice/baseline-report.json)。
- Apple M5、1920×1080、2×MSAA、90 FPS 上限；六个第一幕视角各预热 180 帧、采样 360 帧，主循环 P95 为 **11.42–14.68 ms**，六个视角低于 16.67 ms。后台高负载时曾出现波动，此结果仅代表本机该次第一幕短时采样，不替代其他两幕或其他硬件验收。
- 网关六项适配单测通过；常驻识别进程连续三次处理约四秒的合成中文，耗时 **1.12 / 2.87 / 2.06 秒**。报告原样保存专名识别错误，不能据此宣称真实麦克风或高准确率已经验收。见 [升级前语音回环](LocalVoice/baseline-voice-gateway-check.json)。
- 最新 macOS 开发包构建成功，零错误、零警告。朝歌与中世纪的 3,316 / 323 个 Transform 与基线逐块哈希一致，只新增交谈、语音和持物组件；角色源模型、第一人称控制器及后两幕剧情配置保持原样。见 [最终审计](delivery.json)。

仍需真实 DeepSeek 密钥验证在线角色表现；真实麦克风、口音与现场噪声需要玩家实际体验。临时合成配音、火焰画风和细致手指接触可在后续美术音频制作中继续完善。独立包仍依赖随附本机服务，尚未把 Python、识别模型和配音引擎做成跨平台安装器。

## 本地神经语音升级验收

- [二十条回环](LocalVoice/benchmark.json)：318 个参考字符中 8 个字符编辑错误，CER 约 2.52%；中位识别耗时约 0.24 秒，中位合成耗时约 1.08 秒。数据来自模型合成语音，不能当成真实玩家识别率或主观自然度排名。试听 WAV 保存在同一目录。
- [Unity 语音运行](Native/voice/report.json)：18 项通过；从提交一句回复到实际播放约 2.84 秒，24 kHz 音频解码播放、即时打断与丢弃晚到回复正常。采样期间按真实顺序执行 5 次 ASR、5 次未缓存 TTS，前台帧时间 P95 为 11.54 ms。
- [完整第一幕](Native/functional/report.json)：49 项通过，约 83.09 米实走，烧尽与遮阴存活仍为 85 / 53。六视角实际帧率上限 90、VSync 0、焦点为前台，P95 为 11.21–11.46 ms。未再重复十分钟等待，升级不改剧情计时。
- [EditMode](LocalVoice/editmode.json) 44 项通过。六条预生成台词和实时周文王回复共用声线；[安装与回退](LocalVoice/launcher-fallback-check.json) 双向服务切换通过，依赖检查无冲突。
- 曾有一轮 P95 约 54 ms 的慢样本，原始数据保存在 [诊断目录](LocalVoice/Diagnostics/)。新增焦点、帧率上限和等待标记后，缩小切场检查与前台完整复测均通过；原慢样本没有焦点记录，因此不把后台限帧当作已证明的唯一原因。没有为排查降低游戏画质。
- 权重固定到模型仓库提交，14 个 LFS 权重 / 图文件核验上游 SHA-256；Python 依赖锁定在 `Tools/requirements-local-voice.lock.txt`，完整权重留在用户缓存。后台进程内存足迹快照约为 ASR 1.1 GiB、TTS 1.6 GiB，不能和 Metal 分配峰值直接相加，也不能替代其他场景与硬件验收。

运行 `python3 Tools/audit_local_voice.py` 可重新核对本机模型哈希、六条 WAV 与 GUID、语音和第一幕报告、依赖、网关及适配单测，并生成 [语音交付审计](LocalVoice/delivery.json)。

## 统一提问入口（2026-10-31）

当前源代码统一为按住 F 说话、松开自动提问；对话选项已移除。接收者按提问开始时的玩家视线锁定，LLM 可通过结构化字段静默转交给其他在场人物。离线回退不作语义转交；协议验证与真实模型、硬件录音的边界见 [统一 F 键提问](../F_DIALOGUE.md)。历史独立包与验收记录对应各自构建版本，重新构建后才会包含新流程。
