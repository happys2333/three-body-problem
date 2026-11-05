#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace ThreeBodyProblem
{
    public sealed class SettingsPlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public bool hardwareMicrophoneUsed;
            public int simulatedMicrophoneStarts;
            public int checks;
            public List<string> errors = new List<string>(), resolutions = new List<string>();
        }
        private readonly Report report = new Report();
        private string output, storedPreferences, storedJournal;
        private bool hadPreferences, hadJournal, captured;
        private TravellerStage stage;
        private TravellerHud hud;
        private TravellerJourney journey;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Option("-settings-player-check") == null || FindFirstObjectByType<SettingsPlayerCheck>() != null) return;
            var root = new GameObject("Development settings and first-person verification"); DontDestroyOnLoad(root);
            root.AddComponent<SettingsPlayerCheck>().output = Option("-settings-player-check");
        }
        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog; Application.runInBackground = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            hadPreferences = PlayerPrefs.HasKey(TravellerUserSettings.StorageKey); storedPreferences = PlayerPrefs.GetString(TravellerUserSettings.StorageKey, "");
            hadJournal = PlayerPrefs.HasKey("ThreeBodyTraveller.Journal.v1"); storedJournal = PlayerPrefs.GetString("ThreeBodyTraveller.Journal.v1", ""); captured = true;
            yield return Frames(15); Bind();
            yield return Until(() => Application.isFocused, 45);
            Check(journey.IsTitle && Cursor.lockState == CursorLockMode.None, "The title should leave the pointer available.");
            Click(hud.Root.Q<Button>("title-settings")); yield return Frames(3);
            Check(hud.SettingsPanel.IsOpen && hud.IsWorldPaused, "Title settings did not open.");
            var ui = hud.SettingsPanel.Root;
            Check(ui.Q<TextField>("settings-api-key").isPasswordField, "API key is not masked by default.");
            ui.Q<DropdownField>("settings-provider").index = 1;
            foreach (var size in new[] { new Vector2Int(1600,900),new Vector2Int(1280,720),new Vector2Int(960,540),new Vector2Int(1024,768) })
            {
                Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);yield return Frames(12);
                report.resolutions.Add(Screen.width+"x"+Screen.height);
                CheckInside(ui.Q("settings-card")); CheckInside(ui.Q("settings-save")); CheckInside(ui.Q("settings-test"));
                Capture("Settings_"+Screen.width+"x"+Screen.height); yield return Frames(3);
            }
            Screen.SetResolution(1600,900,FullScreenMode.Windowed);yield return Frames(8);
            string originalModel = TravellerUserSettings.Current.connection.model;
            ui.Q<TextField>("settings-model").value = "unsaved-model";
            Click(ui.Q<Button>("settings-cancel")); yield return Frames(3);
            Check(!hud.SettingsPanel.IsOpen && TravellerUserSettings.Current.connection.model == originalModel, "Cancel applied an unsaved model.");

            hud.OpenSettings(); ui = hud.SettingsPanel.Root;
            ui.Q<DropdownField>("settings-provider").index = 2;
            ui.Q<TextField>("settings-api-url").value = Option("-settings-test-upstream");
            ui.Q<TextField>("settings-model").value = "qa-selected-model";
            ui.Q<TextField>("settings-api-key").value = "test-session-only-key";
            ui.Q<TextField>("settings-gateway-url").value = Option("-settings-test-gateway");
            ui.Q<IntegerField>("settings-timeout").value = 5;
            var context = ui.Q<DropdownField>("settings-context-window");
            Check(context.choices.SequenceEqual(new[] { "32K", "128K", "512K", "1M" }), "Unexpected context size choices.");
            context.value = "512K";
            ui.Q<Toggle>("settings-speak-replies").value = false;
            Click(ui.Q<Button>("settings-test"));
            yield return Until(() => ui.Q<Button>("settings-test").enabledSelf, 12);
            Check(ui.Q<Label>("settings-status").text.Contains("连接成功"), "The gateway did not accept the 512K context setting.");
            context.value = "1M";
            ui.Q<TextField>("settings-api-key").value = "bad-test-key";
            Click(ui.Q<Button>("settings-test"));yield return Until(() => ui.Q<Button>("settings-test").enabledSelf, 12);
            Check(ui.Q<Label>("settings-status").text.Contains("认证失败")&&!ui.Q<Label>("settings-status").text.Contains("bad-test-key"), "Bad credentials were accepted or exposed.");
            Capture("Connection_Failure");yield return Frames(3);
            ui.Q<TextField>("settings-api-key").value = "test-session-only-key";
            ui.Q<TextField>("settings-api-url").value = "not-a-url";
            Click(ui.Q<Button>("settings-save"));yield return Frames(2);
            Check(hud.SettingsPanel.IsOpen, "Invalid API URL closed the settings form.");
            ui.Q<TextField>("settings-api-url").value = Option("-settings-test-upstream");
            Click(ui.Q<Button>("settings-save"));yield return Frames(3);
            Check(!hud.SettingsPanel.IsOpen, "Valid settings were not saved.");
            Check(TravellerUserSettings.Current.connection.model == "qa-selected-model", "The selected model was not applied.");
            Check(TravellerUserSettings.Current.connection.contextWindowTokens == 1000000, "The 1M context setting was not applied.");
            Check(TravellerUserSettings.Current.connection.apiKey == "test-session-only-key", "The key was lost before the game request.");
            string persisted = PlayerPrefs.GetString(TravellerUserSettings.StorageKey);
            Check(!persisted.Contains("test-session-only-key")&&!persisted.Contains("apiKey"), "The API key was persisted.");

            journey.Begin(); journey.Progress.WakeUp(); yield return Frames(8);
            Check(Application.isFocused, "Native player is not focused for mouse capture verification.");
            Check(!Input.GetMouseButton(1)&&!stage.player.InputBlocked&&Cursor.lockState==CursorLockMode.Locked&&!Cursor.visible, "First-person look still requires a held mouse button.");
            var rotation = stage.player.transform.rotation;stage.player.LookInput(new Vector2(9,2));
            Check(Quaternion.Angle(rotation,stage.player.transform.rotation)>5,"Free first-person look did not rotate the view.");
            hud.HandleEscape();yield return Frames(3);
            Check(hud.IsWorldPaused&&Cursor.lockState==CursorLockMode.None&&Cursor.visible,"Pause did not release the pointer.");
            rotation=stage.player.transform.rotation;stage.player.LookInput(new Vector2(10,10));
            Check(Quaternion.Angle(rotation,stage.player.transform.rotation)<.01f,"The camera moved while a menu was open.");
            Click(hud.Root.Q<Button>("pause-settings"));yield return Frames(3);
            float elapsed=journey.Progress.Elapsed;yield return Delay(.5f);
            Check(hud.SettingsPanel.IsOpen&&Mathf.Abs(journey.Progress.Elapsed-elapsed)<.01f,"Settings do not pause the story.");
            hud.SettingsPanel.Root.Q<TextField>("settings-model").value="discard-this-edit";
            hud.SettingsPanel.Root.Q<TextField>("settings-model").Focus();yield return Frames(2);
            hud.HandleEscape();yield return Frames(3);
            Check(!hud.SettingsPanel.IsOpen&&TravellerUserSettings.Current.connection.model=="qa-selected-model","Esc from a settings text field did not discard edits.");
            hud.HandleEscape();yield return Frames(3);
            Check(!hud.IsWorldPaused&&Cursor.lockState==CursorLockMode.Locked,"A hidden settings text field retained keyboard focus.");
            hud.HandleEscape();hud.OpenSettings();yield return Frames(3);
            Click(hud.SettingsPanel.Root.Q<Button>("settings-controls-tab"));yield return Frames(3);Capture("Controls_Settings");yield return Frames(3);
            hud.SettingsPanel.Root.Q<Slider>("settings-sensitivity").value=3;
            Click(hud.SettingsPanel.Root.Q<Button>("settings-cancel"));yield return Frames(3);
            Check(hud.IsWorldPaused&&hud.Root.Q("pause-overlay").resolvedStyle.display==DisplayStyle.Flex,"Closing settings skipped the pause menu.");
            var resume=hud.Root.Q("pause-card").Query<Button>().ToList().First(b=>b.text=="继续旅程");Click(resume);yield return Frames(5);
            Check(!hud.IsWorldPaused&&Cursor.lockState==CursorLockMode.Locked,"Resume failed to capture the mouse.");

            var companion=stage.GetComponent<TravellerCastDirector>().companion;
            stage.player.Teleport(companion.transform.position+companion.transform.forward*2.3f+Vector3.up*1.65f,companion.transform.position+Vector3.up*1.45f);
            journey.Progress.DiscoverTraveller();journey.Progress.MeetTraveller();yield return Frames(4);
            var director=stage.GetComponent<NpcDialogueDirector>();director.EnsureVoiceConversation();yield return Frames(3);
            Check(!stage.player.InputBlocked&&Cursor.lockState==CursorLockMode.Locked,"Voice-only conversation prevents normal first-person control.");
            hud.HandleEscape();yield return Frames(3);
            Check(hud.IsWorldPaused&&!journey.DialogueOpen&&Cursor.lockState==CursorLockMode.None,"Esc during voice dialogue does not open the menu.");
            hud.HandleEscape();yield return Frames(3);
            Check(!hud.IsWorldPaused&&Cursor.lockState==CursorLockMode.Locked,"Esc cannot resume after voice dialogue.");
            hud.BeginTextChat();yield return Frames(4);
            Check(journey.DialogueOpen&&stage.player.InputBlocked&&Cursor.lockState==CursorLockMode.None,"Text chat did not release the pointer and block movement.");
            Check(hud.Root.Q("text-fallback").resolvedStyle.display==DisplayStyle.Flex,"Text chat cannot be opened without a microphone failure.");
            hud.Root.Q<TextField>("chat-input").value="这里安全吗？";Click(hud.Root.Q<Button>("send-chat"));
            yield return Until(()=>!director.IsBusy,12);
            Check(!director.UsingFallback&&!string.IsNullOrEmpty(director.LastReply),"NPC dialogue did not use the applied API/model/key.");
            Capture("Configured_Npc_Text_Chat");yield return Frames(3);
            hud.HandleEscape();yield return Frames(5);
            Check(!stage.player.InputBlocked&&Cursor.lockState==CursorLockMode.Locked,"Closing text chat did not restore first-person control.");
            Capture("FirstPerson_Gameplay");yield return Frames(3);
            var load=SceneManager.LoadSceneAsync(TravellerStage.SceneNames[0]);while(!load.isDone)yield return null;
            yield return Frames(12);Bind();hud.OpenSettings();yield return Frames(3);
            Check(hud.SettingsPanel.Root.Q<TextField>("settings-model").value=="qa-selected-model"&&
                  hud.SettingsPanel.Root.Q<TextField>("settings-api-key").value=="test-session-only-key", "Scene reload lost session settings.");
            Check(TravellerPreferences.FromSavedJson(persisted).connection.apiKey=="","A fresh preference load recovered a secret.");
            Check(TravellerPreferences.FromSavedJson(persisted).connection.contextWindowTokens==1000000,"The long-context setting was not persisted.");
            yield return VerifyMicrophoneSettings();
            yield return VerifyGraphicsSettings();
            Finish();
        }
        private IEnumerator VerifyGraphicsSettings()
        {
            hud.OpenSettings();
            var ui = hud.SettingsPanel.Root;
            Click(ui.Q<Button>("settings-graphics-tab"));
            yield return Frames(3);
            var mode = ui.Q<DropdownField>("settings-display-mode");
            var size = ui.Q<DropdownField>("settings-resolution");
            var quality = ui.Q<DropdownField>("settings-graphics-quality");
            Check(ui.Q("settings-graphics").resolvedStyle.display == DisplayStyle.Flex &&
                  ui.Q<Button>("settings-test").resolvedStyle.display == DisplayStyle.None &&
                  size.choices.Contains("1280 × 720") && size.choices.Contains("1600 × 900"), "Graphics options were not available.");
            CheckInside(ui.Q("settings-card"));
            Capture("Display_And_Graphics");
            yield return Frames(3);

            mode.value = "窗口";
            size.value = "1280 × 720";
            quality.value = "低";
            hud.SettingsPanel.Apply();
            yield return Until(() => Screen.width == 1280 && Screen.height == 720, 6);
            Check(Screen.width == 1280 && Screen.height == 720 && Screen.fullScreenMode == FullScreenMode.Windowed &&
                  QualitySettings.names[QualitySettings.GetQualityLevel()] == "Low" && QualitySettings.shadows == ShadowQuality.Disable,
                  "Windowed resolution or the Low graphics preset was not applied.");
            hud.OpenSettings();
            Click(ui.Q<Button>("settings-graphics-tab"));
            yield return Frames(3);
            Check(mode.value == "窗口" && size.value == "1280 × 720" && quality.value == "低", "Saved graphics choices did not reopen.");
            mode.value = "全屏";
            hud.SettingsPanel.Apply();
            yield return Until(() => Screen.fullScreenMode == FullScreenMode.FullScreenWindow, 6);
            var stored = TravellerPreferences.FromSavedJson(PlayerPrefs.GetString(TravellerUserSettings.StorageKey));
            Check(Screen.fullScreenMode == FullScreenMode.FullScreenWindow && stored.fullscreen &&
                  stored.displayWidth == 1280 && stored.displayHeight == 720 && stored.graphicsQuality == TravellerGraphicsQuality.Low,
                  "Full screen was not applied and saved.");
            report.resolutions.Add(Screen.width + "x" + Screen.height + " fullscreen");

            hud.OpenSettings();
            Click(ui.Q<Button>("settings-graphics-tab"));
            mode.value = "窗口";
            size.value = "1600 × 900";
            quality.value = "默认";
            hud.SettingsPanel.Apply();
            yield return Until(() => Screen.fullScreenMode == FullScreenMode.Windowed && Screen.width == 1600 && Screen.height == 900, 6);
            var gobi = stage.GetComponent<GobiSequenceController>();
            Check(Screen.fullScreenMode == FullScreenMode.Windowed && Screen.width == 1600 && Screen.height == 900 &&
                  Mathf.Approximately(QualitySettings.shadowDistance, gobi.settings.shadowDistance), "Default Gobi quality or windowed mode was not restored.");
            QualitySettings.vSyncCount = 0;
        }
        private sealed class SimulatedMicrophone : IMicrophoneCaptureSource
        {
            public string[] devices={"QA Built-in Microphone","QA USB Microphone"};
            public bool authorized=true,holdPermission,releasePermission;
            public int starts,ends,requests,permissionChecks,enumerations;
            public string lastStarted,lastEnded;
            public string[] Devices { get { enumerations++;return devices; } }
            public bool HasPermission { get { permissionChecks++;return authorized; } }
            public IEnumerator RequestPermission(){requests++;while(holdPermission&&!releasePermission)yield return null;if(releasePermission)authorized=true;}
            public AudioClip Start(string device){starts++;lastStarted=device;return AudioClip.Create("Synthetic microphone verification",16000,1,16000,false);}
            public int GetPosition(string device)=>16000;
            public void End(string device){ends++;lastEnded=device;}
        }
        private IEnumerator VerifyMicrophoneSettings()
        {
            hud.SettingsPanel.Cancel();
            var mic=stage.GetComponent<PlayerMicrophoneInput>();var input=new SimulatedMicrophone();mic.Source=input;
            hud.OpenSettings();var ui=hud.SettingsPanel.Root;
            Check(input.enumerations==0&&input.requests==0&&input.starts==0,"Opening AI settings unexpectedly accessed microphone capture.");
            Click(ui.Q<Button>("settings-controls-tab"));yield return Frames(3);
            var enabled=ui.Q<Toggle>("settings-microphone-enabled");var devices=ui.Q<DropdownField>("settings-microphone-device");
            enabled.value=true;Click(ui.Q<Button>("settings-refresh-microphones"));yield return Frames(2);
            Check(devices.choices.Contains("QA USB Microphone")&&devices.choices.Contains("系统默认麦克风"),"Microphone device choices are missing.");
            bool previous=TravellerUserSettings.Current.microphoneEnabled;string previousDevice=TravellerUserSettings.Current.microphoneDevice;
            enabled.value=false;Check(!devices.enabledSelf&&!ui.Q<Button>("settings-refresh-microphones").enabledSelf,"Disabling microphone did not disable its controls.");
            hud.SettingsPanel.Cancel();
            Check(TravellerUserSettings.Current.microphoneEnabled==previous&&TravellerUserSettings.Current.microphoneDevice==previousDevice,"Cancel applied an unsaved microphone choice.");
            hud.OpenSettings();Click(ui.Q<Button>("settings-controls-tab"));enabled.value=true;
            devices.index=devices.choices.IndexOf("QA USB Microphone");
            foreach(var size in new[]{new Vector2Int(1600,900),new Vector2Int(960,540)})
            {
                Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);yield return Frames(10);
                ui.Q<ScrollView>("settings-scroll").ScrollTo(devices);yield return Frames(3);
                CheckInside(devices);CheckInside(ui.Q("settings-save"));Capture("Microphone_Settings_"+Screen.width+"x"+Screen.height);yield return Frames(3);
            }
            Screen.SetResolution(1600,900,FullScreenMode.Windowed);yield return Frames(8);hud.SettingsPanel.Apply();
            var loaded=TravellerPreferences.FromSavedJson(PlayerPrefs.GetString(TravellerUserSettings.StorageKey));
            Check(loaded.microphoneEnabled&&loaded.microphoneDevice=="QA USB Microphone","The explicit microphone choice was not persisted.");
            Check(loaded.connection.contextWindowTokens==1000000&&TravellerUserSettings.Current.connection.apiKey=="test-session-only-key","Saving microphone settings changed the AI configuration.");
            journey.Begin();journey.Progress.WakeUp();yield return Frames(5);
            var companion=stage.GetComponent<TravellerCastDirector>().companion;
            stage.player.Teleport(companion.transform.position+companion.transform.forward*2.3f+Vector3.up*1.65f,companion.transform.position+Vector3.up*1.45f);
            journey.Progress.DiscoverTraveller();journey.Progress.MeetTraveller();yield return Frames(3);
            yield return mic.BeginCapture();yield return Frames(2);
            Check(mic.IsRecording&&input.lastStarted=="QA USB Microphone","Recording did not start on the chosen USB device.");
            hud.OpenSettings();Check(!mic.IsRecording&&input.lastEnded=="QA USB Microphone","Opening settings did not stop the original capture device.");
            Click(ui.Q<Button>("settings-controls-tab"));devices.index=0;hud.SettingsPanel.Apply();
            yield return mic.BeginCapture();yield return Frames(2);
            Check(mic.IsRecording&&input.lastStarted==null,"System default did not route to Unity's default device.");mic.CancelCapture();journey.CloseDialogue();

            hud.OpenSettings();Click(ui.Q<Button>("settings-controls-tab"));enabled.value=false;hud.SettingsPanel.Apply();
            int starts=input.starts,requests=input.requests,permissionChecks=input.permissionChecks,enumerations=input.enumerations;
            yield return mic.BeginCapture();yield return Frames(3);
            Check(!mic.IsRecording&&!mic.AwaitingPermission&&input.starts==starts&&input.requests==requests&&input.permissionChecks==permissionChecks&&input.enumerations==enumerations,"A disabled microphone accessed devices, permission, or capture.");
            Check(journey.DialogueOpen&&hud.Root.Q("text-fallback").resolvedStyle.display==DisplayStyle.Flex,"Disabled microphone did not allow text conversation.");
            journey.CloseDialogue();

            hud.OpenSettings();Click(ui.Q<Button>("settings-controls-tab"));enabled.value=true;hud.SettingsPanel.Apply();
            input.authorized=false;input.holdPermission=true;
            mic.StartCoroutine(mic.BeginCapture());yield return Frames(3);
            Check(mic.AwaitingPermission,"Microphone permission request did not enter the waiting state.");
            hud.OpenSettings();input.releasePermission=true;yield return Frames(3);
            Check(!mic.IsRecording&&!mic.AwaitingPermission&&input.starts==starts,"Cancelled permission later began recording.");
            hud.SettingsPanel.Cancel();input.holdPermission=false;input.releasePermission=false;input.authorized=false;
            yield return mic.BeginCapture();yield return Frames(3);
            Check(!mic.IsRecording&&mic.NeedsTextFallback,"Permission denial did not preserve text fallback.");journey.CloseDialogue();input.authorized=true;

            hud.OpenSettings();Click(ui.Q<Button>("settings-controls-tab"));devices.index=devices.choices.IndexOf("QA USB Microphone");hud.SettingsPanel.Apply();
            input.devices=new[]{"QA Built-in Microphone"};hud.OpenSettings();Click(ui.Q<Button>("settings-controls-tab"));
            Check(devices.value.Contains("不可用")&&devices.value.Contains("QA USB Microphone"),"An unplugged chosen device was silently replaced.");hud.SettingsPanel.Apply();
            yield return mic.BeginCapture();yield return Frames(3);
            Check(!mic.IsRecording&&input.starts==starts&&mic.NeedsTextFallback,"A missing chosen device recorded from another microphone.");journey.CloseDialogue();
            input.devices=new string[0];hud.OpenSettings();Click(ui.Q<Button>("settings-controls-tab"));devices.index=0;hud.SettingsPanel.Apply();
            yield return mic.BeginCapture();yield return Frames(3);
            Check(!mic.IsRecording&&input.starts==starts&&mic.NeedsTextFallback,"No-device capture did not return to text chat.");journey.CloseDialogue();

            input.devices=new[]{"QA Built-in Microphone","QA USB Microphone"};hud.OpenSettings();Click(ui.Q<Button>("settings-controls-tab"));
            Click(ui.Q<Button>("settings-refresh-microphones"));devices.index=devices.choices.IndexOf("QA USB Microphone");hud.SettingsPanel.Apply();
            var director=stage.GetComponent<NpcDialogueDirector>();
            int answers=journey.Progress.OpeningAnswers;
            yield return mic.BeginCapture();yield return Frames(2);mic.FinishCapture();
            Check(mic.IsTranscribing,"Synthetic capture did not reach the transcription request.");
            yield return Delay(.12f);
            mic.CancelCapture();yield return Delay(.7f);
            Check(!mic.IsTranscribing&&journey.Progress.OpeningAnswers==answers,"A cancelled transcription submitted an old player question.");journey.CloseDialogue();
            yield return mic.BeginCapture();yield return Frames(2);mic.FinishCapture();
            yield return Until(()=>!mic.IsTranscribing&&!director.IsBusy,15);
            Check(input.lastStarted=="QA USB Microphone"&&!director.UsingFallback&&journey.Progress.OpeningAnswers==answers+1,"The chosen microphone did not complete capture, transcription and NPC auto-send.");
            report.simulatedMicrophoneStarts=input.starts;journey.CloseDialogue();
        }
        private void Bind(){stage=FindFirstObjectByType<TravellerStage>();journey=stage.Journey;hud=stage.GetComponentInChildren<TravellerHud>();}
        private static void Click(Button button)
        { if(button==null)throw new InvalidOperationException("Missing settings button");button.Focus();using(var e=NavigationSubmitEvent.GetPooled())button.SendEvent(e); }
        private void CheckInside(VisualElement element)
        {var b=element.worldBound;var r=hud.Root.worldBound;Check(b.width>0&&b.height>0&&b.xMin>=r.xMin-2&&b.yMin>=r.yMin-2&&b.xMax<=r.xMax+2&&b.yMax<=r.yMax+2,"Settings outside viewport: "+element.name);}
        private void Check(bool value,string message){report.checks++;if(!value)report.errors.Add(message);}
        private void Capture(string name){ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),name+".png"));}
        private IEnumerator Until(Func<bool> ready,float timeout)
        {float end=Time.realtimeSinceStartup+timeout;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(ready(),"Settings operation timed out.");}
        private static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        private static IEnumerator Delay(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
        private static string Option(string name){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
        private void Restore()
        {
            if(!captured)return;captured=false;
            if(hadPreferences)PlayerPrefs.SetString(TravellerUserSettings.StorageKey,storedPreferences);else PlayerPrefs.DeleteKey(TravellerUserSettings.StorageKey);
            if(hadJournal)PlayerPrefs.SetString("ThreeBodyTraveller.Journal.v1",storedJournal);else PlayerPrefs.DeleteKey("ThreeBodyTraveller.Journal.v1");
            PlayerPrefs.Save();stage?.player?.ReleasePointer();
        }
        private void Finish(){Restore();report.passed=report.errors.Count==0;File.WriteAllText(output,JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);}
        private void OnDestroy(){Restore();Application.logMessageReceived-=OnLog;}
    }
}
#endif
