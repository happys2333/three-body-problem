using System;
using UnityEngine;
using UnityEngine.AI;

namespace ThreeBodyProblem
{
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(TravellerStage))]
    public sealed class NpcDecisionController : MonoBehaviour
    {
        public NpcDecisionSettings settings;
        public NavMeshData navigation;
        public int Health { get; private set; }
        public int DamageEvents { get; private set; }
        public float HitPulse { get; private set; }
        public float Blackout { get; private set; }
        public int ReadyGuardCount => guards.Length;
        public bool NavigationReady => meshInstance.valid;
        public bool CanDispatchGuards => meshInstance.valid && guards.Length > 0 && !State.Defeated;
        public bool IsDefeated => stage != null && State.Defeated;
        public NpcDecisionState State => stage.Journey.Decisions;
        private sealed class Guard
        {
            public TravellerActor actor;
            public EraGuardEquipment equipment;
            public NavMeshAgent agent;
            public Vector3 home, start;
            public Quaternion homeRotation;
            public float nextPath, attackAge = -1;
        }
        private Guard[] guards = Array.Empty<Guard>();
        private TravellerStage stage;
        private TravellerHud hud;
        private NpcDialogueDirector dialogue;
        private NavMeshDataInstance meshInstance;
        private NpcConversationMemory.Checkpoint checkpoint;
        private float grace, negotiationRemaining;
        private int previousFrameRate;
        private bool rateApplied, pursuitDispatched;
        private readonly RaycastHit[] sightHits = new RaycastHit[32];

        private void Start()
        {
            stage = GetComponent<TravellerStage>(); hud = GetComponentInChildren<TravellerHud>(); dialogue = GetComponent<NpcDialogueDirector>();
            if (settings == null) { enabled = false; return; }
            previousFrameRate=Application.targetFrameRate;Application.targetFrameRate=Mathf.Clamp(settings.frameRateLimit,60,240);rateApplied=true;
            Health = settings.maximumHealth;
            checkpoint = NpcConversationMemory.Capture();
            if (navigation != null) meshInstance = NavMesh.AddNavMeshData(navigation);
            var candidates = new System.Collections.Generic.List<Guard>();
            // Arrival starts outside the palace navigation bounds. Guards are
            // selected for the audience anchor, not the player's distant spawn.
            Vector3 audienceAnchor=stage.profile.sceneIndex==1?stage.profile.courtGoal:new Vector3(0,0,22);
            NavMeshHit audience; bool hasAudience = NavMesh.SamplePosition(audienceAnchor,out audience,8,NavMesh.AllAreas);
            foreach (var gear in GetComponentsInChildren<EraGuardEquipment>())
            {
                if (gear.profile == null || stage.profile.sceneIndex == 1 && gear.profile.role != TravellerRole.ShangGuard ||
                    stage.profile.sceneIndex == 2 && gear.profile.role != TravellerRole.ChurchGuard) continue;
                NavMeshHit hit;
                if (!meshInstance.valid || !NavMesh.SamplePosition(gear.transform.position, out hit, 2, NavMesh.AllAreas)) continue;
                var path = new NavMeshPath();
                if(!hasAudience||!NavMesh.CalculatePath(hit.position,audience.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                candidates.Add(new Guard { actor = gear.GetComponent<TravellerActor>(), equipment = gear,
                    home = gear.transform.position, start = hit.position, homeRotation = gear.transform.rotation });
            }
            var authority = stage.GetComponent<TravellerCastDirector>();
            var ruler = authority == null ? null : stage.profile.sceneIndex == 1 ? authority.king : authority.pope;
            Vector3 focus = ruler != null ? ruler.transform.position : stage.player.transform.position;
            candidates.Sort((a, b) => (a.home - focus).sqrMagnitude.CompareTo((b.home - focus).sqrMagnitude));
            guards = candidates.GetRange(0, Mathf.Min(settings.maximumGuards, candidates.Count)).ToArray();
            stage.Journey.Changed += OnJourneyChanged;
        }
        public void Execute(NpcDecisionOutcome outcome)
        {
            if (outcome == NpcDecisionOutcome.Pursue || outcome == NpcDecisionOutcome.Attack) BeginPursuit();
            else if (outcome == NpcDecisionOutcome.StandDown) RecallGuards();
        }
        private void BeginPursuit()
        {
            if (pursuitDispatched) return;
            pursuitDispatched = true;
            grace = settings.initialGraceSeconds; negotiationRemaining = settings.negotiationSeconds;
            foreach (var guard in guards)
            {
                guard.actor.StopTravelling(); guard.actor.CancelGesture(); guard.actor.FaceTarget = stage.player.transform;
                guard.equipment.CombatControlled = true; guard.equipment.CombatAim = 1;
                if (guard.agent == null)
                {
                    guard.actor.transform.position = guard.start;
                    guard.agent = guard.actor.gameObject.AddComponent<NavMeshAgent>();
                    guard.agent.radius = .32f; guard.agent.height = 1.8f; guard.agent.baseOffset = 0;
                    guard.agent.speed = settings.pursuitSpeed; guard.agent.acceleration = 9;
                    guard.agent.angularSpeed = 240; guard.agent.stoppingDistance = settings.attackRange * .8f;
                    guard.agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                }
                guard.agent.enabled = true; guard.agent.stoppingDistance = settings.attackRange * .8f;
                guard.agent.isStopped = false; guard.nextPath = 0; guard.attackAge = -1;
            }
        }
        private void Update()
        {
            if (stage == null || settings == null) return;
            HitPulse = Mathf.MoveTowards(HitPulse, 0, Time.unscaledDeltaTime * 2.2f);
            if (IsDefeated) { Blackout = Mathf.MoveTowards(Blackout, 1, Time.unscaledDeltaTime / settings.defeatFadeSeconds); return; }
            if (!State.Pursuing) { UpdateReturns(); return; }
            bool paused = stage.IsLoading || stage.Journey.IsTitle || stage.Journey.IsPreviewing || hud != null && hud.IsWorldPaused;
            bool negotiating = !paused && negotiationRemaining > 0 && dialogue != null && (dialogue.HasPlayerQuestion || dialogue.IsBusy);
            if (negotiating) negotiationRemaining = Mathf.Max(0, negotiationRemaining - Time.deltaTime);
            if (!paused && !negotiating) grace = Mathf.Max(0, grace - Time.deltaTime);
            foreach (var guard in guards)
            {
                if (guard.agent == null || !guard.agent.enabled || !guard.agent.isOnNavMesh) continue;
                bool hold = paused || negotiating || grace > 0;
                guard.agent.isStopped = hold;
                if (!hold) TickGuard(guard);
                if (State.Defeated) break;
            }
        }
        private void TickGuard(Guard guard)
        {
            var delta = stage.player.transform.position - guard.actor.transform.position;
            bool close = Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude <= settings.attackRange * settings.attackRange && Mathf.Abs(delta.y) < 1.4f;
            if (guard.attackAge >= 0)
            {
                guard.agent.isStopped = true; guard.attackAge += Time.deltaTime;
                float attack = guard.attackAge / settings.attackWindup;
                guard.equipment.CombatThrust = attack < 1 ? Mathf.Sin(attack * Mathf.PI * .5f) : Mathf.Max(0, 1 - (guard.attackAge - settings.attackWindup) / settings.attackRecovery);
                var facing = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (facing.sqrMagnitude > .001f) guard.actor.transform.rotation = Quaternion.RotateTowards(guard.actor.transform.rotation, Quaternion.LookRotation(facing), 200 * Time.deltaTime);
                if (guard.attackAge - Time.deltaTime < settings.attackWindup && guard.attackAge >= settings.attackWindup && close && ClearStrike(guard))
                {
                    Health = Mathf.Max(0, Health - settings.attackDamage); DamageEvents++; HitPulse = 1;
                    if (Health == 0) { Defeat(); return; }
                }
                if (guard.attackAge >= settings.attackWindup + settings.attackRecovery) { guard.attackAge = -1; guard.equipment.CombatThrust = 0; }
                return;
            }
            if (close && ClearStrike(guard))
            { guard.agent.isStopped = true; guard.actor.StopTravelling(); guard.attackAge = 0; return; }
            if (Time.time < guard.nextPath) return;
            guard.nextPath = Time.time + settings.pathRefreshSeconds;
            NavMeshHit destination;
            if (NavMesh.SamplePosition(stage.player.transform.position, out destination, 2, NavMesh.AllAreas))
                guard.agent.SetDestination(destination.position);
            else guard.agent.isStopped = true;
        }
        private bool ClearStrike(Guard guard)
        {
            Vector3 start = guard.actor.transform.position + Vector3.up * 1.05f;
            Vector3 delta = stage.player.transform.position + Vector3.up - start;
            int count = Physics.RaycastNonAlloc(start, delta.normalized, sightHits, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (hit.transform.IsChildOf(stage.player.transform) || hit.collider.GetComponentInParent<TravellerActor>() != null) continue;
                return false;
            }
            return true;
        }
        public bool HasStrikeLine(TravellerActor actor)
        { foreach(var guard in guards)if(guard.actor==actor)return ClearStrike(guard);return false; }
        private void RecallGuards()
        {
            pursuitDispatched = false;
            foreach (var guard in guards)
            {
                guard.attackAge = -1; guard.equipment.CombatThrust = guard.equipment.CombatAim = 0;
                if (guard.agent != null && guard.agent.enabled && guard.agent.isOnNavMesh)
                { guard.agent.isStopped = false; guard.agent.stoppingDistance = .12f; guard.agent.SetDestination(guard.start); }
                else guard.equipment.CombatControlled = false;
            }
        }
        private void UpdateReturns()
        {
            if (hud != null && hud.IsWorldPaused) { StopAgents(); return; }
            foreach (var guard in guards)
            {
                if (guard.agent == null || !guard.agent.enabled || !guard.agent.isOnNavMesh) continue;
                guard.agent.isStopped = false;
                if (guard.agent.pathPending || (guard.actor.transform.position - guard.start).sqrMagnitude > .12f) continue;
                guard.agent.enabled = false; guard.actor.transform.rotation = guard.homeRotation;
                guard.actor.FaceTarget = null; guard.equipment.CombatControlled = false;
            }
        }
        private void StopAgents()
        { foreach (var guard in guards) if (guard.agent != null && guard.agent.enabled && guard.agent.isOnNavMesh) guard.agent.isStopped = true; }
        private void Defeat()
        {
            State.MarkDefeated(); StopAgents(); dialogue?.CancelQuestion(); stage.Journey.CancelPendingDialogueDecision();
            stage.Journey.CloseDialogue(); stage.player.InputBlocked = true; stage.player.ReleasePointer();
            foreach (var guard in guards) { guard.equipment.CombatThrust = 0; guard.actor.StopTravelling(); }
            stage.Journey.Speak(State.Lethal?"你被卫兵杀死了。":"你被卫兵制服了。", 4);
        }
        public void RetryAudience()
        {
            if (!IsDefeated) return;
            NpcConversationMemory.Restore(checkpoint); State.Reset();
            stage.Journey.Progress.RestartAudience(); stage.ReloadCurrentScene("重返觐见之前");
        }
        private void OnJourneyChanged()
        {
            if (!stage.Journey.IsTitle && !stage.Journey.IsPreviewing && stage.Journey.Progress.Step != JourneyStep.Awakening) return;
            if (State.WarningActive || State.Pursuing || State.Defeated) { State.Reset(); RecallGuards(); }
            Health = settings.maximumHealth; HitPulse = Blackout = 0;
        }
        private void OnDestroy()
        {
            if (stage != null && stage.Journey != null) stage.Journey.Changed -= OnJourneyChanged;
            if (meshInstance.valid) meshInstance.Remove();
            if(rateApplied)Application.targetFrameRate=previousFrameRate;
        }
    }
}
