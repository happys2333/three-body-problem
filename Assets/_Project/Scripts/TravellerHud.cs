using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class TravellerHud : MonoBehaviour
    {
        public TravellerStage stage;
        public Font font;
        public StyleSheet stylesheet;
        private TravellerJourney journey;
        private VisualElement root, gameplay, titleOverlay, pauseOverlay, journalOverlay, summaryOverlay, debugOverlay, travelOverlay;
        private VisualElement dialogue, dialogueChoices, debugMoments, debugScenes, captionCard;
        private Label chapter, objective, navigation, caption, notice, summaryText, debugTitle, travelTitle;
        private ScrollView journalEntries;
        private TextField hypothesis;
        private Button interact, observe;
        private NpcDialogueDirector dialogueDirector;
        private PlayerMicrophoneInput microphone;
        private TextField chatInput;
        private Label voiceStatus;
        private Button sendChat,microphoneButton;
        private bool paused, journalOpen, hidden;
        private float noticeUntil, nextNavigationUpdate;
        public VisualElement Root => root;
        public bool IsWorldPaused => paused || journalOpen || journey == null || journey.IsTitle || journey.IsSummary || stage.IsLoading;
        public bool IsEditingText
        {get{var focused=root?.focusController?.focusedElement as VisualElement;return focused!=null&&((chatInput!=null&&chatInput.Contains(focused))||(hypothesis!=null&&hypothesis.Contains(focused)));}}

        private void Start()
        {
            journey = stage.Journey;
            dialogueDirector=stage.GetComponent<NpcDialogueDirector>();microphone=stage.GetComponent<PlayerMicrophoneInput>();
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear(); root.AddToClassList("traveller"); root.pickingMode = PickingMode.Ignore;
            root.style.unityFontDefinition = FontDefinition.FromFont(font); root.styleSheets.Add(stylesheet);
            root.RegisterCallback<GeometryChangedEvent>(_ => root.EnableInClassList("compact", root.layout.width < 1100f || root.layout.height < 650f));
            gameplay = Box(root, "gameplay", "gameplay-hud");
            var top = Box(gameplay, "top", "hud-top");
            var mission = Box(top, "mission", "mission");
            chapter = Text(mission, "", "chapter-small"); objective = Text(mission, "", "objective"); navigation = Text(mission, "", "navigation");
            var tools = Box(top, "tools", "hud-tools");
            Button(tools, "观察簿  J", ToggleJournal); Button(tools, "暂停  Esc", TogglePause);
            var bottom = Box(gameplay, "bottom", "hud-bottom");
            captionCard = Box(bottom, "caption-card", "caption-card"); caption = Text(captionCard, "", "caption");
            dialogue = Box(bottom, "dialogue", "dialogue-card");
            var dialogueScroll=new ScrollView{name="dialogue-scroll"};dialogueScroll.AddToClassList("dialogue-scroll");dialogue.Add(dialogueScroll);
            Text(dialogueScroll, "与眼前的人交谈", "panel-heading"); dialogueChoices = Box(dialogueScroll, "dialogue-choices", "choice-list");
            var chat=Box(dialogueScroll,"free-dialogue","free-dialogue");
            chatInput=new TextField{maxLength=1200,name="chat-input"};chatInput.AddToClassList("chat-input");chat.Add(chatInput);
            chatInput.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Return&&!e.shiftKey){SendChat();e.StopPropagation();}});
            voiceStatus=Text(chat,"可以打字，或按住 V 说话","voice-status");voiceStatus.name="voice-status";
            var chatActions=Box(chat,"chat-actions","chat-actions");sendChat=Button(chatActions,"发送",SendChat,"primary");sendChat.name="send-chat";
            microphoneButton=Button(chatActions,"麦克风 · 按住 V",()=>microphone?.ToggleCapture());microphoneButton.name="microphone";
            dialogueScroll.Insert(1,chat);
            if(dialogueDirector!=null)dialogueDirector.Changed+=RefreshChat;
            if(microphone!=null)microphone.TranscriptReady+=SetTranscript;
            var actions = Box(bottom, "action-row", "action-row");
            interact = Button(actions, "交谈  F", () => journey.Interact(), "primary");
            observe = Button(actions, "记录所见  E", () => stage.RecordObservation(), "primary");
            Text(bottom, "WASD 行走 · Shift 快走 · 按住右键环顾 · F 交谈 · V 说话 · T 火把 · E 记录", "controls");

            titleOverlay = Overlay("title-overlay");
            var card = Box(titleOverlay, "title-card", "menu-card title-card");
            Text(card, "T R I S O L A R I S   /   T H E   T R A V E L L E R", "eyebrow");
            Text(card, "三体 · 旅人", "game-title");
            Text(card, "在无法预测的世界里，带着自己的观察继续前行。", "menu-description");
            Button(card, "开始旅程", () => { hidden = false; journey.Begin(); }, "primary start-button").name = "start-journey";
            Text(card, "从夜色中的戈壁初遇开始，跟随火光，留意天象。", "menu-description");
            Text(card, "WASD 行走，按住鼠标右键环顾；靠近人物后按 F 交谈。", "controls");

            pauseOverlay = Overlay("pause-overlay"); card = Box(pauseOverlay, "pause-card", "menu-card");
            Text(card, "旅程暂停", "menu-title"); Button(card, "继续旅程", TogglePause, "primary");
            Button(card, "查看观察簿", () => { paused = false; ToggleJournal(); });
            Button(card, "从戈壁重新开始", () => { paused = false; journey.Begin(); });
            Button(card, "返回标题", () => { paused = false; journey.ReturnToTitle(); });

            journalOverlay = Overlay("journal-overlay"); card = Box(journalOverlay, "journal-card", "menu-card journal-card");
            Text(card, "旅人的观察簿", "menu-title"); Text(card, "亲眼观察、人物判断与自己的假说分别保存。", "menu-description");
            journalEntries = new ScrollView(); journalEntries.AddToClassList("journal-entries"); card.Add(journalEntries);
            Text(card, "我的假说", "panel-heading");
            hypothesis = new TextField { multiline = true }; hypothesis.AddToClassList("hypothesis"); card.Add(hypothesis);
            Button(card, "保存假说", () => { stage.Journal.hypothesis = hypothesis.value; stage.Journal.Save(); ShowNotice("假说已保存。"); });
            Button(card, "返回旅程  J / Esc", ToggleJournal, "primary");

            summaryOverlay = Overlay("summary-overlay"); card = Box(summaryOverlay, "summary-card", "menu-card");
            Text(card, "旅程回顾", "menu-title"); summaryText = Text(card, "", "menu-description");
            Text(card, "解释世界的结构之后，能否得到足够可靠的预测？", "menu-description");
            Button(card, "翻阅自己的观察", ToggleJournal, "primary"); Button(card, "重新踏上旅程", () => journey.Begin()); Button(card, "返回标题", () => journey.ReturnToTitle());

            debugOverlay = Box(root, "debug-overlay", "debug-overlay");
            var debugHeader = Box(debugOverlay, "debug-header", "debug-header"); debugTitle = Text(debugHeader, "", "panel-heading");
            Button(debugHeader, "退出场景调试  F8", () => journey.TogglePreview());
            var sidebar = Box(debugOverlay, "debug-sidebar", "debug-sidebar"); Text(sidebar, "场景调试 · 天象时刻", "panel-heading");
            var scroll = new ScrollView(); scroll.AddToClassList("debug-scroll"); sidebar.Add(scroll);
            debugMoments = Box(scroll, "debug-moments", "choice-list"); debugScenes = Box(scroll, "debug-scenes", "choice-list");
            Button(sidebar, "回到观察位", stage.ResetView); Button(sidebar, "记录观察", () => stage.RecordObservation());
            notice = Text(root, "", "notice"); notice.name = "notice";
            travelOverlay = Box(root, "travel-overlay", "overlay travel-overlay");
            travelTitle = Text(travelOverlay, "", "travel-title");
            Text(travelOverlay, "带着自己的观察继续前行", "travel-subtitle");
            stage.Changed += Refresh; stage.Notice += ShowNotice; journey.Changed += Refresh;
            Refresh();
        }

        private VisualElement Overlay(string name) => Box(root, name, "overlay");
        private static void Display(VisualElement value, bool visible) => value.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private void Refresh()
        {
            if (root == null) return;
            bool debug = journey.IsPreviewing, modal = journey.IsTitle || journey.IsSummary || paused || journalOpen || stage.IsLoading;
            Display(gameplay, !debug && !modal && !hidden); Display(titleOverlay, journey.IsTitle && !debug);
            Display(pauseOverlay, paused && !journalOpen); Display(journalOverlay, journalOpen); Display(summaryOverlay, journey.IsSummary && !journalOpen);
            Display(debugOverlay, debug && !journalOpen && !paused && !hidden);
            Display(travelOverlay, stage.IsLoading); travelTitle.text = stage.DestinationTitle ?? "";
            var exposure=stage.GetComponent<SunExposureSystem>();
            stage.player.InputBlocked = modal || (!debug && journey.DialogueOpen)||(exposure!=null&&exposure.IsRecovering);
            chapter.text = stage.profile.chapter;
            objective.text = journey.Objective; caption.text = journey.Speech; Display(captionCard, !string.IsNullOrEmpty(journey.Speech));
            Display(dialogue, journey.DialogueOpen); Display(observe, journey.CanObserve && !journey.DialogueOpen);
            if(!journey.DialogueOpen&&chatInput!=null)chatInput.Blur();
            var step = journey.Progress.Step;
            bool waiting = step == JourneyStep.Sunset || step == JourneyStep.CalendarWait || step == JourneyStep.WaitForDawn || step == JourneyStep.Collapse;
            Display(interact, !journey.DialogueOpen && !waiting);
            interact.text = step == JourneyStep.DepartForCapital ? "启程前往朝歌  F" : journey.CanObserve ? "指认天象  F" : journey.IsWalking ? "查看前进方向  F" : step == JourneyStep.AfterRevival ? "等待下一次日出  F" : step == JourneyStep.AfterLongNight ? "进入新的文明  F" : step == JourneyStep.AfterThreeSuns ? "继续观察  F" : "交谈  F";
            dialogueChoices.Clear();
            if (journey.DialogueOpen)
            {
                if (step == JourneyStep.ShelterTalk||step==JourneyStep.SeekShelter||step==JourneyStep.WalkToChaoge)
                {
                    Choice("你是谁？你要去哪里？", "identity"); Choice("为什么带着沙漏？", "hourglass"); Choice("你如何判断天象？", "calendar");
                    if (step==JourneyStep.ShelterTalk)Choice("留在岩下等候", "continue", true);
                }
                else if (step == JourneyStep.CourtTalk)
                {
                    Choice("补充一颗与两颗飞星的观察", "evidence"); Choice("要怎样验证历法？", "verification"); Choice("旁听完毕，开始等待验证", "continue", true);
                }
                else if (step == JourneyStep.TribunalTalk)
                {
                    Choice("飞星可能是远处的太阳；三太阳能联系这些观察", "explain"); Choice("我还不能确定，愿意继续观察", "uncertain");
                    if (journey.Progress.ConsideredExplanation) Choice("回应质询后继续", "continue", true);
                }
                Choice("再看看周围", "close");
            }
            summaryText.text = "这一程你完成了 " + journey.Progress.ObservationsThisRun + " 次关键观察。\n\n" + (journey.Progress.OfferedExplanation ? "你把飞星与远处的太阳联系起来，提出了自己的解释。" : "你保留了疑问，并亲眼见证了三日凌空。") + "\n\n短期应验，仍不能保证文明长期安全。";
            if (debug) RefreshDebug(); if (journalOpen) RefreshJournal();
            RefreshChat();
        }

        private void SendChat(){if(dialogueDirector==null||dialogueDirector.IsBusy||string.IsNullOrWhiteSpace(chatInput.value))return;dialogueDirector.Submit(chatInput.value);chatInput.value="";chatInput.Blur();}
        private void SetTranscript(string text){chatInput.value=text;}
        private void RefreshChat()
        {
            if(voiceStatus==null)return;voiceStatus.text=dialogueDirector!=null?dialogueDirector.Status:"可以使用剧情选项交谈";
            sendChat.SetEnabled(dialogueDirector!=null&&!dialogueDirector.IsBusy);
            microphoneButton.SetEnabled(microphone!=null&&!microphone.IsTranscribing);
            microphoneButton.text=microphone!=null&&microphone.IsRecording?"结束录音":"麦克风 · 按住 V";
        }

        private void Choice(string text, string action, bool primary = false) => Button(dialogueChoices, text, () => journey.SelectDialogue(action), primary ? "primary" : "");
        private void RefreshDebug()
        {
            debugTitle.text = "场景调试 / " + stage.profile.chapter; debugMoments.Clear(); debugScenes.Clear();
            for (int i = 0; i < stage.profile.moments.Length; i++) { int index = i; var b = Button(debugMoments, (i + 1).ToString("00") + "  " + stage.profile.moments[i].title, () => stage.ApplyMoment(index)); if (i == stage.MomentIndex) b.AddToClassList("selected"); }
            string[] names = { "戈壁与巨岩", "朝歌与长夜", "中世纪与三日" };
            for (int i = 0; i < names.Length; i++) { int index = i; Button(debugScenes, names[i], () => stage.LoadScene(index)); }
        }

        private void RefreshJournal()
        {
            journalEntries.Clear();
            foreach (string category in new[] { "亲眼观察", "人物判断" })
            {
                Text(journalEntries, category, "panel-heading"); var lines = new StringBuilder();
                foreach (var entry in stage.Journal.entries) if (entry.category == category) lines.Append("• ").Append(entry.text).Append('\n');
                Text(journalEntries, lines.Length == 0 ? "还没有记录。亲眼看清后，按 E 留下所见。" : lines.ToString().TrimEnd(), "journal-text");
            }
        }

        private void ToggleJournal() { if (journey.IsTitle) return; journalOpen = !journalOpen; if (journalOpen) hypothesis.value = stage.Journal.hypothesis; Refresh(); }
        private void TogglePause() { if (journey.IsTitle || journey.IsSummary) return; paused = !paused; Refresh(); }
        private void ShowNotice(string text) { notice.text = text; Display(notice, true); noticeUntil = Time.unscaledTime + 3.5f; }

        private void Update()
        {
            if (root == null) return;
            bool editing=IsEditingText;
            if (!editing && Input.GetKeyDown(KeyCode.J)) ToggleJournal();
            if (Input.GetKeyDown(KeyCode.Escape)) { if (journalOpen) ToggleJournal(); else if (journey.DialogueOpen) journey.SelectDialogue("close"); else TogglePause(); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!journalOpen && Input.GetKeyDown(KeyCode.F8)) { paused = false; hidden = false; journey.TogglePreview(); }
#endif
            if (!editing && !journey.IsTitle && !journey.IsSummary && !paused && Input.GetKeyDown(KeyCode.H)) { hidden = !hidden; Refresh(); }
            if (noticeUntil > 0f && Time.unscaledTime > noticeUntil) { Display(notice, false); noticeUntil = 0f; }
            if (Time.unscaledTime >= nextNavigationUpdate) { nextNavigationUpdate = Time.unscaledTime + 0.25f; navigation.text = journey.IsWalking ? "目标距离 " + Vector3.Distance(stage.player.transform.position,journey.Goal).ToString("0") + " 米  ·  R 查看方向" : journey.CanObserve ? "看向天空后按 E 记录；F 可请人指认" : ""; }
        }

        private void OnDestroy() { if (stage != null) { stage.Changed -= Refresh; stage.Notice -= ShowNotice; } if (journey != null) journey.Changed -= Refresh;if(dialogueDirector!=null)dialogueDirector.Changed-=RefreshChat;if(microphone!=null)microphone.TranscriptReady-=SetTranscript; }
        private static VisualElement Box(VisualElement parent, string name, string classes) { var v = new VisualElement { name = name }; foreach (var c in classes.Split(' ')) v.AddToClassList(c); parent.Add(v); return v; }
        private static Label Text(VisualElement parent, string value, string cls) { var v = new Label(value) { pickingMode = PickingMode.Ignore }; v.AddToClassList(cls); parent.Add(v); return v; }
        private static Button Button(VisualElement parent, string text, System.Action action, string classes = "") { var v = new Button(action) { text = text }; v.AddToClassList("button"); foreach (var c in classes.Split(' ')) if (c.Length > 0) v.AddToClassList(c); parent.Add(v); return v; }
    }
}
