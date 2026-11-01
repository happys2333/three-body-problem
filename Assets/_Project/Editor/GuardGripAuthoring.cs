using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    // The platform's 23-bone rigs have no finger joints. Small, reversible hand
    // morphs keep the downloaded rig intact and close only the gripping digits.
    public static class GuardGripAuthoring
    {
        private sealed class Hand
        {
            public Transform bone;
            public Vector3 position, finger, bend, row;
            public Quaternion rotation, reference;
            public Matrix4x4 world;
            public float palmDepth;
        }

        public static void Configure(GameObject root, Animator animator, EraGuardProfile profile, EraGuardEquipment equipment)
        {
            var hands = AlignHands(root, animator, profile, equipment);
            var right = hands[0]; var left = hands[1];
            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var source = skin.sharedMesh;
                var mesh = Object.Instantiate(source);
                mesh.name = Path.GetFileNameWithoutExtension(profile.bodySource) + "_" + source.name + "_Grips";
                AddGrip(skin, mesh, right, profile, "GuardGripRight");
                AddGrip(skin, mesh, left, profile, "GuardGripLeft");
                string path = EraGuardProduction.Root + "/Meshes/" + Regex.Replace(mesh.name, "[^A-Za-z0-9_-]", "_") + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                skin.sharedMesh = saved;
                PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
            }
        }

        // A weapon variant can reuse an already prepared hand mesh without
        // adding duplicate blend-shape frames or modifying the original guard.
        public static void AlignTargets(GameObject root, Animator animator, EraGuardProfile profile, EraGuardEquipment equipment)
        {
            AlignHands(root, animator, profile, equipment);
        }

        public static void PrepareCourtHands(TravellerActor actor, CourtPropGrip grip, string id, bool staff)
        {
            var animator=actor.animator;var profile=ScriptableObject.CreateInstance<EraGuardProfile>();
            profile.knuckleDistance=.075f;profile.gripRadius=.019f;
            try
            {
                var right=Capture(animator,HumanBodyBones.RightHand,HumanBodyBones.RightLowerArm);
                var left=Capture(animator,HumanBodyBones.LeftHand,HumanBodyBones.LeftLowerArm);
                CalibratePalm(animator,right,profile);CalibratePalm(animator,left,profile);
                TravellerStoryCharacters.WithSample(animator,"MX_CarryBoxIdle",.25f,()=>{
                    SetReference(right,actor.transform,Vector3.up,false);SetReference(left,actor.transform,Vector3.up,true);
                    grip.rightReference=staff?right.reference:TabletRotation(right,Vector3.left);
                    grip.leftReference=staff?left.reference:TabletRotation(left,Vector3.right);
                });
                grip.rightPalmOffset=staff?right.finger*profile.knuckleDistance+right.bend*(profile.gripRadius+right.palmDepth):right.position;
                grip.leftPalmOffset=staff?left.finger*profile.knuckleDistance+left.bend*(profile.gripRadius+left.palmDepth):left.position;
                grip.kind=staff?CourtPropGrip.GripKind.Staff:CourtPropGrip.GripKind.Tablet;grip.fingerCurl=staff?100:35;
                var skins=animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if(grip.sourceMeshes==null||grip.sourceMeshes.Length!=skins.Length)grip.sourceMeshes=skins.Select(s=>s.sharedMesh).ToArray();
                for(int i=0;i<skins.Length;i++)
                {
                    var source=grip.sourceMeshes[i];var mesh=Object.Instantiate(source);mesh.name=id+"_CourtHands_"+i;
                    AddGrip(skins[i],mesh,right,profile,"CourtGripRight");AddGrip(skins[i],mesh,left,profile,"CourtGripLeft");
                    string path=CourtRetinueProduction.Root+"/Meshes/"+mesh.name+".asset";
                    var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
                    skins[i].sharedMesh=saved;
                }
            }
            finally{Object.DestroyImmediate(profile);}
        }

        private static Quaternion TabletRotation(Hand hand,Vector3 inward)
        {
            // Support the slip from below, with fingers directed toward its
            // centre. The imported carry pose alone leaves these wrists bent.
            return Quaternion.LookRotation(inward,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(hand.finger,hand.bend));
        }

        private static Hand[] AlignHands(GameObject root, Animator animator, EraGuardProfile profile, EraGuardEquipment equipment)
        {
            var right = Capture(animator, HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm);
            var left = Capture(animator, HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm);
            CalibratePalm(animator, right, profile);
            CalibratePalm(animator, left, profile);
            Vector3 poleAxis = Quaternion.Euler(profile.polearmEuler) * Vector3.up;
            TravellerStoryCharacters.WithSample(animator, "MX_CarryBoxIdle", .25f, () => {
                SetReference(right, root.transform, poleAxis, false);
                SetReference(left, root.transform, Vector3.up, true);
            });
            equipment.rightHandLocalRotation = right.reference;
            equipment.leftHandLocalRotation = left.reference;
            equipment.rightPalmOffset = right.finger * profile.knuckleDistance + right.bend * (profile.gripRadius + right.palmDepth);
            equipment.leftPalmOffset = left.finger * profile.knuckleDistance + left.bend * (profile.gripRadius + left.palmDepth);
            Vector3 poleCenter = profile.polearmPosition + Quaternion.Euler(profile.polearmEuler) * Vector3.up * profile.polearmGripHeight;
            profile.rightGrip = poleCenter - right.reference * equipment.rightPalmOffset;
            profile.leftGrip = profile.shieldPosition + new Vector3(0, 0, -.16f) - left.reference * equipment.leftPalmOffset;
            EditorUtility.SetDirty(profile);
            return new[] { right, left };
        }

        private static void CalibratePalm(Animator animator, Hand hand, EraGuardProfile profile)
        {
            var points = new System.Collections.Generic.List<Vector3>();
            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skin.sharedMesh; int b = Array.IndexOf(skin.bones, hand.bone);
                if (b < 0) continue;
                var transform = Matrix4x4.Scale(hand.bone.lossyScale) * mesh.bindposes[b];
                var vertices = mesh.vertices; var weights = mesh.boneWeights;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i];
                    float influence = (w.boneIndex0 == b ? w.weight0 : 0) + (w.boneIndex1 == b ? w.weight1 : 0) +
                        (w.boneIndex2 == b ? w.weight2 : 0) + (w.boneIndex3 == b ? w.weight3 : 0);
                    if (influence > .8f) points.Add(transform.MultiplyPoint3x4(vertices[i]));
                }
            }
            if (points.Count < 8) throw new InvalidDataException("Missing weighted hand vertices.");
            // Scan rigs have no finger bones. The atlas mesh itself is the reliable
            // source of the finger axis after Humanoid pose normalization.
            var tips = points.OrderByDescending(p => Vector3.Dot(p, hand.finger)).Take(Mathf.Max(8, points.Count / 3));
            hand.finger = tips.Aggregate(Vector3.zero, (sum, p) => sum + p).normalized;
            hand.position = points.Aggregate(Vector3.zero, (sum, p) => sum + p) / points.Count;
        }

        private static Hand Capture(Animator animator, HumanBodyBones hand, HumanBodyBones forearm)
        {
            var bone = animator.GetBoneTransform(hand);
            var lower = animator.GetBoneTransform(forearm);
            return new Hand { bone = bone, position = bone.position, rotation = bone.rotation,
                finger = Quaternion.Inverse(bone.rotation) * (bone.position - lower.position).normalized,
                world = Matrix4x4.TRS(bone.position, bone.rotation, Vector3.one) };
        }

        private static void SetReference(Hand hand, Transform root, Vector3 shaft, bool left)
        {
            var rotation = Quaternion.Inverse(root.rotation) * hand.bone.rotation;
            Vector3 finger = rotation * hand.finger;
            Vector3 projected = Vector3.ProjectOnPlane(finger, shaft).normalized;
            if (projected.sqrMagnitude < .1f) projected = Vector3.forward;
            rotation = Quaternion.FromToRotation(finger, projected) * rotation;
            Vector3 bend = Vector3.Cross(projected, shaft).normalized;
            if (Vector3.Dot(bend, left ? Vector3.right : Vector3.left) < 0) bend = -bend;
            hand.reference = rotation;
            hand.bend = Quaternion.Inverse(rotation) * bend;
            hand.row = Vector3.Cross(hand.finger, hand.bend).normalized;
            hand.palmDepth = Vector3.Dot(hand.position, hand.bend);
        }

        private static void AddGrip(SkinnedMeshRenderer skin, Mesh mesh, Hand hand, EraGuardProfile profile, string name)
        {
            int bone = Array.IndexOf(skin.bones, hand.bone);
            if (bone < 0) throw new InvalidDataException("Hand is absent from skin bones: " + name);
            var points = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents; var weights = mesh.boneWeights;
            var delta = new Vector3[points.Length]; var normalDelta = new Vector3[points.Length]; var tangentDelta = new Vector3[points.Length];
            // Humanoid import can normalize the skeleton pose without rewriting the
            // mesh vertices. Work in the actual mesh bind pose, in world metres.
            Matrix4x4 toHand = Matrix4x4.Scale(hand.bone.lossyScale) * mesh.bindposes[bone];
            Matrix4x4 fromHand = toHand.inverse;
            int changed = 0;
            for (int i = 0; i < points.Length; i++)
            {
                var w = weights[i];
                float influence = (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0) +
                    (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);
                if (influence < .65f) continue;
                Vector3 p = toHand.MultiplyPoint3x4(points[i]);
                float u = Vector3.Dot(p, hand.finger);
                if (u <= profile.knuckleDistance || u > .24f) continue;
                float angle = Mathf.Min((u - profile.knuckleDistance) / profile.gripRadius, Mathf.PI * .95f);
                float row = Vector3.Dot(p, hand.row), depth = Vector3.Dot(p, hand.bend);
                Vector3 bent = hand.finger * (profile.knuckleDistance + profile.gripRadius * Mathf.Sin(angle)) +
                    hand.bend * (depth + profile.gripRadius * (1 - Mathf.Cos(angle))) + hand.row * row;
                delta[i] = fromHand.MultiplyVector(bent - p);
                var turn = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, hand.row);
                normalDelta[i] = fromHand.MultiplyVector(turn * toHand.MultiplyVector(normals[i]).normalized).normalized - normals[i];
                if (tangents.Length == points.Length)
                {
                    Vector3 t = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                    tangentDelta[i] = fromHand.MultiplyVector(turn * toHand.MultiplyVector(t).normalized).normalized - t;
                }
                changed++;
            }
            if (changed < 8) throw new InvalidDataException("No usable finger region in " + mesh.name + " / " + name);
            mesh.AddBlendShapeFrame(name, 100, delta, normalDelta, tangentDelta);
        }
    }
}
