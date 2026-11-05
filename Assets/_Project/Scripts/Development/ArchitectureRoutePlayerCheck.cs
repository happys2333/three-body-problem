#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem
{
    public sealed class ArchitectureRoutePlayerCheck : MonoBehaviour
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int checks;
            public float gateMetres, naveMetres, exitMetres, poolAccessMetres, altarHeight, ancestralHallHeight;
            public List<string> errors = new();
        }

        private const string JournalKey = "ThreeBodyTraveller.Journal.v1";
        private readonly Report report = new();
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-architecture-route-check");
            if (index < 0 || index + 1 >= args.Length || FindFirstObjectByType<ArchitectureRoutePlayerCheck>() != null) return;
            var test = new GameObject("Development architecture route verification");
            DontDestroyOnLoad(test);
            test.AddComponent<ArchitectureRoutePlayerCheck>().output = args[index + 1];
        }

        private IEnumerator Start()
        {
            bool hadJournal = PlayerPrefs.HasKey(JournalKey);
            string journal = PlayerPrefs.GetString(JournalKey, "");
            Application.runInBackground = true;
            Application.targetFrameRate = 90;
            Application.logMessageReceived += Log;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            yield return Run();
            Application.logMessageReceived -= Log;
            if (hadJournal) PlayerPrefs.SetString(JournalKey, journal);
            else PlayerPrefs.DeleteKey(JournalKey);
            PlayerPrefs.Save();
            report.passed = report.errors.Count == 0;
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 1);
        }

        private IEnumerator Run()
        {
            yield return null;
            var stage = FindFirstObjectByType<TravellerStage>();
            Enter(stage.Journey.Progress, JourneyStep.CourtApproach);
            yield return WaitForScene(1);
            stage = FindFirstObjectByType<TravellerStage>();
            if (!Check(stage.profile.sceneIndex == 1, "Chaoge did not load.")) yield break;
            var player = stage.player;
            player.enabled = false;
            player.Teleport(new Vector3(0, 1.76f, -246f), new Vector3(0, 1.76f, -184f));
            yield return Walk(player, new Vector3(0, 0, -184f), 24);
            report.gateMetres = player.transform.position.z + 246f;
            Check(report.gateMetres > 61.5f && Mathf.Abs(player.transform.position.y) < .25f,
                "The player could not pass the new timber gates.");

            player.Teleport(new Vector3(81.7f, 1.76f, 12.25f), new Vector3(89, 1.76f, 12.25f));
            yield return Walk(player, new Vector3(89, 0, 12.25f), 12);
            report.poolAccessMetres = player.transform.position.x - 81.7f;
            Check(report.poolAccessMetres > 7f, "The player could not cross the new pool steps.");

            player.Teleport(new Vector3(-107, 1.76f, 14), new Vector3(-107, 1.76f, 24));
            yield return Walk(player, new Vector3(-107, 0, 24), 12);
            report.altarHeight = player.transform.position.y;
            Check(player.transform.position.z > 23.7f && Mathf.Abs(report.altarHeight - 1.8f) < .20f,
                "The player could not climb onto the ceremonial platform.");

            player.Teleport(new Vector3(-107, 1.76f, 45), new Vector3(-107, 1.76f, 74));
            yield return Walk(player, new Vector3(-107, 0, 74), 18);
            report.ancestralHallHeight = player.transform.position.y;
            Check(player.transform.position.z > 73.7f && Mathf.Abs(report.ancestralHallHeight - 3.28f) < .20f,
                "The player could not walk from the forecourt into the ancestral hall.");

            Enter(stage.Journey.Progress, JourneyStep.TribunalApproach);
            yield return WaitForScene(2);
            stage = FindFirstObjectByType<TravellerStage>();
            if (!Check(stage.profile.sceneIndex == 2, "Medieval tribunal did not load.")) yield break;
            player = stage.player;
            var progress = stage.Journey.Progress;
            player.enabled = false;
            player.Teleport(new Vector3(0, 1.76f, -47f), new Vector3(0, 1.76f, 21f));
            yield return Walk(player, new Vector3(0, 0, 21f), 25);
            report.naveMetres = player.transform.position.z + 47f;
            if (!Check(report.naveMetres > 67.5f && progress.Step == JourneyStep.TribunalTalk,
                "The entrance or hall stopped the player from reaching the tribunal.")) yield break;

            progress.Explain(true);
            Check(progress.Continue(), "The tribunal could not begin the exterior observation.");
            stage.Journal.entries.RemoveAll(entry => entry.observationId == "medieval_three_suns");
            yield return Walk(player, new Vector3(0, 0, -49f), 25);
            report.exitMetres = 21f - player.transform.position.z;
            if (!Check(report.exitMetres > 69.5f && progress.Step == JourneyStep.ObserveThreeSuns,
                "The player could not leave the hall for the observation plaza.")) yield break;

            Vector3 eye = player.viewCamera.transform.position;
            var sky = stage.profile.moments[1];
            player.FaceTarget(eye + sky.sunA.normalized + sky.sunB.normalized + sky.sunC.normalized);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline && progress.Step == JourneyStep.ObserveThreeSuns) yield return null;
            Check(progress.Step == JourneyStep.AfterThreeSuns || progress.Step == JourneyStep.Collapse,
                "The new skyline blocked or stalled the three-sun observation.");
            Check(stage.Journal.entries.Exists(entry => entry.observationId == "medieval_three_suns" && entry.category == "亲眼观察"),
                "The visible three suns were not added to the journal.");
        }

        private static void Enter(JourneyProgress progress, JourneyStep step) =>
            typeof(JourneyProgress).GetMethod("Enter", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(progress, new object[] { step });

        private static IEnumerator WaitForScene(int index)
        {
            float deadline = Time.realtimeSinceStartup + 25f;
            while (SceneManager.GetActiveScene().name != TravellerStage.SceneNames[index] &&
                Time.realtimeSinceStartup < deadline) yield return null;
            for (int i = 0; i < 5; i++) yield return null;
        }

        private static IEnumerator Walk(TravellerPlayer player, Vector3 point, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                Vector3 delta = Vector3.ProjectOnPlane(point - player.transform.position, Vector3.up);
                if (delta.magnitude <= .12f) yield break;
                float step = Mathf.Min(.04f, Time.deltaTime);
                float speed = player.walkSpeed * player.runMultiplier;
                player.FaceTarget(player.viewCamera.transform.position + delta);
                player.MoveInput(Vector2.up * Mathf.Min(1f, delta.magnitude / (speed * Mathf.Max(.001f, step))), true, step);
                yield return null;
            }
        }

        private bool Check(bool condition, string message)
        {
            report.checks++;
            if (!condition) report.errors.Add(message);
            return condition;
        }

        private void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.errors.Add(message);
        }
    }
}
#endif
