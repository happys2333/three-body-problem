using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ThreeBodyProblem
{
    public static class TravellerAiAccess
    {
        private static string verified;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => verified = null;

        public static bool Validate(NpcApiConnection connection, out string error)
        {
            error = "请先填写 API Key，并测试 AI 模型连接。";
            if (connection == null || (connection.mode != "deepseek" && connection.mode != "custom")) return false;
            if (string.IsNullOrWhiteSpace(connection.apiKey)) return false;
            return connection.Validate(out error);
        }

        private static string Fingerprint(TravellerPreferences preferences, NpcDialogueSettings defaults)
        {
            var connection = preferences.connection.Copy();
            connection.apiKey = (connection.apiKey ?? "").Trim();
            connection.apiUrl = (connection.apiUrl ?? "").Trim();
            connection.model = (connection.model ?? "").Trim();
            string value = JsonUtility.ToJson(connection) + "\n" + preferences.GatewayUrl(defaults);
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        public static void Confirm(TravellerPreferences preferences, NpcDialogueSettings defaults)
        {
            if (Validate(preferences.connection, out _)) verified = Fingerprint(preferences, defaults);
        }

        public static bool CanPlay(TravellerPreferences preferences, NpcDialogueSettings defaults, out string error)
        {
            if (!Validate(preferences.connection, out error)) return false;
            if (verified != null && verified == Fingerprint(preferences, defaults)) return true;
            error = "请先点击“测试连接”，成功后保存设置再开始旅程。";
            return false;
        }
    }
}
