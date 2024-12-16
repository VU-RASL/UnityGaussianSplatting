using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class PoseController : MonoBehaviour
{
    [SerializeField] public SMPLX smplx; // Reference to the SMPL-X model
    public float poseSwitchTime = 3f;   // Time in seconds to switch poses
    public Material smplxMaterial;     // Assign the material with your custom shader

    public ComputeBuffer vertexBuffer; // GPU buffer to store vertex positions
    private Transform[] joints;         // Array to store SMPL-X joint transforms
    private bool isTPose = true;        // Toggle between poses
    private SkinnedMeshRenderer smr;    // Reference to SkinnedMeshRenderer
    [SerializeField] public HahaImporter hahaImporter;
    void Awake()
    {
        
        
        Debug.Log(hahaImporter.data.betas);
        smplx.ResetBodyPose();
        
        Debug.Log("ResetTPose");


    }

    void Start()
    {
        // Validate the SMPL-X reference
        if (smplx == null)
        {
            Debug.LogError("SMPLX object not assigned!");
            return;
        }

        // Validate the shader material reference
        if (smplxMaterial == null)
        {
            Debug.LogError("Material not assigned!");
            return;
        }

        // Get the SkinnedMeshRenderer component
        smr = smplx.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null)
        {
            Debug.LogError("SkinnedMeshRenderer not found on SMPLX object!");
            return;
        }

        // Initialize the joints array from the SMPL-X hierarchy
        InitializeJoints();
        UpdateSMPLXBetas(hahaImporter.data.betas);
        // Initialize the GPU vertex buffer
        InitializeVertexBuffer();
        
        // Start the pose animation loop
        StartCoroutine(AnimatePose());
        
    }

    void InitializeJoints()
    {
        // Get all child transforms of the SMPL-X model
        joints = smplx.GetComponentsInChildren<Transform>();
        Debug.Log($"Found {joints.Length} joints in SMPL-X hierarchy.");
    }
    void UpdateSMPLXBetas(float[] betas)
    {
        if (betas.Length != SMPLX.NUM_BETAS)
        {
            Debug.LogError($"Invalid betas length! Expected  {SMPLX.NUM_BETAS} values.");
            return;
        }

        // Update the betas array in the SMPLX class
        for (int i = 0; i < SMPLX.NUM_BETAS; i++)
        {
            smplx.betas[i] = betas[i];
        }

        // Apply the updated betas to the SMPLX model
        smplx.SetBetaShapes();
        Debug.Log("Updated and applied new betas to SMPLX model.");
    }
    void InitializeVertexBuffer()
    {
        // Create the GPU buffer for vertices
        Mesh mesh = smr.sharedMesh;
        if (mesh == null)
        {
            Debug.LogError("Shared mesh not found in SkinnedMeshRenderer!");
            return;
        }

        vertexBuffer = new ComputeBuffer(mesh.vertexCount, sizeof(float) * 3);

        // Assign the buffer to the material
        smplxMaterial.SetBuffer("_VertexBuffer", vertexBuffer);
        Debug.Log("Initialized GPU buffer for vertex positions and assigned it to the material.");
    }

    System.Collections.IEnumerator AnimatePose()
    {
        
        while (true)
        {
            if (isTPose)
            {
                smplx.SetBodyPose(SMPLX.BodyPose.A);
                // ApplyCustomPose(GenerateRandomPose()); // Apply a random pose
            }
            else
            {
                smplx.SetBodyPose(SMPLX.BodyPose.C);
            }

            // Update the GPU vertex buffer with the baked mesh data
            UpdateVertexBuffer();

            isTPose = !isTPose; // Toggle pose state
            yield return new WaitForSeconds(poseSwitchTime);
        }
    }

    void ApplyTPose()
    {
        foreach (var joint in joints)
        {
            joint.localRotation = Quaternion.identity; // Reset to default (T-pose)
        }
        Debug.Log("Applied T-pose to SMPL-X.");
    }

    void ApplyCustomPose(float[] customPose)
    {
        if (customPose.Length != joints.Length * 3)
        {
            Debug.LogError($"Invalid custom pose array! Expected {joints.Length * 3} values.");
            return;
        }

        for (int i = 0; i < joints.Length; i++)
        {
            // Extract rotation for each joint
            Vector3 rotation = new Vector3(
                customPose[i * 3 + 0], // X rotation
                customPose[i * 3 + 1], // Y rotation
                customPose[i * 3 + 2]  // Z rotation
            );

            joints[i].localEulerAngles = rotation; // Apply rotation
        }
        Debug.Log("Applied custom pose to SMPL-X.");
    }

    float[] GenerateRandomPose()
    {
        float[] randomPose = new float[joints.Length * 3];

        // Generate random rotations for all joints
        for (int i = 0; i < joints.Length; i++)
        {
            randomPose[i * 3 + 0] = Random.Range(-30f, 30f); // X rotation
            randomPose[i * 3 + 1] = Random.Range(-30f, 30f); // Y rotation
            randomPose[i * 3 + 2] = Random.Range(-30f, 30f); // Z rotation
        }

        Debug.Log($"Generated pose length: {randomPose.Length}");
        return randomPose;
    }
    [System.Serializable]
    public class FaceIndexData
    {
        public int FaceIndex;
        public int[] VertexIndices;
    }

    // Class to hold the list of face data for JSON serialization
    [System.Serializable]
    public class FaceIndexDataList
    {
        public List<FaceIndexData> Faces;
    }
    void UpdateVertexBuffer()
    {
        if (smr == null)
        {
            Debug.LogError("SkinnedMeshRenderer not initialized!");
            return;
        }

        // Bake the updated mesh
        Mesh bakedMesh = new Mesh();
        smr.BakeMesh(bakedMesh);

        // Get the updated vertex positions
        Vector3[] vertices = bakedMesh.vertices;

        // Print the first vertex
        if (vertices.Length > 0)
        {
            Debug.Log($"First vertex after update: {vertices[0]}");
        }
        else
        {
            Debug.LogError("No vertices found in the baked mesh!");
        }

        // Upload the vertex positions to the GPU buffer
        if (vertexBuffer != null)
        {
            vertexBuffer.SetData(vertices);
            Debug.Log("Uploaded new vertex positions to GPU.");
        }
        else
        {
            Debug.LogError("Vertex buffer is not initialized!");
        }


        // Get the triangles (face indices)
        int[] triangles = bakedMesh.triangles;

        // Create a list to store face-to-vertex index mappings
        List<FaceIndexData> faceToVertexIndices = new List<FaceIndexData>();

        // Iterate through the triangles (each face has 3 indices)
        for (int i = 0; i < triangles.Length; i += 3)
        {
            // Get the indices of the vertices for this face
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];

            // Add the face index and its vertex indices to the list
            faceToVertexIndices.Add(new FaceIndexData
            {
                FaceIndex = i / 3,
                VertexIndices = new int[] { v0, v1, v2 }
            });
        }

        // Convert to JSON and save to file
        string json = JsonUtility.ToJson(new FaceIndexDataList { Faces = faceToVertexIndices }, true);
        string path = Path.Combine(Application.dataPath, "FaceToVertexIndices.json");
        File.WriteAllText(path, json);

        Debug.Log($"Face-to-vertex indices mapping saved to {path}");
    }

    void OnDestroy()
    {
        // Clean up GPU resources
        if (vertexBuffer != null)
        {
            vertexBuffer.Dispose();
            vertexBuffer = null;
        }
        Debug.Log("Disposed GPU vertex buffer.");
    }

    public void UpdateBetas(float[] newBetas)
    {
        if (newBetas.Length != SMPLX.NUM_BETAS)
        {
            Debug.LogError($"Invalid betas length! Expected {SMPLX.NUM_BETAS} values.");
            return;
        }

        // Update the `betas` field in SMPLX
        for (int i = 0; i < SMPLX.NUM_BETAS; i++)
        {
            smplx.betas[i] = newBetas[i];
        }

        // Apply the updated betas to the mesh
        smplx.SetBetaShapes();
        Debug.Log("Updated and applied new betas to SMPLX mesh.");
    }
    public ComputeBuffer GetVertexBuffer()
    {
        return vertexBuffer;
    }
}
