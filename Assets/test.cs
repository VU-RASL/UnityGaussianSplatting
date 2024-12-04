using UnityEngine;

public class VertexCountLogger : MonoBehaviour
{
    void Start()
    {
        // Get the Mesh from the MeshFilter or SkinnedMeshRenderer
        Mesh mesh = null;

        if (TryGetComponent<MeshFilter>(out MeshFilter meshFilter))
        {
            mesh = meshFilter.mesh;
        }
        else if (TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer skinnedMeshRenderer))
        {
            mesh = skinnedMeshRenderer.sharedMesh;
        }

        // Print the number of vertices
        if (mesh != null)
        {
            Debug.Log($"Number of vertices in the mesh: {mesh.vertexCount}");
        }
        else
        {
            Debug.LogError("No Mesh found on this GameObject.");
        }
    }
}
