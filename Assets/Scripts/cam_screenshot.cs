using UnityEngine;
using System.IO;

public class AutoScreenshot : MonoBehaviour
{
    public string folderName = "Assets/ScreenShot"; // Folder to save the screenshot
    public string fileName = "screenshot";         // Base file name
    public int resolutionWidth = 1080;             // Resolution width
    public int resolutionHeight = 1080;            // Resolution height

    void Start()
    {
        TakeScreenshot(); // Automatically take a screenshot when the scene starts
    }

    void TakeScreenshot()
    {
        // Ensure the folder exists
        if (!Directory.Exists(folderName))
        {
            Directory.CreateDirectory(folderName);
        }

        // Create a RenderTexture for capturing
        RenderTexture rt = new RenderTexture(resolutionWidth, resolutionHeight, 24);
        Camera cam = GetComponent<Camera>();
        cam.targetTexture = rt;

        // Render to the texture
        RenderTexture.active = rt;
        cam.Render();

        // Create a Texture2D to store the image
        Texture2D screenshot = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
        screenshot.Apply();

        // Save the image
        byte[] bytes = screenshot.EncodeToPNG();
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string path = Path.Combine(folderName, $"{fileName}_{timestamp}.png");
        File.WriteAllBytes(path, bytes);

        // Clean up
        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        Destroy(screenshot);

        Debug.Log($"Screenshot saved to {path}");
    }
}
