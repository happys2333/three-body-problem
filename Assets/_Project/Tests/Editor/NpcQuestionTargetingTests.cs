using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeBodyProblem.Tests
{
    public sealed class NpcQuestionTargetingTests
    {
        private Scene scene;
        private string previousPath;
        private Camera camera;
        private readonly List<Object> owned=new List<Object>();
        [SetUp] public void SetUp()
        {
            previousPath=SceneManager.GetActiveScene().path;scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=Track(new GameObject("Question test camera"));camera=go.AddComponent<Camera>();camera.transform.position=Vector3.up*1.5f;
        }
        [TearDown] public void TearDown()
        {
            foreach(var item in owned)if(item!=null)Object.DestroyImmediate(item);owned.Clear();
            if(!string.IsNullOrEmpty(previousPath))EditorSceneManager.OpenScene(previousPath,OpenSceneMode.Single);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
        private GameObject Track(GameObject go){SceneManager.MoveGameObjectToScene(go,scene);owned.Add(go);return go;}
        private NpcIdentity Npc(string id,Vector3 position)
        {
            var persona=ScriptableObject.CreateInstance<NpcPersona>();owned.Add(persona);persona.characterId=id;persona.displayName=id;
            var go=Track(new GameObject(id));go.transform.position=position;var npc=go.AddComponent<NpcIdentity>();npc.persona=persona;return npc;
        }
        [Test] public void SoleNpcAnswersEvenFarAwayAndBehindThePlayer()
        {var npc=Npc("wenwang",new Vector3(0,0,-1000));Assert.That(NpcQuestionTargeting.Find(camera,new[]{npc}),Is.SameAs(npc));}
        [Test] public void LookingAtSomeoneBeatsStandingCloserToSomebodyElse()
        {var near=Npc("king",new Vector3(3,0,2));var lookedAt=Npc("wenwang",new Vector3(0,0,80));Assert.That(NpcQuestionTargeting.Find(camera,new[]{near,lookedAt}),Is.SameAs(lookedAt));}
        [Test] public void EqualViewingAnglesUseDistance()
        {var far=Npc("king",new Vector3(0,0,20));var near=Npc("wenwang",new Vector3(0,0,8));Assert.That(NpcQuestionTargeting.Find(camera,new[]{far,near}),Is.SameAs(near));}
        [Test] public void AVisiblePersonWinsOverSomeoneBehindAWall()
        {
            var hidden=Npc("king",new Vector3(0,0,10));var visible=Npc("wenwang",new Vector3(5,0,8));
            var wall=Track(GameObject.CreatePrimitive(PrimitiveType.Cube));wall.transform.position=new Vector3(0,1.5f,4);Physics.SyncTransforms();
            Assert.That(NpcQuestionTargeting.Find(camera,new[]{hidden,visible}),Is.SameAs(visible));
        }
        [Test] public void SupportingPeopleCanAnswerWithoutBeingOverriddenByAMainCharacter()
        {var guard=Npc("shang_guard",new Vector3(0,0,8));var king=Npc("king",new Vector3(4,0,8));Assert.That(NpcQuestionTargeting.Find(camera,new[]{king,guard}),Is.SameAs(guard));}
        [Test] public void DisabledAndNonInteractivePeopleAreNotRecipients()
        {
            var disabled=Npc("king",new Vector3(0,0,2));disabled.enabled=false;
            var background=Npc("citizen",new Vector3(0,0,3));background.interactive=false;
            var live=Npc("wenwang",new Vector3(8,0,20));Assert.That(NpcQuestionTargeting.Find(camera,new[]{disabled,background,live}),Is.SameAs(live));
        }
        [Test] public void TurningDuringASentenceKeepsTheOriginalRecipient()
        {
            var king=Npc("king",new Vector3(0,0,8));var traveller=Npc("wenwang",new Vector3(8,0,0));var people=new[]{king,traveller};
            var turn=new NpcQuestionTurn();turn.Begin(NpcQuestionTargeting.Find(camera,people));int id=turn.Id;
            camera.transform.LookAt(traveller.ConversationPoint);Assert.That(NpcQuestionTargeting.Find(camera,people),Is.SameAs(traveller));
            Assert.That(turn.Recipient,Is.SameAs(king));Assert.That(turn.Accepts(id,king),Is.True);
        }
        [Test] public void AnOldTranscriptCannotBeSubmittedAsANewQuestion()
        {
            var king=Npc("king",Vector3.forward);var traveller=Npc("wenwang",Vector3.right);var turn=new NpcQuestionTurn();
            turn.Begin(king);int old=turn.Id;turn.Cancel();turn.Begin(traveller);
            Assert.That(turn.Accepts(old,king),Is.False);Assert.That(turn.Accepts(old,traveller),Is.False);Assert.That(turn.Accepts(turn.Id,traveller),Is.True);
        }
        [Test] public void ADepartedRecipientDoesNotSilentlyRetargetTheQuestion()
        {
            var king=Npc("king",Vector3.forward);var turn=new NpcQuestionTurn();turn.Begin(king);king.gameObject.SetActive(false);
            Assert.That(turn.Accepts(turn.Id,king),Is.False);Assert.That(turn.Recipient,Is.SameAs(king));
        }
        [Test] public void OneQuestionCanOnlyBeSubmittedOnce()
        {
            var npc=Npc("king",Vector3.forward);var turn=new NpcQuestionTurn();turn.Begin(npc);
            Assert.That(turn.TrySubmit(turn.Id,npc),Is.True);Assert.That(turn.TrySubmit(turn.Id,npc),Is.False);
        }
        [Test] public void AStoryLineCannotInvalidateAnInFlightPlayerQuestion()
        {
            var npc=Npc("king",Vector3.forward);var go=Track(new GameObject("Question director"));
            var director=go.AddComponent<NpcDialogueDirector>();var speaker=go.AddComponent<AudioSource>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(NpcDialogueDirector).GetField("speaker",flags).SetValue(director,speaker);
            var turn=(NpcQuestionTurn)typeof(NpcDialogueDirector).GetField("question",flags).GetValue(director);
            turn.Begin(npc);turn.TrySubmit(turn.Id,npc);
            var clip=AudioClip.Create("Story line",160,1,16000,false);owned.Add(clip);
            director.PlayScriptedLine(clip);
            Assert.That((int)typeof(NpcDialogueDirector).GetField("version",flags).GetValue(director),Is.Zero,"A scripted line invalidated the player's pending response.");
            Assert.That(speaker.clip,Is.Null);Assert.That(director.HasPlayerQuestion,Is.True);
        }
    }
}
