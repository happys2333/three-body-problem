using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    public static partial class EnvironmentProduction
    {
        public const string Work = "ArtSource/Environment/Prepared/2026-10-28";
        public const string Root = "Assets/_Project/Art/EnvironmentReady";
        public const string Docs = "Documentation/EnvironmentProduction/Ready";
        [Serializable] public class SourceEntry { public string id, sourcePrefab, albedo; public float width, height, depth; }
        [Serializable] public class SourceManifest { public List<SourceEntry> assets = new List<SourceEntry>(); }

        // TBM1 stores positions, normals, UVs, triangle indices and face material IDs.
        // Working files use Blender Z-up: (Unity x, -Unity z, Unity y), in metres.
        [MenuItem("Tools/Three Body Traveller/Assets/Export Environment Working Meshes")]
        public static int ExportWorkingMeshes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            Directory.CreateDirectory(Work + "/Input"); Directory.CreateDirectory(Docs);
            var source = JsonUtility.FromJson<HunyuanEnvironmentLibrary.Report>(File.ReadAllText("Documentation/EnvironmentProduction/hunyuan-environment-unity-validation.json"));
            var manifest = new SourceManifest();
            foreach (var entry in source.assets)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefab);
                var high = prefab.GetComponent<LODGroup>().GetLODs()[0].renderers;
                if (high.Length != 1) throw new InvalidDataException(entry.id + " requires an explicit mesh merge.");
                var renderer = high[0]; var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
                var indices = mesh.triangles;
                using (var writer = new BinaryWriter(File.Create(Work + "/Input/" + entry.id + ".meshbin")))
                {
                    writer.Write(new byte[] { 84, 66, 77, 49 });
                    writer.Write(vertices.Length); writer.Write(indices.Length); writer.Write(mesh.subMeshCount);
                    foreach (var point in vertices) { var p = renderer.transform.TransformPoint(point); writer.Write(p.x); writer.Write(-p.z); writer.Write(p.y); }
                    foreach (var normal in normals) { var n = renderer.transform.TransformDirection(normal).normalized; writer.Write(n.x); writer.Write(-n.z); writer.Write(n.y); }
                    foreach (var value in uv) { writer.Write(value.x); writer.Write(value.y); }
                    foreach (int index in indices) writer.Write(index);
                    for (int sub = 0; sub < mesh.subMeshCount; sub++) for (int i = 0; i < mesh.GetIndexCount(sub) / 3; i++) writer.Write(sub);
                }
                manifest.assets.Add(new SourceEntry { id = entry.id, sourcePrefab = entry.prefab,
                    albedo = Path.GetFullPath(AssetDatabase.GetAssetPath(renderer.sharedMaterial.mainTexture)),
                    width = entry.widthMetres, height = entry.heightMetres, depth = entry.depthMetres });
            }
            File.WriteAllText(Work + "/sources.json", JsonUtility.ToJson(manifest, true));
            return manifest.assets.Count;
        }
    }
}
