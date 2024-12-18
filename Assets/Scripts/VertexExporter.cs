using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

public class VertexExporter : MonoBehaviour
{
    public string outputFilePath = "Assets/ExportedVertices.json";
    public SMPLX smplx;
    public Transform root;

    void Start()
    {
        // ApplyToHierarchy(root, (Transform t) => Debug.Log(t.localScale));
        // ApplyToHierarchy(root, (Transform t) => Debug.Log(t.lossyScale));

        // Get the SkinnedMeshRenderer component
        SkinnedMeshRenderer skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer == null)
        {
            Debug.LogError("No SkinnedMeshRenderer found on the GameObject.");
            return;
        }

        if (smplx == null)
        {
            Debug.LogError("No SMPLX component found on the GameObject.");
            return;
        }

        // Create a new Mesh to bake the skinned mesh into
        Mesh bakedMesh = new Mesh();
        skinnedMeshRenderer.BakeMesh(bakedMesh);

        // Get the vertices from the baked mesh
        Vector3[] vertices = bakedMesh.vertices;

        // Convert vertices to a list of float arrays
        List<float[]> vertexList = new List<float[]>();
        foreach (Vector3 vertex in vertices)
        {
            vertexList.Add(new float[] { vertex.x, vertex.y, vertex.z });
        }

        smplx.GetModelInfo(out int numBetas, out int numExpressions, out int numPoseCorrectives);

        // Prepare the export data
        SMPLXExportData exportData = new SMPLXExportData
        {
            numBetas = numBetas,
            numExpressions = numExpressions,
            numPoseCorrectives = numPoseCorrectives,
            vertices = vertexList,
            betas = smplx.betas,
            expressions = smplx.expressions
            // pose_correctives = smplx.pose
        };

        // Serialize the data to JSON
        string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);

        // Write the JSON to a file
        File.WriteAllText(outputFilePath, json);

        Debug.Log($"Exported {vertexList.Count} vertices and SMPL-X model info to {outputFilePath}");
    }

    // Helper class for JSON serialization
    private class SMPLXExportData
    {
        public int numBetas;
        public int numExpressions;
        public int numPoseCorrectives;
        public List<float[]> vertices;
        public float[] betas;
        public float[] expressions;
        // public float[] pose_correctives;
    }

    private void ApplyToHierarchy(Transform parent, Action<Transform> action)
    {
        action?.Invoke(parent);

        if (parent.childCount > 0)
        {
            foreach (Transform child in parent)
            {
                ApplyToHierarchy(child, action);
            }
        }
    }
}
