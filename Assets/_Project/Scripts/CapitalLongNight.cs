using System;
using System.Linq;
using UnityEngine;

namespace ThreeBodyProblem
{
    // The collapse is authored. Model replies cannot skip, extend, or alter it.
    internal sealed class CapitalLongNight : IDisposable
    {
        private readonly TravellerStage stage;
        private readonly TravellerJourney journey;
        private readonly TravellerCastDirector cast;
        private readonly CapitalRevivalDirector revival;
        private readonly ParticleSystem steam;
        private readonly ParticleSystem snow;
        private readonly Transform exitSign;
        private CapitalSnowCover snowCover;
        private readonly TorchFlameBillboard[] fixedFlames;
        private readonly ParticleSystem[] fireParticles;
        private readonly (Light light, float intensity)[] fireLights;
        private bool[] fireWasPlaying;
        private int courtCue;
        private bool castExited;

        internal Vector3 CourtFocus => cast != null && cast.king != null
            ? cast.king.transform.position + Vector3.up * 1.45f
            : stage.profile.courtGoal + new Vector3(0, 2, 12);

        internal CapitalLongNight(TravellerStage stage, TravellerJourney journey)
        {
            this.stage = stage;
            this.journey = journey;
            cast = stage.GetComponent<TravellerCastDirector>();
            revival = stage.GetComponent<CapitalRevivalDirector>();
            var effects = stage.GetComponentsInChildren<ParticleSystem>(true);
            steam = effects.FirstOrDefault(effect => effect.name == "Gentle ritual steam");
            fireParticles = effects.Where(effect => effect.name == "Brazier flame" ||
                effect.name == "Torch sparks" || effect.name == "Torch smoke").ToArray();
            fixedFlames = stage.GetComponentsInChildren<TorchFlameBillboard>(true);
            fireLights = stage.GetComponentsInChildren<Light>(true)
                .Where(light => light.name == "Brazier light" || light.name == "Torch light")
                .Select(light => (light, light.intensity)).ToArray();

            if (stage.snow.Length > 0 && stage.snow[0] != null)
            {
                snow = UnityEngine.Object.Instantiate(stage.snow[0], stage.transform);
                snow.name = "Long night snow near the observer";
                snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = snow.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = 7;
                main.startSpeed = 0;
                main.prewarm = false;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.maxParticles = 900;
                var shape = snow.shape;
                shape.scale = new Vector3(20, 1.5f, 20);
                snow.gameObject.SetActive(false);
            }

            var marker = new GameObject("EXIT above the stone passage", typeof(TextMesh));
            exitSign = marker.transform;
            exitSign.SetParent(stage.transform, false);
            exitSign.SetPositionAndRotation(new Vector3(-1.2f, 6.1f, -8.6f), Quaternion.Euler(0, 180, 0));
            var text = marker.GetComponent<TextMesh>();
            text.text = "EXIT";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 72;
            text.characterSize = .17f;
            text.color = new Color(1f, .19f, .15f);
            marker.SetActive(false);
        }

        internal void Enter(JourneyStep step)
        {
            if (step == JourneyStep.Title || step == JourneyStep.Awakening)
            {
                Reset();
                return;
            }
            if (step == JourneyStep.WaitForDawn)
            {
                SetStars(false);
                foreach (var system in stage.snow)
                {
                    var emission = system.emission;
                    emission.enabled = false;
                    system.Clear();
                }
            }
            else if (step == JourneyStep.CalendarFailed)
            {
                courtCue = 0;
                if (cast != null && cast.companion != null)
                {
                    cast.companion.StopTravelling();
                    // The audience opens after the two-day time jump, with Wen already before the throne.
                    cast.companion.transform.position = stage.profile.courtGoal + new Vector3(-2.5f, 0, 2.6f);
                    cast.companion.FaceTarget = cast.king != null ? cast.king.transform : null;
                    cast.companion.Gesture(cast.companion.MotionDuration("MX_KneelDown") > 0 ? "MX_KneelDown" : "MX_Bow", 5);
                }
            }
            else if (step == JourneyStep.ObserveLongNight)
                SetStars(true);
            else if (step == JourneyStep.AfterLongNight)
            {
                snowCover ??= new CapitalSnowCover(stage);
                SetSnow(new Color(.88f, .94f, 1f, .92f), .105f, -1.8f, 94);
            }
            else if (step == JourneyStep.DryIceSnow)
            {
                snowCover?.SetDeposits(Vector3.right);
                SetSnow(new Color(.73f, .77f, .84f, .96f), .17f, -2.6f, 105);
            }
            else if (step == JourneyStep.AtmosphereFreeze)
            {
                snowCover?.SetDeposits(new Vector3(1, 1, 0));
                SetSnow(new Color(.49f, .82f, 1f, .94f), .14f, -1.45f, 110);
                SetCombustion(.99f);
            }
            else if (step == JourneyStep.CapitalEnding)
            {
                stage.SetAtmosphereLoss(1);
                SetCombustion(0f);
                snowCover?.SetDeposits(Vector3.one);
                if (snow != null) snow.gameObject.SetActive(false);
            }
        }

        internal void Update(JourneyStep step, float elapsed)
        {
            if (snow != null && snow.gameObject.activeSelf)
                snow.transform.position = stage.player.transform.position + Vector3.up * 7;
            if (step == JourneyStep.AfterLongNight)
                snowCover?.SetDeposits(new Vector3(Mathf.Clamp01(elapsed / JourneyProgress.SnowStageSeconds), 0, 0));
            else if (step == JourneyStep.DryIceSnow)
                snowCover?.SetDeposits(new Vector3(1, Mathf.Clamp01(elapsed / JourneyProgress.SnowStageSeconds), 0));
            else if (step == JourneyStep.AtmosphereFreeze)
            {
                float loss = Mathf.Clamp01(elapsed / JourneyProgress.AtmosphereFreezeSeconds);
                stage.SetAtmosphereLoss(loss);
                SetCombustion(.99f * (1f - Mathf.SmoothStep(0f, 1f, elapsed / 2f)));
                snowCover?.SetDeposits(new Vector3(1, 1, loss));
            }
            if (step != JourneyStep.CalendarFailed) return;

            if (courtCue == 0 && elapsed >= 3f)
            {
                courtCue++;
                cast?.king?.Gesture("MX_AnnoyedHeadShake");
                journey.Speak("纣王：等了两日，天还是黑的。把鼎火生起来。", 3);
            }
            if (courtCue == 1 && elapsed >= 6f)
            {
                courtCue++;
                SetStars(true);
                journey.Speak("殿外来报：三颗飞星已经出现！姬昌垂下头。他知道酷寒将至，历法已无可挽回。", 4);
            }
            if (courtCue == 2 && elapsed >= 10f)
            {
                courtCue++;
                if (revival != null)
                    foreach (var citizen in revival.citizens) citizen.ResetDry();
                journey.Speak("纣王：传令脱水。将来得及收拢的人送进储藏所。", 4);
                cast?.companion?.FollowRoute(new[]
                {
                    new Vector3(-4.7f, 4, 21), new Vector3(-7.4f, 4, 27.5f), new Vector3(-9.4f, 4, 29.5f)
                }, 4.6f);
            }
            if (courtCue == 3 && elapsed >= 17.5f)
            {
                courtCue++;
                castExited = true;
                if (steam != null) steam.Emit(42);
                if (cast != null && cast.companion != null)
                {
                    cast.companion.StopTravelling();
                    cast.companion.gameObject.SetActive(false);
                }
                journey.Speak("姬昌走向铜鼎，身影没入蒸汽。纣王：火收小些。这一局到这里了，诸位可以退出。", 4);
                exitSign.gameObject.SetActive(true);
            }
            if (courtCue == 4 && elapsed >= 21f)
            {
                courtCue++;
                if (cast != null && cast.king != null) cast.king.gameObject.SetActive(false);
                journey.Speak("纣王已退出。循红色 EXIT 标志离开王殿。", 7f);
            }
        }

        private void SetCombustion(float strength)
        {
            float previous = stage.Combustion;
            if (previous == strength) return;
            stage.Combustion = strength;
            foreach (var flame in fixedFlames) flame.SetCombustion(strength);
            foreach (var (light, intensity) in fireLights) light.intensity = intensity * strength;

            if (strength < 1f && previous == 1f)
            {
                fireWasPlaying = fireParticles.Select(fire => fire.isPlaying).ToArray();
                foreach (var fire in fireParticles)
                    fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (strength == 0f)
                foreach (var fire in fireParticles)
                    fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (strength == 1f && fireWasPlaying != null)
                for (int i = 0; i < fireParticles.Length; i++)
                    if (fireWasPlaying[i] && fireParticles[i].gameObject.activeInHierarchy)
                        fireParticles[i].Play();
        }

        private void SetStars(bool active)
        {
            foreach (var star in stage.flyingStars) star.gameObject.SetActive(active);
        }

        private void SetSnow(Color color, float size, float speed, float rate)
        {
            if (snow == null) return;
            snow.gameObject.SetActive(true);
            var main = snow.main;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(size * .55f, size * 1.5f);
            // The inherited box is horizontal. Drive flakes down in world space.
            var fall = snow.velocityOverLifetime;
            fall.y = new ParticleSystem.MinMaxCurve(speed, speed);
            var emission = snow.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            snow.transform.position = stage.player.transform.position + Vector3.up * 7;
            snow.Play();
        }

        internal void Reset()
        {
            SetCombustion(1f);
            snowCover?.SetDeposits(Vector3.zero);
            exitSign.gameObject.SetActive(false);
            if (snow != null) snow.gameObject.SetActive(false);
            if (!castExited || cast == null) return;
            if (cast.companion != null)
            {
                cast.companion.StopTravelling();
                cast.companion.FaceTarget = stage.player.transform;
                cast.companion.gameObject.SetActive(true);
            }
            if (cast.king != null) cast.king.gameObject.SetActive(true);
            castExited = false;
        }

        public void Dispose()
        {
            snowCover?.Dispose();
            if (snow != null) UnityEngine.Object.Destroy(snow.gameObject);
            if (exitSign != null) UnityEngine.Object.Destroy(exitSign.gameObject);
        }
    }
}
