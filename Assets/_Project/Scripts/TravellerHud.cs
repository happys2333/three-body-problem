using System.Text;
using System.Collections.Generic;
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
        private VisualElement dialogue, debugMoments, debugScenes, captionCard, helpCard, missionCard;
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
        private NpcDecisionController decisions;
        private VisualElement defeatOverlay;
        private Label defeatTitle;
        private Button continueAudience;
        private bool wasDefeated,wasScriptedAvailable;
        private VisualElement questMarker,wakeTop,wakeBottom;
        private Label questArrow,questDistance;
        private bool paused, journalOpen, hidden, textChatOpen;
        private float noticeUntil, nextNavigationUpdate,nextMarkerText;
        private List<string> subtitlePages = new List<string>();
        private string displayedSpeech;
        private int subtitlePage, displayedSpeechVersion = -1;
        private float subtitleNextAt;
        public VisualElement Root => root;
        public TravellerSettingsPanel SettingsPanel { get; private set; }
        public bool IsWorldPaused => paused || journalOpen || (decisions != null && decisions.IsDefeated) || (SettingsPanel != null && SettingsPanel.IsOpen) || journey == null || journey.IsTitle || journey.IsSummary || stage.IsLoading;
        private bool TextChatVisible => journey != null && journey.DialogueOpen && (textChatOpen || microphone == null || microphone.NeedsTextFallback);
        public bool IsEditingText
        {get{var focused=root?.focusController?.focusedElement as VisualElement;return focused!=null&&(focused is TextField||focused.GetFirstAncestorOfType<TextField>()!=null);}}

        private void Start()
        {
            journey = stage.Journey;
            dialogueDirector=stage.GetComponent<NpcDialogueDirector>();microphone=stage.GetComponent<PlayerMicrophoneInput>();
            gobi=stage.GetComponent<GobiSequenceController>();
            exposure=stage.GetComponent<SunExposureSystem>();
            decisions=stage.GetComponent<NpcDecisionController>();
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear(); root.AddToClassList("traveller"); root.pickingMode = PickingMode.Ignore;
            root.style.unityFontDefinition = FontDefinition.FromFont(font); root.styleSheets.Add(stylesheet);
            root.RegisterCallback<GeometryChangedEvent>(_ => { root.EnableInClassList("compact", root.layout.width < 1100f || root.layout.height < 650f); displayedSpeechVersion = -1; });
            gameplay = Box(root, "gameplay", "gameplay-hud");
            var top = Box(gameplay, "top", "hud-top");
            helpCard = Box(top, "help-card", "help-card");
            chapter = Text(helpCard, "", "chapter-small"); navigation = Text(helpCard, "", "navigation");
            var missionDock = Box(gameplay, "mission-dock", "mission-dock");
            missionCard = Box(missionDock, "mission", "mission");
            objective = Text(missionCard, "", "objective"); objective.name = "objective";
            objective.enableRichText = true;
            var tools = Box(top, "tools", "hud-tools");
            Button(tools, "观察簿  J", ToggleJournal); Button(tools, "暂停  Esc", TogglePause);
            questMarker=Box(gameplay,"quest-marker","quest-marker");questMarker.pickingMode=PickingMode.Ignore;
            questArrow=Text(questMarker,"▲","quest-arrow");questDistance=Text(questMarker,"","quest-distance");
            var bottom = Box(gameplay, "bottom", "hud-bottom");
            captionCard = Box(bottom, "caption-card", "caption-card"); caption = Text(captionCard, "", "caption");
            caption.name = "subtitle"; caption.enableRichText = false;
            dialogue = Box(gameplay, "dialogue", "dialogue-card");
            if(gobi!=null)dialogue.AddToClassList("gobi-chat");
            var dialogueScroll=new ScrollView{name="dialogue-scroll"};dialogueScroll.AddToClassList("dialogue-scroll");dialogue.Add(dialogueScroll);
            conversationRecipient=Text(dialogueScroll, "与眼前的人交谈", "panel-heading");conversationRecipient.name="conversation-recipient";
            var chat=Box(dialogueScroll,"free-dialogue","free-dialogue");
            textFallback=Box(chat,"text-fallback","text-fallback");
            chatInput=new TextField{maxLength=1200,name="chat-input"};chatInput.AddToClassList("chat-input");textFallback.Add(chatInput);
            chatInput.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Return&&!e.shiftKey){SendChat();e.StopPropagation();}});
            voiceStatus=Text(chat,"按住 F 说话，松开后自动提问；也可打字","voice-status");voiceStatus.name="voice-status";
            var chatActions=Box(textFallback,"chat-actions","chat-actions");sendChat=Button(chatActions,"发送",SendChat,"primary");sendChat.name="send-chat";
            Button(chatActions,"结束交谈  Esc",()=>journey.CloseDialogue());
            continueAudience=Button(dialogueScroll,"继续演出",()=>journey.ContinueScriptedAudience(),"primary");continueAudience.name="continue-scripted-audience";
            dialogueScroll.Insert(1,chat);
            if(dialogueDirector!=null)dialogueDirector.Changed+=RefreshChat;
            if(microphone!=null)microphone.TranscriptReady+=SetTranscript;
            var actions = Box(gameplay, "action-row", "action-row context-actions");
            observe = Button(actions, "E  记录所见", () => stage.RecordObservation(), "context-action");
            wakeTop=Box(gameplay,"wake-top","wake-lid wake-top");wakeBottom=Box(gameplay,"wake-bottom","wake-lid wake-bottom");
            wakeTop.pickingMode=PickingMode.Ignore;wakeBottom.pickingMode=PickingMode.Ignore;

            titleOverlay = Overlay("title-overlay");
            var card = Box(titleOverlay, "title-card", "menu-card title-card");
            Text(card, "T R I S O L A R I S   /   T H E   T R A V E L L E R", "eyebrow");
            Text(card, "三体 · 旅人", "game-title");
            Text(card, "在无法预测的世界里，带着自己的观察继续前行。", "menu-description");
            Button(card, "开始旅程", () => { hidden = false; journey.Begin(); }, "primary start-button").name = "start-journey";
            Button(card, "设置 · AI 与操作", OpenSettings).name = "title-settings";
            Text(card, "在荒漠中醒来，调查四周，寻找远处的火光。", "menu-description");
            Text(card, "WASD 行走 · 鼠标环顾 · F 语音交谈 · Enter 文字交谈 · Esc 菜单", "controls");

            pauseOverlay = Overlay("pause-overlay"); card = Box(pauseOverlay, "pause-card", "menu-card");
            Text(card, "旅程暂停", "menu-title"); Button(card, "继续旅程", TogglePause, "primary");
            Button(card, "设置 · AI 与操作", OpenSettings).name = "pause-settings";
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

            defeatOverlay=Overlay("defeat-overlay");card=Box(defeatOverlay,"defeat-card","menu-card");
            defeatTitle=Text(card,"你被卫兵制服了","menu-title");Text(card,"这一场觐见就此中断。","menu-description");
            Button(card,"重试本次觐见",()=>decisions.RetryAudience(),"primary").name="retry-audience";
            Button(card,"返回标题",()=>journey.ReturnToTitle());

            debugOverlay = Box(root, "debug-overlay", "debug-overlay");
            var debugHeader = Box(debugOverlay, "debug-header", "debug-header"); debugTitle = Text(debugHeader, "", "panel-heading");
            Button(debugHeader, "退出场景调试  F8", () => journey.TogglePreview());
            var sidebar = Box(debugOverlay, "debug-sidebar", "debug-sidebar"); Text(sidebar, "场景调试 · 天象时刻", "panel-heading");
            var scroll = new ScrollView(); scroll.AddToClassList("debug-scroll"); sidebar.Add(scroll);
            debugMoments = Box(scroll, "debug-moments", "choice-list"); debugScenes = Box(scroll, "debug-scenes", "choice-list");
            Button(sidebar, "回到观察位", stage.ResetView); Button(sidebar, "记录观察", () => stage.RecordObservation());
            SettingsPanel = new TravellerSettingsPanel(this, root, dialogueDirector.settings, Refresh, OnSettingsSaved);
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
            bool settingsOpen = SettingsPanel != null && SettingsPanel.IsOpen;
            bool debug = journey.IsPreviewing, modal = IsWorldPaused;
            Display(gameplay, !debug && !modal && !hidden); Display(titleOverlay, journey.IsTitle && !debug && !settingsOpen);
            Display(pauseOverlay, paused && !journalOpen && !settingsOpen); Display(journalOverlay, journalOpen && !settingsOpen); Display(summaryOverlay, journey.IsSummary && !journalOpen && !settingsOpen);
            Display(debugOverlay, debug && !journalOpen && !paused && !hidden && !settingsOpen);
            Display(travelOverlay, stage.IsLoading); travelTitle.text = stage.DestinationTitle ?? "";
            Display(defeatOverlay,decisions!=null&&decisions.IsDefeated&&!stage.IsLoading&&!journey.IsTitle);
            defeatTitle.text=journey.Decisions.Lethal?"你被卫兵杀死了":"你被卫兵制服了";
            SyncPlayerInput();
            chapter.text = stage.profile.chapter;
            objective.text = TravellerHudText.Objective(journey.Objective); Display(missionCard, !string.IsNullOrEmpty(journey.Objective));
            UpdateSubtitle();
            Display(dialogue, journey.DialogueOpen); Display(observe, journey.CanObserve && !journey.DialogueOpen);
            if(!journey.DialogueOpen&&chatInput!=null){textChatOpen=false;chatInput.Blur();}
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
            Display(textFallback,TextChatVisible);
            Display(continueAudience,journey.CanContinueScriptedAudience&&!dialogueDirector.IsBusy);
            sendChat.SetEnabled(dialogueDirector!=null&&!dialogueDirector.IsBusy&&(microphone==null||!microphone.IsRecording&&!microphone.IsTranscribing));
            SyncPlayerInput();
        }

        public void OpenSettings()
        {
            if (SettingsPanel == null || stage.IsLoading) return;
            microphone?.CancelCapture(); dialogueDirector?.CancelQuestion(); journey.CloseDialogue();
            SettingsPanel.Open(); Refresh();
        }
        private void OnSettingsSaved()
        {
            microphone?.CancelCapture(); dialogueDirector?.CancelQuestion();
            ShowNotice("设置已保存。API Key 仅在本次运行中使用。");
        }
        public void BeginTextChat()
        {
            if (IsWorldPaused || journey.IsPreviewing) return;
            microphone?.CancelCapture();
            if (dialogueDirector == null || !dialogueDirector.BeginPlayerQuestion())
            { ShowNotice("先靠近并看向可以交谈的人物。"); return; }
            textChatOpen = true; RefreshChat(); chatInput.Focus(); SyncPlayerInput();
        }
        private void SyncPlayerInput()
        {
            if (journey == null || stage.player == null) return;
            stage.player.InputBlocked = IsWorldPaused || journey.Progress.Step == JourneyStep.Awakening || TextChatVisible || IsEditingText || exposure != null && exposure.IsRecovering;
            stage.player.PointerReleased = IsWorldPaused || journey.IsPreviewing || TextChatVisible || IsEditingText;
            if (stage.player.PointerReleased) stage.player.ReleasePointer();
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
        public void HandleEscape()
        {
            if(decisions!=null&&decisions.IsDefeated)return;
            if (SettingsPanel != null && SettingsPanel.IsOpen) { SettingsPanel.Cancel(); return; }
            if (journalOpen) { ToggleJournal(); return; }
            if (TextChatVisible || IsEditingText) { journey.CloseDialogue(); return; }
            if (journey.DialogueOpen) journey.CloseDialogue();
            TogglePause();
        }
        private void ShowNotice(string text) { notice.text = text; Display(notice, true); noticeUntil = Time.unscaledTime + 3.5f; }
        public void NotifyDialogueFailure(string text) { ShowNotice(text); noticeUntil=Time.unscaledTime+10; }

        private void Update()
        {
            if (root == null) return;
            bool defeated=decisions!=null&&decisions.IsDefeated;
            bool available=journey.CanContinueScriptedAudience;
            if(defeated!=wasDefeated||available!=wasScriptedAvailable){wasDefeated=defeated;wasScriptedAvailable=available;Refresh();}
            bool editing=IsEditingText;
            SyncPlayerInput();
            if(defeated)return;
            if (SettingsPanel.IsOpen)
            { if (Input.GetKeyDown(KeyCode.Escape)) HandleEscape(); return; }
            bool waking=gobi!=null&&journey.Progress.Step==JourneyStep.Awakening&&!journey.IsPreviewing;
            Display(wakeTop,waking);Display(wakeBottom,waking);
            if(waking){float closed=50*(1-Mathf.SmoothStep(0,1,gobi.WakeProgress));wakeTop.style.height=Length.Percent(closed);wakeBottom.style.height=Length.Percent(closed);}
            UpdateQuestMarker();
            UpdateSubtitle();
            if (!editing && Input.GetKeyDown(KeyCode.Return) && !IsWorldPaused) BeginTextChat();
            if (!editing && Input.GetKeyDown(KeyCode.J)) ToggleJournal();
            if (Input.GetKeyDown(KeyCode.Escape)) HandleEscape();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!journalOpen && Input.GetKeyDown(KeyCode.F8)) { paused = false; hidden = false; journey.TogglePreview(); }
#endif
            if (!editing && !journey.IsTitle && !journey.IsSummary && !paused && Input.GetKeyDown(KeyCode.H)) { hidden = !hidden; Refresh(); }
            if (noticeUntil > 0f && Time.unscaledTime > noticeUntil) { Display(notice, false); noticeUntil = 0f; }
            if (Time.unscaledTime >= nextNavigationUpdate)
            {
                nextNavigationUpdate = Time.unscaledTime + 0.25f;
                navigation.text = journey.HasNavigation ? "沿绿色箭头前行  ·  " + Vector3.Distance(stage.player.transform.position,journey.Goal).ToString("0") + " 米\nWASD 行走 · 鼠标环顾 · Esc 菜单" : journey.CanObserve ? "E 记录观察  ·  F 语音 / Enter 文字交谈" : journey.Progress.Step==JourneyStep.OpeningTalk||journey.Progress.Step==JourneyStep.ShelterTalk||journey.Progress.Step==JourneyStep.CourtTalk||journey.Progress.Step==JourneyStep.TribunalTalk?"看向人物 · F 按住说话 · Enter 打字":"";
                Display(helpCard, !string.IsNullOrEmpty(navigation.text));
            }
        }

        private void UpdateSubtitle()
        {
            string value = journey.Speech;
            Display(captionCard, !string.IsNullOrEmpty(value));
            if (value != displayedSpeech || displayedSpeechVersion != journey.SpeechVersion)
            {
                displayedSpeech = value; displayedSpeechVersion = journey.SpeechVersion; subtitlePage = 0;
                float width = captionCard.resolvedStyle.width;
                if (float.IsNaN(width) || width < 100) width = Mathf.Min(1040, root.layout.width * .78f);
                float size = caption.resolvedStyle.fontSize;
                int budget = Mathf.Clamp(Mathf.FloorToInt((width - 36) / Mathf.Max(17, size) * 1.65f), 36, 76);
                subtitlePages = TravellerHudText.SubtitlePages(value, budget);
                caption.text = subtitlePages.Count > 0 ? subtitlePages[0] : "";
                ScheduleSubtitlePage();
            }
            if (IsWorldPaused || subtitlePage + 1 >= subtitlePages.Count || Time.unscaledTime < subtitleNextAt) return;
            subtitlePage++; caption.text = subtitlePages[subtitlePage]; ScheduleSubtitlePage();
        }
        private void ScheduleSubtitlePage()
        {
            int remaining = 0; for (int i = subtitlePage; i < subtitlePages.Count; i++) remaining += subtitlePages[i].Length;
            float part = remaining > 0 ? (float)subtitlePages[subtitlePage].Length / remaining : 1;
            subtitleNextAt = Time.unscaledTime + Mathf.Max(.65f, journey.SpeechRemainingSeconds * part);
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

        private void OnDestroy() { SettingsPanel?.Dispose(); if (stage != null) { stage.Changed -= Refresh; stage.Notice -= ShowNotice; } if (journey != null) journey.Changed -= Refresh;if(dialogueDirector!=null)dialogueDirector.Changed-=RefreshChat;if(microphone!=null)microphone.TranscriptReady-=SetTranscript; }
        private static VisualElement Box(VisualElement parent, string name, string classes) { var v = new VisualElement { name = name }; foreach (var c in classes.Split(' ')) v.AddToClassList(c); parent.Add(v); return v; }
        private static Label Text(VisualElement parent, string value, string cls) { var v = new Label(value) { pickingMode = PickingMode.Ignore }; v.AddToClassList(cls); parent.Add(v); return v; }
        private static Button Button(VisualElement parent, string text, System.Action action, string classes = "") { var v = new Button(action) { text = text }; v.AddToClassList("button"); foreach (var c in classes.Split(' ')) if (c.Length > 0) v.AddToClassList(c); parent.Add(v); return v; }
    }
}
