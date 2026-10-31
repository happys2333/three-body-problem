using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    // Audits saved prefab contents, not the temporary objects used by an importer.
    public static class EnvironmentLibraryAudit
    {
        [Serializable] public class Row
        {
            public string id;
            public bool passed, meshReferences, pbrMaterials, textureColourSpaces, lodThresholds, collisionReferences, rootScaleAndGrounding, finiteGeometry, completeTangents;
            public int[] trianglesByLod;
            public int colliderCount, missingScripts;
            public float width, height, depth;
        }
        [Serializable] public class Report
        {
            public bool passed;
            public string scope = "Reloaded saved prefabs: geometry, LOD, PBR maps, texture colour spaces, collision and references. Art and runtime evidence are separate.";
            public List<Row> assets = new List<Row>();
        }
        private static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
        private static bool ValidMesh(Mesh m)
        {
            if (m == null || m.vertexCount == 0) return false;
            var n = m.normals; var uv = m.uv;
            return n.Length == m.vertexCount && uv.Length == m.vertexCount
                && m.vertices.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z))
                && n.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z) && v.sqrMagnitude > .5f)
                && uv.All(v => Finite(v.x) && Finite(v.y))
                && m.triangles.Length > 0 && m.triangles.All(i => i >= 0 && i < m.vertexCount);
        }
        private static bool ColourSpace(Material material)
        {
            foreach (var property in new[] { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_SurfaceMap" })
            {
                if (!material.HasProperty(property)) continue;
                var texture = material.GetTexture(property); if (texture == null) continue;
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                if (importer == null) return false;
                if (property == "_MainTex" && !importer.sRGBTexture) return false;
                if (property != "_MainTex" && importer.sRGBTexture) return false;
                if (property == "_BumpMap" && importer.textureType != TextureImporterType.NormalMap) return false;
            }
            return true;
        }
        public static Report Audit(string sourceReport, string destination)
        {
            var source = JsonUtility.FromJson<EnvironmentProduction.ReadyReport>(File.ReadAllText(sourceReport));
            var report = new Report();
            foreach (var entry in source.assets)
            {
                var root = PrefabUtility.LoadPrefabContents(entry.prefab);
                try
                {
                    var group = root.GetComponent<LODGroup>(); var lods = group.GetLODs();
                    var meshes = lods.SelectMany(l => l.renderers).Select(r => r.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();
                    var materials = lods.SelectMany(l => l.renderers).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                    var bounds = lods[0].renderers[0].bounds;
                    var colliders = root.GetComponentsInChildren<Collider>(true);
                    var row = new Row { id = entry.id,
                        meshReferences = meshes.All(m => m != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))),
                        finiteGeometry = meshes.All(ValidMesh),
                        completeTangents = meshes.All(m => m != null && m.tangents.Length == m.vertexCount && m.tangents.All(t => Finite(t.x) && Finite(t.y) && Finite(t.z) && Finite(t.w) && new Vector3(t.x, t.y, t.z).sqrMagnitude > .5f)),
                        pbrMaterials = materials.Length > 0 && materials.All(m => m != null && m.shader != null && m.shader.isSupported && m.mainTexture != null && m.GetTexture("_BumpMap") != null
                            && (m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap") != null || m.HasProperty("_SurfaceMap") && m.GetTexture("_SurfaceMap") != null)),
                        textureColourSpaces = materials.All(m => m != null && ColourSpace(m)),
                        trianglesByLod = lods.Select(l => l.renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3)).ToArray(),
                        lodThresholds = lods.Length == 3 && lods[0].screenRelativeTransitionHeight > lods[1].screenRelativeTransitionHeight && lods[1].screenRelativeTransitionHeight > lods[2].screenRelativeTransitionHeight,
                        colliderCount = colliders.Length,
                        collisionReferences = colliders.Length > 0 && colliders.All(c => c.enabled && (!(c is MeshCollider) || ((MeshCollider)c).sharedMesh != null))
                            && colliders.All(c => c.GetComponentInParent<LODGroup>() == group),
                        rootScaleAndGrounding = root.transform.localScale == Vector3.one && Mathf.Abs(bounds.min.y) < .02f,
                        missingScripts = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
                        width = bounds.size.x, height = bounds.size.y, depth = bounds.size.z };
                    row.passed = row.meshReferences && row.finiteGeometry && row.completeTangents && row.pbrMaterials && row.textureColourSpaces && row.lodThresholds && row.collisionReferences
                        && row.rootScaleAndGrounding && row.missingScripts == 0 && row.trianglesByLod[0] > row.trianglesByLod[1] && row.trianglesByLod[1] > row.trianglesByLod[2];
                    report.assets.Add(row);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            report.passed = report.assets.Count > 0 && report.assets.All(a => a.passed);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.WriteAllText(destination, JsonUtility.ToJson(report, true)); return report;
        }
    }
}
