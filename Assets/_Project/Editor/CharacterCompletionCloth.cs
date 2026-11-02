using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static class CharacterCompletionCloth
    {
        [Serializable] private sealed class Report
        { public string source, output, method; public int changedVertices; public bool topologyAndUvsPreserved; }

        public static void RepairCourtSkirt()
        {
            EraGuardProduction.GuardProject(); if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            string path = CharacterCompletionProduction.Prefab("ShangCourtLady");
            var root = PrefabUtility.LoadPrefabContents(path);
            var report = new Report { source = "ShangCourtLady_Rig.fbx", output = "ShangCourtLady_SkirtWeights.asset", method = "Continuous garment/covered-leg weights; arms, feet and exposed calf skin protected; original FBX retained.", topologyAndUvsPreserved = true };
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    // Always derive from the immutable rig, so an earlier repair
                    // cannot accidentally turn a cuff into part of the skirt.
                    var original = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterCompletionProduction.Root + "/ShangCourtLady/ShangCourtLady_Rig.fbx").GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
                    var mesh = Object.Instantiate(original);
                    var vertices = mesh.vertices; var weights = mesh.boneWeights; var uv = mesh.uv;
                    int hips = Array.IndexOf(skin.bones, animator.GetBoneTransform(HumanBodyBones.Hips));
                    int left = Array.IndexOf(skin.bones, animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg));
                    int right = Array.IndexOf(skin.bones, animator.GetBoneTransform(HumanBodyBones.RightUpperLeg));
                    if (hips < 0 || left < 0 || right < 0) throw new InvalidDataException("Missing skirt support bones.");
                    Func<HumanBodyBones, Vector3> bindPoint = bone => {
                        int index = Array.IndexOf(skin.bones, animator.GetBoneTransform(bone));
                        return root.transform.InverseTransformPoint(skin.transform.TransformPoint(mesh.bindposes[index].inverse.MultiplyPoint3x4(Vector3.zero)));
                    };
                    var leftKnee = bindPoint(HumanBodyBones.LeftLowerLeg); var leftFoot = bindPoint(HumanBodyBones.LeftFoot);
                    var rightKnee = bindPoint(HumanBodyBones.RightLowerLeg); var rightFoot = bindPoint(HumanBodyBones.RightFoot);
                    var points = new Vector3[vertices.Length]; var keys = new Vector3Int[vertices.Length]; var cloth = new HashSet<Vector3Int>();
                    var protectedBones = new HashSet<int>();
                    for (int i = 0; i < skin.bones.Length; i++)
                    {
                        var bone = skin.bones[i];
                        foreach (var hand in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                            if (bone.IsChildOf(animator.GetBoneTransform(hand))) protectedBones.Add(i);
                    }
                    var albedo = new Texture2D(2, 2);
                    try
                    {
                        if (!ImageConversion.LoadImage(albedo, File.ReadAllBytes(AssetDatabase.GetAssetPath(skin.sharedMaterial.mainTexture)), false)) throw new InvalidDataException("Could not read cloth/skin atlas.");
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            var p = root.transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[i])); points[i] = p;
                            keys[i] = new Vector3Int(Mathf.RoundToInt(p.x * 10000), Mathf.RoundToInt(p.y * 10000), Mathf.RoundToInt(p.z * 10000));
                            if (p.y < .18f || p.y > 1.06f || Mathf.Abs(p.x) > .55f || Mathf.Abs(p.z) > .45f) continue;
                            var w = weights[i];
                            float protect = (protectedBones.Contains(w.boneIndex0) ? w.weight0 : 0) + (protectedBones.Contains(w.boneIndex1) ? w.weight1 : 0) + (protectedBones.Contains(w.boneIndex2) ? w.weight2 : 0) + (protectedBones.Contains(w.boneIndex3) ? w.weight3 : 0);
                            if (protect >= .1f) continue;
                            var colour = albedo.GetPixelBilinear(uv[i].x, uv[i].y);
                            bool skinColour = colour.r > colour.g * 1.12f && colour.g > colour.b * 1.025f && colour.r > colour.b * 1.15f && colour.g > colour.r * .55f && colour.b > colour.r * .4f;
                            // Preserve the exposed calves inside the garment. They
                            // must still follow their own leg, unlike the skirt shell.
                            if (p.y < .29f && Mathf.Min(SegmentDistance(p, leftKnee, leftFoot), SegmentDistance(p, rightKnee, rightFoot)) < .09f)
                            {
                                if (skinColour) continue;
                            }
                            if (protect < .1f) cloth.Add(keys[i]);
                        }
                    }
                    finally { Object.DestroyImmediate(albedo); }
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        if (!cloth.Contains(keys[i])) continue;
                        float hip = Mathf.Lerp(.55f, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.3f, 1.02f, points[i].y)));
                        weights[i] = new BoneWeight { boneIndex0 = hips, weight0 = hip, boneIndex1 = left, weight1 = (1 - hip) * .5f, boneIndex2 = right, weight2 = (1 - hip) * .5f };
                        report.changedVertices++;
                    }
                    if (report.changedVertices < 500) throw new InvalidDataException("The skirt mask did not find enough cloth vertices.");
                    mesh.boneWeights = weights;
                    string output = CharacterCompletionProduction.Root + "/Meshes/" + report.output;
                    Directory.CreateDirectory(Path.GetDirectoryName(output)); AssetDatabase.Refresh();
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(output);
                    if (saved == null) { AssetDatabase.CreateAsset(mesh, output); saved = mesh; }
                    else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                    skin.sharedMesh = saved; PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path); AssetDatabase.SaveAssets();
                File.WriteAllText(CharacterCompletionProduction.Docs + "/Unity/cloth-repair.json", JsonUtility.ToJson(report, true));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        { var direction = b - a; return Vector3.Distance(p, a + direction * Mathf.Clamp01(Vector3.Dot(p - a, direction) / Mathf.Max(.000001f, direction.sqrMagnitude))); }
    }
}
