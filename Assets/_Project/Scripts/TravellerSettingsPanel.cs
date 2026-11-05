using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace ThreeBodyProblem
{
    public sealed class TravellerSettingsPanel : IDisposable
    {
        private static readonly List<string> Providers = new List<string> { "沿用本地服务配置", "DeepSeek", "自定义接口（OpenAI 兼容）", "离线剧情对白" };
        private static readonly string[] Modes = { "gateway", "deepseek", "custom", "scripted" };
        private readonly TravellerHud owner;
        private readonly NpcDialogueSettings defaults;
        private readonly Action closed, saved;
        private readonly VisualElement overlay, apiFields, aiPage, controlsPage, graphicsPage;
        private readonly DropdownField provider;
        private readonly DropdownField contextWindow, displayMode, resolution, graphicsQuality;
        private readonly List<Vector2Int> resolutionSizes = new List<Vector2Int>();
        private static readonly List<string> DisplayModes = new List<string> { "窗口", "全屏" };
        private static readonly List<string> QualityLabels = new List<string> { "默认", "低", "中", "高", "极高" };
        private static readonly Vector2Int[] StandardSizes =
        {
            new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160)
        };
        private static readonly List<string> ContextLabels = new List<string> { "32K", "128K", "512K", "1M" };
        private static readonly int[] ContextTokens = { 32000, 128000, 512000, 1000000 };
        private readonly TextField apiUrl, apiKey, model, gatewayUrl;
        private readonly IntegerField timeout;
        private readonly Slider sensitivity;
        private readonly Toggle invertY, speakReplies, showKey, microphoneEnabled;
        private readonly DropdownField microphoneDevice;
        private readonly Button refreshMicrophones;
        private readonly Label microphoneHint;
        private readonly ScrollView scroll;
        private readonly List<string> microphoneIds = new List<string>();
        private string[] availableMicrophones = Array.Empty<string>();
        private readonly Label status;
        private readonly Button test, aiTab, controlsTab, graphicsTab;
        private UnityWebRequest pending;
        private Coroutine testing;
        private int generation;
        public bool IsOpen { get; private set; }
        public VisualElement Root => overlay;
        [Serializable] private class TestRequest { public NpcApiConnection connection; }
        [Serializable] private class TestReply { public bool ok; public string message; }

        public TravellerSettingsPanel(TravellerHud owner, VisualElement parent, NpcDialogueSettings defaults, Action closed, Action saved)
        {
            this.owner = owner; this.defaults = defaults; this.closed = closed; this.saved = saved;
            overlay = Box(parent, "settings-overlay", "overlay settings-overlay");
            var card = Box(overlay, "settings-card", "menu-card settings-card");
            Text(card, "设置", "menu-title settings-title");
            var tabs = Box(card, "settings-tabs", "settings-tabs");
            aiTab = Button(tabs, "AI 对话", () => SelectTab(0), "settings-ai-tab");
            controlsTab = Button(tabs, "操作与声音", () => SelectTab(1), "settings-controls-tab");
            graphicsTab = Button(tabs, "显示与画质", () => SelectTab(2), "settings-graphics-tab");
            scroll = new ScrollView { name = "settings-scroll" }; scroll.AddToClassList("settings-scroll"); card.Add(scroll);
            aiPage = Box(scroll, "settings-ai", "settings-page");
            provider = new DropdownField("对话模式", Providers, 0) { name = "settings-provider" };
            provider.AddToClassList("settings-select"); aiPage.Add(provider);
            contextWindow = new DropdownField("上下文长度", ContextLabels, 0) { name = "settings-context-window" };
            contextWindow.AddToClassList("settings-select"); aiPage.Add(contextWindow);
            apiFields = Box(aiPage, "settings-api-fields", "settings-api-fields");
            apiUrl = Field(apiFields, "API 地址", "settings-api-url");
            Text(apiFields, "可填写 Base URL（如含 /v1 的地址），也可填写完整 /chat/completions 地址。", "settings-hint");
            model = Field(apiFields, "模型名称", "settings-model");
            Text(apiFields, "填写服务商提供的模型 ID，例如 deepseek-flash。", "settings-hint");
            Text(apiFields, "API Key", "settings-label");
            var keyRow = Box(apiFields, "settings-key-row", "settings-key-row");
            apiKey = new TextField { name = "settings-api-key", isPasswordField = true, maskChar = '*', maxLength = 4096 };
            apiKey.AddToClassList("settings-input"); apiKey.AddToClassList("settings-key-input"); keyRow.Add(apiKey);
            showKey = new Toggle("显示") { name = "settings-show-key" }; keyRow.Add(showKey);
            showKey.RegisterValueChangedCallback(e => apiKey.isPasswordField = !e.newValue);
            Text(apiFields, "Key 仅在本次运行中使用；API 地址、模型和其他选项保存在本机。", "settings-hint");
            timeout = new IntegerField("回复等待时间（秒）") { name = "settings-timeout" };
            timeout.AddToClassList("settings-select"); apiFields.Add(timeout);
            Text(apiFields,"回复较慢的模型可增加等待时间；超时或服务拒绝不会自动推进剧情。Atria-Dawn 本次实测建议 60 秒。","settings-hint");
            Button(apiFields,"使用 60 秒等待",()=>timeout.value=60,"settings-use-longer-wait");
            gatewayUrl = Field(aiPage, "本地对话与语音服务", "settings-gateway-url");
            Text(aiPage, "默认 http://127.0.0.1:8769。语音识别、NPC 声音与对话转接由本地服务提供。", "settings-hint");
            controlsPage = Box(scroll, "settings-controls", "settings-page");
            Text(controlsPage, "进入旅程后，直接移动鼠标环顾，无需按住右键。Esc 打开菜单；Enter 打开文字交谈。", "settings-description");
            sensitivity = new Slider("鼠标灵敏度", .2f, 5) { name = "settings-sensitivity", showInputField = true };
            sensitivity.AddToClassList("settings-select"); controlsPage.Add(sensitivity);
            invertY = new Toggle("反转鼠标纵向") { name = "settings-invert-y" }; controlsPage.Add(invertY);
            microphoneEnabled = new Toggle("使用麦克风与人物交谈") { name = "settings-microphone-enabled" }; controlsPage.Add(microphoneEnabled);
            microphoneDevice = new DropdownField("输入设备", new List<string> { "系统默认麦克风" }, 0) { name = "settings-microphone-device" };
            microphoneDevice.AddToClassList("settings-select"); controlsPage.Add(microphoneDevice);
            refreshMicrophones = Button(controlsPage, "刷新麦克风列表", () => RefreshMicrophones(), "settings-refresh-microphones");
            refreshMicrophones.style.alignSelf = Align.FlexStart;
            microphoneHint = Text(controlsPage, "", "settings-hint"); microphoneHint.name = "settings-microphone-status";
            Text(controlsPage, "仅在按住 F 时录音，松开后自动转写发送；首次使用时按系统提示授权。关闭后仍可按 Enter 打字交谈。", "settings-hint");
            speakReplies = new Toggle("播放 NPC 语音回复") { name = "settings-speak-replies" }; controlsPage.Add(speakReplies);
            Text(controlsPage, "WASD 行走 · Shift 快走 · F 按住说话、松开发送 · J 观察簿", "settings-hint");
            graphicsPage = Box(scroll, "settings-graphics", "settings-page");
            displayMode = new DropdownField("显示模式", DisplayModes, 0) { name = "settings-display-mode" };
            resolution = new DropdownField("分辨率", new List<string> { "当前分辨率" }, 0) { name = "settings-resolution" };
            graphicsQuality = new DropdownField("画质", QualityLabels, 0) { name = "settings-graphics-quality" };
            foreach (var field in new[] { displayMode, resolution, graphicsQuality })
            {
                field.AddToClassList("settings-select");
                graphicsPage.Add(field);
            }
            status = Text(card, "", "settings-status"); status.name = "settings-status";
            var footer = Box(card, "settings-footer", "settings-footer");
            test = Button(footer, "测试连接", BeginTest, "settings-test");
            var closing = Box(footer, "settings-closing", "settings-closing");
            Button(closing, "取消", Cancel, "settings-cancel");
            Button(closing, "保存并返回", Apply, "settings-save", "primary");
            provider.RegisterValueChangedCallback(_ => {
                CancelTest(); apiKey.value = ""; showKey.value = false;
                if (provider.index == 1) { apiUrl.value = "https://api.deepseek.com/chat/completions"; model.value = "deepseek-flash"; }
                else if (provider.index == 2) { apiUrl.value = "http://127.0.0.1:11434/v1"; model.value = ""; }
                SetStatus("", false); RefreshProvider();
            });
            foreach (var input in new[] { apiUrl, apiKey, model, gatewayUrl }) input.RegisterValueChangedCallback(_ => { CancelTest(); SetStatus("", false); });
            timeout.RegisterValueChangedCallback(_ => { CancelTest(); SetStatus("", false); });
            contextWindow.RegisterValueChangedCallback(_ => { CancelTest(); SetStatus("", false); });
            microphoneEnabled.RegisterValueChangedCallback(e => RefreshMicrophones(null, e.newValue));
            microphoneDevice.RegisterValueChangedCallback(_ => RefreshMicrophoneControls());
            overlay.style.display = DisplayStyle.None;
        }

        public void Open()
        {
            var value = TravellerUserSettings.Current;
            provider.SetValueWithoutNotify(Providers[Mathf.Max(0, Array.IndexOf(Modes, value.connection.mode))]);
            apiUrl.SetValueWithoutNotify(value.connection.apiUrl); model.SetValueWithoutNotify(value.connection.model);
            apiKey.SetValueWithoutNotify(value.connection.apiKey); showKey.SetValueWithoutNotify(false); apiKey.isPasswordField = true;
            gatewayUrl.SetValueWithoutNotify(value.GatewayUrl(defaults)); timeout.SetValueWithoutNotify(value.connection.timeoutSeconds);
            contextWindow.SetValueWithoutNotify(ContextLabels[Mathf.Max(0, Array.IndexOf(ContextTokens, value.connection.contextWindowTokens))]);
            sensitivity.SetValueWithoutNotify(value.mouseSensitivity); invertY.SetValueWithoutNotify(value.invertMouseY); speakReplies.SetValueWithoutNotify(value.speakReplies);
            microphoneEnabled.SetValueWithoutNotify(value.microphoneEnabled); RefreshMicrophones(value.microphoneDevice, false);
            displayMode.SetValueWithoutNotify(DisplayModes[(value.displayWidth > 0 ? value.fullscreen : Screen.fullScreen) ? 1 : 0]);
            graphicsQuality.SetValueWithoutNotify(QualityLabels[(int)value.graphicsQuality]);
            var size = value.displayWidth > 0 ? new Vector2Int(value.displayWidth, value.displayHeight) : new Vector2Int(Screen.width, Screen.height);
            RefreshResolutions(size);
            IsOpen = true; overlay.style.display = DisplayStyle.Flex; SelectTab(0); RefreshProvider(); SetStatus("", false);
        }

        private void SelectTab(int tab)
        {
            aiPage.style.display = tab == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            controlsPage.style.display = tab == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            graphicsPage.style.display = tab == 2 ? DisplayStyle.Flex : DisplayStyle.None;
            aiTab.EnableInClassList("selected", tab == 0);
            controlsTab.EnableInClassList("selected", tab == 1);
            graphicsTab.EnableInClassList("selected", tab == 2);
            test.style.display = tab == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            scroll.scrollOffset = Vector2.zero;
            if (tab == 1) RefreshMicrophones(null, microphoneEnabled.value);
        }

        private void RefreshResolutions(Vector2Int selected)
        {
            if (!TravellerPreferences.ValidDisplaySize(selected.x, selected.y)) selected = new Vector2Int(960, 540);
            int width = Display.main.systemWidth;
            int height = Display.main.systemHeight;
            var sizes = StandardSizes.Where(size => size.x <= width && size.y <= height)
                .Concat(Screen.resolutions.Select(mode => new Vector2Int(mode.width, mode.height)))
                .Append(selected);
            resolutionSizes.Clear();
            resolutionSizes.AddRange(sizes.Where(size => TravellerPreferences.ValidDisplaySize(size.x, size.y))
                .Distinct().OrderBy(size => size.x).ThenBy(size => size.y));
            resolution.choices = resolutionSizes.Select(size => size.x + " × " + size.y).ToList();
            resolution.SetValueWithoutNotify(resolution.choices[resolutionSizes.IndexOf(selected)]);
        }

        private void RefreshProvider() => apiFields.style.display = provider.index == 1 || provider.index == 2 ? DisplayStyle.Flex : DisplayStyle.None;

        private string SelectedMicrophone => microphoneIds.Count == 0 ? "" : microphoneIds[Mathf.Clamp(microphoneDevice.index, 0, microphoneIds.Count - 1)];
        private void RefreshMicrophones(string selection = null, bool scan = true)
        {
            selection = selection ?? SelectedMicrophone;
            availableMicrophones = Array.Empty<string>();
            if (scan)
            {
                try { availableMicrophones = owner.stage.GetComponent<PlayerMicrophoneInput>().Source.Devices ?? Array.Empty<string>(); }
                catch (Exception) { }
            }
            microphoneIds.Clear(); microphoneIds.Add("");
            var labels = new List<string> { "系统默认麦克风" };
            foreach (string device in availableMicrophones)
                if (!string.IsNullOrEmpty(device) && !microphoneIds.Contains(device)) { microphoneIds.Add(device); labels.Add(device); }
            if (!microphoneIds.Contains(selection)) { microphoneIds.Add(selection); labels.Add(scan ? "不可用：" + selection : selection); }
            microphoneDevice.choices = labels;
            microphoneDevice.SetValueWithoutNotify(labels[microphoneIds.IndexOf(selection)]);
            RefreshMicrophoneControls();
        }
        private void RefreshMicrophoneControls()
        {
            microphoneDevice.SetEnabled(microphoneEnabled.value); refreshMicrophones.SetEnabled(microphoneEnabled.value);
            string device, error;
            microphoneHint.text = !microphoneEnabled.value ? "麦克风已关闭；F 或 Enter 可打开文字交谈。" :
                MicrophoneDeviceSelection.TryResolve(SelectedMicrophone, availableMicrophones, out device, out error) ? "录音只使用所选设备；插拔或切换耳机后可刷新列表。" : error;
        }

        private TravellerPreferences ReadDraft()
        {
            Vector2Int size = resolutionSizes[Mathf.Clamp(resolution.index, 0, resolutionSizes.Count - 1)];
            return new TravellerPreferences {
                connection = new NpcApiConnection { mode = Modes[Mathf.Clamp(provider.index, 0, Modes.Length - 1)],
                    apiUrl = apiUrl.value, apiKey = apiKey.value, model = model.value, timeoutSeconds = timeout.value,
                    contextWindowTokens = ContextTokens[Mathf.Clamp(contextWindow.index, 0, ContextTokens.Length - 1)] },
                gatewayUrl = gatewayUrl.value, mouseSensitivity = sensitivity.value, invertMouseY = invertY.value, speakReplies = speakReplies.value,
                microphoneEnabled = microphoneEnabled.value, microphoneDevice = SelectedMicrophone,
                fullscreen = displayMode.index == 1, displayWidth = size.x, displayHeight = size.y,
                graphicsQuality = (TravellerGraphicsQuality)Mathf.Clamp(graphicsQuality.index, 0, QualityLabels.Count - 1)
            };
        }

        public void Apply()
        {
            string error; var value = ReadDraft();
            if (!TravellerUserSettings.Save(value, out error)) { SetStatus(error, true); return; }
            saved(); Close();
        }
        public void Cancel() => Close();
        private void Close()
        {
            var focused = overlay.focusController?.focusedElement as VisualElement;
            if (focused != null && overlay.Contains(focused)) focused.Blur();
            CancelTest(); IsOpen = false; overlay.style.display = DisplayStyle.None; apiKey.SetValueWithoutNotify("");
            overlay.Blur(); closed();
        }

        public void BeginTest()
        {
            CancelTest(); var draft = ReadDraft(); string error;
            if (!draft.Validate(out error)) { SetStatus(error, true); return; }
            SetStatus("正在测试本地服务与所选模型……", false); test.SetEnabled(false);
            testing = owner.StartCoroutine(Test(draft, generation));
        }

        private IEnumerator Test(TravellerPreferences draft, int token)
        {
            using (var web = new UnityWebRequest(draft.GatewayUrl(defaults) + "/connection-test", "POST"))
            {
                web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new TestRequest { connection = draft.connection })));
                web.downloadHandler = new DownloadHandlerBuffer(); web.SetRequestHeader("Content-Type", "application/json");
                web.timeout = draft.connection.timeoutSeconds + 3; pending = web;
                yield return web.SendWebRequest();
                if (token != generation || !IsOpen) yield break;
                TestReply reply = null;
                try { reply = JsonUtility.FromJson<TestReply>(web.downloadHandler.text); } catch (ArgumentException) { }
                if (reply != null && !string.IsNullOrWhiteSpace(reply.message)) SetStatus(reply.message, !reply.ok);
                else SetStatus(web.responseCode == 404 ? "本地服务版本较旧，请重启更新后的对话服务。" : "无法连接本地对话服务，请检查地址及服务是否已启动。", true);
                pending = null; testing = null; test.SetEnabled(true);
            }
        }

        private void CancelTest()
        {
            generation++; if (pending != null) { pending.Abort(); pending = null; }
            if (testing != null) { owner.StopCoroutine(testing); testing = null; }
            test.SetEnabled(true);
        }
        private void SetStatus(string text, bool error)
        { status.text = text; status.EnableInClassList("settings-error", error); status.EnableInClassList("settings-good", !error && !string.IsNullOrEmpty(text)); }
        public void Dispose() { CancelTest(); overlay.RemoveFromHierarchy(); }
        private static VisualElement Box(VisualElement parent, string name, string classes)
        { var box = new VisualElement { name = name }; foreach (var c in classes.Split(' ')) box.AddToClassList(c); parent.Add(box); return box; }
        private static Label Text(VisualElement parent, string text, string cls)
        { var label = new Label(text); label.AddToClassList(cls); parent.Add(label); return label; }
        private static TextField Field(VisualElement parent, string label, string name)
        { Text(parent, label, "settings-label"); var input = new TextField { name = name, maxLength = 2048 }; input.AddToClassList("settings-input"); parent.Add(input); return input; }
        private static Button Button(VisualElement parent, string text, Action action, string name, string extra = "")
        { var button = new Button(action) { text = text, name = name }; button.AddToClassList("button"); if (extra.Length > 0) button.AddToClassList(extra); parent.Add(button); return button; }
    }
}
