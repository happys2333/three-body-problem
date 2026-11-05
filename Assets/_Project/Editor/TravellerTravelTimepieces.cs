using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThreeBodyProblem.Editor
{
    public static partial class TravellerSceneBuilder
    {
        private const string TravelTimerName = "Five-compartment travelling timer";
        private const string TravelTimerPath = PrefabFolder + "/FiveTierTravelTimer.prefab";

        private static void UpgradeTravelTimepieces(Scene scene, ILookup<string, Transform> objects)
        {
            foreach (var installed in objects[TravelTimerName])
                installed.localPosition = Vector3.up * (installed.GetComponentInParent<TravellerActor>() != null ? .50f : .35f);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/Hourglass.prefab");
            if (source == null) return;
            var legacy = objects.SelectMany(group => group).Where(t =>
                PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == source).ToArray();
            if (legacy.Length == 0) return;
            var timer = PrepareTravelTimepiece();
            foreach (var anchor in legacy)
            {
                bool carried = anchor.GetComponentInParent<TravellerActor>() != null;
                var previous = anchor.Cast<Transform>().ToArray();
                PrefabUtility.UnpackPrefabInstance(anchor.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(timer, scene);
                replacement.name = TravelTimerName;
                replacement.transform.SetParent(anchor, false);
                replacement.transform.localPosition = Vector3.up * (carried ? .50f : .35f);
                replacement.transform.localScale = Vector3.one * .58f;
                if (!carried)
                {
                    var door = replacement.transform.Find("Balanced turning case/Open front panel hinge");
                    door.localRotation = Quaternion.Euler(0, -108f, 0);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(door);
                }
                foreach (var child in previous) Object.DestroyImmediate(child.gameObject);
            }
        }

        private static GameObject PrepareTravelTimepiece()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TravelTimerPath);
            if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "/Art/PalaceInterior/Prefabs/FiveTierSandTimer.prefab");
            if (source == null) throw new InvalidOperationException("The five-tier sand timer must be prepared first.");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "FiveTierTravelTimer";
                var mechanism = model.GetComponent<FiveTierSandTimer>();
                foreach (var stream in mechanism.streams) stream.gameObject.SetActive(false);
                Object.DestroyImmediate(mechanism);
                model.transform.Find("Balanced turning case/Open front panel hinge").localRotation = Quaternion.identity;
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
                return PrefabUtility.SaveAsPrefabAsset(model, TravelTimerPath);
            }
            finally { Object.DestroyImmediate(model); }
        }
    }
}
