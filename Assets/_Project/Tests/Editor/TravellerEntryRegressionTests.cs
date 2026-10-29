using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThreeBodyProblem.Tests
{
    public class TravellerEntryRegressionTests
    {
        [Test]
        public void SavedHudPanelUsesResolutionScalingRatherThanMonitorDpi()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/Settings/Traveller/FieldNotesPanel.asset");
            Assert.That(panel.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize), "Opening the project on a different display must not enlarge every HUD panel using the monitor DPI.");
            Assert.That(panel.referenceResolution, Is.EqualTo(new Vector2Int(1600, 900)));
        }
    }
}
