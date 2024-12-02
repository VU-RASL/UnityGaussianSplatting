using UnityEngine;

public class PoseController : MonoBehaviour
{
    [SerializeField] public SMPLX smplx; 
    public float poseSwitchTime = 3f;   // Time in seconds to switch poses
    public Material smplxMaterial;     // Assign the material with your custom shader

    private GraphicsBuffer vertexBuffer; // GPU buffer to store vertex positions
    private Transform[] joints;         // Array to store SMPL-X joint transforms
    private bool isTPose = true;        // Toggle between poses

    void Start()
    {
        if (smplx == null)
        {
            Debug.LogError("SMPLX object not assigned!");
            return;
        }

        if (smplxMaterial == null)
        {
            Debug.LogError("Material not assigned!");
            return;
        }

        // Initialize the joints array from SMPLX hierarchy
        InitializeJoints();

        // Initialize the GPU buffer
        InitializeVertexBuffer();

        // Start the animation loop
        StartCoroutine(AnimatePose());
    }

    void InitializeJoints()
    {
        // Get all child transforms of the SMPLX model
        joints = smplx.GetComponentsInChildren<Transform>();
        Debug.Log($"Found {joints.Length} joints in SMPLX hierarchy.");
    }

    void InitializeVertexBuffer()
    {
        // Get the SkinnedMeshRenderer from the SMPLX model
        SkinnedMeshRenderer smr = smplx.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null)
        {
            Debug.LogError("SkinnedMeshRenderer not found!");
            return;
        }

        Mesh mesh = smr.sharedMesh;

        // Create the GPU buffer for vertices
        vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, mesh.vertexCount, sizeof(float) * 3);

        // Assign the buffer to the material
        smplxMaterial.SetBuffer("_VertexBuffer", vertexBuffer);
        Debug.Log("Initialized GPU buffer for vertex positions.");
    }

    System.Collections.IEnumerator AnimatePose()
    {
        while (true)
        {
            if (isTPose)
            {
                ApplyCustomPose(GenerateRandomPose()); // Apply a random pose
            }
            else
            {
                ApplyTPose(); // Apply predefined T-pose
            }

            // Bake the updated mesh and upload the new vertex data to the GPU
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

        // Ensure the root joint faces the camera
        Transform rootJoint = joints[0]; // Assuming the root joint is the first one
        Vector3 cameraPosition = Camera.main.transform.position;
        Vector3 directionToCamera = (cameraPosition - rootJoint.position).normalized;

        // Calculate the rotation to face the camera
        Quaternion lookRotation = Quaternion.LookRotation(directionToCamera, Vector3.up);
        Vector3 rootEulerAngles = lookRotation.eulerAngles;

        // Apply the rotation to the root joint
        randomPose[0] = rootEulerAngles.x;
        randomPose[1] = rootEulerAngles.y;
        randomPose[2] = rootEulerAngles.z;

        // Generate random rotations for other joints
        for (int i = 1; i < joints.Length; i++)
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

        // Get the updated vertex positions
        Vector3[] vertices = bakedMesh.vertices;

        // Upload the vertex positions to the GPU buffer
        vertexBuffer.SetData(vertices);
        Debug.Log("Uploaded new vertex positions to GPU.");
    }

    void OnDestroy()
    {
        // Clean up GPU resources
        vertexBuffer?.Dispose();
        Debug.Log("Disposed GPU buffer.");
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
}
