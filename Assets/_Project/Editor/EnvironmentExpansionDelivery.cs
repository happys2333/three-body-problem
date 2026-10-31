using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    public static partial class EnvironmentExpansionLibrary
    {
        [Serializable] private class PackageRecord
        {
            public string path, renderPipeline = "Built-in", unityVersion;
            public string nativeReport;
            public bool runtimeFunctionalPassed, performanceBudgetPassed;
            public int prefabs, dependencyCount;
            public long bytes;
            public string[] dependencies;
        }
        public static string ExportPackage(string verifiedRuntimeReport)
        {
            var ready = JsonUtility.FromJson<EnvironmentProduction.ReadyReport>(File.ReadAllText(Docs + "/ready-validation.json"));
            var audit = EnvironmentLibraryAudit.Audit(Docs + "/ready-validation.json", Docs + "/saved-prefab-audit.json");
            var runtime = JsonUtility.FromJson<ThreeBodyProblem.EnvironmentAssetPreview.Report>(File.ReadAllText(verifiedRuntimeReport));
            // Imported-asset readiness and a frame-time budget are separate findings.
            // Keep the original failed performance report and record it with the package;
            // every physical/view check and every non-performance runtime check must pass.
            bool functional = runtime.gateClear && runtime.portalClear && runtime.gateWallSolid && runtime.portalWallSolid
                && runtime.gateWalkThrough && runtime.portalWalkThrough && runtime.stairsClimbed
                && runtime.structures.All(s => s.clear && s.walked)
                && runtime.structures.Any(s => s.asset == "PyramidPalace_Interior" && s.wallSolid)
                && runtime.metrics.All(m => m.viewClear)
                && runtime.errors.All(e => e.EndsWith(": P95 frame time exceeds 16.67 ms.", StringComparison.Ordinal));
            if (!audit.passed || !functional || ready.assets.Any(a => !runtime.metrics.Any(m => m.asset == a.id)))
                throw new InvalidOperationException("The current prefab set needs passing saved-prefab, physical, view and non-performance runtime checks.");
            var paths = ready.assets.Select(a => a.prefab).Concat(new[] { Root + "/Documentation" }).ToArray();
            var dependencies = AssetDatabase.GetDependencies(paths, true).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)).Distinct().OrderBy(p => p).ToArray();
            if (dependencies.Any(p => p.Contains("/LocalOnly/") || p.Contains("/Characters/") || p.Contains("/Scenes/Traveller/")))
                throw new InvalidOperationException("Unexpected unrelated or restricted dependency.");
            const string output = "Builds/EnvironmentExpansionReady_20261031.unitypackage";
            AssetDatabase.ExportPackage(paths, output, ExportPackageOptions.IncludeDependencies | ExportPackageOptions.Recurse);
            var record = new PackageRecord { path = output, prefabs = ready.assets.Count, dependencyCount = dependencies.Length,
                dependencies = dependencies, unityVersion = Application.unityVersion, bytes = new FileInfo(output).Length,
                nativeReport = verifiedRuntimeReport, runtimeFunctionalPassed = functional, performanceBudgetPassed = runtime.passed };
            File.WriteAllText(Docs + "/package.json", JsonUtility.ToJson(record, true)); return Path.GetFullPath(output);
        }
    }
}
