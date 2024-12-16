using UnityEngine;
using GSAvatar.Runtime;
public class Test_shader_with_buffer : MonoBehaviour
{
//     public ComputeShader testShader;
//     public HahaImporter hahaImporter;
//     public PoseController poseController;
//     [SerializeField] private GameObject gaussianSplatsObject;
//     private GSAvatarRenderer gaussianRenderer;
//     // GPU Buffers
//     private ComputeBuffer gaussianToFaceBuffer;
//     private ComputeBuffer haha_xyzBuffer;
//     private ComputeBuffer haha_scalingBuffer;
//     private ComputeBuffer vertexBuffer;
//     private ComputeBuffer faceBuffer;
//     private ComputeBuffer TBuffer;
//     private ComputeBuffer RBuffer;
//     private ComputeBuffer kBuffer;

//     // Gaussian-specific output buffers
//     private ComputeBuffer GaussianTBuffer;
//     private ComputeBuffer GaussianRBuffer;
//     private ComputeBuffer GaussianKBuffer;
//     private ComputeBuffer UpdatedXyzBuffer;
//     private ComputeBuffer UpdatedScalingBuffer;
//     private int calcFacesKernelHandle;
//     private int mapGaussiansKernelHandle;
//     private bool isInitialized = false;

//     void Start()
//     {
//         // Check dependencies
//         if (hahaImporter == null || poseController == null)
//         {
//             Debug.LogError("HahaImporter or PoseController not assigned! Drag them in the Inspector.");
//             return;
//         }
//         gaussianRenderer = gaussianSplatsObject.GetComponent<GSAvatarRenderer>();

//         // Check for Gaussian Renderer
//         if (gaussianRenderer == null)
//         {
//             Debug.LogError("GSAvatarRenderer not assigned! Drag it in the Inspector.");
//             return;
//         }

//         // Wait for buffers to be initialized
//         Invoke(nameof(InitializeBuffers), 0.1f);
//     }

//     void InitializeBuffers()
//     {
//         if (hahaImporter == null || poseController == null)
//         {
//             Debug.LogError("HahaImporter or PoseController not found!");
//             return;
//         }

//         // Get buffers from HahaImporter and PoseController
//         gaussianToFaceBuffer = hahaImporter.GetGaussianToFaceBuffer();
//         haha_xyzBuffer = hahaImporter.GetHahaXyzBuffer();
//         haha_scalingBuffer = hahaImporter.GetHahaScalingBuffer();
//         faceBuffer = hahaImporter.GetFaceBuffer();
//         vertexBuffer = poseController.GetVertexBuffer();

//         // Check if buffers are initialized
//         if (gaussianToFaceBuffer == null || haha_xyzBuffer == null || faceBuffer == null || vertexBuffer == null)
//         {
//             Debug.LogError("One or more buffers are null! Ensure HahaImporter and PoseController are initialized.");
//             return;
//         }

//         Debug.Log("All buffers initialized successfully!");

//         // Create output buffers
//         int faceCount = faceBuffer.count;
//         int gaussianCount = gaussianToFaceBuffer.count;
//         Debug.Log("Testing !");
//         Debug.Log(gaussianCount);
//         TBuffer = new ComputeBuffer(faceCount, sizeof(float) * 3); // float3
//         RBuffer = new ComputeBuffer(faceCount, sizeof(float) * 4); // float4
//         kBuffer = new ComputeBuffer(faceCount, sizeof(float));    // float

//         GaussianTBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 3); // float3
//         GaussianRBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 4); // float4
//         GaussianKBuffer = new ComputeBuffer(gaussianCount, sizeof(float));    // float
//         UpdatedXyzBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 3); // float3
//         UpdatedScalingBuffer = new ComputeBuffer(gaussianCount, sizeof(float) * 3); // float3
//         isInitialized = true;

//         // Initialize compute shader
//         InitializeComputeShader();
//     }

//     void InitializeComputeShader()
//     {
//         if (!isInitialized) return;

//         calcFacesKernelHandle = testShader.FindKernel("CalcFacesTransform");
//         if (calcFacesKernelHandle < 0)
//         {
//             Debug.LogError("Kernel 'CalcFacesTransform' not found!");
//             return;
//         }

//         mapGaussiansKernelHandle = testShader.FindKernel("MapGaussiansToFaces");
//         if (mapGaussiansKernelHandle < 0)
//         {
//             Debug.LogError("Kernel 'MapGaussiansToFaces' not found!");
//             return;
//         }

//         // Set buffers for CalcFacesTransform kernel
//         testShader.SetBuffer(calcFacesKernelHandle, "FaceBuffer", faceBuffer);
//         testShader.SetBuffer(calcFacesKernelHandle, "VertexBuffer", vertexBuffer);
//         testShader.SetBuffer(calcFacesKernelHandle, "TBuffer", TBuffer);
//         testShader.SetBuffer(calcFacesKernelHandle, "RBuffer", RBuffer);
//         testShader.SetBuffer(calcFacesKernelHandle, "kBuffer", kBuffer);

//         // Set buffers for MapGaussiansToFaces kernel
//         testShader.SetBuffer(mapGaussiansKernelHandle, "GaussianToFaceBuffer", gaussianToFaceBuffer);
//         testShader.SetBuffer(mapGaussiansKernelHandle, "TBuffer", TBuffer);
//         testShader.SetBuffer(mapGaussiansKernelHandle, "RBuffer", RBuffer);
//         testShader.SetBuffer(mapGaussiansKernelHandle, "kBuffer", kBuffer);
//         testShader.SetBuffer(mapGaussiansKernelHandle, "GaussianTBuffer", GaussianTBuffer);
//         testShader.SetBuffer(mapGaussiansKernelHandle, "GaussianRBuffer", GaussianRBuffer);
//         testShader.SetBuffer(mapGaussiansKernelHandle, "GaussianKBuffer", GaussianKBuffer);
//         testShader.SetBuffer(mapGaussiansKernelHandle, "HahaXyzBuffer", haha_xyzBuffer);
//         testShader.SetBuffer(mapGaussiansKernelHandle, "UpdatedXyzBuffer", UpdatedXyzBuffer);
//         testShader.SetBuffer(mapGaussiansKernelHandle, "HahaScalingBuffer", haha_scalingBuffer);
//         testShader.SetBuffer(mapGaussiansKernelHandle, "UpdatedScalingBuffer", UpdatedScalingBuffer);
//         ExecuteShader();
//     }
//     void Update()
//     {
//         vertexBuffer = poseController.GetVertexBuffer();
//         testShader.SetBuffer(calcFacesKernelHandle, "VertexBuffer", vertexBuffer);
//         ExecuteShader();
//     }
//     void ExecuteShader()
//     {
//         if (!isInitialized) return;

//         // Dispatch CalcFacesTransform kernel
//         int faceThreadGroups = Mathf.CeilToInt(faceBuffer.count / 64.0f);
//         testShader.Dispatch(calcFacesKernelHandle, faceThreadGroups, 1, 1);
//         Debug.Log("CalcFacesTransform kernel executed.");

//         // Dispatch MapGaussiansToFaces kernel
//         int gaussianThreadGroups = Mathf.CeilToInt(gaussianToFaceBuffer.count / 64.0f);
//         testShader.Dispatch(mapGaussiansKernelHandle, gaussianThreadGroups, 1, 1);
//         Debug.Log("MapGaussiansToFaces kernel executed.");

//         // Update the GaussianRenderer with the new positions
//         UpdateGaussianRenderer();
//     }

//     void UpdateGaussianRenderer()
//     {
//         if (gaussianRenderer != null && UpdatedXyzBuffer != null)
//         {
//             int gpuPosDataSize = gaussianRenderer.m_GpuPosData.count;
//             int updatedBufferSize = UpdatedXyzBuffer.count;

//             // Retrieve data from the compute buffer (2D array: N x 3)
//             Vector3[] updatedPositions = new Vector3[updatedBufferSize];
//             UpdatedXyzBuffer.GetData(updatedPositions);

//             // Flatten the 2D array into a 1D array
//             int numFloats = updatedPositions.Length * 3;
//             float[] flattenedData = new float[numFloats];
//             for (int i = 0; i < updatedPositions.Length; i++)
//             {
//                 flattenedData[i * 3 + 0] = updatedPositions[i].x;
//                 flattenedData[i * 3 + 1] = updatedPositions[i].y;
//                 flattenedData[i * 3 + 2] = updatedPositions[i].z;
//             }


//             // Set the flattened (and potentially zero-padded) data to the GPU buffer
//             gaussianRenderer.m_GpuPosData.SetData(flattenedData);

//             Debug.Log("Updated m_GpuPosData in GSAvatarRenderer.");
//         }

//     }

//     void OnDestroy()
//     {
//         // Release buffers
//         gaussianToFaceBuffer?.Release();
//         haha_xyzBuffer?.Release();
//         faceBuffer?.Release();
//         vertexBuffer?.Release();
//         TBuffer?.Release();
//         RBuffer?.Release();
//         kBuffer?.Release();
//         GaussianTBuffer?.Release();
//         GaussianRBuffer?.Release();
//         GaussianKBuffer?.Release();
//         UpdatedXyzBuffer?.Release();
//         UpdatedScalingBuffer?.Release();
//         Debug.Log("Released ComputeBuffers.");
//     }

//     public ComputeBuffer GetUpdatedXyzBuffer()
//     {
//         return UpdatedXyzBuffer;
//     }
}
