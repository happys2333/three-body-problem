# 麦克风开关与输入设备选择

标题页或暂停菜单 → **设置 · AI 与操作** → **操作与声音**。

- **使用麦克风与人物交谈**：开启后按住 F 录音，松开后转写并自动向本次锁定的 NPC 提问。关闭后 F 打开文字交谈，Enter 也始终可用。关闭状态不会因按 F 而枚举设备、申请授权或启动录音。
- **输入设备**：可以使用系统默认麦克风，或指定已连接的内置、USB、耳机等输入设备。指定设备按名称保存，设备顺序改变不会改选其他麦克风。
- **刷新麦克风列表**：插拔设备后刷新。原先指定的设备断开时保留该选择并标为“不可用”，提问会提示重新选择或打字，不自动改用另一支麦克风。
- **播放 NPC 语音回复**仍是独立选项；关闭玩家录音不影响 NPC 发声。

![运行包中的麦克风设置](Native/Microphone_Settings_1600x900.png)

麦克风选择随“保存并返回”应用并保存在本机，跨场景和重启后可继续使用；取消或 Esc 不应用草稿。旧版设置默认延续原来的 F 键录音行为，使用系统默认设备；首次实际使用时按操作系统提示授权，拒绝授权仍可文字交谈。

打开设置、暂停或离开应用时，录音会结束在原先启动的设备上；等待授权或转写时取消，不允许旧结果在后来的一次提问中提交。设置界面不提供常驻后台监听。

## 实现

- `TravellerPreferences` 保存麦克风开关和设备名；显式版本字段区分旧设置与用户主动关闭。API Key 仍不落盘，修改麦克风不会改写模型、上下文等 AI 选项。
- `TravellerSettingsPanel` 提供开关、设备列表与刷新，设备暂时不在线时仍保留用户选择。
- `PlayerMicrophoneInput` 在权限与录音操作前检查开关，按选择启动、结束同一设备；权限、设备不可用和转写失败均保留文字入口。
- `MicrophoneCaptureSource` 封装 Unity 的设备/权限接口，原生验收替换成模拟设备，避免录制真实用户音频；正常游戏仍使用 Unity 麦克风实现。

没有修改语音模型、NPC 角色设定、三幕场景或美术资源。此前的[角色记忆与 1M 上下文](../../Dialogue/2026-11-02/README_CN.md)继续有效，当前独立包为 `Builds/ActOneTraveller.app`。

## 验证

71 项 EditMode 检查通过，覆盖设置兼容、保存、设备排序、断开时不改选，以及已有角色记忆、场景与剧情规则；见 [editmode-tests.json](editmode-tests.json)。

73 项原生设置与操作检查通过，包含四种窗口、保存/取消、1M 与 Key 设置保留、麦克风关闭时不调用录音和权限、指定/默认设备路由、等待授权时取消、权限拒绝、设备断开、无设备、转写取消及录音→本地 HTTP→NPC 自动发送。模拟设备启动共 4 次，无硬件录音、无真实付费请求；见 [Native/report.json](Native/report.json)与[请求证据](Native/upstream-evidence.json)。设置截图另覆盖 960×540 小窗口。

第一幕完整回归 57 项通过，包含真实控制器约 103.39 米行走、合成语音自动发送、日出晒伤、阴影恢复、植被燃烧、日落与朝歌转场。最终烈日视角 P95 为 11.37 ms，采样 360 帧均在前台，90 FPS 上限；见 [Regression/GobiOpening/report.json](Regression/GobiOpening/report.json)与[交付核对](delivery.json)。独立 macOS 开发包构建 0 错误、0 警告，见 [native-build.json](native-build.json)。真实设备音质、音量、环境噪声与系统权限弹窗仍需在玩家机器上实际使用验证，模拟录音检查不替代这些测试。

初次原生验收因窗口未处于前台，鼠标捕获相关检查失败；诊断记录保留在 `Attempts/`。验收辅助程序现先等待实际窗口焦点，再执行操作检查，最终原生结果为通过。

第一次第一幕帧采样 P95 为 55.89 ms，未达性能门槛，记录保留在 `Attempts/GobiFrameBudget/`。随后夜景独立性能探针 P95 为 11.51 ms、GPU 平均约 8.32 ms，同一运行时代码的完整第一幕复测也通过；未复现持续回归，未将首次波动归因于已证实的根因。新验收记录了采样焦点与帧率上限。上述数据只代表本机这几次戈壁视角，不替代全城性能验收。

Unity API 已在当前编辑器反射核验；参考 [Microphone.Start](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/microphone/start)、[Microphone.devices](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/microphone/devices)和[授权接口](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/application/requestuserauthorization)。具体系统的权限处理沿用 Unity 平台实现。

复验（从仓库根目录运行）：

```sh
~/.cache/three-body-voice/venv/bin/python Tools/run_settings_checks.py --output Documentation/Settings/2026-11-02/Native
python3 Tools/run_palace_interior_checks.py --app Builds/ActOneTraveller.app --output-root Documentation/Settings/2026-11-02/Regression --case gobi
```
