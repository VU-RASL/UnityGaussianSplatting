using UnityEngine;

public class GaussianSplatUpdaterWithFaces : MonoBehaviour
{
    [SerializeField] public SMPLX smplx; // SMPL-X model reference
    public Material gaussianMaterial;   // Gaussian splatting material
    public ComputeShader gaussianComputeShader; // Compute shader for Gaussian updates

    private GraphicsBuffer vertexBuffer;       // SMPL-X vertex GPU buffer
    private GraphicsBuffer faceBuffer;         // SMPL-X face index GPU buffer
    private GraphicsBuffer gaussianPositionBuffer; // Gaussian positions GPU buffer
    private GraphicsBuffer gaussianRotationBuffer; // Gaussian rotations GPU buffer
    private GraphicsBuffer gaussianScaleBuffer;    // Gaussian scales GPU buffer

    private int numVertices; // Number of vertices in SMPL-X
    private int numFaces;    // Number of faces in SMPL-X
    private int numGaussians; // Number of Gaussians (mapped to faces)

    private int[] gaussianToFaceMapping; // Mapping from Gaussians to faces

    void Start()
    {
        if (smplx == null || gaussianMaterial == null || gaussianComputeShader == null)
        {
            Debug.LogError("Missing references in GaussianSplatUpdaterWithFaces!");
            return;
        }

        InitializeBuffers();
    }

    void InitializeBuffers()
    {
        // Get the SkinnedMeshRenderer from the SMPL-X model
        SkinnedMeshRenderer smr = smplx.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null)
        {
            Debug.LogError("SkinnedMeshRenderer not found in SMPL-X model!");
            return;
        }

        Mesh mesh = smr.sharedMesh;
        numVertices = mesh.vertexCount;
        numFaces = mesh.triangles.Length / 3;
        numGaussians = numFaces; // Assuming one Gaussian per face

        // Initialize SMPL-X Vertex GPU Buffer
        vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numVertices, sizeof(float) * 3);

        // Initialize Face Index GPU Buffer
        faceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numFaces * 3, sizeof(int));
        faceBuffer.SetData(mesh.triangles);

        // Initialize Gaussian Data Buffers
        gaussianPositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numGaussians, sizeof(float) * 3);
        gaussianRotationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numGaussians, sizeof(float) * 4);
        gaussianScaleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numGaussians, sizeof(float) * 3);

        // Assign buffers to Gaussian material
        gaussianMaterial.SetBuffer("_GaussianPositions", gaussianPositionBuffer);
        gaussianMaterial.SetBuffer("_GaussianRotations", gaussianRotationBuffer);
        gaussianMaterial.SetBuffer("_GaussianScales", gaussianScaleBuffer);

        Debug.Log("Initialized SMPL-X and Gaussian GPU Buffers.");
    }

    void UpdateGaussianData()
    {
        // Ensure compute shader is properly set
        if (gaussianComputeShader == null) return;

        int kernel = gaussianComputeShader.FindKernel("UpdateGaussians");

        // Set compute shader buffers
        gaussianComputeShader.SetBuffer(kernel, "VertexBuffer", vertexBuffer);
        gaussianComputeShader.SetBuffer(kernel, "FaceBuffer", faceBuffer);
        gaussianComputeShader.SetBuffer(kernel, "GaussianPositions", gaussianPositionBuffer);
        gaussianComputeShader.SetBuffer(kernel, "GaussianRotations", gaussianRotationBuffer);
        gaussianComputeShader.SetBuffer(kernel, "GaussianScales", gaussianScaleBuffer);

        // Set other parameters (e.g., Gaussian-to-face mapping)
        gaussianComputeShader.SetInt("_NumFaces", numFaces);

        // Dispatch compute shader
        int threadGroups = Mathf.CeilToInt(numFaces / 256.0f);
        gaussianComputeShader.Dispatch(kernel, threadGroups, 1, 1);

        Debug.Log("Updated Gaussian data based on SMPL-X faces.");
    }

    void UpdateVertexBuffer()
    {
        // Get the SkinnedMeshRenderer from the SMPL-X model
        SkinnedMeshRenderer smr = smplx.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null)
        {
            Debug.LogError("SkinnedMeshRenderer not found!");
            return;
        }

        // Bake the updated mesh
        Mesh bakedMesh = new Mesh();
        smr.BakeMesh(bakedMesh);

        // Upload the vertex positions to the GPU buffer
        Vector3[] vertices = bakedMesh.vertices;
        vertexBuffer.SetData(vertices);
        Debug.Log("Uploaded SMPL-X vertex positions to GPU.");
    }

    void Update()
    {
        // Update SMPL-X vertices and Gaussian data each frame
        UpdateVertexBuffer();
        UpdateGaussianData();
    }

    void OnDestroy()
    {
        // Clean up GPU resources
        vertexBuffer?.Dispose();
        faceBuffer?.Dispose();
        gaussianPositionBuffer?.Dispose();
        gaussianRotationBuffer?.Dispose();
        gaussianScaleBuffer?.Dispose();
        Debug.Log("Disposed GPU buffers.");
    }
}
