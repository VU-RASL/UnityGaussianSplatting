using UnityEngine;

public class Test_shader_with_buffer : MonoBehaviour
{
    public ComputeShader testShader;
    public HahaImporter hahaImporter;
    public PoseController poseController;

    // GPU Buffers
    private ComputeBuffer gaussianToFaceBuffer;
    private ComputeBuffer haha_xyzBuffer;
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer faceBuffer;

    private int kernelHandle;
    private bool isInitialized = false;

    void Start()
    {
        // Check dependencies
        if (hahaImporter == null || poseController == null)
        {
            Debug.LogError("HahaImporter or PoseController not assigned! Drag them in the Inspector.");
            return;
        }

        // Wait for buffers to be initialized
        Invoke(nameof(InitializeBuffers), 0.1f);
    }

    void InitializeBuffers()
    {
        if (hahaImporter == null || poseController == null)
        {
            Debug.LogError("HahaImporter or PoseController not found!");
            return;
        }

        // Get buffers from HahaImporter and PoseController
        gaussianToFaceBuffer = hahaImporter.GetGaussianToFaceBuffer();
        haha_xyzBuffer = hahaImporter.GetHahaXyzBuffer();
        faceBuffer = hahaImporter.GetFaceBuffer();
        vertexBuffer = poseController.GetVertexBuffer();

        // Check if buffers are initialized
        if (gaussianToFaceBuffer == null || haha_xyzBuffer == null || faceBuffer == null || vertexBuffer == null)
        {
            Debug.LogError("One or more buffers are null! Ensure HahaImporter and PoseController are initialized.");
            return;
        }

        Debug.Log("All buffers initialized successfully!");
        isInitialized = true;

        // Initialize compute shader
        InitializeComputeShader();
    }

    void InitializeComputeShader()
    {
        if (!isInitialized) return;

        kernelHandle = testShader.FindKernel("CSMain");

        // Set buffers in compute shader
        testShader.SetBuffer(kernelHandle, "GaussianToFaceBuffer", gaussianToFaceBuffer);
        testShader.SetBuffer(kernelHandle, "HahaXyzBuffer", haha_xyzBuffer);
        testShader.SetBuffer(kernelHandle, "FaceBuffer", faceBuffer);
        testShader.SetBuffer(kernelHandle, "VertexBuffer", vertexBuffer);

        ExecuteShader();
    }

    void ExecuteShader()
    {
        if (!isInitialized) return;

        int threadGroups = Mathf.CeilToInt(gaussianToFaceBuffer.count / 64.0f);
        testShader.Dispatch(kernelHandle, threadGroups, 1, 1);
        Debug.Log("Compute shader executed.");
    }

    void OnDestroy()
    {
        gaussianToFaceBuffer?.Release();
        haha_xyzBuffer?.Release();
        faceBuffer?.Release();
        vertexBuffer?.Release();
        Debug.Log("Released ComputeBuffers.");
    }
}
