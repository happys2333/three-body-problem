using System;
using System.IO;

namespace ThreeBodyProblem.Editor
{
    internal static class WindowsNvidiaRuntime
    {
        private static readonly string[] Libraries = { "NVUnityPlugin.dll", "nvngx_dlss.dll" };

        // Some editor build configurations omit the native libraries even with
        // the built-in NVIDIA module referenced by the managed player.
        internal static ulong CopyMissing(string playbackEngine, string executable)
        {
            string output = Path.GetDirectoryName(executable);
            string plugins = Path.Combine(output, Path.GetFileNameWithoutExtension(executable) + "_Data", "Plugins", "x86_64");
            if (Array.TrueForAll(Libraries, name => File.Exists(Path.Combine(plugins, name)))) return 0;

            string playerTemplate = Path.Combine(playbackEngine, "Variations", "win64_player_nondevelopment_mono");
            ulong addedBytes = 0;
            foreach (string name in Libraries)
            {
                string destination = Path.Combine(output, name);
                if (File.Exists(destination)) continue;
                string source = Path.Combine(playerTemplate, name);
                if (!File.Exists(source))
                    throw new FileNotFoundException("The matching Windows Build Support (Mono) installation is missing " + name, source);
                File.Copy(source, destination);
                addedBytes += (ulong)new FileInfo(destination).Length;
            }
            return addedBytes;
        }
    }
}
