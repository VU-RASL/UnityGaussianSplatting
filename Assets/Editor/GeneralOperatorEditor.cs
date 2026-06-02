#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

[CustomEditor(typeof(GeneralOperator))]
public sealed class GeneralOperatorEditor : Editor
{
    SerializedProperty buildVR;
    SerializedProperty buildAR;

    void OnEnable()
    {
        buildVR = serializedObject.FindProperty("buildVR");
        buildAR = serializedObject.FindProperty("buildAR");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        bool oldVR = buildVR.boolValue;
        bool oldAR = buildAR.boolValue;

        EditorGUILayout.LabelField("Build Mode", EditorStyles.boldLabel);
        bool newVR = EditorGUILayout.ToggleLeft("VR", oldVR);
        bool newAR = EditorGUILayout.ToggleLeft("AR", oldAR);

        GeneralBuildMode? requestedMode = null;
        if (newVR != oldVR && newVR)
            requestedMode = GeneralBuildMode.VR;
        else if (newAR != oldAR && newAR)
            requestedMode = GeneralBuildMode.AR;
        else if (!newVR && !newAR)
            requestedMode = GeneralBuildMode.VR;

        if (requestedMode.HasValue)
        {
            buildVR.boolValue = requestedMode.Value == GeneralBuildMode.VR;
            buildAR.boolValue = requestedMode.Value == GeneralBuildMode.AR;
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            foreach (Object selectedTarget in targets)
            {
                var generalOperator = (GeneralOperator)selectedTarget;
                EditorUtility.SetDirty(generalOperator);
                GeneralOperatorOpenXRUtility.ApplyMode(generalOperator.Mode);
            }
        }
    }
}

public sealed class GeneralOperatorBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        GeneralBuildMode mode = GeneralOperatorOpenXRUtility.FindLoadedOperatorMode();
        GeneralOperatorOpenXRUtility.ApplyMode(mode);
        Debug.Log($"General Operator build mode for Android: {mode}");
    }
}

public static class GeneralOperatorOpenXRUtility
{
    const string MetaQuestFeatureId = "com.unity.openxr.feature.metaquest";
    const string MetaArSessionFeatureId = "com.unity.openxr.feature.arfoundation-meta-session";
    const string MetaArCameraFeatureId = "com.unity.openxr.feature.arfoundation-meta-camera";

    public static GeneralBuildMode FindLoadedOperatorMode()
    {
        foreach (GeneralOperator generalOperator in Resources.FindObjectsOfTypeAll<GeneralOperator>())
        {
            if (generalOperator == null)
                continue;

            if (!generalOperator.gameObject.scene.IsValid())
                continue;

            return generalOperator.Mode;
        }

        return GeneralBuildMode.VR;
    }

    [MenuItem("Gaussian Splatting/Apply General Operator Build Mode")]
    public static void ApplyCurrentMode()
    {
        ApplyMode(FindLoadedOperatorMode());
    }

    public static void ApplyMode(GeneralBuildMode mode)
    {
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null)
            return;

        bool changed = false;
        foreach (OpenXRFeature feature in settings.GetFeatures())
        {
            if (feature == null)
                continue;

            string featureId = GetInternalString(feature, "featureIdInternal");
            bool shouldEnable =
                featureId == MetaQuestFeatureId ||
                (mode == GeneralBuildMode.AR &&
                 (featureId == MetaArSessionFeatureId || featureId == MetaArCameraFeatureId));
            bool shouldDisable =
                mode == GeneralBuildMode.VR &&
                (featureId == MetaArSessionFeatureId || featureId == MetaArCameraFeatureId);

            if (shouldEnable && !feature.enabled)
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
                changed = true;
            }
            else if (shouldDisable && feature.enabled)
            {
                feature.enabled = false;
                EditorUtility.SetDirty(feature);
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }

    static string GetInternalString(OpenXRFeature feature, string fieldName)
    {
        FieldInfo field = typeof(OpenXRFeature).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(feature) as string ?? string.Empty;
    }
}
#endif
