using System;
using UnityEngine;

namespace ThreeBodyProblem
{
    [Serializable]
    public class SceneMoment
    {
        public string title;
        [TextArea] public string description;
        public string caption;
        public string objective;
        public Vector3 viewPosition;
        public Vector3 viewTarget;
        public Color zenith = new Color(0.16f, 0.26f, 0.36f);
        public Color horizon = new Color(0.85f, 0.58f, 0.34f);
        public Color fog = new Color(0.56f, 0.43f, 0.32f);
        public Color ambient = new Color(0.45f, 0.44f, 0.41f);
        public Color lightColor = new Color(1f, 0.76f, 0.48f);
        public float lightIntensity = 1.3f;
        public float fogDensity = 0.004f;
        [Range(0, 3)] public int sunCount = 1;
        public Vector3 sunA = new Vector3(0.4f, 0.2f, 1f);
        public Vector3 sunB = new Vector3(-0.3f, 0.4f, 1f);
        public Vector3 sunC = new Vector3(0.1f, 0.6f, 1f);
        public Vector3 sunRadii = new Vector3(0.035f, 0.035f, 0.035f);
        [Range(0f, 1f)] public float stars;
        [Range(0, 3)] public int flyingStars;
        [Range(0f, 1f)] public float heat;
        [Range(0f, 1f)] public float frost;
        public string observation;
        public string interpretation;
    }

    [CreateAssetMenu(menuName = "Three Body/Scene Profile")]
    public class TravellerSceneProfile : ScriptableObject
    {
        public string chapter;
        public string subtitle;
        public int sceneIndex;
        public float transitionSeconds = 3f;
        public SceneMoment[] moments;
        [Header("Capital arrival and traversable routes")]
        public bool hasCapitalRoute;
        public Vector3 companionArrival;
        public Vector3 courtGoal;
        public Vector3 waterGoal;
        public Vector3[] courtWaypoints;
        public Vector3[] waterWaypoints;
        public float companionWalkSpeed = 2.6f;
        public GobiStorySettings gobiStory;
    }
}
