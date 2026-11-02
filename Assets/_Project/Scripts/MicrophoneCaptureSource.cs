using System;
using System.Collections;
using UnityEngine;

namespace ThreeBodyProblem
{
    // The device operations are separate so capture routing and cancellation can
    // be verified without recording a person's microphone or opening OS prompts.
    public interface IMicrophoneCaptureSource
    {
        string[] Devices { get; }
        bool HasPermission { get; }
        IEnumerator RequestPermission();
        AudioClip Start(string device);
        int GetPosition(string device);
        void End(string device);
    }

    public sealed class UnityMicrophoneCaptureSource : IMicrophoneCaptureSource
    {
        public string[] Devices => Microphone.devices;
        public bool HasPermission => Application.HasUserAuthorization(UserAuthorization.Microphone);
        public IEnumerator RequestPermission() { yield return Application.RequestUserAuthorization(UserAuthorization.Microphone); }
        public AudioClip Start(string device) => Microphone.Start(device, false, 25, 16000);
        public int GetPosition(string device) => Microphone.GetPosition(device);
        public void End(string device) => Microphone.End(device);
    }

    public static class MicrophoneDeviceSelection
    {
        public static bool TryResolve(string selection, string[] devices, out string device, out string error)
        {
            device = null; error = null;
            if (devices == null || devices.Length == 0)
            { error = "没有找到麦克风，请连接设备后刷新，或按 Enter 打字交谈。"; return false; }
            if (string.IsNullOrEmpty(selection)) return true;
            foreach (var name in devices)
                if (string.Equals(name, selection, StringComparison.Ordinal)) { device = name; return true; }
            error = "所选麦克风已断开或不可用，请在设置中重新选择；仍可按 Enter 打字交谈。";
            return false;
        }
    }
}
