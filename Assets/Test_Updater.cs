using UnityEngine;
using GaussianSplatting.Runtime;
using System;
using Unity.Mathematics;
using System.IO;

public class TestShaderWithBuffer : MonoBehaviour
{
    public Transform debug;

    public ComputeShader testShader;
    public HahaImporter hahaImporter;

    public PoseController poseController;
    [SerializeField] private GaussianSplatRenderer gaussianRenderer;
    
    // GPU Buffers
    private ComputeBuffer gaussianToFaceBuffer;
    private ComputeBuffer haha_xyzBuffer;
    private ComputeBuffer haha_rotationBuffer;
    private ComputeBuffer haha_scalingBuffer;
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer faceBuffer;
    public ComputeBuffer TBuffer;
    private ComputeBuffer RBuffer;
    private ComputeBuffer kBuffer;
    private int calcFacesKernelHandle;
    private int mapGaussiansKernelHandle;
    private bool isInitialized = false;
    public struct GaussianData
    {
        public Vector4 rotation;  // Quaternion rotation (x, y, z, w)
        public Vector3 scaling;   // Scaling vector (x, y, z)
        public float shIndex;
    }

    void Reset()
    {
        AutoAssignReferences();
    }

    void OnValidate()
    {
        AutoAssignReferences();
    }

    void AutoAssignReferences()
    {
        if (hahaImporter == null)
        {
            hahaImporter = GetComponent<HahaImporter>();
        }

        if (poseController == null)
        {
            poseController = GetComponent<PoseController>();
        }

        if (gaussianRenderer == null)
        {
            gaussianRenderer = GetComponent<GaussianSplatRenderer>();
        }
    }

    void Start()
    {
        AutoAssignReferences();

        if (hahaImporter == null || poseController == null)
        {
            Debug.LogError("HahaImporter or PoseController not assigned! Drag them in the Inspector.");
            return;
        }

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
        // haha_scalingBuffer = hahaImporter.GetHahaScalingBuffer();
        float3[] haha_scaling = hahaImporter.GetHAHAScaling();
        faceBuffer = hahaImporter.GetFaceBuffer();
        vertexBuffer = poseController.GetVertexBuffer();

        if (gaussianToFaceBuffer == null || haha_xyzBuffer == null || faceBuffer == null || vertexBuffer == null)
        {
            Debug.LogError("One or more buffers are null! Ensure HahaImporter and PoseController are initialized.");
            return;
        }

        int faceCount = faceBuffer.count;
        int gaussianCount = gaussianToFaceBuffer.count;
        if (gaussianRenderer.asset == null)
        {
            Invoke(nameof(InitializeBuffers), 0.1f);
            return;
        }
        if (gaussianRenderer.asset.posFormat != GaussianSplatAsset.VectorFormat.Float32)
        {
            Debug.LogError("Quest runtime Gaussian animation currently requires Float32/VeryHigh position data to avoid CPU readbacks.");
            enabled = false;
            return;
        }
        if (gaussianRenderer.m_GpuPosData == null || gaussianRenderer.m_GpuOtherData == null)
        {
            Invoke(nameof(InitializeBuffers), 0.1f);
            return;
        }

        haha_scalingBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 3);
        haha_scalingBuffer.SetData(haha_scaling);
        // haha_scalingBuffer.GetData()
        // SaveHahaScalingToTxt();
        TBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 3);
        RBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 4);
        kBuffer = new ComputeBuffer(gaussianCount, sizeof(float));
        isInitialized = true;
        // SaveVertexBufferAsTxt();
        InitializeComputeShader();
    
    }

#if UNITY_EDITOR
void SaveHahaScalingToTxt()
{
    if (haha_scalingBuffer == null)
    {
        Debug.LogError("HahaScalingBuffer is null! Ensure it is initialized.");
        return;
    }

    string filePath = "Assets/GaussianAssets/HahaScaling.txt";

    try
    {
        int count = haha_scalingBuffer.count;
        float3[] data = new float3[count];
        haha_scalingBuffer.GetData(data); // Fetch actual buffer data

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("HahaScaling (Scaling Vectors):");
            foreach (var item in data)
            {
                writer.WriteLine($"{item.x} {item.y} {item.z}"); // Write x, y, z for each scaling vector
            }
        }

        Debug.Log($"HahaScalingBuffer saved to: {filePath}");
    }
    catch (Exception ex)
    {
        Debug.LogError($"Error saving HahaScalingBuffer: {ex.Message}");
    }
}


    void SaveVertexBufferAsTxt()
    {
        if (vertexBuffer == null)
        {
            Debug.LogError("VertexBuffer is null! Ensure the PoseController is initialized and the VertexBuffer is set.");
            return;
        }

        string vertexFilePath = "Assets/GaussianAssets/VertexBuffer.txt";

        try
        {
            // Read vertex data from the buffer
            int vertexCount = vertexBuffer.count;
            Vector3[] vertices = new Vector3[vertexCount];
            vertexBuffer.GetData(vertices);

            // Write the vertices to the file
            using (StreamWriter writer = new StreamWriter(vertexFilePath))
            {
                foreach (var vertex in vertices)
                {
                    writer.WriteLine($"{vertex.x} {vertex.y} {vertex.z}");
                }
            }

            Debug.Log($"VertexBuffer saved to: {vertexFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving VertexBuffer: {ex.Message}");
        }
    }
#endif

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
        testShader.SetBuffer(calcFacesKernelHandle,  "GaussianToFaceBuffer", gaussianToFaceBuffer);
        // testShader.SetBuffer(calcFacesKernelHandle, "tempBuffer", tempBuffer);



        testShader.SetBuffer(mapGaussiansKernelHandle, "GaussianToFaceBuffer", gaussianToFaceBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "HahaXyzBuffer", haha_xyzBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "HahaRotationBuffer", haha_rotationBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "HahaScalingBuffer", haha_scalingBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "TBuffer", TBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "RBuffer", RBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "kBuffer", kBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "SplatPosOutput", gaussianRenderer.m_GpuPosData);
        testShader.SetBuffer(mapGaussiansKernelHandle, "SplatOtherOutput", gaussianRenderer.m_GpuOtherData);
        int otherStride = GaussianSplatAsset.GetOtherSizeNoSHIndex(gaussianRenderer.asset.scaleFormat);
        if (gaussianRenderer.asset.shFormat > GaussianSplatAsset.SHFormat.Norm6)
        {
            otherStride += 2;
        }
        testShader.SetInt("_SplatOtherStride", otherStride);

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

        int faceThreadGroups = Mathf.CeilToInt(gaussianToFaceBuffer.count / 64.0f);
        testShader.Dispatch(calcFacesKernelHandle, faceThreadGroups, 1, 1);

        int gaussianThreadGroups = Mathf.CeilToInt(gaussianToFaceBuffer.count / 64.0f);
        testShader.Dispatch(mapGaussiansKernelHandle, gaussianThreadGroups, 1, 1);

        UpdateGaussianRenderer();
        // CreateOtherDataAsset();
        // SaveTBufferToTxt();
        // SaveRBufferToTxt();
        // SaveKBufferToTxt();
        // DebugFaceBuffer();
        // DebugGaussianToFaceBuffer();
    }
#if UNITY_EDITOR
void DebugGaussianToFaceBuffer()
{
    if (gaussianToFaceBuffer == null)
    {
        Debug.LogError("GaussianToFaceBuffer is null!");
        return;
    }

    int gaussianCount = gaussianToFaceBuffer.count;
    int[] gaussianToFaceData = new int[gaussianCount];
    gaussianToFaceBuffer.GetData(gaussianToFaceData);

    using (StreamWriter writer = new StreamWriter("Assets/GaussianToFaceBuffer.txt"))
    {
        for (int i = 0; i < gaussianToFaceData.Length; i++)
        {
            writer.WriteLine($"Gaussian {i} -> Face {gaussianToFaceData[i]}");
        }
    }

    Debug.Log("GaussianToFaceBuffer saved to file.");
}
void SaveTBufferToTxt()
{
    if (TBuffer == null)
    {
        Debug.LogError("TBuffer is null! Ensure it is initialized.");
        return;
    }

    string filePath = "Assets/GaussianAssets/TBuffer.txt";

    try
    {
        int count = TBuffer.count;
        Vector3[] data = new Vector3[count];
        TBuffer.GetData(data); // Fetch actual buffer data

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            foreach (var item in data)
            {
                writer.WriteLine($"{item.x} {item.y} {item.z}"); // Write x, y, z for each row
            }
        }

        Debug.Log($"TBuffer saved to: {filePath}");
    }
    catch (Exception ex)
    {
        Debug.LogError($"Error saving TBuffer: {ex.Message}");
    }
}

void SaveRBufferToTxt()
{
    if (RBuffer == null)
    {
        Debug.LogError("RBuffer is null! Ensure it is initialized.");
        return;
    }

    string filePath = "Assets/GaussianAssets/RBuffer.txt";

    try
    {
        int count = RBuffer.count;
        Quaternion[] data = new Quaternion[count];
        RBuffer.GetData(data); // Fetch actual buffer data

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("RBuffer (Rotation Quaternions):");
            foreach (var item in data)
            {
                writer.WriteLine($"{item.x} {item.y} {item.z} {item.w}"); // Write quaternion x, y, z, w
            }
        }

        Debug.Log($"RBuffer saved to: {filePath}");
    }
    catch (Exception ex)
    {
        Debug.LogError($"Error saving RBuffer: {ex.Message}");
    }
}

void SaveKBufferToTxt()
{
    if (kBuffer == null)
    {
        Debug.LogError("kBuffer is null! Ensure it is initialized.");
        return;
    }

    string filePath = "Assets/GaussianAssets/kBuffer.txt";

    try
    {
        int count = kBuffer.count;
        float[] data = new float[count];
        kBuffer.GetData(data); // Fetch actual buffer data

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("kBuffer (Scaling Factors):");
            foreach (var item in data)
            {
                writer.WriteLine($"{item}"); // Write each scaling factor
            }
        }

        Debug.Log($"kBuffer saved to: {filePath}");
    }
    catch (Exception ex)
    {
        Debug.LogError($"Error saving kBuffer: {ex.Message}");
    }
}
void DebugFaceBuffer()
{
    if (faceBuffer == null)
    {
        Debug.LogError("FaceBuffer is null!");
        return;
    }

    int faceCount = faceBuffer.count;
    int3[] faceData = new int3[faceCount];
    faceBuffer.GetData(faceData);

    using (StreamWriter writer = new StreamWriter("Assets/FaceBuffer.txt"))
    {
        for (int i = 0; i < faceData.Length; i++)
        {
            writer.WriteLine($"Face {i}: {faceData[i].x} {faceData[i].y} {faceData[i].z}");
        }
    }

    Debug.Log("FaceBuffer saved to file.");
}
#endif
    void UpdateGaussianRenderer()
    {
        // Updated directly on GPU by MapGaussiansToFaces.
    }

    public ComputeBuffer GetTBuffer()
    {
        return TBuffer;
    }



    void OnDestroy()
    {
        haha_scalingBuffer?.Release();
        TBuffer?.Release();
        RBuffer?.Release();
        kBuffer?.Release();
    }
}
