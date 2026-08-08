using UnityEditor;
using UnityEngine;

namespace SoulGames.Utilities
{
    [CustomEditor(typeof(EasyScreenShotMaker))]
    public class EasyScreenShotMakerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default Inspector UI
            DrawDefaultInspector();

            // Add a button to capture a screenshot
            EasyScreenShotMaker screenshotMaker = (EasyScreenShotMaker)target;
            if (GUILayout.Button("Capture Screenshot"))
            {
                screenshotMaker.CaptureScreenshot();
            }
        }
    }
}