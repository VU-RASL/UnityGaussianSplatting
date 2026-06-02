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

    void OnValidate()
    {
        if (!buildVR && !buildAR)
            SetMode(GeneralBuildMode.VR);
        else if (buildVR && buildAR)
            SetMode(GeneralBuildMode.AR);
    }
}
