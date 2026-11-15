# 中文文字交谈修复与复测

复测日期：2026-11-15。环境：macOS、Unity 6000.3.27f2、`codex/chaoge-pendulum`。第一轮记录中的行号对应旧 `main`，开发分支指定的编辑器已经是 6000.3.27f2。

文字交谈使用深色输入区和浅色文字；Enter 用于打开对话，输入时由编辑器和输入法处理。玩家确认候选字后点击“发送”。拼音组合中的预览也可能出现在 Unity 的字段值里，因此组合期间禁用发送；按 Esc 取消候选不会关闭对话。仅在对话请求已受理后，才结束编辑并清空草稿。

用隔离工程打开朝歌场景并定位到纣王对话，测试实际 Game 视图中的 macOS 简体拼音和鼠标。此定位只用于复测文字交谈，未验证从戈壁开始的剧情。

| 测试项 | 复测结果 |
| --- | --- |
| 输入框可读性 | 深底浅字，已确认的中文和光标可见 |
| 拼音与回车 | 组合期间发送按钮禁用，按回车没有发出请求 |
| 拼音与 Esc | 取消候选后，已确认的“天窗”保留，对话保持开启 |
| 中文发送 | 输入“沙漏的作用”并点击发送，发出一次请求，纣王返回真实 DeepSeek 在线回复 |
| 发送后再次编辑 | 草稿清空，可重新输入、移动光标及删除，未发出额外请求 |

输入和发送期间没有新增 Console 错误，141 项 Unity EditMode 回归全部通过。原报告的 `TextEditingUtilities` 越界堆栈在本机没有重现；隔离工程启动时的 Quick Search 索引异常有不同堆栈。后续主线、麦克风、性能和多分辨率仍需独立验收。

输入框类名和输入法预览行为可对照 [Unity 6000.3.27f2 的 TextField](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3.27f2/Modules/UIElements/Core/Controls/InputField/TextField.cs) 与 [键盘输入处理](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3.27f2/Modules/UIElements/Core/Controls/InputField/KeyboardTextEditor.cs)。
