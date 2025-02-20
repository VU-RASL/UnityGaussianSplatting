using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AverageFPSLogger : MonoBehaviour
{
    private List<float> fpsList = new List<float>();
    private float totalFrames = 0;
    private float totalTime = 0;
    private float logInterval = 60f; // 1 minute interval

    void Start()
    {
        StartCoroutine(LogAverageFPS());
    }

    void Update()
    {
        float currentFPS = 1.0f / Time.deltaTime;
        fpsList.Add(currentFPS);
        totalFrames++;
        totalTime += Time.deltaTime;
    }

    IEnumerator LogAverageFPS()
    {
        while (true)
        {
            yield return new WaitForSeconds(logInterval);
            PrintFPSStatistics();
        }
    }

    void PrintFPSStatistics()
    {
        if (fpsList.Count == 0) return;

        float sum = 0;
        foreach (float fps in fpsList)
        {
            sum += fps;
        }

        float meanFPS = sum / fpsList.Count;

        // Calculate Standard Deviation
        float varianceSum = 0;
        foreach (float fps in fpsList)
        {
            varianceSum += Mathf.Pow(fps - meanFPS, 2);
        }
        float stdDevFPS = Mathf.Sqrt(varianceSum / fpsList.Count);

        Debug.Log($"[FPS Logger] Average FPS (last {logInterval}s): {meanFPS:F2} ± {stdDevFPS:F2}");

        // Clear the list to start fresh for the next interval
        fpsList.Clear();
    }
}
