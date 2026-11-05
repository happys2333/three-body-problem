using System;

namespace ThreeBodyProblem
{
    [Serializable]
    internal sealed class SceneObservation
    {
        public string id;
        public string text;
        [NonSerialized] internal readonly string legacyText;

        internal SceneObservation(string id, string text, string legacyText = null)
        {
            this.id = id;
            this.text = text;
            this.legacyText = legacyText;
        }
    }
}
