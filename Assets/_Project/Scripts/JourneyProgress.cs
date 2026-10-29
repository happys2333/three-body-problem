using System;

namespace ThreeBodyProblem
{
    public enum JourneyStep
    {
        Title, SeekShelter, ShelterTalk, Sunset, WalkToFirstStar, ObserveFirstStar,
        WalkToTwoStars, ObserveTwoStars, WalkToChaoge, DepartForCapital,
        CourtApproach, CourtTalk, CalendarWait, WalkToWater, ObserveRevival,
        AfterRevival, WaitForDawn, ObserveLongNight, AfterLongNight,
        TribunalApproach, TribunalTalk, WalkOutside, ObserveThreeSuns, AfterThreeSuns, Collapse, Summary
    }

    // Progress is driven by this run's actions, not by old entries in the saved notebook.
    public sealed class JourneyProgress
    {
        public JourneyStep Step { get; private set; } = JourneyStep.Title;
        public float Elapsed { get; private set; }
        public bool KnowsIdentity { get; private set; }
        public bool OfferedExplanation { get; private set; }
        public bool ConsideredExplanation { get; private set; }
        public int ObservationsThisRun { get; private set; }
        public bool GiantSunSeen { get; private set; }
        public float ShelterWaitSeconds { get; private set; }=600;
        public float SuddenSunsetSeconds { get; private set; }=2;
        public float MinimumNightWalkSeconds { get; private set; }=12;
        public float MinimumNightWalkMetres { get; private set; }=35;
        public float NightWalkMetres { get; private set; }
        private bool firstStarRecorded,twoStarsRecorded;
        public int SceneIndex => Step < JourneyStep.CourtApproach ? 0 : Step < JourneyStep.TribunalApproach ? 1 : 2;
        public event Action Changed;

        public void Begin()
        {
            KnowsIdentity = false; OfferedExplanation = false; ConsideredExplanation = false; ObservationsThisRun = 0;
            GiantSunSeen=false;firstStarRecorded=false;twoStarsRecorded=false;NightWalkMetres=0;
            Enter(JourneyStep.SeekShelter);
        }

        public void ReturnToTitle() => Enter(JourneyStep.Title);
        public void IdentifyTraveller() { if (Step == JourneyStep.ShelterTalk||Step==JourneyStep.SeekShelter) { KnowsIdentity = true; Changed?.Invoke(); } }
        public void ConfigureGobi(float shelterSeconds,float sunsetSeconds,float walkSeconds,float walkMetres)
        {ShelterWaitSeconds=Math.Max(.1f,shelterSeconds);SuddenSunsetSeconds=Math.Max(.1f,sunsetSeconds);MinimumNightWalkSeconds=Math.Max(0,walkSeconds);MinimumNightWalkMetres=Math.Max(0,walkMetres);}
        public void MarkGiantSunVisible(){if(Step==JourneyStep.SeekShelter){GiantSunSeen=true;Changed?.Invoke();}}
        public void AddNightWalking(float metres){if(Step==JourneyStep.WalkToChaoge)NightWalkMetres+=Math.Max(0,metres);}
        public void Explain(bool offered)
        {
            if (Step != JourneyStep.TribunalTalk) return;
            ConsideredExplanation = true; OfferedExplanation = offered; Changed?.Invoke();
        }

        public bool Arrive()
        {
            switch (Step)
            {
                case JourneyStep.SeekShelter: if (Elapsed < 6f||!GiantSunSeen) return false; Enter(JourneyStep.ShelterTalk); return true;
                case JourneyStep.WalkToFirstStar: Enter(JourneyStep.ObserveFirstStar); return true;
                case JourneyStep.WalkToTwoStars: Enter(JourneyStep.ObserveTwoStars); return true;
                case JourneyStep.WalkToChaoge:
                    if(Elapsed<MinimumNightWalkSeconds||NightWalkMetres<MinimumNightWalkMetres)return false;
                    Enter(JourneyStep.CourtApproach);return true;
                case JourneyStep.CourtApproach: Enter(JourneyStep.CourtTalk); return true;
                case JourneyStep.WalkToWater: Enter(JourneyStep.ObserveRevival); return true;
                case JourneyStep.TribunalApproach: Enter(JourneyStep.TribunalTalk); return true;
                case JourneyStep.WalkOutside: Enter(JourneyStep.ObserveThreeSuns); return true;
                default: return false;
            }
        }

        public bool Continue()
        {
            switch (Step)
            {
                case JourneyStep.ShelterTalk: return false;
                case JourneyStep.DepartForCapital: Enter(JourneyStep.CourtApproach); return true;
                case JourneyStep.CourtTalk: Enter(JourneyStep.CalendarWait); return true;
                case JourneyStep.AfterRevival: Enter(JourneyStep.WaitForDawn); return true;
                case JourneyStep.AfterLongNight: Enter(JourneyStep.TribunalApproach); return true;
                case JourneyStep.TribunalTalk: if (!ConsideredExplanation) return false; Enter(JourneyStep.WalkOutside); return true;
                case JourneyStep.AfterThreeSuns: Enter(JourneyStep.Collapse); return true;
                default: return false;
            }
        }

        public bool Observe(int scene, int moment)
        {
            if(Step==JourneyStep.WalkToChaoge&&scene==0)
            {
                if(moment==3&&!firstStarRecorded){firstStarRecorded=true;ObservationsThisRun++;Changed?.Invoke();return true;}
                if(moment==4&&!twoStarsRecorded){twoStarsRecorded=true;ObservationsThisRun++;Changed?.Invoke();return true;}
                return false;
            }
            JourneyStep next;
            if (Step == JourneyStep.ObserveFirstStar && scene == 0 && moment == 3) next = JourneyStep.WalkToTwoStars;
            else if (Step == JourneyStep.ObserveTwoStars && scene == 0 && moment == 4) next = JourneyStep.WalkToChaoge;
            else if (Step == JourneyStep.ObserveRevival && scene == 1 && moment == 1) next = JourneyStep.AfterRevival;
            else if (Step == JourneyStep.ObserveLongNight && scene == 1 && moment == 2) next = JourneyStep.AfterLongNight;
            else if (Step == JourneyStep.ObserveThreeSuns && scene == 2 && moment == 1 && Elapsed >= 3f) next = JourneyStep.AfterThreeSuns;
            else return false;
            ObservationsThisRun++; Enter(next); return true;
        }

        public void Tick(float seconds)
        {
            Elapsed += Math.Max(0f, seconds);
            if(Step==JourneyStep.ShelterTalk&&Elapsed>=ShelterWaitSeconds)Enter(JourneyStep.Sunset);
            else if (Step == JourneyStep.Sunset && Elapsed >= SuddenSunsetSeconds) Enter(JourneyStep.WalkToChaoge);
            else if (Step == JourneyStep.CalendarWait && Elapsed >= 7f) Enter(JourneyStep.WalkToWater);
            else if (Step == JourneyStep.WaitForDawn && Elapsed >= 5f) Enter(JourneyStep.ObserveLongNight);
            else if (Step == JourneyStep.Collapse && Elapsed >= 9f) Enter(JourneyStep.Summary);
        }

        private void Enter(JourneyStep step) { Step = step; Elapsed = 0f; Changed?.Invoke(); }
    }
}
