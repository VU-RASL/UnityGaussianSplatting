using System;
using UnityEngine;

public enum GeneralBuildMode
{
    VR,
    AR,
}

[ExecuteAlways]
public sealed class GeneralOperator : MonoBehaviour
{
    [SerializeField] bool buildVR = true;
    [SerializeField] bool buildAR;

    public bool BuildVR => buildVR;
    public bool BuildAR => buildAR;
    public GeneralBuildMode Mode => buildAR ? GeneralBuildMode.AR : GeneralBuildMode.VR;

    public void SetMode(GeneralBuildMode mode)
    {
        buildVR = mode == GeneralBuildMode.VR;
        buildAR = mode == GeneralBuildMode.AR;
        ApplySceneModeComponents();
    }

    public static GeneralBuildMode GetSceneMode()
    {
        var generalOperator = FindObjectOfType<GeneralOperator>(true);
        return generalOperator != null ? generalOperator.Mode : GeneralBuildMode.VR;
    }

    void Reset()
    {
        SetMode(GeneralBuildMode.VR);
    }

    void Awake()
    {
        ApplySceneModeComponents();
    }

    void OnEnable()
    {
        ApplySceneModeComponents();
    }

    void OnValidate()
    {
        if (!buildVR && !buildAR)
            SetMode(GeneralBuildMode.VR);
        else if (buildVR && buildAR)
            SetMode(GeneralBuildMode.AR);
        else
            ApplySceneModeComponents();
    }

    public void ApplySceneModeComponents()
    {
        bool enableAR = Mode == GeneralBuildMode.AR;
        SetComponentsEnabled("UnityEngine.XR.ARFoundation.ARSession, Unity.XR.ARFoundation", enableAR);
        SetComponentsEnabled("UnityEngine.XR.ARFoundation.ARCameraManager, Unity.XR.ARFoundation", enableAR);
    }

    static void SetComponentsEnabled(string typeName, bool enabled)
    {
        Type type = Type.GetType(typeName);
        if (type == null || !typeof(Behaviour).IsAssignableFrom(type))
            return;

        foreach (var behaviour in FindObjectsOfType(type, true))
            ((Behaviour)behaviour).enabled = enabled;
    }
}
