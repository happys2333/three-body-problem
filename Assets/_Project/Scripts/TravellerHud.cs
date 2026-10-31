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
        private VisualElement dialogue, debugMoments, debugScenes, captionCard;
        private Label chapter, objective, navigation, caption, notice, summaryText, debugTitle, travelTitle;
        private ScrollView journalEntries;
        private TextField hypothesis;
        private Button observe;
        private NpcDialogueDirector dialogueDirector;
        private PlayerMicrophoneInput microphone;
        private TextField chatInput;
        private Label voiceStatus;
        private Button sendChat;
        private Label conversationRecipient;
        private VisualElement textFallback;
        private GobiSequenceController gobi;
        private SunExposureSystem exposure;
        private VisualElement questMarker,wakeTop,wakeBottom;
        private Label questArrow,questDistance;
        private bool paused, journalOpen, hidden;
        private float noticeUntil, nextNavigationUpdate,nextMarkerText;
        public VisualElement Root => root;
        public bool IsWorldPaused => paused || journalOpen || journey == null || journey.IsTitle || journey.IsSummary || stage.IsLoading;
        public bool IsEditingText
        {get{var focused=root?.focusController?.focusedElement as VisualElement;return focused!=null&&((chatInput!=null&&chatInput.Contains(focused))||(hypothesis!=null&&hypothesis.Contains(focused)));}}

        private void Start()
        {
            journey = stage.Journey;
            dialogueDirector=stage.GetComponent<NpcDialogueDirector>();microphone=stage.GetComponent<PlayerMicrophoneInput>();
            gobi=stage.GetComponent<GobiSequenceController>();
            exposure=stage.GetComponent<SunExposureSystem>();
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
            questMarker=Box(gameplay,"quest-marker","quest-marker");questMarker.pickingMode=PickingMode.Ignore;
            questArrow=Text(questMarker,"▲","quest-arrow");questDistance=Text(questMarker,"","quest-distance");
            var bottom = Box(gameplay, "bottom", "hud-bottom");
            captionCard = Box(bottom, "caption-card", "caption-card"); caption = Text(captionCard, "", "caption");
            dialogue = Box(bottom, "dialogue", "dialogue-card");
            if(gobi!=null)dialogue.AddToClassList("gobi-chat");
            var dialogueScroll=new ScrollView{name="dialogue-scroll"};dialogueScroll.AddToClassList("dialogue-scroll");dialogue.Add(dialogueScroll);
            conversationRecipient=Text(dialogueScroll, "与眼前的人交谈", "panel-heading");conversationRecipient.name="conversation-recipient";
            var chat=Box(dialogueScroll,"free-dialogue","free-dialogue");
            textFallback=Box(chat,"text-fallback","text-fallback");
            chatInput=new TextField{maxLength=1200,name="chat-input"};chatInput.AddToClassList("chat-input");textFallback.Add(chatInput);
            chatInput.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Return&&!e.shiftKey){SendChat();e.StopPropagation();}});
            voiceStatus=Text(chat,"按住 F 说话，松开后自动提问；也可打字","voice-status");voiceStatus.name="voice-status";
            var chatActions=Box(textFallback,"chat-actions","chat-actions");sendChat=Button(chatActions,"发送",SendChat,"primary");sendChat.name="send-chat";
            dialogueScroll.Insert(1,chat);
            if(dialogueDirector!=null)dialogueDirector.Changed+=RefreshChat;
            if(microphone!=null)microphone.TranscriptReady+=SetTranscript;
            var actions = Box(bottom, "action-row", "action-row");
            Text(actions,"看向人物，按住 F 提问","controls");
            observe = Button(actions, "记录所见  E", () => stage.RecordObservation(), "primary");
            Text(bottom, "WASD 行走 · Shift 快走 · 按住右键环顾 · 按住 F 提问 · T 火把 · E 记录", "controls");
            wakeTop=Box(gameplay,"wake-top","wake-lid wake-top");wakeBottom=Box(gameplay,"wake-bottom","wake-lid wake-bottom");
            wakeTop.pickingMode=PickingMode.Ignore;wakeBottom.pickingMode=PickingMode.Ignore;

            titleOverlay = Overlay("title-overlay");
            var card = Box(titleOverlay, "title-card", "menu-card title-card");
            Text(card, "T R I S O L A R I S   /   T H E   T R A V E L L E R", "eyebrow");
            Text(card, "三体 · 旅人", "game-title");
            Text(card, "在无法预测的世界里，带着自己的观察继续前行。", "menu-description");
            Button(card, "开始旅程", () => { hidden = false; journey.Begin(); }, "primary start-button").name = "start-journey";
            Text(card, "在荒漠中醒来，调查四周，寻找远处的火光。", "menu-description");
            Text(card, "WASD 行走，按住鼠标右键环顾；看向人物按住 F 说话，松开后自动提问。", "controls");

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
            stage.player.InputBlocked = modal || journey.Progress.Step==JourneyStep.Awakening || IsEditingText||(exposure!=null&&exposure.IsRecovering);
            chapter.text = stage.profile.chapter;
            objective.text = journey.Objective; caption.text = journey.Speech; Display(captionCard, !string.IsNullOrEmpty(journey.Speech));
            Display(dialogue, journey.DialogueOpen); Display(observe, journey.CanObserve && !journey.DialogueOpen);
            if(!journey.DialogueOpen&&chatInput!=null)chatInput.Blur();
            summaryText.text = "这一程你完成了 " + journey.Progress.ObservationsThisRun + " 次关键观察。\n\n" + (journey.Progress.OfferedExplanation ? "你把飞星与远处的太阳联系起来，提出了自己的解释。" : "你保留了疑问，并亲眼见证了三日凌空。") + "\n\n短期应验，仍不能保证文明长期安全。";
            if (debug) RefreshDebug(); if (journalOpen) RefreshJournal();
            RefreshChat();
            nextNavigationUpdate=0;
        }

        private void SendChat(){if(dialogueDirector==null||dialogueDirector.IsBusy||string.IsNullOrWhiteSpace(chatInput.value))return;dialogueDirector.Submit(chatInput.value);chatInput.value="";chatInput.Blur();}
        private void SetTranscript(string text){chatInput.value="";}
        private void RefreshChat()
        {
            if(voiceStatus==null)return;voiceStatus.text=dialogueDirector!=null?dialogueDirector.Status:"看向人物，按住 F 说话";
            conversationRecipient.text=dialogueDirector!=null&&dialogueDirector.Current!=null?"向"+dialogueDirector.DisplayName(dialogueDirector.Current)+"提问":"与眼前的人交谈";
            Display(textFallback,microphone==null||microphone.NeedsTextFallback);
            sendChat.SetEnabled(dialogueDirector!=null&&!dialogueDirector.IsBusy&&(microphone==null||!microphone.IsRecording&&!microphone.IsTranscribing));
        }

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
            {
                stage.player.InputBlocked=IsWorldPaused||journey.Progress.Step==JourneyStep.Awakening||editing||exposure!=null&&exposure.IsRecovering;
            }
            bool waking=gobi!=null&&journey.Progress.Step==JourneyStep.Awakening&&!journey.IsPreviewing;
            Display(wakeTop,waking);Display(wakeBottom,waking);
            if(waking){float closed=50*(1-Mathf.SmoothStep(0,1,gobi.WakeProgress));wakeTop.style.height=Length.Percent(closed);wakeBottom.style.height=Length.Percent(closed);}
            UpdateQuestMarker();
            if (!editing && Input.GetKeyDown(KeyCode.J)) ToggleJournal();
            if (Input.GetKeyDown(KeyCode.Escape)) { if (journalOpen) ToggleJournal(); else if (journey.DialogueOpen) journey.SelectDialogue("close"); else TogglePause(); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!journalOpen && Input.GetKeyDown(KeyCode.F8)) { paused = false; hidden = false; journey.TogglePreview(); }
#endif
            if (!editing && !journey.IsTitle && !journey.IsSummary && !paused && Input.GetKeyDown(KeyCode.H)) { hidden = !hidden; Refresh(); }
            if (noticeUntil > 0f && Time.unscaledTime > noticeUntil) { Display(notice, false); noticeUntil = 0f; }
            if (Time.unscaledTime >= nextNavigationUpdate) { nextNavigationUpdate = Time.unscaledTime + 0.25f; navigation.text = journey.HasNavigation ? "沿绿色箭头前行  ·  目标 " + Vector3.Distance(stage.player.transform.position,journey.Goal).ToString("0") + " 米" : journey.CanObserve ? "看向天空后按 E 记录；按住 F 可向人物提问" : journey.Progress.Step==JourneyStep.OpeningTalk||journey.Progress.Step==JourneyStep.ShelterTalk?"看向人物按住 F 说话，松开后自动发送":""; }
        }

        private void UpdateQuestMarker()
        {
            bool visible=journey.HasNavigation&&!journey.IsPreviewing&&!IsWorldPaused;
            Display(questMarker,visible);if(!visible)return;
            var camera=stage.player.viewCamera;
            Vector3 screen=camera.WorldToViewportPoint(journey.Goal+Vector3.up*2.8f);
            float width=root.layout.width,height=root.layout.height;
            float x=screen.x*width,y=(1-screen.y)*height;
            bool outside=screen.z<0||x<70||x>width-70||y<165||y>height*.62f;
            if(screen.z<0){x=width-x;y=height-y;}
            float angle=outside?Mathf.Atan2(x-width*.5f,height*.5f-y)*Mathf.Rad2Deg:180;
            questMarker.style.left=Mathf.Clamp(x,70,width-70)-42;
            questMarker.style.top=Mathf.Clamp(y,165,height*.62f)-20;
            questArrow.style.rotate=new Rotate(new Angle(angle));
            if(Time.unscaledTime>=nextMarkerText){nextMarkerText=Time.unscaledTime+.25f;questDistance.text=Vector3.Distance(stage.player.transform.position,journey.Goal).ToString("0")+" m";}
        }

        private void OnDestroy() { if (stage != null) { stage.Changed -= Refresh; stage.Notice -= ShowNotice; } if (journey != null) journey.Changed -= Refresh;if(dialogueDirector!=null)dialogueDirector.Changed-=RefreshChat;if(microphone!=null)microphone.TranscriptReady-=SetTranscript; }
        private static VisualElement Box(VisualElement parent, string name, string classes) { var v = new VisualElement { name = name }; foreach (var c in classes.Split(' ')) v.AddToClassList(c); parent.Add(v); return v; }
        private static Label Text(VisualElement parent, string value, string cls) { var v = new Label(value) { pickingMode = PickingMode.Ignore }; v.AddToClassList(cls); parent.Add(v); return v; }
        private static Button Button(VisualElement parent, string text, System.Action action, string classes = "") { var v = new Button(action) { text = text }; v.AddToClassList("button"); foreach (var c in classes.Split(' ')) if (c.Length > 0) v.AddToClassList(c); parent.Add(v); return v; }
    }
}
