using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GaussianSplatting.Runtime;
using UnityEngine;
using UnityEngine.Profiling;

public enum PerformanceProtocolPoseMode
{
    CurrentScenePose,
    AutomaticPoseSequence,
}

[DefaultExecutionOrder(900)]
public sealed class PerformanceProtocolRecorder : MonoBehaviour
{
    const string RecorderVersion = "passive_fps_windows_v3";
    const int TextureWidth = 768;
    const int TextureHeight = 256;
    const int GlyphWidth = 5;
    const int GlyphHeight = 7;
    const int GlyphScale = 4;

    [Header("Protocol")]
    [SerializeField] bool autoRunOnStart = true;
    [SerializeField] PerformanceProtocolPoseMode poseMode = PerformanceProtocolPoseMode.CurrentScenePose;
    [Tooltip("Keep this off for paper measurements. When off, the recorder never changes pose state.")]
    [SerializeField] bool allowAutomaticPoseSequence;
    [Tooltip("Leave empty to infer from PoseController. In CurrentScenePose mode this label is written to the CSV.")]
    [SerializeField] string currentPoseLabel = "";
    [SerializeField] float startupDelaySeconds = 3.0f;
    [SerializeField] float warmupSeconds = 5.0f;
    [SerializeField] float trialDurationSeconds = 60.0f;
    [SerializeField] float fpsSampleWindowSeconds = 0.5f;
    [SerializeField] float segmentDurationSeconds = 10.0f;
    [SerializeField] bool recordCleanPose = true;
    [SerializeField] bool recordTAPose = true;
    [SerializeField] bool recordPose1 = true;

    [Header("Headset Display")]
    [SerializeField] bool showHeadsetPanel = false;
    [SerializeField] Vector3 cameraLocalPosition = new(0.0f, -0.23f, 0.95f);
    [SerializeField] Vector2 displaySizeMeters = new(0.48f, 0.16f);
    [SerializeField] float panelRefreshSeconds = 0.25f;
    [SerializeField] float memorySampleIntervalSeconds = 1.0f;
    [SerializeField] bool sampleMemoryDuringTrial = false;

    [Header("Output")]
    [SerializeField] string outputFilePrefix = "gsac_performance";
    [SerializeField] bool alsoWriteLatestFile = true;

    const int MaxSegmentCount = 12;

    readonly int[] segmentFrameCounts = new int[MaxSegmentCount];
    readonly float[] segmentSeconds = new float[MaxSegmentCount];
    readonly List<float> fpsWindowSamples = new(180);
    readonly List<TrialResult> results = new();
    readonly List<string> statusLines = new();

    Texture2D texture;
    Material material;
    Renderer quadRenderer;
    Camera targetCamera;
    PoseController poseController;
    Coroutine protocolCoroutine;
    string currentPose = "Idle";
    string currentPhase = "Waiting";
    float phaseStartTime;
    float phaseDuration;
    float trialFrameSeconds;
    float trialFrameTimeMs;
    float sampleWindowSeconds;
    int sampleWindowFrames;
    int activeSegmentIndex;
    float minFrameMs;
    float maxFrameMs;
    int trialFrameCount;
    long peakAllocatedBytes;
    float nextMemorySampleTime;
    float nextPanelRefresh;
    string lastSavedPath = "";
    bool protocolComplete;

    static readonly Color32 Clear = new(0, 0, 0, 0);
    static readonly Color32 Background = new(5, 8, 12, 185);
    static readonly Color32 Header = new(120, 220, 255, 255);
    static readonly Color32 Text = new(236, 244, 248, 255);
    static readonly Color32 Good = new(95, 255, 150, 255);
    static readonly Color32 Warn = new(255, 220, 80, 255);
    static readonly Color32 Bad = new(255, 95, 80, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateRuntimeRecorder()
    {
        if (!GeneralOperator.GetScenePerformanceProtocolEnabled())
            return;

        if (FindObjectOfType<PerformanceProtocolRecorder>() != null)
            return;

        GameObject go = new("GSAC Performance Protocol Recorder");
        DontDestroyOnLoad(go);
        go.AddComponent<PerformanceProtocolRecorder>();
    }

    void Awake()
    {
        if (showHeadsetPanel)
            BuildDisplay();
    }

    void Start()
    {
        if (autoRunOnStart)
            protocolCoroutine = StartCoroutine(RunProtocol());
    }

    void OnDestroy()
    {
        if (protocolCoroutine != null)
        {
            StopCoroutine(protocolCoroutine);
            protocolCoroutine = null;
        }

        if (material != null)
            Destroy(material);
        if (texture != null)
            Destroy(texture);
    }

    void Update()
    {
        if (currentPhase == "Record")
        {
            float deltaSeconds = Mathf.Max(Time.unscaledDeltaTime, 0.00001f);
            float frameMs = deltaSeconds * 1000.0f;
            trialFrameSeconds += deltaSeconds;
            trialFrameTimeMs += frameMs;
            trialFrameCount++;
            minFrameMs = Mathf.Min(minFrameMs, frameMs);
            maxFrameMs = Mathf.Max(maxFrameMs, frameMs);
            sampleWindowSeconds += deltaSeconds;
            sampleWindowFrames++;

            if (sampleWindowSeconds >= Mathf.Max(0.1f, fpsSampleWindowSeconds))
            {
                fpsWindowSamples.Add(sampleWindowFrames / sampleWindowSeconds);
                sampleWindowSeconds = 0.0f;
                sampleWindowFrames = 0;
            }

            segmentFrameCounts[activeSegmentIndex]++;
            segmentSeconds[activeSegmentIndex] += deltaSeconds;
            if (segmentSeconds[activeSegmentIndex] >= Mathf.Max(0.1f, segmentDurationSeconds) && activeSegmentIndex < MaxSegmentCount - 1)
                activeSegmentIndex++;

            if (sampleMemoryDuringTrial && Time.unscaledTime >= nextMemorySampleTime)
            {
                peakAllocatedBytes = Math.Max(peakAllocatedBytes, Profiler.GetTotalAllocatedMemoryLong());
                nextMemorySampleTime = Time.unscaledTime + Mathf.Max(0.25f, memorySampleIntervalSeconds);
            }
        }
    }

    void LateUpdate()
    {
        if (!showHeadsetPanel)
            return;

        AttachToCamera();
        if (Time.unscaledTime >= nextPanelRefresh)
        {
            nextPanelRefresh = Time.unscaledTime + panelRefreshSeconds;
            DrawPanel();
        }
    }

    IEnumerator RunProtocol()
    {
        currentPhase = "Startup";
        phaseStartTime = Time.unscaledTime;
        phaseDuration = startupDelaySeconds;
        yield return WaitRealtime(startupDelaySeconds);

        poseController = FindObjectOfType<PoseController>(true);
        if (poseController == null)
        {
            statusLines.Add("POSE CONTROLLER NOT FOUND");
            Debug.LogWarning("[GSAC Perf] PoseController not found. Recording current scene only.");
        }

        List<Trial> trials = BuildTrials();
        if (trials.Count == 0)
            trials.Add(new Trial("Current", null));

        foreach (Trial trial in trials)
        {
            currentPose = trial.Name;
            trial.Apply?.Invoke();

            currentPhase = "Warmup";
            phaseStartTime = Time.unscaledTime;
            phaseDuration = warmupSeconds;
            yield return WaitRealtime(warmupSeconds);

            BeginTrial();
            currentPhase = "Record";
            phaseStartTime = Time.unscaledTime;
            phaseDuration = trialDurationSeconds;
            yield return WaitRealtime(trialDurationSeconds);

            TrialResult result = EndTrial(trial.Name);
            results.Add(result);
            statusLines.Add($"{result.PoseName}: {result.AverageFps:F1} FPS {result.MedianFrameMs:F1} MS P95 {result.P95FrameMs:F1}");
            Debug.Log(result.ToLogLine());
        }

        currentPhase = "Saving";
        lastSavedPath = SaveResults();
        protocolComplete = true;
        currentPhase = "Done";
        phaseStartTime = Time.unscaledTime;
        phaseDuration = 0.0f;

        Debug.Log($"[GSAC Perf] Saved performance protocol CSV: {lastSavedPath}");
        DrawPanel();
    }

    IEnumerator WaitRealtime(float seconds)
    {
        float endTime = Time.unscaledTime + Mathf.Max(0.0f, seconds);
        while (Time.unscaledTime < endTime)
            yield return null;
    }

    List<Trial> BuildTrials()
    {
        List<Trial> trials = new();

        if (poseMode == PerformanceProtocolPoseMode.CurrentScenePose || !allowAutomaticPoseSequence)
        {
            trials.Add(new Trial(ResolveCurrentPoseLabel(), null));
            return trials;
        }

        if (poseController == null)
            return trials;

        if (recordCleanPose)
            trials.Add(new Trial("No Pose", poseController.ApplyCleanPose));
        if (recordTAPose)
            trials.Add(new Trial("T-A pose", poseController.ApplyTAPose));
        if (recordPose1)
            trials.Add(new Trial("Pose 1", poseController.ApplyPose1));
        return trials;
    }

    string ResolveCurrentPoseLabel()
    {
        if (!string.IsNullOrWhiteSpace(currentPoseLabel))
            return currentPoseLabel.Trim();

        if (poseController == null)
            return "Current pose";

        if (poseController.NoPose)
            return "No Pose";

        if (poseController.using_custom)
            return "T-A pose";

        foreach (Animator animator in FindObjectsOfType<Animator>(true))
        {
            if (animator != null && animator.enabled && animator.speed > 0.0f)
                return "Pose 1";
        }

        return "No Pose";
    }

    void BeginTrial()
    {
        Array.Clear(segmentFrameCounts, 0, segmentFrameCounts.Length);
        Array.Clear(segmentSeconds, 0, segmentSeconds.Length);
        fpsWindowSamples.Clear();
        trialFrameSeconds = 0.0f;
        trialFrameTimeMs = 0.0f;
        sampleWindowSeconds = 0.0f;
        sampleWindowFrames = 0;
        activeSegmentIndex = 0;
        trialFrameCount = 0;
        minFrameMs = float.MaxValue;
        maxFrameMs = 0.0f;
        peakAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
        nextMemorySampleTime = Time.unscaledTime + Mathf.Max(0.25f, memorySampleIntervalSeconds);
    }

    TrialResult EndTrial(string poseName)
    {
        if (sampleWindowFrames > 0 && sampleWindowSeconds > 0.0f)
            fpsWindowSamples.Add(sampleWindowFrames / sampleWindowSeconds);

        int frameCount = trialFrameCount;
        float averageFps = trialFrameSeconds > 0.0f ? frameCount / trialFrameSeconds : 0.0f;
        float meanFrameMs = frameCount > 0 ? trialFrameTimeMs / frameCount : 0.0f;
        float medianFrameMs = PercentileWindowFrameMs(0.50f);
        float p95FrameMs = PercentileWindowFrameMs(0.95f);
        float minFrame = frameCount > 0 ? minFrameMs : 0.0f;
        float maxFrame = frameCount > 0 ? maxFrameMs : 0.0f;
        float[] segmentFps = new float[MaxSegmentCount];
        for (int i = 0; i < segmentFps.Length; ++i)
            segmentFps[i] = segmentSeconds[i] > 0.0f ? segmentFrameCounts[i] / segmentSeconds[i] : 0.0f;

        return new TrialResult
        {
            Mode = GeneralOperator.GetSceneMode().ToString(),
            RecorderVersion = RecorderVersion,
            ProtocolPoseMode = poseMode.ToString(),
            SplatCount = GetTotalSplatCount(),
            PoseName = poseName,
            DurationSeconds = trialFrameSeconds,
            FrameCount = frameCount,
            AverageFps = averageFps,
            MeanFrameMs = meanFrameMs,
            MedianFrameMs = medianFrameMs,
            P95FrameMs = p95FrameMs,
            MinFrameMs = minFrame,
            MaxFrameMs = maxFrame,
            SegmentFps = segmentFps,
            AllocatedMemoryMb = BytesToMb(Profiler.GetTotalAllocatedMemoryLong()),
            ReservedMemoryMb = BytesToMb(Profiler.GetTotalReservedMemoryLong()),
            PeakAllocatedMemoryMb = BytesToMb(peakAllocatedBytes),
            ManagedMemoryMb = BytesToMb(GC.GetTotalMemory(false)),
            DeviceModel = SystemInfo.deviceModel,
            GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            UnityVersion = Application.unityVersion,
            TimestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        };
    }

    float PercentileWindowFrameMs(float percentile)
    {
        if (fpsWindowSamples.Count == 0)
            return 0.0f;

        List<float> frameMsSamples = new(fpsWindowSamples.Count);
        for (int i = 0; i < fpsWindowSamples.Count; ++i)
            frameMsSamples.Add(fpsWindowSamples[i] > 0.0f ? 1000.0f / fpsWindowSamples[i] : 0.0f);

        frameMsSamples.Sort();
        int index = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(percentile) * frameMsSamples.Count) - 1, 0, frameMsSamples.Count - 1);
        return frameMsSamples[index];
    }

    static float BytesToMb(long bytes)
    {
        return bytes / (1024.0f * 1024.0f);
    }

    static int GetTotalSplatCount()
    {
        int total = 0;
        foreach (GaussianSplatRenderer renderer in FindObjectsOfType<GaussianSplatRenderer>(true))
        {
            if (renderer != null)
                total += renderer.splatCount;
        }
        return total;
    }

    string SaveResults()
    {
        Directory.CreateDirectory(Application.persistentDataPath);

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string fileName = $"{outputFilePrefix}_{timestamp}.csv";
        string path = Path.Combine(Application.persistentDataPath, fileName);
        string csv = BuildCsv();
        File.WriteAllText(path, csv, Encoding.UTF8);

        if (alsoWriteLatestFile)
        {
            string latestPath = Path.Combine(Application.persistentDataPath, $"{outputFilePrefix}_latest.csv");
            File.WriteAllText(latestPath, csv, Encoding.UTF8);
        }

        return path;
    }

    string BuildCsv()
    {
        StringBuilder builder = new();
        builder.AppendLine("mode,recorder_version,protocol_pose_mode,splat_count,pose,duration_s,frame_count,avg_fps,mean_frame_ms,median_frame_ms,p95_frame_ms,min_frame_ms,max_frame_ms,fps_0_10s,fps_10_20s,fps_20_30s,fps_30_40s,fps_40_50s,fps_50_60s,allocated_mem_mb,reserved_mem_mb,peak_allocated_mem_mb,managed_mem_mb,device_model,graphics_api,unity_version,timestamp_utc");
        foreach (TrialResult result in results)
            builder.AppendLine(result.ToCsvLine());
        return builder.ToString();
    }

    void BuildDisplay()
    {
        texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        Shader shader = Shader.Find("GSAC/Headset FPS Overlay");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Standard");

        material = new Material(shader)
        {
            mainTexture = texture,
            renderQueue = 5001,
        };

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Performance Protocol Quad";
        quad.transform.SetParent(transform, false);
        quad.transform.localScale = new Vector3(displaySizeMeters.x, displaySizeMeters.y, 1.0f);

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        quadRenderer = quad.GetComponent<Renderer>();
        quadRenderer.sharedMaterial = material;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;

        DrawPanel();
    }

    void AttachToCamera()
    {
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            targetCamera = Camera.main;

        if (targetCamera == null || transform.parent == targetCamera.transform)
            return;

        transform.SetParent(targetCamera.transform, false);
        transform.localPosition = cameraLocalPosition;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    void DrawPanel()
    {
        if (texture == null)
            return;

        Color32[] pixels = new Color32[TextureWidth * TextureHeight];
        for (int i = 0; i < pixels.Length; ++i)
            pixels[i] = Clear;

        FillRect(pixels, 0, 0, TextureWidth, TextureHeight, Background);
        DrawTextLine(pixels, 16, 212, "GSAC PERFORMANCE PROTOCOL", Header);

        float elapsed = Mathf.Max(0.0f, Time.unscaledTime - phaseStartTime);
        float remaining = phaseDuration > 0.0f ? Mathf.Max(0.0f, phaseDuration - elapsed) : 0.0f;
        string phaseLine = protocolComplete
            ? "DONE - CSV SAVED"
            : $"{currentPhase.ToUpperInvariant()} {currentPose.ToUpperInvariant()} {elapsed:F0}/{phaseDuration:F0}S";
        DrawTextLine(pixels, 16, 174, phaseLine, ColorForPhase());

        if (currentPhase == "Record" && trialFrameSeconds > 0.0f)
        {
            float fps = trialFrameCount / trialFrameSeconds;
            DrawTextLine(pixels, 16, 136, $"LIVE FPS {fps:F1}  REM {remaining:F0}S", ColorForFps(fps));
        }
        else if (results.Count > 0)
        {
            TrialResult last = results[results.Count - 1];
            DrawTextLine(pixels, 16, 136, $"LAST {last.PoseName.ToUpperInvariant()} {last.AverageFps:F1} FPS P95 {last.P95FrameMs:F1}MS", ColorForFps(last.AverageFps));
        }
        else
        {
            DrawTextLine(pixels, 16, 136, $"WARMUP {warmupSeconds:F0}S  TRIAL {trialDurationSeconds:F0}S", Text);
        }

        DrawTextLine(pixels, 16, 98, $"SPLATS {GetTotalSplatCount()}  MEM {BytesToMb(Profiler.GetTotalAllocatedMemoryLong()):F0}MB", Text);

        int y = 60;
        int start = Mathf.Max(0, statusLines.Count - 2);
        for (int i = start; i < statusLines.Count; ++i)
        {
            DrawTextLine(pixels, 16, y, statusLines[i].ToUpperInvariant(), Text);
            y -= 32;
        }

        if (protocolComplete)
            DrawTextLine(pixels, 16, 20, "PULL FILE: GSAC_PERFORMANCE_LATEST.CSV", Warn);

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    Color32 ColorForPhase()
    {
        if (currentPhase == "Record")
            return Warn;
        if (currentPhase == "Done")
            return Good;
        return Text;
    }

    static Color32 ColorForFps(float fps)
    {
        if (fps >= 68.0f)
            return Good;
        if (fps >= 50.0f)
            return Warn;
        return Bad;
    }

    static void DrawTextLine(Color32[] pixels, int x, int y, string text, Color32 color)
    {
        int cursorX = x;
        foreach (char rawCharacter in text)
        {
            char character = char.ToUpperInvariant(rawCharacter);
            if (character == ' ')
            {
                cursorX += 4 * GlyphScale;
                continue;
            }

            string[] glyph = GetGlyph(character);
            if (glyph != null)
                DrawGlyph(pixels, cursorX, y, glyph, color);

            cursorX += (GlyphWidth + 1) * GlyphScale;
            if (cursorX > TextureWidth - GlyphWidth * GlyphScale)
                break;
        }
    }

    static void DrawGlyph(Color32[] pixels, int originX, int originY, string[] glyph, Color32 color)
    {
        for (int row = 0; row < glyph.Length; ++row)
        {
            for (int column = 0; column < glyph[row].Length; ++column)
            {
                if (glyph[row][column] != '1')
                    continue;

                int pixelX = originX + column * GlyphScale;
                int pixelY = originY + (GlyphHeight - 1 - row) * GlyphScale;
                FillRect(pixels, pixelX, pixelY, GlyphScale, GlyphScale, color);
            }
        }
    }

    static void FillRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
    {
        for (int py = y; py < y + height; ++py)
        {
            for (int px = x; px < x + width; ++px)
            {
                if (px < 0 || px >= TextureWidth || py < 0 || py >= TextureHeight)
                    continue;
                pixels[py * TextureWidth + px] = color;
            }
        }
    }

    static string[] GetGlyph(char character)
    {
        switch (character)
        {
            case 'A': return new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" };
            case 'B': return new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" };
            case 'C': return new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" };
            case 'D': return new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" };
            case 'E': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" };
            case 'F': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" };
            case 'G': return new[] { "01111", "10000", "10000", "10011", "10001", "10001", "01111" };
            case 'H': return new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" };
            case 'I': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" };
            case 'J': return new[] { "00111", "00010", "00010", "00010", "00010", "10010", "01100" };
            case 'K': return new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" };
            case 'L': return new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" };
            case 'M': return new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" };
            case 'N': return new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" };
            case 'O': return new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" };
            case 'P': return new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" };
            case 'Q': return new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" };
            case 'R': return new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" };
            case 'S': return new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" };
            case 'T': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" };
            case 'U': return new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" };
            case 'V': return new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" };
            case 'W': return new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" };
            case 'X': return new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" };
            case 'Y': return new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" };
            case 'Z': return new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" };
            case '0': return new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" };
            case '1': return new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" };
            case '2': return new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" };
            case '3': return new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" };
            case '4': return new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" };
            case '5': return new[] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" };
            case '6': return new[] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" };
            case '7': return new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" };
            case '8': return new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" };
            case '9': return new[] { "01110", "10001", "10001", "01111", "00001", "00001", "01110" };
            case '-': return new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" };
            case '.': return new[] { "00000", "00000", "00000", "00000", "00000", "01100", "01100" };
            case ':': return new[] { "00000", "01100", "01100", "00000", "01100", "01100", "00000" };
            case '/': return new[] { "00001", "00010", "00010", "00100", "01000", "01000", "10000" };
            case '_': return new[] { "00000", "00000", "00000", "00000", "00000", "00000", "11111" };
            default: return null;
        }
    }

    readonly struct Trial
    {
        public readonly string Name;
        public readonly Action Apply;

        public Trial(string name, Action apply)
        {
            Name = name;
            Apply = apply;
        }
    }

    struct TrialResult
    {
        public string Mode;
        public string RecorderVersion;
        public string ProtocolPoseMode;
        public int SplatCount;
        public string PoseName;
        public float DurationSeconds;
        public int FrameCount;
        public float AverageFps;
        public float MeanFrameMs;
        public float MedianFrameMs;
        public float P95FrameMs;
        public float MinFrameMs;
        public float MaxFrameMs;
        public float[] SegmentFps;
        public float AllocatedMemoryMb;
        public float ReservedMemoryMb;
        public float PeakAllocatedMemoryMb;
        public float ManagedMemoryMb;
        public string DeviceModel;
        public string GraphicsApi;
        public string UnityVersion;
        public string TimestampUtc;

        public string ToCsvLine()
        {
            return string.Join(",",
                Csv(Mode),
                Csv(RecorderVersion),
                Csv(ProtocolPoseMode),
                SplatCount.ToString(CultureInfo.InvariantCulture),
                Csv(PoseName),
                F(DurationSeconds),
                FrameCount.ToString(CultureInfo.InvariantCulture),
                F(AverageFps),
                F(MeanFrameMs),
                F(MedianFrameMs),
                F(P95FrameMs),
                F(MinFrameMs),
                F(MaxFrameMs),
                F(SegmentAt(0)),
                F(SegmentAt(1)),
                F(SegmentAt(2)),
                F(SegmentAt(3)),
                F(SegmentAt(4)),
                F(SegmentAt(5)),
                F(AllocatedMemoryMb),
                F(ReservedMemoryMb),
                F(PeakAllocatedMemoryMb),
                F(ManagedMemoryMb),
                Csv(DeviceModel),
                Csv(GraphicsApi),
                Csv(UnityVersion),
                Csv(TimestampUtc));
        }

        public string ToLogLine()
        {
            return $"[GSAC Perf] {PoseName}: avg FPS {AverageFps:F2}, median {MedianFrameMs:F2} ms, p95 {P95FrameMs:F2} ms, memory {AllocatedMemoryMb:F1} MB, splats {SplatCount}";
        }

        static string F(float value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }

        float SegmentAt(int index)
        {
            if (SegmentFps == null || index < 0 || index >= SegmentFps.Length)
                return 0.0f;

            return SegmentFps[index];
        }

        static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            string escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}
