using UnityEngine;

public class PoseController : MonoBehaviour
{
    [SerializeField] public SMPLX smplx; // Reference to the SMPL-X model
    public float poseSwitchTime = 3f;   // Time in seconds to switch poses
    public Material smplxMaterial;     // Assign the material with your custom shader

    public ComputeBuffer vertexBuffer; // GPU buffer to store vertex positions
    private Transform[] joints;         // Array to store SMPL-X joint transforms
    private bool isTPose = true;        // Toggle between poses
    private SkinnedMeshRenderer smr;    // Reference to SkinnedMeshRenderer
    void Awake()
    {
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
