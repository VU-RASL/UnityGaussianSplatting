using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        if (GeneralOperator.GetSceneMode() == GeneralBuildMode.AR)
            ConfigureCameras();
#endif
    }

    void ConfigureAR()
    {
        EnsureComponent("UnityEngine.XR.ARFoundation.ARSession, Unity.XR.ARFoundation", null);
        ConfigureCameras();
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
        RestartEnabledBehaviours("UnityEngine.XR.ARFoundation.ARPlaneManager, Unity.XR.ARFoundation");
        RestartEnabledBehaviours("UnityEngine.XR.ARFoundation.ARRaycastManager, Unity.XR.ARFoundation");
        RestartEnabledBehaviours(typeof(ARGroundPlacement));
        Debug.Log("Quest AR passthrough components configured.");
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
        }
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
