using System;
using UnityEngine;

namespace ThreeBodyProblem
{
    [Serializable]
    public sealed class NpcApiConnection
    {
        public string mode = "gateway";
        public string apiUrl = "https://api.deepseek.com/chat/completions";
        public string model = "deepseek-flash";
        public string apiKey = "";
        public int timeoutSeconds = 20;
        public int contextWindowTokens = 32000;

        public NpcApiConnection Copy() => (NpcApiConnection)MemberwiseClone();

        public static bool TryEndpoint(string value, out string endpoint, out string error)
        {
            endpoint = null; error = null;
            Uri uri;
            if (!Uri.TryCreate((value ?? "").Trim(), UriKind.Absolute, out uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http") || string.IsNullOrEmpty(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            { error = "请输入有效的 http / https API 地址，不要在地址中填写 Key。"; return false; }
            var builder = new UriBuilder(uri);
            string path = uri.AbsolutePath.TrimEnd('/');
            if (!path.EndsWith("/chat/completions", StringComparison.Ordinal))
            {
                if (path.Length == 0 && !uri.Host.Equals("api.deepseek.com", StringComparison.OrdinalIgnoreCase)) path = "/v1";
                path += "/chat/completions";
            }
            builder.Path = path; endpoint = builder.Uri.AbsoluteUri; return true;
        }

        public bool Validate(out string error)
        {
            error = null;
            if (contextWindowTokens != 32000 && contextWindowTokens != 128000 && contextWindowTokens != 1000000)
            { error = "上下文长度请选择 32K、128K 或 1M。"; return false; }
            if (mode != "gateway" && mode != "scripted" && mode != "deepseek" && mode != "custom")
            { error = "请选择对话模式。"; return false; }
            if (mode == "gateway" || mode == "scripted") return true;
            string endpoint;
            if (!TryEndpoint(apiUrl, out endpoint, out error)) return false;
            if (string.IsNullOrWhiteSpace(model) || model.Trim().Length > 160 || model.Contains("\n") || model.Contains("\r"))
            { error = "请填写服务商提供的模型名称。"; return false; }
            if ((apiKey ?? "").Length > 4096 || (apiKey ?? "").Contains("\n") || (apiKey ?? "").Contains("\r"))
            { error = "API Key 格式不正确，请检查是否包含换行。"; return false; }
            if (timeoutSeconds < 5 || timeoutSeconds > 600)
            { error = "等待时间需在 5–600 秒之间。"; return false; }
            return true;
        }
    }

    [Serializable]
    public sealed class TravellerPreferences
    {
        public NpcApiConnection connection = new NpcApiConnection();
        public string gatewayUrl = "";
        public float mouseSensitivity = 2;
        public bool invertMouseY;
        public bool speakReplies = true;
        public bool microphoneEnabled = true;
        public string microphoneDevice = "";

        // Deliberately has no credential field: only the in-memory connection
        // is sent to the local service. Keys never enter PlayerPrefs or assets.
        [Serializable] private sealed class Saved
        {
            public string mode = "gateway", apiUrl = "https://api.deepseek.com/chat/completions", model = "deepseek-flash", gatewayUrl = "";
            public int timeoutSeconds = 20;
            public int contextWindowTokens = 32000;
            public float mouseSensitivity = 2;
            public bool invertMouseY, speakReplies = true;
            // Missing fields in older JSON must not disable a previously working microphone.
            public int microphonePreferencesVersion;
            public bool microphoneEnabled;
            public string microphoneDevice;
        }

        public TravellerPreferences Copy()
        {
            var value = (TravellerPreferences)MemberwiseClone(); value.connection = connection.Copy(); return value;
        }

        public bool Validate(out string error)
        {
            if (connection == null) { error = "对话配置为空。"; return false; }
            if (!connection.Validate(out error)) return false;
            if (!string.IsNullOrWhiteSpace(gatewayUrl))
            {
                Uri uri;
                if (!Uri.TryCreate(gatewayUrl.Trim(), UriKind.Absolute, out uri) || !uri.IsLoopback ||
                    (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
                    !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                { error = "本地对话服务请填写 localhost 或 127.0.0.1 地址。"; return false; }
            }
            if (float.IsNaN(mouseSensitivity) || float.IsInfinity(mouseSensitivity) || mouseSensitivity < .2f || mouseSensitivity > 5)
            { error = "鼠标灵敏度需在 0.2–5 之间。"; return false; }
            if ((microphoneDevice ?? "").Length > 512 || (microphoneDevice ?? "").IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            { error = "麦克风名称无效，请重新选择设备。"; return false; }
            error = null; return true;
        }

        public string GatewayUrl(NpcDialogueSettings defaults) =>
            (string.IsNullOrWhiteSpace(gatewayUrl) ? defaults.gatewayUrl : gatewayUrl).Trim().TrimEnd('/');

        public string ToSavedJson() => JsonUtility.ToJson(new Saved {
            mode = connection.mode, apiUrl = connection.apiUrl, model = connection.model,
            timeoutSeconds = connection.timeoutSeconds, contextWindowTokens = connection.contextWindowTokens, gatewayUrl = gatewayUrl,
            mouseSensitivity = mouseSensitivity, invertMouseY = invertMouseY, speakReplies = speakReplies,
            microphonePreferencesVersion = 1, microphoneEnabled = microphoneEnabled, microphoneDevice = microphoneDevice ?? ""
        });

        public static TravellerPreferences FromSavedJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new TravellerPreferences();
            try
            {
                var saved = JsonUtility.FromJson<Saved>(json);
                if (saved == null) return new TravellerPreferences();
                var value = new TravellerPreferences {
                    connection = new NpcApiConnection { mode = saved.mode, apiUrl = saved.apiUrl, model = saved.model, timeoutSeconds = saved.timeoutSeconds,
                        contextWindowTokens = saved.contextWindowTokens > 0 ? saved.contextWindowTokens : 32000 },
                    gatewayUrl = saved.gatewayUrl, mouseSensitivity = saved.mouseSensitivity,
                    invertMouseY = saved.invertMouseY, speakReplies = saved.speakReplies,
                    microphoneEnabled = saved.microphonePreferencesVersion < 1 || saved.microphoneEnabled,
                    microphoneDevice = saved.microphoneDevice ?? ""
                };
                string error; return value.Validate(out error) ? value : new TravellerPreferences();
            }
            catch (ArgumentException) { return new TravellerPreferences(); }
        }
    }

    public static class TravellerUserSettings
    {
        public const string StorageKey = "ThreeBodyTraveller.Preferences.v1";
        private static TravellerPreferences current;
        public static TravellerPreferences Current => current ?? (current = TravellerPreferences.FromSavedJson(PlayerPrefs.GetString(StorageKey, "")));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void NewRun() { current = null; }

        public static bool Save(TravellerPreferences value, out string error)
        {
            if (!value.Validate(out error)) return false;
            var copy = value.Copy();
            copy.gatewayUrl = (copy.gatewayUrl ?? "").Trim().TrimEnd('/');
            copy.connection.apiUrl = (copy.connection.apiUrl ?? "").Trim();
            copy.connection.model = (copy.connection.model ?? "").Trim();
            copy.connection.apiKey = (copy.connection.apiKey ?? "").Trim();
            PlayerPrefs.SetString(StorageKey, copy.ToSavedJson()); PlayerPrefs.Save(); current = copy; return true;
        }
    }
}
