using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;

namespace SoulGames.Utilities
{
    public class EasyScreenShotMaker : MonoBehaviour
    {
        [SerializeField] private string screenshotFileName = "screenshot"; // Base name for the screenshot file
        [SerializeField] private int screenshotResolutionMultiplier = 1;    // Resolution multiplier (1 = current resolution)

        private string screenshotFolder;
        
        private void Update()
        {
            // Check if the Space key is pressed using the new Input System
            if (Keyboard.current.spaceKey.wasPressedThisFrame) CaptureScreenshot();
        }

        public void CaptureScreenshot()
        {
            // Set the screenshot folder to the desktop path
            string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            screenshotFolder = Path.Combine(desktopPath, "Screenshots");

            // Create the folder if it doesn’t exist
            if (!Directory.Exists(screenshotFolder))  Directory.CreateDirectory(screenshotFolder);

            // Generate unique file name with timestamp
            string filePath = Path.Combine(screenshotFolder, screenshotFileName + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");

            // Capture the screenshot
            ScreenCapture.CaptureScreenshot(filePath, screenshotResolutionMultiplier);
            Debug.Log($"Screenshot saved to: {filePath}");
        }
    }
}