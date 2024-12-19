using UnityEngine;
using System.IO;

public class ExportVertexAndFaces : MonoBehaviour
{
    [SerializeField] public SMPLX smplx; // Reference to the SMPL-X model
    private SkinnedMeshRenderer smr;    // Reference to SkinnedMeshRenderer

    // File paths for export
    private string verticesFilePath = "UnityVertices.txt";
    private string facesFilePath = "UnityFaces.txt";

    void Start()
    {
        // Get the SkinnedMeshRenderer component
        smr = smplx.GetComponentInChildren<SkinnedMeshRenderer>();
        
        // Bake the mesh to get the current deformed vertex positions
        Mesh bakedMesh = new Mesh();
        smr.BakeMesh(bakedMesh);

        // Get vertices and triangles (faces)
        Vector3[] vertices = bakedMesh.vertices; // Vertex positions
        int[] triangles = bakedMesh.triangles;  // Face indices (triplets of vertex indices)

        // Export vertices
        using (StreamWriter writer = new StreamWriter(verticesFilePath))
        {
            foreach (Vector3 vertex in vertices)
            {
                writer.WriteLine($"{vertex.x} {vertex.y} {vertex.z}");
            }
        }

        Debug.Log($"Exported {vertices.Length} vertices to {verticesFilePath}");

        // Export faces
        using (StreamWriter writer = new StreamWriter(facesFilePath))
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                writer.WriteLine($"{triangles[i]} {triangles[i + 1]} {triangles[i + 2]}");
            }
        }

        Debug.Log($"Exported {triangles.Length / 3} faces to {facesFilePath}");
    }
}
