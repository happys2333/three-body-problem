using UnityEngine;

namespace ThreeBodyProblem
{
    [RequireComponent(typeof(TravellerActor), typeof(CourtPropGrip))]
    public sealed class NpcHeldBook : MonoBehaviour
    {
        public Transform book;
        public Vector3 readingPosition = new Vector3(0, 1.18f, .12f);
        public Vector3 carryingPosition = new Vector3(0, 1.10f, .12f);
        public Vector3 readingEuler = new Vector3(-18, 180, 0);
        private TravellerActor actor;
        private CourtPropGrip grip;
        private TravellerHud hud;
        private TravellerStage stage;
        private NpcDialogueDirector dialogue;
        private NpcIdentity identity;
        public bool IsReading => book != null && grip != null && grip.IsHolding && !actor.IsMoving;

        private void Awake()
        {
            actor = GetComponent<TravellerActor>();
            grip = GetComponent<CourtPropGrip>();
            stage = GetComponentInParent<TravellerStage>(); identity = GetComponent<NpcIdentity>();
            if (stage != null) { hud = stage.GetComponentInChildren<TravellerHud>(); dialogue = stage.GetComponent<NpcDialogueDirector>(); }
        }
        private void Start()
        {
            if (book == null) { enabled = false; return; }
            book.localPosition = readingPosition;
            book.localRotation = Quaternion.Euler(readingEuler);
            actor.SetRestMotion("MX_CarryBoxIdle");
            grip.Attach(book);
        }
        private void Update()
        {
            if (actor.Paused || hud != null && hud.IsWorldPaused || book == null) return;
            actor.FaceTarget = identity != null && dialogue != null && dialogue.Current == identity && stage.Journey.DialogueOpen ? stage.player.transform : null;
            float blend = 1 - Mathf.Exp(-8 * Time.deltaTime);
            book.localPosition = Vector3.Lerp(book.localPosition, actor.IsMoving ? carryingPosition : readingPosition, blend);
            book.localRotation = Quaternion.Slerp(book.localRotation, Quaternion.Euler(readingEuler), blend);
        }
        private void OnEnable() { if (grip != null && book != null) grip.Attach(book); }
        private void OnDisable() { if (grip != null && grip.Prop == book) grip.Release(); if (actor != null) actor.FaceTarget = null; }
    }
}
