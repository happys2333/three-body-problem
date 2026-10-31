using System;
using System.Collections;
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
        public string DestinationTitle { get; private set; }
        private float motionTime;
        private Vector3[] starPositions;
        private TorchBearer playerTorch;
        private CapitalRevivalDirector revival;
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
            liveSky = new Material(skyMaterial);
            RenderSettings.skybox = liveSky;
            starPositions = new Vector3[flyingStars.Length];
            for (int i = 0; i < flyingStars.Length; i++) starPositions[i] = flyingStars[i].localPosition;
            ApplyMoment(0, true);
            Journey = GetComponent<TravellerJourney>();
            if (Journey == null) Journey = gameObject.AddComponent<TravellerJourney>();
            if(player.bodyActor!=null)playerTorch=player.bodyActor.GetComponent<TorchBearer>();
        }

        public void ApplyMoment(int index, bool instant = false, bool moveToView = true, float duration = -1)
        {
            momentIndex = Mathf.Clamp(index, 0, profile.moments.Length - 1);
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
            var hud=GetComponentInChildren<TravellerHud>();
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

        public void ResetView() => player.Teleport(Current.viewPosition, Current.viewTarget);
        public void NotifyWorldPresentationChanged() { Changed?.Invoke(); }

        public bool RecordObservation()
        {
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
            if (!string.IsNullOrEmpty(Current.interpretation)) Journal.Add("人物判断", Current.interpretation);
            Notice?.Invoke(added ? "已记入观察簿。观察与人物判断分别保存。" : "这条观察已经记录。");
            Changed?.Invoke();
            ObservationRecorded?.Invoke(momentIndex);
            return added;
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
                if (flyingStars[i].gameObject.activeSelf)
                    flyingStars[i].localPosition = starPositions[i] + Vector3.right * (Mathf.Sin(motionTime * 0.045f + i) * 12f);
            }
            if (ambience != null) ambience.pitch = Mathf.Lerp(0.75f, 1.1f, Current.heat);
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
