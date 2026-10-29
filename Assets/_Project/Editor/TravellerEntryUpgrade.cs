using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ThreeBodyProblem.Editor
{
    public static class TravellerEntryUpgrade
    {
        [MenuItem("Tools/Three Body Traveller/Upgrade Journey Entry")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before upgrading scene components.");
            var original = SceneManager.GetActiveScene();
            string originalPath = original.path;
            if (original.isDirty && !string.IsNullOrEmpty(originalPath)) EditorSceneManager.SaveScene(original);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(TravellerSceneBuilder.Root + "/Settings/Traveller/FieldNotesPanel.asset");
            TravellerSceneBuilder.ConfigurePanelSettings(panel);
            AssetDatabase.SaveAssets();
            foreach (string name in TravellerStage.SceneNames)
            {
                var scene = EditorSceneManager.OpenScene(TravellerSceneBuilder.SceneFolder + "/" + name + ".unity");
                var stage = UnityEngine.Object.FindFirstObjectByType<TravellerStage>();
                if (stage.GetComponent<TravellerJourney>() == null) stage.gameObject.AddComponent<TravellerJourney>();
                foreach (var part in stage.GetComponentsInChildren<Transform>(true))
                    if (part.name.StartsWith("周文王 [", System.StringComparison.Ordinal))
                        foreach (var child in part.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(originalPath) ? TravellerSceneBuilder.SceneFolder + "/01_GobiJourney.unity" : originalPath);
            AssetDatabase.SaveAssets();
        }
    }
}
