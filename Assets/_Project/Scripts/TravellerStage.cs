using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem
{
    public sealed class TravellerStage : MonoBehaviour
    {
        public static readonly string[] SceneNames = { "01_GobiJourney", "02_ChaogeCourt", "03_MedievalTribunal" };
        public TravellerSceneProfile profile;
        public TravellerPlayer player;
        public Light sun;
        public Material skyMaterial;
        public Transform[] flyingStars;
        public GameObject heatDetails;
        public GameObject frostDetails;
        public ParticleSystem[] fires;
        public ParticleSystem[] snow;
        public ParticleSystem embers;
        public AudioSource ambience;
        public Transform shelterCenter;
        public float shelterRadius = 8f;
        [SerializeField] private int momentIndex;
        private Material liveSky;
        private Coroutine transition;
        private bool loading;
        public bool IsLoading => loading;
        internal float Combustion { get; set; } = 1f;
        public string DestinationTitle { get; private set; }
        private float motionTime;
        private Vector3[] starPositions;
        private Vector3 starCenter;
        private TorchBearer playerTorch;
        private CapitalRevivalDirector revival;
        private CourtAudienceSequence audience;
        private TravellerHud hud;
        private NpcIdentity[] observers;
        private readonly Dictionary<NpcIdentity, List<SceneObservation>> witnessed = new();
        private string lookingAt, recordedAt;
        private JourneyStep recordedStep;
        private float nextObservationSample, lookStarted;
        public CourtAudienceSequence Audience => audience;
        public bool HasPendingCourtAudience => audience != null && !audience.IsComplete;
        public event Action Changed;
        public event Action<string> Notice;
        public event Action<int> ObservationRecorded;
        public TravellerJourney Journey { get; private set; }
        public ObservationJournal Journal { get; private set; }
        public int MomentIndex => momentIndex;
        public SceneMoment Current => profile.moments[momentIndex];
        public bool IsRevivalComplete => revival == null || revival.IsComplete;
        public bool IsInShelter => shelterCenter != null && Vector3.Distance(player.transform.position, shelterCenter.position) < shelterRadius;

        private void Awake()
        {
            Journal = ObservationJournal.Load();
            revival = GetComponent<CapitalRevivalDirector>();
            audience = GetComponentInChildren<CourtAudienceSequence>(true);
            hud = GetComponentInChildren<TravellerHud>();
            observers = GetComponentsInChildren<NpcIdentity>(true);
            liveSky = new Material(skyMaterial);
            RenderSettings.skybox = liveSky;
            starPositions = new Vector3[flyingStars.Length];
            for (int i = 0; i < flyingStars.Length; i++)
            {
                starPositions[i] = flyingStars[i].localPosition;
                starCenter += starPositions[i];
            }
            if (flyingStars.Length > 0) starCenter /= flyingStars.Length;
            ApplyMoment(0, true);
            Journey = GetComponent<TravellerJourney>();
            if (Journey == null) Journey = gameObject.AddComponent<TravellerJourney>();
            if(player.bodyActor!=null)playerTorch=player.bodyActor.GetComponent<TorchBearer>();
        }

        public void ApplyMoment(int index, bool instant = false, bool moveToView = true, float duration = -1)
        {
            momentIndex = Mathf.Clamp(index, 0, profile.moments.Length - 1);
            lookingAt = null;
            if (transition != null) StopCoroutine(transition);
            if (!Application.isPlaying || instant) ApplySky(Current, 1f);
            else transition = StartCoroutine(Transition(Current,duration>0?duration:profile.transitionSeconds));
            SetParticles(fires, Current.heat > 0.5f);
            SetParticles(snow, Current.frost > 0.5f);
            if (embers != null) { var emission = embers.emission; emission.enabled = Current.heat > 0.7f; }
            if (heatDetails != null) heatDetails.SetActive(Current.heat > 0.5f);
            if (frostDetails != null) frostDetails.SetActive(Current.frost > 0.5f);
            for (int i = 0; i < flyingStars.Length; i++) flyingStars[i].gameObject.SetActive(i < Current.flyingStars);
            if (moveToView) ResetView();
            Changed?.Invoke();
        }

        private IEnumerator Transition(SceneMoment target,float seconds)
        {
            var startTop = liveSky.GetColor("_Zenith");
            var startHorizon = liveSky.GetColor("_Horizon");
            var startFog = RenderSettings.fogColor;
            var startAmbient = RenderSettings.ambientLight;
            var startLight = sun.color;
            float startIntensity = sun.intensity;
            float startStars = liveSky.GetFloat("_Stars");
            float startDensity = RenderSettings.fogDensity;
            Vector3 startDirection = liveSky.GetVector("_SunA");
            Vector3 startRadii = liveSky.GetVector("_SunRadii");
            // Keep the disc visible as it descends; hide it only after it crosses the horizon.
            bool setting=target.sunCount==0&&liveSky.GetFloat("_SunCount")>0;
            liveSky.SetFloat("_SunCount", setting?1:target.sunCount);
            liveSky.SetVector("_SunB", target.sunB.normalized);
            liveSky.SetVector("_SunC", target.sunC.normalized);
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if(hud!=null&&hud.IsWorldPaused){yield return null;continue;}
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
                liveSky.SetColor("_Zenith", Color.Lerp(startTop, target.zenith, t));
                liveSky.SetColor("_Horizon", Color.Lerp(startHorizon, target.horizon, t));
                liveSky.SetVector("_SunA", Vector3.Slerp(startDirection, target.sunA.normalized, t));
                liveSky.SetVector("_SunRadii", Vector3.Lerp(startRadii, target.sunRadii, t));
                liveSky.SetFloat("_Stars", Mathf.Lerp(startStars, target.stars, t));
                RenderSettings.fogColor = Color.Lerp(startFog, target.fog, t);
                RenderSettings.fogDensity = Mathf.Lerp(startDensity, target.fogDensity, t);
                RenderSettings.ambientLight = Color.Lerp(startAmbient, target.ambient, t);
                sun.color = Color.Lerp(startLight, target.lightColor, t);
                sun.intensity = Mathf.Lerp(startIntensity, target.lightIntensity, t);
                sun.transform.rotation = Quaternion.LookRotation(-((Vector3)liveSky.GetVector("_SunA")).normalized);
                yield return null;
            }
            ApplySky(target, 1f);
            transition = null;
        }

        private void ApplySky(SceneMoment value, float unused)
        {
            var material = liveSky != null ? liveSky : skyMaterial;
            RenderSettings.skybox = material;
            material.SetColor("_Zenith", value.zenith);
            material.SetColor("_Horizon", value.horizon);
            material.SetColor("_Ground", value.fog * 0.4f);
            material.SetVector("_SunA", value.sunA.normalized);
            material.SetVector("_SunB", value.sunB.normalized);
            material.SetVector("_SunC", value.sunC.normalized);
            material.SetVector("_SunRadii", value.sunRadii);
            material.SetFloat("_SunCount", value.sunCount);
            material.SetFloat("_Stars", value.stars);
            sun.color = value.lightColor;
            sun.intensity = value.lightIntensity;
            sun.transform.rotation = Quaternion.LookRotation(-value.sunA.normalized);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = value.ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = value.fog;
            RenderSettings.fogDensity = value.fogDensity;
        }

        private static void SetParticles(ParticleSystem[] particles, bool enabled)
        {
            foreach (var system in particles)
            {
                var emission = system.emission;
                emission.enabled = enabled;
            }
        }

        internal void SetAtmosphereLoss(float loss)
        {
            RenderSettings.fogDensity = Mathf.Lerp(Current.fogDensity, .00035f, loss);
            liveSky.SetColor("_Horizon", Color.Lerp(Current.horizon, Current.zenith, loss));
            liveSky.SetFloat("_Stars", Mathf.Lerp(Current.stars, 1.6f, loss));
        }

        public void ResetView() => player.Teleport(Current.viewPosition, Current.viewTarget);
        public void NotifyWorldPresentationChanged() { Changed?.Invoke(); }

        private bool CanTrackObservations => Journey != null && !Journey.IsPreviewing &&
            !Journey.IsTitle && !Journey.IsSummary && !loading && (hud == null || !hud.IsWorldPaused);

        private SceneObservation ActiveObservation()
        {
            switch ((profile.sceneIndex, momentIndex))
            {
                case (0, 1) when liveSky.GetVector("_SunRadii").x >= .16f:
                    return new SceneObservation("gobi_giant_sun", "戈壁上空出现了一轮异常巨大的太阳。", Current.observation);
                case (0, 3): return new SceneObservation("gobi_one_star", Current.observation);
                case (0, 4): return new SceneObservation("gobi_two_stars", "夜里同时看见两颗飞星，高度和位置不同。", Current.observation);
                case (1, 1) when revival != null && revival.IsComplete:
                    return new SceneObservation("chaoge_revival", "复苏池里，浸过水的人恢复了形体，重新站了起来。", Current.observation);
                case (1, 2) when Journey.Progress.Step == JourneyStep.ObserveLongNight:
                    return new SceneObservation("chaoge_three_stars", "长夜中，三颗飞星靠得很近，绕着彼此旋转。", Current.observation);
                case (1, 2) when Journey.Progress.Elapsed >= 1.5f:
                    switch (Journey.Progress.Step)
                    {
                        case JourneyStep.AfterLongNight:
                            return new SceneObservation("chaoge_snow", "长夜中下起了雪，地面开始泛白。");
                        case JourneyStep.DryIceSnow:
                            return new SceneObservation("chaoge_heavy_snow", "落下的雪片变得更重，地面的积雪更厚了。");
                        case JourneyStep.AtmosphereFreeze:
                            return new SceneObservation("chaoge_blue_snow", "淡蓝色的雪落在地面，火光逐渐熄灭。");
                    }
                    break;
                case (2, 1) when Journey.Progress.Step != JourneyStep.ObserveThreeSuns || Journey.Progress.Elapsed >= 3f:
                    return new SceneObservation("medieval_three_suns", "天空中同时出现了三个太阳。", Current.observation);
            }
            return null;
        }

        internal bool CanObserve => CanTrackObservations && ActiveObservation() != null;

        internal Vector3 ObservationTarget()
        {
            Vector3 eye = player.viewCamera.transform.position;
            if (Current.flyingStars > 0)
            {
                Vector3 center = Vector3.zero;
                for (int i = 0; i < Current.flyingStars; i++) center += flyingStars[i].position;
                return center / Current.flyingStars;
            }
            if (profile.sceneIndex == 1 && momentIndex == 1 && revival != null && revival.citizens.Length > 0)
            {
                Vector3 nearest = revival.citizens[0].transform.position + Vector3.up;
                foreach (var citizen in revival.citizens)
                {
                    var target = citizen.transform.position + Vector3.up;
                    if ((eye - target).sqrMagnitude < (eye - nearest).sqrMagnitude) nearest = target;
                }
                return nearest;
            }
            if (Current.sunCount > 0)
            {
                Vector3 center = (Vector3)liveSky.GetVector("_SunA");
                if (Current.sunCount == 3) center += (Vector3)liveSky.GetVector("_SunB") + (Vector3)liveSky.GetVector("_SunC");
                return eye + center.normalized * 100f;
            }
            return Current.viewTarget;
        }

        private bool CanWitness(SceneObservation observation, Vector3 eye, Transform observer, Camera camera = null)
        {
            bool Visible(Vector3 point, Transform subject = null, bool checkCamera = true)
            {
                if (checkCamera && camera != null)
                {
                    var view = camera.WorldToViewportPoint(point);
                    if (view.z <= 0 || view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1) return false;
                }
                var delta = point - eye;
                foreach (var hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(observer) || subject != null && hit.transform.IsChildOf(subject)) continue;
                    return false;
                }
                return true;
            }

            switch (observation.id)
            {
                case "gobi_giant_sun":
                case "medieval_three_suns":
                    int count = observation.id == "gobi_giant_sun" ? 1 : 3;
                    if (liveSky.GetFloat("_SunCount") < count) return false;
                    float distance = player.viewCamera.farClipPlane * .8f;
                    return Visible(eye + (Vector3)liveSky.GetVector("_SunA") * distance) &&
                        (count == 1 || Visible(eye + (Vector3)liveSky.GetVector("_SunB") * distance) &&
                            Visible(eye + (Vector3)liveSky.GetVector("_SunC") * distance));
                case "chaoge_revival":
                    foreach (var citizen in revival.citizens)
                    {
                        var point = citizen.transform.position + Vector3.up;
                        if ((point - eye).sqrMagnitude < 32f * 32f && Visible(point, citizen.transform)) return true;
                    }
                    return false;
                case "chaoge_snow":
                case "chaoge_heavy_snow":
                case "chaoge_blue_snow":
                    // Snow is spawned outdoors around the current player, including nearby witnesses.
                    return (eye - player.viewCamera.transform.position).sqrMagnitude < 12f * 12f &&
                        Visible(eye + Vector3.up * 8f, checkCamera: false) &&
                        (camera == null || Visible(eye + camera.transform.forward * 5f + Vector3.up));
                default:
                    for (int i = 0; i < Current.flyingStars; i++)
                        if (!flyingStars[i].gameObject.activeInHierarchy || !Visible(flyingStars[i].position, flyingStars[i])) return false;
                    return Current.flyingStars > 0;
            }
        }

        internal void ResetObservationWitnesses()
        {
            witnessed.Clear();
            lookingAt = recordedAt = null;
        }

        private void RememberWitness(NpcIdentity observer, SceneObservation fact)
        {
            if (!witnessed.TryGetValue(observer, out var known)) witnessed[observer] = known = new List<SceneObservation>();
            if (!known.Exists(item => item.id == fact.id) && CanWitness(fact, observer.ConversationPoint, observer.transform))
                known.Add(fact);
        }

        internal List<SceneObservation> ObservationsKnownTo(NpcIdentity observer)
        {
            if (!CanTrackObservations) return new List<SceneObservation>();
            var fact = ActiveObservation();
            if (fact != null) RememberWitness(observer, fact);
            return witnessed.TryGetValue(observer, out var known) ? new List<SceneObservation>(known) : new List<SceneObservation>();
        }

        internal void RecordConfirmedObservation(SceneObservation observation, string confirmedBy)
        {
            if (CanTrackObservations && Journal.Record(observation, confirmedBy))
            {
                Notice?.Invoke("已记入观察簿 · " + confirmedBy + "确认");
                Changed?.Invoke();
            }
        }

        public bool RecordObservation()
        {
            if (Journey != null && !Journey.IsPreviewing) return RecordLiveObservation(ActiveObservation(), false);
            if (profile.sceneIndex == 1 && momentIndex == 1 && !IsRevivalComplete)
            {
                Notice?.Invoke("先观察他们浸水、恢复形体并重新站稳，再记录复苏的结果。");
                return false;
            }
            if (string.IsNullOrEmpty(Current.observation))
            {
                Notice?.Invoke("先观察环境；这一时刻没有待记录的天象。");
                return false;
            }
            Vector3 direction = Current.viewTarget - player.viewCamera.transform.position;
            if ((Current.flyingStars > 0 || Current.sunCount > 1) && Vector3.Angle(player.viewCamera.transform.forward, direction) > 48f)
            {
                Notice?.Invoke("抬头看向观测方向，或按 R 请旅人指认。");
                return false;
            }
            bool added = Journal.Add("亲眼观察", Current.observation);
            Notice?.Invoke(added ? "已记入观察簿。" : "这条观察已经记录。");
            Changed?.Invoke();
            ObservationRecorded?.Invoke(momentIndex);
            return added;
        }

        private bool RecordLiveObservation(SceneObservation fact, bool automatic)
        {
            if (!CanTrackObservations || fact == null) return false;
            var camera = player.viewCamera;
            if (!CanWitness(fact, camera.transform.position, player.transform, camera))
            {
                if (!automatic) Notice?.Invoke("看清眼前的现象后再记录，按 R 可转向观察位置。");
                return false;
            }
            recordedAt = fact.id;
            recordedStep = Journey.Progress.Step;
            bool added = Journal.Record(fact);
            if (added || !automatic) Notice?.Invoke(added ? "已记入观察簿 · " + fact.text : "这条观察已经记录。");
            Changed?.Invoke();
            ObservationRecorded?.Invoke(momentIndex);
            return added;
        }

        private void TrackObservations()
        {
            if (!CanTrackObservations)
            {
                lookingAt = null;
                return;
            }
            float now = Time.unscaledTime;
            if (now < nextObservationSample) return;
            nextObservationSample = now + .2f;
            var fact = ActiveObservation();
            if (fact == null) { lookingAt = null; return; }
            var camera = player.viewCamera;
            Vector3 eye = camera.transform.position;
            foreach (var observer in observers)
                if (observer.CanAnswer && (observer.ConversationPoint - eye).sqrMagnitude < 24f * 24f)
                    RememberWitness(observer, fact);

            if (player.InputBlocked || Journey.DialogueOpen || recordedAt == fact.id && recordedStep == Journey.Progress.Step ||
                !CanWitness(fact, eye, player.transform, camera))
            {
                lookingAt = null;
                return;
            }
            if (lookingAt != fact.id) { lookingAt = fact.id; lookStarted = now; }
            else if (now - lookStarted >= .55f) RecordLiveObservation(fact, true);
        }

        public void LoadScene(int index)
        {
            if (loading || index < 0 || index >= SceneNames.Length || index == profile.sceneIndex) return;
            loading = true;
            DestinationTitle = index == 1 ? "数日之后\n朝歌 · 殷商王都" : index == 2 ? "文明更替\n中世纪" : "旅途再启\n戈壁";
            player.InputBlocked = true;
            Changed?.Invoke();
            StartCoroutine(LoadAsync(SceneNames[index]));
        }
        public void ReloadCurrentScene(string title)
        {
            if (loading) return;
            loading = true; DestinationTitle = title; player.InputBlocked = true; Changed?.Invoke();
            StartCoroutine(LoadAsync(SceneNames[profile.sceneIndex]));
        }

        private static IEnumerator LoadAsync(string name)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            var operation = SceneManager.LoadSceneAsync(name);
            while (operation != null && !operation.isDone) yield return null;
        }

        private void Update()
        {
            motionTime += Time.deltaTime;
            for (int i = 0; i < flyingStars.Length; i++)
            {
                if (!flyingStars[i].gameObject.activeSelf) continue;
                if (profile.sceneIndex == 1 && Current.flyingStars == 3)
                {
                    float angle = motionTime * .56f + i * Mathf.PI * 2 / 3;
                    flyingStars[i].localPosition = starCenter + new Vector3(10 * Mathf.Cos(angle), 8 * Mathf.Sin(angle), 3 * Mathf.Cos(angle));
                }
                else flyingStars[i].localPosition = starPositions[i] + Vector3.right * (Mathf.Sin(motionTime * 0.045f + i) * 12f);
            }
            if (ambience != null) ambience.pitch = Mathf.Lerp(0.75f, 1.1f, Current.heat);
            TrackObservations();
            if (player.InputBlocked) return;
            if(Input.GetKeyDown(KeyCode.T)&&playerTorch!=null)playerTorch.ToggleCarrying();
            if (Journey != null && !Journey.IsPreviewing)
            {
                if (Input.GetKeyDown(KeyCode.R)) Journey.FaceGoal();
                if (Input.GetKeyDown(KeyCode.E) && Journey.CanObserve) RecordObservation();
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.RightArrow)) ApplyMoment((momentIndex + 1) % profile.moments.Length);
                if (Input.GetKeyDown(KeyCode.LeftArrow)) ApplyMoment((momentIndex + profile.moments.Length - 1) % profile.moments.Length);
                if (Input.GetKeyDown(KeyCode.R)) ResetView();
                if (Input.GetKeyDown(KeyCode.E)) RecordObservation();
                if (Input.GetKeyDown(KeyCode.Alpha1)) LoadScene(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) LoadScene(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) LoadScene(2);
            }
        }

        private void OnDestroy()
        {
            if (liveSky != null) Destroy(liveSky);
        }
    }
}
