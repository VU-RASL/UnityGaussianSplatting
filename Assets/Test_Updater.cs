using UnityEngine;
using GaussianSplatting.Runtime;
using Unity.Collections;          // For NativeArray and Allocator
using Unity.Collections.LowLevel.Unsafe; // For unsafe NativeArray operations
using UnityEngine.Rendering;
using System;
using Unity.Mathematics;
using GaussianSplatting.Editor;
using System.IO;

public class TestShaderWithBuffer : MonoBehaviour
{
    public ComputeShader testShader;
    public HahaImporter hahaImporter;
    private GaussianSplatAssetCreator assetCreator; // Reference to GaussianSplatAssetCreator
    private NativeArray<GaussianSplatAssetCreator.InputSplatData> inputSplats;
    private NativeArray<int> splatSHIndices;

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
    private ComputeBuffer tempBuffer;
    // Combined Gaussian-specific buffer
    private ComputeBuffer GaussianDataBuffer;
    private ComputeBuffer UpdatedXyzBuffer;
    private ComputeBuffer UpdatedScalingBuffer;
    private ComputeBuffer UpdatedRotationBuffer;
    private int calcFacesKernelHandle;
    private int mapGaussiansKernelHandle;
    private bool isInitialized = false;
    public struct GaussianData
    {
        public Vector4 rotation;  // Quaternion rotation (x, y, z, w)
        public Vector3 scaling;   // Scaling vector (x, y, z)
        public float shIndex;
    }
    void Start()
    {
        if (hahaImporter == null || poseController == null)
        {
            Debug.LogError("HahaImporter or PoseController not assigned! Drag them in the Inspector.");
            return;
        }

        gaussianRenderer = gaussianSplatsObject.GetComponent<GaussianSplatRenderer>();
        assetCreator = new GaussianSplatAssetCreator();

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
        haha_scalingBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 3);
        haha_scalingBuffer.SetData(haha_scaling);
        // haha_scalingBuffer.GetData()
        // SaveHahaScalingToTxt();
        TBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 3);
        RBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 4);
        kBuffer = new ComputeBuffer(gaussianCount, sizeof(float));
        tempBuffer = new ComputeBuffer(gaussianCount, sizeof(float)*4 );
        GaussianDataBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * (3 + 4 + 1));
        UpdatedXyzBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 3);
        UpdatedScalingBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 3);
        UpdatedRotationBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 4);
        isInitialized = true;
        // SaveVertexBufferAsTxt();
        InitializeComputeShader();
    
    }

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
        testShader.SetBuffer(mapGaussiansKernelHandle, "GaussianDataBuffer", GaussianDataBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "UpdatedXyzBuffer", UpdatedXyzBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "UpdatedScalingBuffer", UpdatedScalingBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "UpdatedRotationBuffer", UpdatedRotationBuffer);
        testShader.SetBuffer(mapGaussiansKernelHandle, "tempBuffer", tempBuffer);

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
        // SavetempBufferToTxt();
        // DebugFaceBuffer();
        // DebugGaussianToFaceBuffer();
    }
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
            writer.WriteLine("TBuffer (Translation Vectors):");
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
void SavetempBufferToTxt()
{
    if (tempBuffer == null)
    {
        Debug.LogError("tempBuffer is null! Ensure it is initialized.");
        return;
    }

    string filePath = "Assets/GaussianAssets/tempBuffer.txt";

    try
    {
        int count = tempBuffer.count; // Get the number of elements
        float4[] data = new float4[count]; // Use Vector3 to match float3 in the shader
        tempBuffer.GetData(data); // Fetch data from the buffer

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("tempBuffer (v12cross):");
            foreach (var item in data)
            {
                writer.WriteLine($"{item.x} {item.y} {item.z} {item.w}"); // Save x, y, z values
                // writer.WriteLine($"{item.x} {item.y} {item.z} ");
                //  writer.WriteLine($"{item}");
            }
        }

        Debug.Log($"tempBuffer data saved to: {filePath}");
    }
    catch (Exception ex)
    {
        Debug.LogError($"Error saving tempBuffer: {ex.Message}");
    }
}
    void UpdateGaussianRenderer()
    {
        if (gaussianRenderer != null && UpdatedXyzBuffer != null)
        {
            int updatedCount = UpdatedXyzBuffer.count;
            if (updatedCount > 0)
            {
                Vector3[] updatedPositions = new Vector3[updatedCount];
                UpdatedXyzBuffer.GetData(updatedPositions);
                gaussianRenderer.m_GpuPosData.SetData(updatedPositions);
            }
        }
        
        if (gaussianRenderer != null && GaussianDataBuffer != null)
        {
            int gaussianCount = GaussianDataBuffer.count;
        if (gaussianRenderer.m_GpuOtherData != null)
        {
            
            int bufferSize = gaussianCount * 16;
            // Retrieve the buffer data
            // Debug.Log(bufferSize);
            byte[] bufferData = new byte[bufferSize];
            gaussianRenderer.m_GpuOtherData.GetData(bufferData);

            float4[] UpdatedRotationArray = new float4[gaussianCount];
        

            UpdatedRotationBuffer.GetData(UpdatedRotationArray);

            // Modify the rotation (first 4 bytes of each entry)
            // string filePath = "Assets/GaussianAssets/qq_output.txt"; // Specify the file path
            // using (StreamWriter writer = new StreamWriter(filePath)){
            for (int i = 0; i < gaussianCount; i++)
            {
                int offset = i * 16; 

                // New rotation
                float4 rot =UpdatedRotationArray[i];
                var q = rot;
                var qq = GaussianUtils.NormalizeSwizzleRotation(new float4(q.x, q.y, q.z, q.w));
                qq = GaussianUtils.PackSmallest3Rotation(qq);
                rot = qq;

                var temp = rot;
                // writer.WriteLine($"Gaussian {i}:  ({temp.x}, {temp.y}, {temp.z}, {temp.w})");

                // rot = GaussianUtils.NormalizeSwizzleRotation(rot);
                // rot = GaussianUtils.PackSmallest3Rotation(rot);
                // Debug.Log(rot);
                uint encodedRot = EncodeQuatToNorm10(rot);

                // Update the first 4 bytes
                Buffer.BlockCopy(BitConverter.GetBytes(encodedRot), 0, bufferData, offset, 4);
            }
            
            // Write the updated data back to the buffer
            gaussianRenderer.m_GpuOtherData.SetData(bufferData);

            Debug.Log("Updated only the rotation in m_GpuOtherData.");
        }
        }
    }


    static uint EncodeQuatToNorm10(float4 v) // 32 bits: 10.10.10.2
        {
            return (uint) (v.x * 1023.5f) | ((uint) (v.y * 1023.5f) << 10) | ((uint) (v.z * 1023.5f) << 20) | ((uint) (v.w * 3.5f) << 30);
        }

    void CreateOtherDataAsset()
{
    NativeArray<GaussianSplatAssetCreator.InputSplatData> updatedInputSplats = default;
    NativeArray<int> splatSHIndices = default;
    if (assetCreator == null)
    {
        Debug.Log("Null");
        return;
    }

    try
    {
        if (gaussianRenderer != null && GaussianDataBuffer != null)
        {
            int gaussianCount = GaussianDataBuffer.count;

            // Retrieve Gaussian data
            GaussianData[] gaussianDataArray = new GaussianData[gaussianCount];
            GaussianDataBuffer.GetData(gaussianDataArray);

            updatedInputSplats = new NativeArray<GaussianSplatAssetCreator.InputSplatData>(gaussianCount, Allocator.TempJob);


            float4[] data = new float4[gaussianCount];
            UpdatedRotationBuffer.GetData(data);
            // Define file path
            // string rotationFilePath = "Assets/GaussianAssets/rotations.txt";


            // using (StreamWriter writer = new StreamWriter(rotationFilePath))
            // {
            //     writer.WriteLine("tempBuffer (v12cross):");
            //     foreach (var item in data)
            //     {
            //         writer.WriteLine($"{item.x} {item.y} {item.z} {item.w}"); // Save x, y, z values
            //         // writer.WriteLine($"{item.x} {item.y} {item.z} ");
            //         //  writer.WriteLine($"{item}");
            //     }
            // }
            float3[] UpdatedScaling = new float3[gaussianCount];
            UpdatedScalingBuffer.GetData(UpdatedScaling);
            float4[] UpdatedRotation = new float4[gaussianCount];
            UpdatedRotationBuffer.GetData(UpdatedRotation);
            // using (StreamWriter rotationWriter = new StreamWriter(rotationFilePath))
            // using (StreamWriter scaleWriter = new StreamWriter(scaleFilePath))
            {
                for (int i = 0; i < gaussianCount; i++)
                {
                    // Write rotation (quaternion) to file

                    float4 rot = UpdatedRotation[i];


                    // rot = GaussianUtils.NormalizeSwizzleRotation(rot);
                    // rot = GaussianUtils.PackSmallest3Rotation(rot);
                    // rotationWriter.WriteLine($"{rot.x} {rot.y} {rot.z} {rot.w}");

                    Quaternion rotation = new Quaternion(
                        rot.x,
                        rot.y,
                        rot.z,
                        rot.w
                    );
       
                    // rotationWriter.WriteLine($"{rotation.x} {rotation.y} {rotation.z} {rotation.w}");

                    // Write scale (vector) to file
                    Vector3 scale = UpdatedScaling[i];
                    // scaleWriter.WriteLine($"{scale.x} {scale.y} {scale.z}");

                    // Update InputSplatData
                    updatedInputSplats[i] = new GaussianSplatAssetCreator.InputSplatData
                    {
                        rot = rotation,
                        scale = GaussianUtils.LinearScale(scale),
                        pos = Vector3.zero,
                        dc0 = Vector3.zero,
                        opacity = 1.0f
                    };
                }
            }

            splatSHIndices = new NativeArray<int>(gaussianCount, Allocator.TempJob);




            string pathOther = "Assets/GaussianAssets/test1_oth.bytes";
            if (string.IsNullOrEmpty(pathOther))
            {
                Debug.LogError("PathOther is not set in GaussianSplatAssetCreator!");
                return;
            }
            Hash128 dataHash = new Hash128();
            assetCreator.CreateOtherData(updatedInputSplats, pathOther, ref dataHash, splatSHIndices);
        }
    }
    finally
    {
        if (updatedInputSplats.IsCreated) updatedInputSplats.Dispose();
        if (splatSHIndices.IsCreated) splatSHIndices.Dispose();
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
