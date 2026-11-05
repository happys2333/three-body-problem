# Windows 游戏实机回归

`WindowsStoryPlayerCheck` 只通过 `TRAVELLER_STORY_QA` 构建开关进入独立测试包，普通试玩包不包含该检查器。测试使用正式版的构建选项，运行在 Windows 的 Direct3D 11 WARP 设备，保存截图和逐阶段报告。

脚本通过正常设置界面输入测试配置，检查空 Key、错误 Key、真实模型连接及密钥不落盘，再点击开始。步行使用 `TravellerPlayer.MoveInput`，人物问答使用文字交谈界面和真实模型回复，不直接设置剧情步骤或选择模型决策。剧情计时与场景转换沿用正式实现，覆盖戈壁、复苏、历法失败、三颗飞星、凝华、文明 137 毁灭和中世纪三日凌空到回顾。

本地设置 `TRAVELLER_STORY_QA=1` 和独立的 `TRAVELLER_WINDOWS_OUTPUT`，调用 `TravellerPlaytestBuild.BuildWindows` 后用现有打包脚本加 `--allow-story-qa` 生成 QA ZIP。普通打包默认拒绝包含测试检查器的程序集。压缩包临时放在未发布的 GitHub 草稿附件中。`request.json` 只包含附件名称、校验值和源码提交，不含凭据；更新该文件启动 Windows 检查，也可手动运行工作流。

真实模型配置通过临时的 `WINDOWS_STORY_QA_CONFIG` Secret 注入，测试机在结果目录之外保存会话文件，测试结束删除。日志与报告遮挡 Key；完成本轮测试后移除临时 Secret。普通发行包不会包含测试配置、凭据或 QA 检查器。
