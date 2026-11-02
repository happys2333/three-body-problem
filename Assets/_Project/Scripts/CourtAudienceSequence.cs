using System;
using UnityEngine;

namespace ThreeBodyProblem
{
    public sealed class CourtAudienceSequence : MonoBehaviour
    {
        public enum AudienceState { Idle, Approaching, Receiving, Ascending, Presenting, Returning, Complete }
        public TravellerStage stage;
        public CourtAudienceSettings settings;
        public TravellerActor companion, officer, king;
        public CourtPropGrip companionHands, officerHands, kingHands;
        public Transform petition;
        public CharacterController motor;
        public CapsuleCollider restingCapsule;
        private TravellerJourney journey;
        private TravellerHud hud;
        private NpcDialogueDirector dialogue;
        private CourtRetainer retainer;
        private Vector3 home, beforeMove;
        private Quaternion homeFacing;
        private int waypoint;
        private float age, stalled;
        private bool prepared;
        public AudienceState State {get;private set;}
        public bool IsComplete => State==AudienceState.Complete;
        public bool BlocksCalendar => !IsComplete;
        public string PetitionHolder {get;private set;}="未呈奏";
        public float WalkedMetres {get;private set;}
        public float PhaseSeconds => age;
        public Vector3 HomePosition => home;
        public event Action Changed;
        public bool ParticipantSpeaking => dialogue!=null&&(dialogue.HasPlayerQuestion||dialogue.IsBusy||dialogue.IsVocalising)&&dialogue.Current!=null&&
            (dialogue.Current.transform==companion.transform||dialogue.Current.transform==officer.transform);

        private void Start()
        {
            journey=stage.Journey;hud=stage.GetComponentInChildren<TravellerHud>();dialogue=stage.GetComponent<NpcDialogueDirector>();
            retainer=officer.GetComponent<CourtRetainer>();home=officer.transform.position;homeFacing=officer.transform.rotation;
            petition.gameObject.SetActive(false);motor.enabled=false;journey.Changed+=Refresh;prepared=true;Refresh();
        }
        private void Refresh()
        {
            if(journey.IsPreviewing)return;
            if(journey.IsTitle){if(State!=AudienceState.Idle)ResetPresentation();return;}
            if(State==AudienceState.Idle&&(journey.Progress.Step==JourneyStep.CourtTalk||journey.Progress.Step==JourneyStep.CalendarWait))Begin();
        }
        private void Begin()
        {
            retainer.DutyControlled=true;officer.CancelGesture();officer.FaceTarget=null;
            restingCapsule.enabled=false;motor.enabled=true;
            companionHands.kind=officerHands.kind=kingHands.kind=CourtPropGrip.GripKind.Tablet;
            companionHands.rightContact=officerHands.rightContact=kingHands.rightContact=new Vector3(.19f,0,0);
            companionHands.leftContact=officerHands.leftContact=kingHands.leftContact=new Vector3(-.19f,0,0);
            companion.SetRestMotion("MX_CarryBoxIdle");petition.gameObject.SetActive(true);PlaceAt(companion,settings.companionCarry);
            companionHands.Attach(petition);PetitionHolder="姬昌";
            journey.GuideCompanionForAudience(settings.companionHandoff);SetState(AudienceState.Approaching);
            journey.Speak("姬昌：臣已将历法推演记于简上，恭请大王览奏。",7);
        }
        private void Update()
        {
            if(!prepared||State==AudienceState.Idle||journey.IsPreviewing||journey.Decisions.Pursuing||hud!=null&&hud.IsWorldPaused||ParticipantSpeaking)return;
            age+=Time.deltaTime;
            switch(State)
            {
                case AudienceState.Approaching:
                    PlaceAt(companion,settings.companionCarry);
                    bool arrived=Move(settings.officerHandoff);
                    if(arrived&&FlatDistance(companion.transform.position,settings.companionHandoff)<.23f)
                    {
                        Face(officer,companion.transform.position);companion.FaceTarget=officer.transform;
                        if(Vector3.Angle(officer.transform.forward,companion.transform.position-officer.transform.position)>8||
                           Vector3.Angle(companion.transform.forward,officer.transform.position-companion.transform.position)>8)return;
                        journey.GuideCompanionForAudience();companion.BeginPresentation("MX_CarryBoxIdle",0);officer.BeginPresentation("MX_CarryBoxIdle",0);
                        officerHands.Attach(petition);SetState(AudienceState.Receiving);
                    }
                    break;
                case AudienceState.Receiving:
                    Transfer(companion,settings.companionCarry,officer,settings.officerCarry);
                    if(age>=settings.transferSeconds)
                    {
                        companionHands.Release();companion.EndPresentation("MX_Idle");companion.FaceTarget=king.transform;
                        officer.EndPresentation("MX_CarryBoxIdle");PetitionHolder="传奏官";waypoint=0;SetState(AudienceState.Ascending);
                    }
                    break;
                case AudienceState.Ascending:
                    PlaceAt(officer,settings.officerCarry);
                    if(MovePath(false))
                    {
                        Face(officer,king.transform.position);
                        if(Vector3.Angle(officer.transform.forward,Vector3.ProjectOnPlane(king.transform.position-officer.transform.position,Vector3.up))>8)return;
                        officer.BeginPresentation("MX_CarryBoxIdle",0);kingHands.Attach(petition);SetState(AudienceState.Presenting);
                        journey.Speak("传奏官：姬昌所进表章，请大王御览。",5);
                    }
                    break;
                case AudienceState.Presenting:
                    Transfer(officer,settings.officerCarry,king,settings.kingCarry);
                    if(age>=settings.transferSeconds+settings.courtesySeconds)
                    {
                        officerHands.Release();officer.EndPresentation("MX_Idle");PetitionHolder="纣王";waypoint=settings.ascent.Length-1;SetState(AudienceState.Returning);
                    }
                    break;
                case AudienceState.Returning:
                    PlaceAt(king,settings.kingCarry);
                    if(MovePath(true)&&Move(home,.025f))
                    {
                        officer.transform.rotation=Quaternion.RotateTowards(officer.transform.rotation,homeFacing,180*Time.deltaTime);
                        if(Quaternion.Angle(officer.transform.rotation,homeFacing)<3)
                        {motor.enabled=false;restingCapsule.enabled=true;retainer.DutyControlled=false;SetState(AudienceState.Complete);journey.Speak("传奏官已退回阶下。姬昌：请大王以日光与沙漏，查验这些卦象。",7);}
                    }
                    break;
                case AudienceState.Complete:PlaceAt(king,settings.kingCarry);break;
            }
        }
        private bool MovePath(bool reverse)
        {
            if(reverse){if(waypoint<0)return true;if(Move(settings.ascent[waypoint]))waypoint--;return waypoint<0;}
            if(waypoint>=settings.ascent.Length)return true;
            if(Move(settings.ascent[waypoint]))waypoint++;return waypoint>=settings.ascent.Length;
        }
        private bool Move(Vector3 target,float tolerance=.16f)
        {
            Vector3 delta=Vector3.ProjectOnPlane(target-officer.transform.position,Vector3.up);
            if(delta.sqrMagnitude<tolerance*tolerance){motor.Move(Vector3.down*.03f);return true;}
            Face(officer,target);beforeMove=officer.transform.position;
            motor.Move(Vector3.ClampMagnitude(delta,settings.walkSpeed*Time.deltaTime)+Vector3.down*(3*Time.deltaTime));
            float moved=Vector3.Distance(beforeMove,officer.transform.position);WalkedMetres+=moved;
            stalled=moved<.002f?stalled+Time.deltaTime:0;
            if(stalled>4){stalled=0;journey.Speak("传奏官：请为呈奏让出通道。",4);}
            return false;
        }
        private static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        private static void Face(TravellerActor actor,Vector3 target)
        {var delta=Vector3.ProjectOnPlane(target-actor.transform.position,Vector3.up);if(delta.sqrMagnitude>.001f)actor.transform.rotation=Quaternion.RotateTowards(actor.transform.rotation,Quaternion.LookRotation(delta),180*Time.deltaTime);}
        private void PlaceAt(TravellerActor actor,Vector3 point){petition.SetPositionAndRotation(actor.transform.TransformPoint(point),actor.transform.rotation);}
        private void Transfer(TravellerActor a,Vector3 from,TravellerActor b,Vector3 to)
        {float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/settings.transferSeconds));petition.SetPositionAndRotation(Vector3.Lerp(a.transform.TransformPoint(from),b.transform.TransformPoint(to),t),Quaternion.Slerp(a.transform.rotation,b.transform.rotation,t));}
        private void SetState(AudienceState value){State=value;age=0;Changed?.Invoke();}
        private void ResetPresentation()
        {
            companionHands.Release();officerHands.Release();kingHands.Release();petition.gameObject.SetActive(false);
            companion.EndPresentation("MX_Idle");officer.EndPresentation("MX_Idle");retainer.DutyControlled=false;
            motor.enabled=false;restingCapsule.enabled=true;State=AudienceState.Idle;PetitionHolder="未呈奏";WalkedMetres=0;
        }
        private void OnDestroy(){if(journey!=null)journey.Changed-=Refresh;}
    }
}
