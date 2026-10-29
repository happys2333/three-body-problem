using System.Collections.Generic;
using UnityEngine;

namespace ThreeBodyProblem
{
    // The actor root owns placement; Humanoid animation only moves the visual skeleton.
    public sealed class TravellerActor : MonoBehaviour
    {
        public TravellerRole role;
        public TravellerCastSettings settings;
        public Animator animator;
        public bool seated;
        public TravellerPlayer firstPersonPlayer;
        [Range(0, 1)] public float initialAnimationPhase;
        public string CurrentMotion { get; private set; }
        public string CurrentGesture { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsTravelling => route != null && waypoint < route.Length;
        public bool Paused { get; set; }
        public Transform FaceTarget { get; set; }
        private readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        private readonly Dictionary<string, int> baseHashes = new Dictionary<string, int>();
        private readonly Dictionary<string, int> upperHashes = new Dictionary<string, int>();
        private Vector3 previousPosition;
        private Vector3[] route;
        private int waypoint;
        private float travelSpeed, gestureRemaining, playbackSpeed = 1f;
        private string restMotion = "MX_Idle";
        private string queuedGesture;
        private float queuedSeconds;
        private Vector3 TrackedPosition => firstPersonPlayer != null ? firstPersonPlayer.transform.position : transform.position;

        private void Awake()
        {
            foreach (var clip in settings.clips)
            {
                clips[clip.name] = clip;
                baseHashes[clip.name] = Animator.StringToHash("Base Layer." + clip.name);
                upperHashes[clip.name] = Animator.StringToHash("Seated Gestures." + clip.name);
            }
            animator.applyRootMotion = false;
            animator.SetLayerWeight(1, seated ? 1 : 0);
            previousPosition = TrackedPosition;
            restMotion = seated ? "MX_SitIdle" : "MX_Idle";
            ChangeBase(restMotion, true);
            if (seated) animator.Play(upperHashes["MX_SitIdle"], 1, 0);
        }

        public void ResetMotionTracking()
        {
            previousPosition = TrackedPosition;
            route = null; queuedGesture = null; IsMoving = false; CancelGesture(); ChangeBase(restMotion, true);
        }

        public void SetRestMotion(string id)
        {
            restMotion = seated ? "MX_SitIdle" : id;
            if (!IsMoving && gestureRemaining <= 0) ChangeBase(restMotion);
        }

        public void Gesture(string id, float seconds = 0)
        {
            AnimationClip clip;
            if (!clips.TryGetValue(id, out clip) || role == TravellerRole.PlayerTraveller) return;
            // A travelling companion keeps a matching gait instead of sliding in a talking pose.
            if ((IsMoving || IsTravelling) && !seated) { queuedGesture = id; queuedSeconds = seconds; return; }
            CurrentGesture = id;
            gestureRemaining = Mathf.Min(seconds > 0 ? seconds : settings.gestureSeconds, clip.length);
            playbackSpeed = 1f;
            if (seated) animator.CrossFadeInFixedTime(upperHashes[id], settings.transitionSeconds, 1, 0);
            else ChangeBase(id, true);
        }

        public void CancelGesture()
        {
            gestureRemaining = 0; CurrentGesture = null;
            if (seated && upperHashes.ContainsKey("MX_SitIdle"))
                animator.CrossFadeInFixedTime(upperHashes["MX_SitIdle"], settings.transitionSeconds, 1, 0);
        }

        public void FollowRoute(Vector3[] points, float metresPerSecond)
        {
            route = points; waypoint = 0; travelSpeed = metresPerSecond; queuedGesture = null; CancelGesture();
        }

        public void StopTravelling() { route = null; }

        private void Update()
        {
            animator.speed = Paused ? 0 : playbackSpeed;
            if (Paused) { previousPosition = TrackedPosition; return; }
            if (IsTravelling)
            {
                var delta = route[waypoint] - transform.position;
                if (delta.sqrMagnitude < 0.09f) waypoint++;
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, route[waypoint], travelSpeed * Time.deltaTime);
                    FaceDirection(delta);
                }
            }
            if (gestureRemaining > 0)
            {
                gestureRemaining -= Time.deltaTime;
                if (gestureRemaining <= 0) { CancelGesture(); ChangeBase(restMotion); }
            }
        }

        private void LateUpdate()
        {
            if (Paused || Time.deltaTime <= 0) return;
            var displacement = TrackedPosition - previousPosition;
            previousPosition = TrackedPosition;
            var velocity = Vector3.ProjectOnPlane(displacement, Vector3.up) / Time.deltaTime;
            // Scene arrivals and debug teleports must not become a one-frame sprint.
            if (displacement.sqrMagnitude > 4f || seated) velocity = Vector3.zero;
            if (firstPersonPlayer != null && firstPersonPlayer.InputBlocked) velocity = Vector3.zero;
            IsMoving = velocity.sqrMagnitude > 0.04f;
            if (IsMoving)
            {
                if (gestureRemaining > 0) CancelGesture();
                var local = transform.InverseTransformDirection(velocity);
                bool running = firstPersonPlayer != null ? firstPersonPlayer.IsRunning : velocity.magnitude > 3.3f;
                string id = running ? "MX_Running" : "MX_Walking";
                if (firstPersonPlayer != null)
                {
                    if (Mathf.Abs(local.x) > Mathf.Abs(local.z) * 1.2f) id = local.x < 0 ? "MX_LeftStrafeWalking" : "MX_RightStrafeWalking";
                    else if (local.z < -0.2f) id = "MX_WalkBackward";
                }
                ChangeBase(id);
                float sourceSpeed = clips[id].averageSpeed.magnitude * animator.humanScale / Mathf.Max(0.01f, settings.referenceHumanScale);
                if (sourceSpeed < 0.2f) sourceSpeed = running ? settings.runMetresPerSecond : settings.walkMetresPerSecond;
                playbackSpeed = Mathf.Clamp(velocity.magnitude / sourceSpeed, 0.65f, 2.2f);
            }
            else
            {
                playbackSpeed = 1f;
                if (queuedGesture != null && !IsTravelling) { string id = queuedGesture; queuedGesture = null; Gesture(id, queuedSeconds); }
                if (gestureRemaining <= 0) ChangeBase(restMotion);
                if (!seated && firstPersonPlayer == null && FaceTarget != null &&
                    (FaceTarget.position - transform.position).sqrMagnitude < settings.conversationDistance * settings.conversationDistance)
                    FaceDirection(FaceTarget.position - transform.position);
            }
        }

        private void ChangeBase(string id, bool restart = false)
        {
            if (!restart && CurrentMotion == id) return;
            int hash;
            if (!baseHashes.TryGetValue(id, out hash)) return;
            float phase = clips[id].isLooping ? initialAnimationPhase : 0;
            if (CurrentMotion == null) animator.Play(hash, 0, phase);
            else animator.CrossFadeInFixedTime(hash, settings.transitionSeconds, 0, clips[id].length * phase);
            CurrentMotion = id;
        }

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), settings.turnDegreesPerSecond * Time.deltaTime);
        }
    }
}
