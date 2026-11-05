using System.Linq;
using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class PalaceSoundscape : MonoBehaviour
    {
        public TravellerStage stage;
        public PalaceSkylight skylight;
        public AudioSource sand, stone, footsteps;
        public AudioSource[] fires;
        private TravellerHud hud;
        private CharacterController controller;
        private AudioSource[] all;
        private Vector3 previousPlayer;
        private float previousOpening, outdoorVolume, indoor, walked;
        private bool paused, started;
        private int steps;
        public float IndoorBlend => indoor;

        private void Start()
        {
            hud = stage.GetComponentInChildren<TravellerHud>();
            controller = stage.player.GetComponent<CharacterController>();
            outdoorVolume = stage.ambience.volume; previousPlayer = stage.player.transform.position;
            previousOpening = skylight.Opening;
            all = fires.Concat(new[] { sand, stone, footsteps }).ToArray();
            started = true;
        }
        private void Update()
        {
            var player = stage.player.transform.position;
            var delta = Vector3.ProjectOnPlane(player - previousPlayer, Vector3.up); previousPlayer = player;
            bool freeze = hud != null && hud.IsWorldPaused;
            if (freeze != paused)
            {
                paused = freeze;
                foreach (var source in all) { if (paused) source.Pause(); else source.UnPause(); }
            }
            if (paused) return;
            float edge = Mathf.Min(14 - Mathf.Abs(player.x), Mathf.Min(player.z - 4, 42 - player.z));
            float target = player.y > 3.5f && player.y < 12.5f ? Mathf.Clamp01(edge / 2) : 0;
            indoor = Mathf.MoveTowards(indoor, target, Time.deltaTime * 2);
            stage.ambience.volume = outdoorVolume * Mathf.Lerp(1, .18f, indoor);
            foreach (var fire in fires) SetLoop(fire, indoor > .01f && stage.Combustion > 0f);
            SetLoop(sand, indoor > .01f && stage.Journey.Progress.Step == JourneyStep.CalendarWait && !stage.HasPendingCourtAudience);
            SetLoop(stone, indoor > .01f && Mathf.Abs(skylight.Opening - previousOpening) > .00001f);
            previousOpening = skylight.Opening;
            if (indoor < .5f || delta.sqrMagnitude > 4 || stage.player.InputBlocked || !controller.isGrounded) { walked = 0; return; }
            walked += delta.magnitude;
            float stride = stage.player.IsRunning ? 1.1f : .75f;
            if (walked < stride) return;
            walked %= stride; footsteps.pitch = .96f + .07f * Mathf.Sin(++steps * 2.4f); footsteps.PlayOneShot(footsteps.clip);
        }
        private static void SetLoop(AudioSource source, bool active)
        {
            if (active && !source.isPlaying) source.Play();
            else if (!active && source.isPlaying) source.Stop();
        }
        private void OnDestroy() { if (started && stage != null && stage.ambience != null) stage.ambience.volume = outdoorVolume; }
    }
}
