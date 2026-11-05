# Windows 试玩包

使用 Unity 6000.3.27f2 的 Windows Build Support (Mono)，目标为 Windows x64。构建包含三个正式游戏场景，入口是戈壁标题页，不启用 Development Build。

试玩者完整解压 ZIP 后双击 `ThreeBodyTraveller.exe`，无需安装 Unity 或 Python。在「设置 → AI 对话」填写自己的 API 地址、模型和 API Key，测试连接成功并保存后才能开始游戏。离线对白和沿用环境变量配置不再是可选游戏模式；模型失败不会通过离线意图继续推进。Key 只在本次运行中使用，不进入偏好设置或发行包。改变连接参数后需要重新测试。

包内的隔离 Python 3.13.17 服务由游戏自动启动，随机分配本机端口，正常退出或父进程结束后关闭。只监听 `127.0.0.1`，转发当前请求中的玩家配置，强制要求在线模型和非空 Key。Player Settings 允许正式版访问本机 HTTP，游戏配置仍把对话网关限制在回环地址。对话、角色决策、工具确认观察簿与上下文裁剪复用项目现有实现。

预录剧情声音保留。Windows 的动态发声与识别使用系统 Speech 组件；未安装中文识别组件时仍可按 Enter 进行 AI 文字对话。macOS 的 Qwen3-TTS 配置不受此 Windows 适配影响。

## 构建与打包

在 Unity 菜单选择 `Tools → Three Body Traveller → Build Windows Playtest`，或批处理调用 `ThreeBodyProblem.Editor.TravellerPlaytestBuild.BuildWindows`。环境变量 `TRAVELLER_WINDOWS_OUTPUT` 可以指定 EXE 的绝对输出路径。构建记录写入同目录的 `.build.json`。

```sh
python3 Tools/package_windows_playtest.py \
  --build-dir Builds/Windows/ThreeBodyTraveller \
  --output Builds/Releases/ThreeBodyTraveller-Windows-x64.zip
```

打包脚本校验官方嵌入式 Python ZIP 的固定 SHA-256，加入 AI 脚本、角色提示和中文试玩说明，生成逐文件校验清单和整个 ZIP 的校验文件。调试备份、Python 缓存和构建报告不进入 ZIP。游戏的本机偏好和 API Key 不参与打包。

本地运行 `python3 -m unittest discover -s Tools -p 'test_*.py'` 验证服务；建议使用项目现有 Python 3.12 环境。Unity EditMode 测试覆盖未填 Key、未测试配置、离线模式、连接改动失效和会话重新加载。GitHub 的 Windows 检查运行真实 HTTP 服务与 Windows PowerShell 语音适配检查。

来源：[Unity 6000.3.27f2](https://unity.com/releases/editor/whats-new/6000.3.27f2)、[Python Windows 发行包](https://www.python.org/downloads/windows/)。
