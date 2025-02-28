using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AverageFPSLogger : MonoBehaviour
{
    private List<float> fpsList = new List<float>();
    private float logInterval = 600f; // Log every 60 seconds
    private float startTime;

    void Start()
    {
        startTime = Time.time; // Record when the game starts
        StartCoroutine(LogAverageFPS());
    }

    void Update()
    {
        if (Time.time - startTime > 1f) // Ignore first second
        {
            float currentFPS = 1.0f / Time.deltaTime;
            fpsList.Add(currentFPS);
        }
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

        Debug.Log($"[FPS Logger] Average FPS (last {logInterval}s, ignoring first second): {meanFPS:F2} ± {stdDevFPS:F2}");

        // Clear the list for the next interval
        fpsList.Clear();
    }
}
