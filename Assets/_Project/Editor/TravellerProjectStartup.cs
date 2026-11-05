using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem.Editor
{
    [InitializeOnLoad]
    public static class TravellerProjectStartup
    {
        public const string EntryScenePath = TravellerSceneBuilder.SceneFolder + "/01_GobiJourney.unity";
        private const string StartupHandled = "ThreeBodyProblem.Editor.GameEntryOpened";

        static TravellerProjectStartup()
        {
            if (!Application.isBatchMode)
                EditorApplication.delayCall += ConfigureGameEntry;
        }

        private static void ConfigureGameEntry()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ConfigureGameEntry;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var entry = AssetDatabase.LoadAssetAtPath<SceneAsset>(EntryScenePath);
            if (entry == null) return;
            EditorSceneManager.playModeStartScene = entry;

            // SessionState survives script reloads, so compiling does not interrupt scene editing.
            if (SessionState.GetBool(StartupHandled, false)) return;
            SessionState.SetBool(StartupHandled, true);

            // Explicit scene arguments and unsaved work take precedence over the startup default.
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return;

            if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != EntryScenePath)
                EditorSceneManager.OpenScene(EntryScenePath, OpenSceneMode.Single);
            Debug.Log("TRAVELLER_EDITOR_ENTRY: " + SceneManager.GetActiveScene().path + "; Play starts from " + entry.name);
        }

        [MenuItem("Tools/Three Body Traveller/Open Game Entry", false, 0)]
        public static void OpenGameEntry()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EntryScenePath);
            EditorSceneManager.OpenScene(EntryScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Three Body Traveller/Open Game Entry", true)]
        private static bool CanOpenGameEntry() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
