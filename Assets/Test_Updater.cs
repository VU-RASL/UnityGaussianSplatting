using UnityEngine;

public class VertexBufferHandler : MonoBehaviour
{
    public SkinnedMeshRenderer skinnedMeshRenderer; // Reference to SkinnedMeshRenderer
    public Material material; // Material using your custom shader

    private ComputeBuffer vertexBuffer; // GPU buffer for storing vertex positions
    private Vector3[] vertexPositions; // CPU-side array for debugging
    private int vertexCount;

    void Start()
    {
        // Get the mesh from the SkinnedMeshRenderer
        Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;

        if (sharedMesh == null)
        {
            Debug.LogError("No mesh assigned to SkinnedMeshRenderer!");
            return;
        }

        // Get the vertex count
        vertexCount = sharedMesh.vertexCount;

        // Initialize the ComputeBuffer
        vertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        vertexPositions = new Vector3[vertexCount]; // CPU-side array to read back data (optional)

        // Bind the ComputeBuffer to the shader
        material.SetBuffer("_VertexOutput", vertexBuffer);

        Debug.Log($"Buffer created for {vertexCount} vertices.");
    }

    void Update()
    {
        // Render the object (ensure it's using the correct material)
        // Graphics.DrawMesh(skinnedMeshRenderer.sharedMesh, transform.localToWorldMatrix, material, 0);

        // Retrieve data from the buffer for debugging (optional)
        vertexBuffer.GetData(vertexPositions);

        // Example: Debug the first vertex position
        if (vertexPositions.Length > 0)
        {
            Debug.Log($"First vertex position: {vertexPositions[100]}");
        }
    }

    void OnDestroy()
    {
        // Release the buffer to avoid memory leaks
        if (vertexBuffer != null)
        {
            vertexBuffer.Release();
        }
    }
}
