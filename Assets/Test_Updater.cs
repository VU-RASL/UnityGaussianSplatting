using UnityEngine;

public class Test_shader_with_buffer : MonoBehaviour
{


    // GPU Buffers
    private ComputeBuffer gaussianToFaceBuffer;
    private ComputeBuffer haha_xyzBuffer;
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer faceBuffer;
    private HahaImporter hahaImporter;
    private PoseController poseController;

    void Start()
    {   
        hahaImporter = FindObjectOfType<HahaImporter>();
        poseController = FindObjectOfType<PoseController>();

        gaussianToFaceBuffer = hahaImporter.GetGaussianToFaceBuffer();
        haha_xyzBuffer = hahaImporter.GetHahaXyzBuffer();
        faceBuffer = hahaImporter.GetFaceBuffer();
        vertexBuffer = poseController.GetVertexBuffer();
        
    }



    void OnRenderObject()
    {
 
    }


    void OnDestroy()
    {
        // Release GPU buffers
        gaussianToFaceBuffer?.Release();
        vertexBuffer?.Release();
        Debug.Log("Released ComputeBuffers.");
    }
}
