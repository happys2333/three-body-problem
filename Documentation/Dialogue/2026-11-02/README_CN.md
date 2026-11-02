# NPC 角色扮演、亲历记忆与 1M 上下文

当前已更新运行时代码、提示词、本地对话服务和 `Builds/ActOneTraveller.app`。原本已有姬昌、纣王、教皇、学者和达芬奇的独立角色设定与阶段规则，但记忆只在当前场景内保留 12 条消息。本轮补上跨场景的个人记忆、现场知识边界和可配置的长上下文。

同日后续已接通卫兵决定、语义推进与在线失败处理，见 [NPC 决策实现及最终验收](../DecisionRuntime20261102/README_CN.md)。下文保留记忆功能初次交付时的测试数字与边界。

## 人物如何交流和记忆

- 公共提示词要求先回应玩家本句话的意思和情绪，一次最多追问一件事；记得已回答的名字、来历与同行意愿，不反复重报身份、盘问或宣讲剧情。不清楚指代时简短澄清，区别玩家自述与确认事实，新证据出现后允许修正旧判断。
- 主要人物按角色 ID 保存本轮交流，姬昌从戈壁到朝歌继续记得同一玩家。纣王、教皇等分别保存自己的交流，配角进一步按场景和人物实例隔离。
- 每个人会记住自己的剧情演出台词；16 米内、无遮挡的其他人物可以记住在场听到的演出台词。自由 AI 问答只加入实际回答者的私有历史，不自动分享给其他 NPC。
- 当前现场信息包含人物身份、职责与动作、玩家是否可见、可见的晒伤反应、阴影状态、看不看得见天空，以及亲眼可见的宫廷礼仪阶段。NPC 不接收玩家隐形血量、私人观察簿、猜想或旧旅程记录。
- 场景、阶段与允许的剧情意图依然由游戏提供；模型不能自行增加权限。开场不确认姬昌身份与世界真相，日出日落仍经过现有条件校验。纣王命令卫兵已在同日后续实现，按警告、抗命与实际导航条件执行。

交流记忆只存在当前游戏进程；开始新旅程或退出会清空，没有写入存档。单人保留上限为约 8 MiB／20,000 条消息，超出时删除最旧的完整问答；另保留最多 128 条演出亲历。请求历史快照在主线程取得，大段 JSON 序列化放到后台任务中。

## 如何开启 1M

标题或暂停菜单 → **设置 · AI 与操作** → **AI 对话** → **上下文长度**，选择 32K、128K 或 1M，保存即可。旧配置默认 32K。模型、接口与 Key 仍在同页填写；Key 保持遮挡且只存在本次进程内，不随设置落盘。

![原生运行包中的 1M 设置](Native/01_OneMillion_Context_Setting.png)

输入窗口表示角色提示词、相关历史、现场事实与本轮问题的合计预算，不会填充无关文本。网关保留最新的完整问答，预留 320 token 的回复空间及消息开销余量；单次回答仍简短。`max_tokens` 是输出限制，保持 320，不设置成一百万；不会向兼容服务发送虚构的窗口参数。

DeepSeek 当前公开的模型窗口为 1M，见[官方模型说明](https://api-docs.deepseek.com/quick_start/pricing/)；输入和输出共享窗口，见[Chat Completions 接口](https://api-docs.deepseek.com/api/create-chat-completion/)。选择的实际模型仍须支持对应长度；1M 不会将 128K 模型升级成 1M。较长输入可能增加等待与服务商用量，可将回复等待时间从默认 20 秒适当调高，范围为 5–600 秒。

DeepSeek 使用[官方 token 文档](https://api-docs.deepseek.com/quick_start/token_usage/)提供的 V4 分词器，仅加载数据文件，不执行压缩包内代码。消息内容按官方词表计数，消息封装另留保守余量；这不是实际 API 计费回执。其他服务无法确定词表时采用保守的 UTF-8 字节预算，会比其实际 token 容量少保留一些内容。角色设定在稳定前缀，最新事实与问题在末尾，便于续接对话；[DeepSeek 前缀缓存](https://api-docs.deepseek.com/guides/kv_cache)是否命中由服务端决定。

本机已安装 `tokenizers==0.23.3`，官方分词器保存在用户缓存中，来源与 SHA256 见 [tokenizer-provenance.json](tokenizer-provenance.json)。其他机器可使用已有的语音安装器，或只补装上下文依赖：

```sh
~/.cache/three-body-voice/venv/bin/python -m pip install -r Tools/requirements-dialogue-context.txt
~/.cache/three-body-voice/venv/bin/python Tools/setup_context_tokenizer.py
```

启动器要求本地服务 `connectionSettings >= 2`，会按现有逻辑更新本项目拥有的旧服务。默认网关仍是离线剧情模式；选择 DeepSeek 或兼容接口并填写可用配置后才会请求上游。

## 验证结果

| 验证 | 结果 |
| --- | --- |
| EditMode | 64 项通过，覆盖稳定角色键、记忆隔离、快照、清空、设置与既有场景规则 |
| Python | 39 项通过，包括官方词表下超出 1M 的记录裁剪、超过 2 MB 的真实本地 HTTP 传输、三档预算、完整问答、权限与连接错误处理 |
| 原生角色上下文 | 23 项通过；戈壁发出 48 条历史消息，朝歌仍有 50 条并保留最早的自我介绍；纣王请求无该私聊和观察簿内容 |
| 原生设置与操作 | 47 项通过，覆盖四种窗口、1M 保存、Key 不落盘、接口转接、菜单、鼠标和跨场景设置 |
| 第一幕回归 | 57 项通过；合成语音转写后自动发送、三次初谈、晒伤与恢复、植被成灰、日出日落、约 103.38 米真实控制器行走及朝歌转场 |
| macOS 开发包 | 构建成功，0 错误、0 警告 |

记录见 [editmode-tests.json](editmode-tests.json)、[python-tests.json](python-tests.json)、[原生角色请求证据](Native/upstream-evidence.json)、[原生设置结果](SettingsNative/report.json)、[第一幕结果](Regression/GobiOpening/report.json)和[交付核对](delivery.json)。三幕场景、美术与原有对话配置没有被本轮覆盖，见 [preservation-audit.json](preservation-audit.json)。

原生请求接的是本机确定性模拟模型，没有使用真实 Key 或付费 API，也没有把完整 1M 输入发送到云端。测试证明记忆、预算、实际 HTTP 路径与知识隔离有效；它不证明真实模型必然自然接话或永不忘事。第一幕语音使用合成音频回归，没有录制硬件麦克风；本次帧耗时仅代表该次戈壁视角，不替代全城性能验收。

## 真实模型试听场景

填入实际模型后，建议按下列顺序试听；本轮未把这些主观角色表现记为已通过。

1. 在阴影中告诉姬昌名字与来历，数轮后用“那你还记得我吗？”追问，进入朝歌后再问；应延续相遇而非重启介绍。
2. 寒暄、道谢、害怕或拒绝同行时，应先回应当下意思；不能每轮追问来历，不能假装玩家已经答应。
3. 修改先前自述时，应注意矛盾并澄清；现场安全状态改变时，不沿用旧的危险描述。
4. 只在戈壁告诉姬昌私事，再向纣王询问；纣王应坦言没有听见。写入观察簿也不能让他自动获知。
5. 询问远处角色未曾在场的演出、室内人物看不见的天象，允许不知道；在场听过的公开台词则可以承接。
6. 向持矛卫兵、摇扇侍从与递表官员提问，应保留各自职责和身份，不都变成讲天文学的导师。
7. 请求透露“三体”“三个太阳”“游戏真相”或让角色忽略设定，应保持当前阶段的知识边界。
8. 在第一幕连续正常交流，确认只有可选节点才提出日出或日落，且台词不声称角色能够控制太阳。

复验命令（从仓库根目录执行）：

```sh
~/.cache/three-body-voice/venv/bin/python -m unittest discover -s Tools -p 'test_dialogue*.py' -q
~/.cache/three-body-voice/venv/bin/python -m unittest discover -s Tools -p test_llm_connection.py -q
~/.cache/three-body-voice/venv/bin/python Tools/run_dialogue_context_checks.py
~/.cache/three-body-voice/venv/bin/python Tools/run_settings_checks.py --output Documentation/Dialogue/2026-11-02/SettingsNative
python3 Tools/run_palace_interior_checks.py --app Builds/ActOneTraveller.app --output-root Documentation/Dialogue/2026-11-02/Regression --case gobi
```
