using System;
using System.Collections.Generic;

namespace ThreeBodyProblem
{
    public enum NpcDecisionOutcome { None, Advance, Warning, Pursue, Attack, StandDown }

    // The model interprets speech. This state machine grants only the advertised
    // chapter actions; a line of dialogue can never directly change health or scenes.
    public sealed class NpcDecisionState
    {
        public int Revision { get; private set; }
        public bool WarningActive { get; private set; }
        public bool Pursuing { get; private set; }
        public bool Lethal { get; private set; }
        public bool Defeated { get; private set; }
        public static bool IsAuthority(string npc, JourneyStep step) =>
            npc == "king" && step == JourneyStep.CourtTalk || npc == "pope" && step == JourneyStep.TribunalTalk;
        public static bool IsConversant(string npc, JourneyStep step) =>
            npc == "king" && step == JourneyStep.CourtTalk || step == JourneyStep.TribunalTalk && (npc == "pope" || npc == "scholar" || npc == "davinci");
        public static bool IsCooperative(string conduct) => conduct == "relevant_proposal" || conduct == "agreement";
        public List<string> Allowed(string npc, JourneyStep step, bool audienceReady, bool guardsAvailable)
        {
            var values = new List<string> { "continue" };
            if (Defeated) return values;
            if (IsConversant(npc, step) && audienceReady && !Pursuing) values.Add("advance");
            if (!IsAuthority(npc, step)) return values;
            if (!WarningActive && !Pursuing) values.Add("warn");
            if (WarningActive && !Pursuing && guardsAvailable) values.Add("pursue");
            if (WarningActive && !Lethal && guardsAvailable) values.Add("attack");
            if (WarningActive || Pursuing) values.Add("stand_down");
            return values;
        }
        public NpcDecisionOutcome Apply(string npc, JourneyStep step, bool audienceReady, bool guardsAvailable,
            string intent, string conduct, bool online, int requestRevision)
        {
            if (!online || requestRevision != Revision || !Allowed(npc, step, audienceReady, guardsAvailable).Contains(intent)) return NpcDecisionOutcome.None;
            switch (intent)
            {
                case "advance":
                    if (!IsCooperative(conduct)) return NpcDecisionOutcome.None;
                    WarningActive = false; Revision++; return NpcDecisionOutcome.Advance;
                case "warn":
                    if (conduct != "disruption" && conduct != "violent_threat" && !(npc == "king" && conduct == "court_incitation")) return NpcDecisionOutcome.None;
                    WarningActive = true; Revision++; return NpcDecisionOutcome.Warning;
                case "pursue":
                    if (conduct != "persistent_defiance" && conduct != "violent_threat") return NpcDecisionOutcome.None;
                    Pursuing = true; Revision++; return NpcDecisionOutcome.Pursue;
                case "attack":
                    if (conduct != "violent_threat") return NpcDecisionOutcome.None;
                    Pursuing = true; Lethal = true; Revision++; return NpcDecisionOutcome.Attack;
                case "stand_down":
                    if (conduct != "apology" && !IsCooperative(conduct)) return NpcDecisionOutcome.None;
                    WarningActive = false; Pursuing = false; Lethal = false; Revision++; return NpcDecisionOutcome.StandDown;
                default: return NpcDecisionOutcome.None;
            }
        }
        public void MarkDefeated() { if (Pursuing) { Defeated = true; Revision++; } }
        public void Reset() { WarningActive = Pursuing = Defeated = Lethal = false; Revision++; }
    }
}
