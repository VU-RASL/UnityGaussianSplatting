using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

public sealed class QuestARBootstrap : MonoBehaviour
{
    static readonly string[] s_DefaultObjectsToHide =
    {
        "Sphere",
        "Plane",
        "Plane (1)",
        "Cube",
    };
    static readonly HashSet<string> s_WarnedMissingTypes = new();
    static bool s_XRSubsystemsStarted;
    const bool kEnableRealWorldEnvironmentOcclusion = false;
    float nextCameraConfigureTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (GeneralOperator.GetSceneMode() != GeneralBuildMode.AR)
            return;

        if (FindObjectOfType<QuestARBootstrap>() != null)
            return;

        var go = new GameObject("Quest AR Bootstrap");
        DontDestroyOnLoad(go);
        go.AddComponent<QuestARBootstrap>();
#endif
    }

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (GeneralOperator.GetSceneMode() != GeneralBuildMode.AR)
        {
            Destroy(gameObject);
            return;
        }

        ConfigureAR();
        StartCoroutine(StartXRThenRestartARManagers());
#endif
    }

    void OnEnable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SceneManager.sceneLoaded += OnSceneLoaded;
#endif
    }

    void OnDisable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        SceneManager.sceneLoaded -= OnSceneLoaded;
#endif
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (GeneralOperator.GetSceneMode() == GeneralBuildMode.AR)
        {
            ConfigureAR();
            StartCoroutine(StartXRThenRestartARManagers());
        }
#endif
    }

    void LateUpdate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (GeneralOperator.GetSceneMode() == GeneralBuildMode.AR && Time.unscaledTime >= nextCameraConfigureTime)
        {
            nextCameraConfigureTime = Time.unscaledTime + 1.0f;
            ConfigureCameras();
        }
#endif
    }

    void ConfigureAR()
    {
        EnsureComponent("UnityEngine.XR.ARFoundation.ARSession, Unity.XR.ARFoundation", null);
        ConfigureCameras();
        if (kEnableRealWorldEnvironmentOcclusion)
        {
            ConfigureMetaDepthApi();
            ApplyDepthOcclusionShaderToOrdinaryObjects();
        }
        else
        {
            DisableEnvironmentOcclusionManagers();
        }
        HideTestEnvironment();
    }

    IEnumerator StartXRThenRestartARManagers()
    {
        yield return null;
        yield return EnsureXRLoaderStarted();
        yield return RestartARManagersWhenXRLoaderIsReady();
    }

    IEnumerator EnsureXRLoaderStarted()
    {
        XRManagerSettings manager = XRGeneralSettings.Instance?.Manager;
        if (manager == null)
        {
            Debug.LogError("Quest AR could not find XR Manager Settings.");
            yield break;
        }

        if (manager.activeLoader == null)
            yield return manager.InitializeLoader();

        if (manager.activeLoader == null)
        {
            Debug.LogError("Quest AR could not initialize an XR loader. Check XR Plug-in Management > Android > OpenXR.");
            yield break;
        }

        if (!s_XRSubsystemsStarted)
        {
            manager.StartSubsystems();
            s_XRSubsystemsStarted = true;
        }
    }

    IEnumerator RestartARManagersWhenXRLoaderIsReady()
    {
        for (int frame = 0; frame < 120; ++frame)
        {
            if (XRGeneralSettings.Instance?.Manager?.activeLoader != null)
                break;

            yield return null;
        }

        ConfigureAR();
        RestartEnabledBehaviours("UnityEngine.XR.ARFoundation.ARSession, Unity.XR.ARFoundation");
        RestartEnabledBehaviours("UnityEngine.XR.ARFoundation.ARCameraManager, Unity.XR.ARFoundation");
        RestartEnabledBehaviours("UnityEngine.XR.ARFoundation.ARCameraBackground, Unity.XR.ARFoundation");
        if (kEnableRealWorldEnvironmentOcclusion)
            RestartEnabledBehaviours("UnityEngine.XR.ARFoundation.AROcclusionManager, Unity.XR.ARFoundation");
        else
            DisableEnvironmentOcclusionManagers();
        RestartEnabledBehaviours("UnityEngine.XR.ARFoundation.ARPlaneManager, Unity.XR.ARFoundation");
        RestartEnabledBehaviours("UnityEngine.XR.ARFoundation.ARRaycastManager, Unity.XR.ARFoundation");
        RestartEnabledBehaviours(typeof(ARGroundPlacement));
        Debug.Log("Quest AR passthrough components configured.");
        if (kEnableRealWorldEnvironmentOcclusion)
            StartCoroutine(ReportOcclusionStatus());
    }

    void ConfigureCameras()
    {
        foreach (var camera in Camera.allCameras)
        {
            if (camera == null || camera.targetTexture != null)
                continue;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            EnsureComponent("UnityEngine.XR.ARFoundation.ARCameraManager, Unity.XR.ARFoundation", camera.gameObject);
            EnsureComponent("UnityEngine.XR.ARFoundation.ARCameraBackground, Unity.XR.ARFoundation", camera.gameObject);
            if (kEnableRealWorldEnvironmentOcclusion)
                ConfigureOcclusionManager(camera.gameObject);
            else
                DisableOcclusionManager(camera.gameObject);
        }
    }

    static void DisableEnvironmentOcclusionManagers()
    {
        foreach (var occlusionManager in FindObjectsOfType<AROcclusionManager>(true))
        {
            if (occlusionManager == null)
                continue;

            occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
            occlusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.NoOcclusion;
            occlusionManager.enabled = false;
        }
    }

    static void DisableOcclusionManager(GameObject cameraObject)
    {
        if (cameraObject == null)
            return;

        var occlusionManager = cameraObject.GetComponent<AROcclusionManager>();
        if (occlusionManager == null)
            return;

        occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
        occlusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.NoOcclusion;
        occlusionManager.enabled = false;
    }

    void ConfigureOcclusionManager(GameObject cameraObject)
    {
        if (cameraObject == null)
            return;

        var occlusionManager = cameraObject.GetComponent<AROcclusionManager>() ?? cameraObject.AddComponent<AROcclusionManager>();
        occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
        occlusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
        occlusionManager.environmentDepthTemporalSmoothingRequested = true;
        occlusionManager.enabled = true;
    }

    void ConfigureMetaDepthApi()
    {
        Type depthManagerType = Type.GetType("Meta.XR.EnvironmentDepth.EnvironmentDepthManager, Meta.XR.EnvironmentDepth");
        if (depthManagerType == null)
            return;

        GameObject depthRoot = GameObject.Find("Quest Meta Depth Runtime");
        if (depthRoot == null)
        {
            depthRoot = new GameObject("Quest Meta Depth Runtime");
            DontDestroyOnLoad(depthRoot);
        }

        Type ovrManagerType = Type.GetType("OVRManager, Oculus.VR");
        Component ovrManager = EnsureOptionalComponent(ovrManagerType, depthRoot);
        if (ovrManager != null)
        {
            SetMemberValue(ovrManager, "isInsightPassthroughEnabled", true);
            SetMemberValue(ovrManager, "requestScenePermissionOnStartup", true);
            SetMemberValue(ovrManager, "SimultaneousHandsAndControllersEnabled", true);
            SetMemberValue(ovrManager, "launchSimultaneousHandsControllersOnStartup", true);
            SetEnumMemberValue(ovrManager, "trackingOriginType", "FloorLevel");
            EnableSimultaneousHandsAndControllersIfAvailable();
        }

        Type passthroughLayerType = Type.GetType("OVRPassthroughLayer, Oculus.VR");
        Component passthroughLayer = EnsureOptionalComponent(passthroughLayerType, depthRoot);
        if (passthroughLayer != null)
        {
            SetEnumMemberValue(passthroughLayer, "overlayType", "Underlay");
            if (passthroughLayer is Behaviour passthroughBehaviour)
                passthroughBehaviour.enabled = true;
        }

        Type cameraRigType = Type.GetType("OVRCameraRig, Oculus.VR");
        Component cameraRig = EnsureOptionalComponent(cameraRigType, depthRoot);
        if (cameraRig != null)
            SetMemberValue(cameraRig, "disableEyeAnchorCameras", true);

        Component depthManager = EnsureOptionalComponent(depthManagerType, depthRoot);
        if (depthManager != null)
        {
            SetEnumMemberValue(depthManager, "OcclusionShadersMode", "SoftOcclusion");
            SetMemberValue(depthManager, "RemoveHands", false);
            if (depthManager is Behaviour behaviour)
                behaviour.enabled = true;
        }

        RequestScenePermissionIfNeeded();
    }

    void ApplyDepthOcclusionShaderToOrdinaryObjects()
    {
        Shader occlusionShader = Shader.Find("GSAC/Quest Depth Occluded Color");
        if (occlusionShader == null)
            occlusionShader = Shader.Find("Meta/Depth/BiRP/Occlusion Standard");
        if (occlusionShader == null)
            return;

        ApplyDepthOcclusionShader(GameObject.Find("Sphere (1)"), occlusionShader);

        foreach (var grabbable in FindObjectsOfType<GrabbableTestBall>(true))
        {
            if (grabbable != null)
                ApplyDepthOcclusionShader(grabbable.gameObject, occlusionShader);
        }
    }

    static void ApplyDepthOcclusionShader(GameObject root, Shader occlusionShader)
    {
        if (root == null || occlusionShader == null)
            return;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; ++i)
            {
                Material material = materials[i];
                if (material == null || material.shader == occlusionShader)
                    continue;

                material.shader = occlusionShader;
                if (material.HasProperty("_EnvironmentDepthBias"))
                    material.SetFloat("_EnvironmentDepthBias", 0.0f);
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    static Component EnsureOptionalComponent(Type type, GameObject target)
    {
        if (type == null || target == null || !typeof(Component).IsAssignableFrom(type))
            return null;

        var existing = FindObjectOfType(type, true) as Component;
        if (existing != null)
            return existing;

        return target.GetComponent(type) ?? target.AddComponent(type);
    }

    static void RequestScenePermissionIfNeeded()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const string scenePermission = "com.oculus.permission.USE_SCENE";
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(scenePermission))
            UnityEngine.Android.Permission.RequestUserPermission(scenePermission);
#endif
    }

    static void EnableSimultaneousHandsAndControllersIfAvailable()
    {
        try
        {
            Type ovrPluginType = Type.GetType("OVRPlugin, Oculus.VR");
            MethodInfo supportMethod = ovrPluginType?.GetMethod(
                "SetMultimodalHandsControllersSupported",
                BindingFlags.Static | BindingFlags.Public);
            supportMethod?.Invoke(null, new object[] { true });

            Type ovrInputType = Type.GetType("OVRInput, Oculus.VR");
            MethodInfo method = ovrInputType?.GetMethod(
                "EnableSimultaneousHandsAndControllers",
                BindingFlags.Static | BindingFlags.Public);

            if (method == null || method.ReturnType != typeof(bool))
                return;

            bool enabled = (bool)method.Invoke(null, null);
            if (!enabled)
                Debug.Log("Quest AR requested simultaneous hands/controllers, but the current runtime did not enable it.");
        }
        catch (Exception exception)
        {
            Debug.Log($"Quest AR could not enable simultaneous hands/controllers: {exception.Message}");
        }
    }

    static void SetMemberValue(Component component, string memberName, object value)
    {
        if (component == null || string.IsNullOrEmpty(memberName))
            return;

        Type type = component.GetType();
        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(component, value);
            return;
        }

        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
            field.SetValue(component, value);
    }

    static void SetEnumMemberValue(Component component, string memberName, string enumValue)
    {
        if (component == null || string.IsNullOrEmpty(memberName) || string.IsNullOrEmpty(enumValue))
            return;

        Type type = component.GetType();
        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType.IsEnum)
        {
            property.SetValue(component, Enum.Parse(property.PropertyType, enumValue));
            return;
        }

        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsEnum)
            field.SetValue(component, Enum.Parse(field.FieldType, enumValue));
    }

    IEnumerator ReportOcclusionStatus()
    {
        for (int frame = 0; frame < 60; ++frame)
            yield return null;

        foreach (var occlusionManager in FindObjectsOfType<AROcclusionManager>(true))
        {
            if (occlusionManager == null || !occlusionManager.enabled)
                continue;

            if (occlusionManager.currentEnvironmentDepthMode == EnvironmentDepthMode.Disabled)
            {
                Debug.LogWarning("Quest AR real-world occlusion requested environment depth, but no active environment depth provider is available. Physical desks/hands will not hide virtual objects until the Meta OpenXR occlusion provider is installed and enabled.");
            }
            else
            {
                Debug.Log($"Quest AR real-world occlusion active. Environment depth mode: {occlusionManager.currentEnvironmentDepthMode}.");
            }

            yield break;
        }

        Debug.LogWarning("Quest AR real-world occlusion manager was not found on an AR camera.");
    }

    void HideTestEnvironment()
    {
        foreach (string objectName in s_DefaultObjectsToHide)
        {
            var go = GameObject.Find(objectName);
            if (go != null)
                go.SetActive(false);
        }
    }

    static Component EnsureComponent(string typeName, GameObject target)
    {
        Type type = Type.GetType(typeName);
        if (type == null || !typeof(Component).IsAssignableFrom(type))
        {
            if (s_WarnedMissingTypes.Add(typeName))
                Debug.LogWarning($"Quest AR setup could not find component type '{typeName}'. Make sure AR Foundation and Unity OpenXR: Meta are installed.");
            return null;
        }

        if (target == null)
        {
            var existing = FindObjectOfType(type) as Component;
            if (existing != null)
                return existing;

            target = new GameObject(type.Name);
        }

        return target.GetComponent(type) ?? target.AddComponent(type);
    }

    static void RestartEnabledBehaviours(string typeName)
    {
        Type type = Type.GetType(typeName);
        if (type == null || !typeof(Behaviour).IsAssignableFrom(type))
            return;

        RestartEnabledBehaviours(type);
    }

    static void RestartEnabledBehaviours(Type type)
    {
        foreach (var obj in FindObjectsOfType(type, true))
        {
            var behaviour = (Behaviour)obj;
            if (!behaviour.enabled)
            {
                behaviour.enabled = true;
                continue;
            }

            behaviour.enabled = false;
            behaviour.enabled = true;
        }
    }
}
