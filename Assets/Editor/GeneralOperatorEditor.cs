#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

[CustomEditor(typeof(GeneralOperator))]
public sealed class GeneralOperatorEditor : Editor
{
    SerializedProperty buildVR;
    SerializedProperty buildAR;
    SerializedProperty showHeadsetFps;
    SerializedProperty runPerformanceProtocol;

    void OnEnable()
    {
        buildVR = serializedObject.FindProperty("buildVR");
        buildAR = serializedObject.FindProperty("buildAR");
        showHeadsetFps = serializedObject.FindProperty("showHeadsetFps");
        runPerformanceProtocol = serializedObject.FindProperty("runPerformanceProtocol");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        bool oldVR = buildVR.boolValue;
        bool oldAR = buildAR.boolValue;

        EditorGUILayout.LabelField("Build Mode", EditorStyles.boldLabel);
        bool newVR = EditorGUILayout.ToggleLeft("VR", oldVR);
        bool newAR = EditorGUILayout.ToggleLeft("AR", oldAR);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Headset Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(showHeadsetFps, new GUIContent("Show FPS count"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Performance Protocol", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(runPerformanceProtocol, new GUIContent("Run on app start"));

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
                generalOperator.ApplySceneModeComponents();
                EditorUtility.SetDirty(generalOperator);
                EditorSceneManager.MarkSceneDirty(generalOperator.gameObject.scene);
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
        GeneralOperatorOpenXRUtility.EnsureShaderIncluded("GSAC/Headset FPS Overlay");
        Debug.Log($"General Operator build mode for Android: {mode}");
    }
}

public sealed class QuestAndroidManifestPostprocessor : IPostGenerateGradleAndroidProject
{
    const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

    public int callbackOrder => 10000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
            return;

        var document = new XmlDocument();
        document.Load(manifestPath);

        XmlElement application = document.SelectSingleNode("/manifest/application") as XmlElement;
        if (application == null)
            return;

        bool changed = false;
        if (application.HasAttribute("label", AndroidNamespace))
        {
            application.RemoveAttribute("label", AndroidNamespace);
            changed = true;
        }

        if (application.HasAttribute("icon", AndroidNamespace))
        {
            application.RemoveAttribute("icon", AndroidNamespace);
            changed = true;
        }

        if (changed)
            document.Save(manifestPath);
    }
}

public static class GeneralOperatorOpenXRUtility
{
    const string MetaQuestFeatureId = "com.unity.openxr.feature.metaquest";
    const string MetaArSessionFeatureId = "com.unity.openxr.feature.arfoundation-meta-session";
    const string MetaArCameraFeatureId = "com.unity.openxr.feature.arfoundation-meta-camera";
    const string MetaArPlaneFeatureId = "com.unity.openxr.feature.arfoundation-meta-plane";
    const string MetaArRaycastFeatureId = "com.unity.openxr.feature.arfoundation-meta-raycast";
    const string MetaArOcclusionFeatureId = "com.unity.openxr.feature.arfoundation-meta-occlusion";
    const string UnityHandTrackingFeatureId = "com.unity.openxr.feature.input.handtracking";

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
        UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);

        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null)
            return;

        bool changed = false;
        foreach (OpenXRFeature feature in settings.GetFeatures())
        {
            if (feature == null)
                continue;

            string featureId = GetInternalString(feature, "featureIdInternal");
            string extensionStrings = GetInternalString(feature, "openxrExtensionStrings");
            bool arFeature = IsArFeature(featureId, extensionStrings);
            bool occlusionFeature = IsQuestOcclusionFeature(featureId, extensionStrings);
            bool shouldEnable =
                featureId == MetaQuestFeatureId ||
                (mode == GeneralBuildMode.AR && arFeature && !occlusionFeature);
            bool shouldDisable =
                (mode == GeneralBuildMode.VR && arFeature) ||
                occlusionFeature;

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

    public static void EnsureShaderIncluded(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"Could not find shader '{shaderName}' to include in the Android build.");
            return;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        UnityEngine.Object graphicsSettings = assets != null && assets.Length > 0 ? assets[0] : null;
        if (graphicsSettings == null)
        {
            Debug.LogWarning($"Could not load GraphicsSettings to include shader '{shaderName}'.");
            return;
        }

        var serializedSettings = new SerializedObject(graphicsSettings);
        SerializedProperty shaders = serializedSettings.FindProperty("m_AlwaysIncludedShaders");
        if (shaders == null || !shaders.isArray)
        {
            Debug.LogWarning($"Could not find always-included shader list for '{shaderName}'.");
            return;
        }

        for (int i = 0; i < shaders.arraySize; ++i)
        {
            if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                return;
        }

        int index = shaders.arraySize;
        shaders.InsertArrayElementAtIndex(index);
        shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(graphicsSettings);
        AssetDatabase.SaveAssets();
    }

    static string GetInternalString(OpenXRFeature feature, string fieldName)
    {
        FieldInfo field = typeof(OpenXRFeature).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(feature) as string ?? string.Empty;
    }

    static bool IsArFeature(string featureId, string extensionStrings)
    {
        return featureId == MetaArSessionFeatureId ||
               featureId == MetaArCameraFeatureId ||
               featureId == MetaArPlaneFeatureId ||
               featureId == MetaArRaycastFeatureId ||
               featureId == MetaArOcclusionFeatureId ||
               IsQuestHandTrackingFeature(featureId, extensionStrings) ||
               IsOcclusionFeature(featureId);
    }

    static bool IsQuestOcclusionFeature(string featureId, string extensionStrings)
    {
        return featureId == MetaArOcclusionFeatureId ||
               IsOcclusionFeature(featureId) ||
               (!string.IsNullOrEmpty(extensionStrings) &&
                extensionStrings.IndexOf("occlusion", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static bool IsQuestHandTrackingFeature(string featureId, string extensionStrings)
    {
        return featureId == UnityHandTrackingFeatureId &&
               !string.IsNullOrEmpty(extensionStrings) &&
               extensionStrings.IndexOf("XR_EXT_hand_tracking", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsOcclusionFeature(string featureId)
    {
        return !string.IsNullOrEmpty(featureId) &&
               featureId.IndexOf("occlusion", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
#endif
