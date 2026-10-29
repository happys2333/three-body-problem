using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ThreeBodyProblem.Editor
{
    public static class MixamoMotionLibrary
    {
        public const string Root = "Assets/_Project/Characters/Mixamo";
        private const string ManifestPath = "Documentation/CharacterProduction/mixamo-assets.json";
        [Serializable] public class Acquisition { public List<MotionSource> clips = new List<MotionSource>(); }
        [Serializable] public class MotionSource { public string id, unityFile, sourceName; public bool looping; }
        [Serializable] public class MotionCheck { public string id; public bool humanoid, looping; public float seconds, fps; }
        [Serializable] public class ImportReport { public bool validReferenceAvatar; public List<MotionCheck> clips = new List<MotionCheck>(); }

        [MenuItem("Tools/Three Body Traveller/Prepare Mixamo Motion Library")]
        public static ImportReport Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing motions.");
            if (!File.Exists(ManifestPath)) throw new FileNotFoundException(ManifestPath);
            AssetDatabase.Refresh();
            string referencePath = Root + "/YBot_TPose.fbx";
            var referenceImporter = (ModelImporter)AssetImporter.GetAtPath(referencePath);
            if (referenceImporter == null) throw new FileNotFoundException(referencePath);
            referenceImporter.animationType = ModelImporterAnimationType.Human;
            referenceImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            referenceImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            referenceImporter.importAnimation = false; referenceImporter.SaveAndReimport();
            var avatar = GeneratedCharacterLibrary.AvatarFor(referencePath);
            var report = new ImportReport { validReferenceAvatar = avatar != null && avatar.isValid && avatar.isHuman };
            if (!report.validReferenceAvatar) throw new InvalidDataException("Mixamo reference avatar is invalid.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GeneratedCharacterLibrary.Root + "/GeneratedActors.controller");
            if (controller == null) throw new InvalidDataException("Prepare the generated character prefabs first.");
            var machine = controller.layers[0].stateMachine;
            var acquisition = JsonUtility.FromJson<Acquisition>(File.ReadAllText(ManifestPath));
            foreach (var source in acquisition.clips)
            {
                if (source.unityFile == null || !source.unityFile.StartsWith(Root + "/Animations/") || !File.Exists(source.unityFile))
                    throw new InvalidDataException("Missing acquired motion: " + source.id);
                var importer = (ModelImporter)AssetImporter.GetAtPath(source.unityFile);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; importer.sourceAvatar = avatar;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                var definitions = importer.defaultClipAnimations;
                if (definitions.Length != 1) throw new InvalidDataException("Expected one exported take: " + source.id);
                var definition = definitions[0]; definition.name = source.id;
                definition.loopTime = source.looping; definition.loopPose = source.looping;
                definition.lockRootHeightY = true; definition.keepOriginalPositionY = true;
                // Extract horizontal root travel; prefabs use game-driven movement with applyRootMotion=false.
                definition.lockRootPositionXZ = false; definition.keepOriginalPositionXZ = false;
                definition.lockRootRotation = true; definition.keepOriginalOrientation = true;
                importer.clipAnimations = definitions; importer.SaveAndReimport();
                var clip = ClipAt(source.unityFile);
                if (clip == null || !clip.humanMotion || clip.length <= 0) throw new InvalidDataException("Invalid humanoid motion: " + source.id);
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == source.id) ?? machine.AddState(source.id);
                state.motion = clip;
                report.clips.Add(new MotionCheck { id = source.id, humanoid = clip.humanMotion, seconds = clip.length, fps = clip.frameRate, looping = clip.isLooping });
            }
            var idle = ClipAt(Root + "/Animations/MX_Idle.fbx");
            var lookUp = ClipAt(Root + "/Animations/MX_LookUpTorchBase.fbx");
            if (idle != null && lookUp != null)
            {
                var observation = PrepareEmptyHandSkyLook(idle, lookUp);
                var observationState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == observation.name)
                    ?? machine.AddState(observation.name);
                observationState.motion = observation;
            }
            var walk = ClipAt(Root + "/Animations/MX_Walking.fbx");
            if (walk != null)
            {
                var backward = PrepareReversedMotion(walk, "MX_WalkBackward");
                var backwardState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == backward.name)
                    ?? machine.AddState(backward.name);
                backwardState.motion = backward;
            }
            var pickup = ClipAt(Root + "/Animations/MX_PickUp.fbx");
            if (pickup != null)
            {
                var putDown = PrepareReversedMotion(pickup, "MX_PutDown");
                var putDownState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == putDown.name)
                    ?? machine.AddState(putDown.name);
                putDownState.motion = putDown;
            }
            if (idle != null)
            {
                var shiver = PrepareShiver(idle);
                var shiverState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == shiver.name)
                    ?? machine.AddState(shiver.name);
                shiverState.motion = shiver;
            }
            var defaultState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "MX_Idle");
            if (defaultState != null) machine.defaultState = defaultState;
            EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            File.WriteAllText("Documentation/CharacterProduction/unity-mixamo-import-validation.json", JsonUtility.ToJson(report, true));
            GeneratedCharacterLibrary.Validate();
            return report;
        }

        private static AnimationClip ClipAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview"));

        private static AnimationClip PrepareEmptyHandSkyLook(AnimationClip idle, AnimationClip lookUp)
        {
            // Keep the observed head/neck motion; replace the torch-holding body and arms with neutral idle.
            Directory.CreateDirectory(Root + "/Derived"); AssetDatabase.Refresh();
            var result = UnityEngine.Object.Instantiate(idle); result.name = "MX_LookSky"; result.hideFlags = HideFlags.None;
            foreach (var binding in AnimationUtility.GetCurveBindings(idle))
            {
                bool head = binding.type == typeof(Animator) && (binding.propertyName.StartsWith("Head ") || binding.propertyName.StartsWith("Neck "));
                var curve = AnimationUtility.GetEditorCurve(head ? lookUp : idle, binding);
                if (curve == null) throw new InvalidDataException("Missing observation curve: " + binding.propertyName);
                int frames = Mathf.CeilToInt(lookUp.length * idle.frameRate);
                var keys = new Keyframe[frames + 1];
                for (int i = 0; i <= frames; i++)
                {
                    float t = Mathf.Min(lookUp.length, i / idle.frameRate);
                    keys[i] = new Keyframe(t, curve.Evaluate(head ? t : Mathf.Repeat(t, idle.length)));
                }
                var sampled = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(sampled, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(sampled, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(result, binding, sampled);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(lookUp);
            settings.loopTime = false; settings.loopBlend = false; AnimationUtility.SetAnimationClipSettings(result, settings);
            string path = Root + "/Derived/MX_LookSky.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(result, path); return result; }
            EditorUtility.CopySerialized(result, existing); UnityEngine.Object.DestroyImmediate(result);
            EditorUtility.SetDirty(existing); return existing;
        }

        private static AnimationClip PrepareReversedMotion(AnimationClip walk, string name)
        {
            Directory.CreateDirectory(Root + "/Derived"); AssetDatabase.Refresh();
            var result = UnityEngine.Object.Instantiate(walk); result.name = name; result.hideFlags = HideFlags.None;
            foreach (var binding in AnimationUtility.GetCurveBindings(walk))
            {
                var source = AnimationUtility.GetEditorCurve(walk, binding);
                int frames = Mathf.CeilToInt(walk.length * walk.frameRate);
                var keys = new Keyframe[frames + 1];
                for (int i = 0; i <= frames; i++)
                {
                    float t = Mathf.Min(walk.length, i / walk.frameRate);
                    keys[i] = new Keyframe(t, source.Evaluate(walk.length - t));
                }
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(result, binding, curve);
            }
            string path = Root + "/Derived/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(result, path); return result; }
            EditorUtility.CopySerialized(result, existing); UnityEngine.Object.DestroyImmediate(result);
            EditorUtility.SetDirty(existing); return existing;
        }

        private static AnimationClip PrepareShiver(AnimationClip idle)
        {
            const float duration = 3f;
            Directory.CreateDirectory(Root + "/Derived"); AssetDatabase.Refresh();
            var result = UnityEngine.Object.Instantiate(idle); result.name = "MX_Shiver"; result.hideFlags = HideFlags.None;
            foreach (var binding in AnimationUtility.GetCurveBindings(idle))
            {
                var source = AnimationUtility.GetEditorCurve(idle, binding);
                int frames = Mathf.RoundToInt(duration * idle.frameRate);
                var keys = new Keyframe[frames + 1];
                for (int i = 0; i <= frames; i++)
                {
                    float t = i / idle.frameRate, offset = 0;
                    switch (binding.propertyName)
                    {
                        case "Spine Front-Back": offset = 0.012f * Mathf.Sin(2 * Mathf.PI * 6 * t); break;
                        case "Chest Front-Back": offset = 0.014f * Mathf.Sin(2 * Mathf.PI * 7 * t); break;
                        case "UpperChest Twist Left-Right": offset = 0.008f * Mathf.Sin(2 * Mathf.PI * 5 * t); break;
                        case "Neck Nod Down-Up": offset = 0.006f * Mathf.Sin(2 * Mathf.PI * 6 * t); break;
                        case "Left Shoulder Down-Up": offset = 0.04f + 0.012f * Mathf.Sin(2 * Mathf.PI * 7 * t); break;
                        case "Right Shoulder Down-Up": offset = 0.04f + 0.012f * Mathf.Sin(2 * Mathf.PI * 7 * t + Mathf.PI * 0.5f); break;
                    }
                    float value = source.Evaluate(Mathf.Repeat(t, idle.length)) + offset;
                    keys[i] = new Keyframe(t, offset == 0 ? value : Mathf.Clamp(value, -1, 1));
                }
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(result, binding, curve);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(idle);
            settings.startTime = 0; settings.stopTime = duration; settings.loopTime = true; settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(result, settings);
            string path = Root + "/Derived/MX_Shiver.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(result, path); return result; }
            EditorUtility.CopySerialized(result, existing); UnityEngine.Object.DestroyImmediate(result);
            EditorUtility.SetDirty(existing); return existing;
        }

        public static AnimationClip[] LoadClips()
        {
            if (!File.Exists(ManifestPath)) return new AnimationClip[0];
            var acquired = JsonUtility.FromJson<Acquisition>(File.ReadAllText(ManifestPath)).clips.Select(s => ClipAt(s.unityFile));
            var derived = Directory.Exists(Root + "/Derived") ? Directory.GetFiles(Root + "/Derived", "*.anim")
                .Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p)) : Enumerable.Empty<AnimationClip>();
            return acquired.Concat(derived).Where(c => c != null && c.humanMotion && c.name.StartsWith("MX_")).OrderBy(c => c.name).ToArray();
        }
    }
}
