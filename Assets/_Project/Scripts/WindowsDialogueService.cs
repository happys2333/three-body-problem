using System;
using System.IO;
using UnityEngine;

namespace ThreeBodyProblem
{
    public static class WindowsDialogueService
    {
        public static string Url { get; private set; }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static System.Diagnostics.Process process;
        private static readonly object LogLock = new object();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Start()
        {
            string root = Path.Combine(Path.GetDirectoryName(Application.dataPath), "AI");
            string python = Path.Combine(root, "runtime", "python.exe");
            string script = Path.Combine(root, "Tools", "dialogue_gateway.py");
            if (!File.Exists(python) || !File.Exists(script))
            {
                Debug.LogError("试玩包缺少 AI 服务文件，请完整解压 ZIP 后运行游戏。");
                return;
            }
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            Url = "http://127.0.0.1:" + port;
            string log = Path.Combine(Application.persistentDataPath, "ai-service.log");
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                var start = new System.Diagnostics.ProcessStartInfo(python,
                    "-u \"" + script + "\" --require-online --port " + port + " --parent-pid " + System.Diagnostics.Process.GetCurrentProcess().Id)
                {
                    WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                start.EnvironmentVariables.Remove("DEEPSEEK_API_KEY");
                process = new System.Diagnostics.Process { StartInfo = start };
                System.Diagnostics.DataReceivedEventHandler receive = (_, e) =>
                {
                    if (e.Data == null) return;
                    try { lock (LogLock) File.AppendAllText(log, e.Data + Environment.NewLine); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                };
                process.OutputDataReceived += receive;
                process.ErrorDataReceived += receive;
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                Application.quitting += Stop;
            }
            catch (Exception error)
            {
                Debug.LogError("无法启动 AI 对话服务：" + error.GetType().Name);
                Stop();
            }
        }

        private static void Stop()
        {
            var owned = process; process = null;
            if (owned == null) return;
            try { if (!owned.HasExited) owned.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally { owned.Dispose(); }
        }
#endif
    }
}
