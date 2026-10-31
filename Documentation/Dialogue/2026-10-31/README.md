# F 键对话验收

运行环境：macOS、Unity 6000.3.27f2，独立开发包 1920 × 1080。交互和职责说明见 [统一 F 键提问](../../F_DIALOGUE.md)。

| 检查 | 结果与范围 |
| --- | --- |
| Unity 完整 EditMode | 59 / 59 通过；包括最初 10 项选人与问题锁定测试 |
| 最终目标回归 | 11 / 11 断言直接执行通过，新增剧情台词不能中止等待中的提问；最后一次 60 项框架调用因启动超时未执行，不计为通过 |
| 网关 | 21 / 21 通过；使用模拟模型，不消耗在线模型额度 |
| 独立包完整开场 | [57 项通过](Native/report.json)，真实本机 ASR 使用合成语音，帧时间 P95 14.45 ms |
| 最终独立包语音回调 | [21 项通过](NativeFinalAsr/report.json)，转写自动提交并得到回答 |
| 构建 | [成功、0 错误、1 警告](build.json) |

实际场景还验证了：开始看向纣王、转写前看向姬昌，仍由纣王接收；模拟 LLM 决定转交后，只有姬昌写入问答记忆并回答，纣王记忆没有写入此次问答。目标离场与取消旧转写时，不会自动找另一个人接收。HTTP 请求证据分别保存在 `stay-events.json`、`handoff-events.json`、`departed-events.json`、`cancellation-events.json`。

首次完整开场出现过一次回调计数未通过和冷场景性能未通过，原始结果保留在 `NativeBeforeCallbackCheck/report.json`；后续聚焦语音与完整开场复核均通过，未把首次失败计入通过数。最终 11 项目标断言与源码哈希保存在 `validation.json`。

构建警告原文为：You are building a player, but you have uncompiled code changes. Unity 随后的说明涉及编辑器导入或后处理代码的生效时序。独立包中的新语音回调已另行运行验证，警告未被隐藏。

没有录制硬件麦克风。真实 LLM 的指向理解、口音与现场噪声仍需单独验收；模拟模型只验证转交协议、名单校验、静默与角色隔离，不证明语言理解准确率。
