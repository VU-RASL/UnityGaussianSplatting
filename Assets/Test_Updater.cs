using UnityEngine;
using GaussianSplatting.Runtime;

public class TestShaderWithBuffer : MonoBehaviour
{
    public ComputeShader testShader;
    public HahaImporter hahaImporter;
    public PoseController poseController;
    [SerializeField] private GameObject gaussianSplatsObject;
    private GaussianSplatRenderer gaussianRenderer;

    // GPU Buffers
    private ComputeBuffer gaussianToFaceBuffer;
    private ComputeBuffer haha_xyzBuffer;
    private ComputeBuffer haha_rotationBuffer;
    private ComputeBuffer haha_scalingBuffer;
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer faceBuffer;
    private ComputeBuffer TBuffer;
    private ComputeBuffer RBuffer;
    private ComputeBuffer kBuffer;

    // Combined Gaussian-specific buffer
    private ComputeBuffer GaussianDataBuffer;
    private ComputeBuffer UpdatedXyzBuffer;

    private int calcFacesKernelHandle;
    private int mapGaussiansKernelHandle;
    private bool isInitialized = false;

    void Start()
    {
        if (hahaImporter == null || poseController == null)
        {
            Debug.LogError("HahaImporter or PoseController not assigned! Drag them in the Inspector.");
            return;
        }

        gaussianRenderer = gaussianSplatsObject.GetComponent<GaussianSplatRenderer>();
        if (gaussianRenderer == null)
        {
            Debug.LogError("GaussianSplatRenderer not assigned!");
            return;
        }

        Invoke(nameof(InitializeBuffers), 0.1f);
    }

    void InitializeBuffers()
    {
        gaussianToFaceBuffer = hahaImporter.GetGaussianToFaceBuffer();
        haha_xyzBuffer = hahaImporter.GetHahaXyzBuffer();
        haha_rotationBuffer = hahaImporter.GetHahaRotationBuffer();
        haha_scalingBuffer = hahaImporter.GetHahaScalingBuffer();
        faceBuffer = hahaImporter.GetFaceBuffer();
        vertexBuffer = poseController.GetVertexBuffer();

        if (gaussianToFaceBuffer == null || haha_xyzBuffer == null || faceBuffer == null || vertexBuffer == null)
        {
            Debug.LogError("One or more buffers are null! Ensure HahaImporter and PoseController are initialized.");
            return;
        }

        int faceCount = faceBuffer.count;
        int gaussianCount = gaussianToFaceBuffer.count;

        TBuffer = new ComputeBuffer(faceCount, sizeof(float) * 3);
        RBuffer = new ComputeBuffer(faceCount, sizeof(float) * 4);
        kBuffer = new ComputeBuffer(faceCount, sizeof(float));

        GaussianDataBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * (3 + 4 + 1));
        UpdatedXyzBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 3);
        isInitialized = true;

        InitializeComputeShader();
    }

    void InitializeComputeShader()
    {
        if (!isInitialized) return;

        calcFacesKernelHandle = testShader.FindKernel("CalcFacesTransform");
        mapGaussiansKernelHandle = testShader.FindKernel("MapGaussiansToFaces");

        testShader.SetBuffer(calcFacesKernelHandle, "FaceBuffer", faceBuffer);
        testShader.SetBuffer(calcFacesKernelHandle, "VertexBuffer", vertexBuffer);
        testShader.SetBuffer(calcFacesKernelHandle, "TBuffer", TBuffer);
        testShader.SetBuffer(calcFacesKernelHandle, "RBuffer", RBuffer);
        testShader.SetBuffer(calcFacesKernelHandle, "kBuffer", kBuffer);

        testShader.SetBuffer(mapGaussiansKernelHandle, "GaussianToFaceBuffer", gaussianToFaceBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "HahaXyzBuffer", haha_xyzBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "HahaRotationBuffer", haha_rotationBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "HahaScalingBuffer", haha_scalingBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "TBuffer", TBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "RBuffer", RBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "kBuffer", kBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "GaussianDataBuffer", GaussianDataBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "UpdatedXyzBuffer", UpdatedXyzBuffer);

        ExecuteShader();
    }
    void Update()
    {
        vertexBuffer = poseController.GetVertexBuffer();
        testShader.SetBuffer(calcFacesKernelHandle, "VertexBuffer", vertexBuffer);

        ExecuteShader();
    }
    void ExecuteShader()
    {
        if (!isInitialized) return;

        int faceThreadGroups = Mathf.CeilToInt(faceBuffer.count / 64.0f);
        testShader.Dispatch(calcFacesKernelHandle, faceThreadGroups, 1, 1);

        int gaussianThreadGroups = Mathf.CeilToInt(gaussianToFaceBuffer.count / 64.0f);
        testShader.Dispatch(mapGaussiansKernelHandle, gaussianThreadGroups, 1, 1);

        UpdateGaussianRenderer();
    }

    void UpdateGaussianRenderer()
    {
        if (gaussianRenderer != null && UpdatedXyzBuffer != null)
        {
            Vector3[] updatedPositions = new Vector3[UpdatedXyzBuffer.count];
            UpdatedXyzBuffer.GetData(updatedPositions);
            gaussianRenderer.m_GpuPosData.SetData(updatedPositions);
        }
    }

    void OnDestroy()
    {
        gaussianToFaceBuffer?.Release();
        haha_xyzBuffer?.Release();
        haha_rotationBuffer?.Release();
        haha_scalingBuffer?.Release();
        faceBuffer?.Release();
        vertexBuffer?.Release();
        TBuffer?.Release();
        RBuffer?.Release();
        kBuffer?.Release();
        GaussianDataBuffer?.Release();
        UpdatedXyzBuffer?.Release();
    }
}
