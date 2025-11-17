using UnityEngine;
using System.Collections;
using System.IO;

public class AutoScreenshot : MonoBehaviour
{
    public string folderName = "Assets/ScreenShot1/"; // Folder to save the screenshot
    public string fileName = "screenshot";          // Base file name
    public int resolutionWidth = 1080;              // Resolution width
    public int resolutionHeight = 1080;             // Resolution height
    public float screenshotInterval = 0.2f;         // Interval in seconds between screenshots

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (!Directory.Exists(folderName))
        {
            Directory.CreateDirectory(folderName);
        }

        StartCoroutine(TakeScreenshotsRoutine());
    }

    IEnumerator TakeScreenshotsRoutine()
    {
        while (true)
        {
            TakeScreenshot();
            yield return new WaitForSeconds(screenshotInterval);
        }
    }

    void TakeScreenshot()
    {
        // Create a RenderTexture for capturing
        RenderTexture rt = new RenderTexture(resolutionWidth, resolutionHeight, 24);
        cam.targetTexture = rt;

        // Render to the texture
        RenderTexture.active = rt;
        cam.Render();

        // Create a Texture2D to store the image
        Texture2D screenshot = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
        screenshot.Apply();

        // Save the image
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        string path = Path.Combine(folderName, $"{fileName}_{timestamp}.png");
        File.WriteAllBytes(path, screenshot.EncodeToPNG());

        // Clean up
        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        Destroy(screenshot);

        Debug.Log($"Screenshot saved to {path}");
    }
}
