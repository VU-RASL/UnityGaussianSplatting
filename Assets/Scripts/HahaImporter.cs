using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class HahaImporter : MonoBehaviour
{
    public string path = "Assets/HahaData/state_dict.json";

    public HahaAvatarData data;

    [Serializable]
    public class HahaAvatarData
    {
        public float[] betas;
        public Vertex[] vertices;
        public Texture2D texture;
        public int[] gaussianToFace; // Mapping from Gaussian to face
        public int[] faces;         // Face indices (flattened)

        public HahaAvatarData(string path)
        {
            string content = File.ReadAllText(path);

            HahaOutputData data = JsonConvert.DeserializeObject<HahaOutputData>(content);
            betas = getBetas(data._betas);
            gaussianToFace = getGaussianToFace(data._gaussian_to_face);
            faces = getFaces(data._faces);
            vertices = getVertices(data._xyz);
        }

        public float[] getBetas(float[][] _betas)
        {
            int rows = 10;
            float[] betas = new float[rows];
            for (int i = 0; i < rows; i++)
            {
                betas[i] = _betas[0][i];
            }
            return betas;
        }

        public int[] getGaussianToFace(float[] _gaussian_to_face)
        {
            int[] result = new int[_gaussian_to_face.Length];
            for (int i = 0; i < _gaussian_to_face.Length; i++)
            {
                result[i] = Mathf.FloorToInt(_gaussian_to_face[i]); // Convert float to int
            }
            return result;
        }

        public int[] getFaces(float[][] _faces)
        {
            int rows = _faces.Length;
            int[] flattenedFaces = new int[rows * 3];
            for (int i = 0; i < rows; i++)
            {
                flattenedFaces[i * 3 + 0] = Mathf.FloorToInt(_faces[i][0]);
                flattenedFaces[i * 3 + 1] = Mathf.FloorToInt(_faces[i][1]);
                flattenedFaces[i * 3 + 2] = Mathf.FloorToInt(_faces[i][2]);
            }
            return flattenedFaces;
        }

        public Vertex[] getVertices(float[][] _xyz)
        {
            int rows = _xyz.GetLength(0);
            Vertex[] verts = new Vertex[rows];
            for (int i = 0; i < rows; i++)
            {
                verts[i] = new Vertex
                {
                    x = _xyz[i][0],
                    y = _xyz[i][1],
                    z = _xyz[i][2]
                };
            }
            return verts;
        }

        [Serializable]
        public class Vertex
        {
            public float x;
            public float y;
            public float z;
        }

        public class HahaOutputData
        {
            public float[][] _betas;
            public float[][][] _trainable_texture;
            public float[][] _xyz;
            public float[][] _color;
            public float[][] _rotation;
            public float[][] _scaling;
            public float[][] _opacity;
            public float[] _gaussian_to_face;
            public float[][] _faces;
        }
    }

    // GPU Buffers
    private GraphicsBuffer gaussianToFaceBuffer;
    private GraphicsBuffer faceBuffer;

    void Start()
    {
        // Load data from the file
        data = new HahaAvatarData(path);

        // Initialize GPU buffers
        InitializeBuffers();

        // Find the PoseController in the scene and pass the betas
        PoseController poseController = FindObjectOfType<PoseController>();
        if (poseController != null)
        {
            poseController.UpdateBetas(data.betas);
        }
        else
        {
            Debug.LogError("PoseController not found in the scene!");
        }
    }

    void InitializeBuffers()
    {
        // Initialize Gaussian-to-Face Buffer
        gaussianToFaceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.gaussianToFace.Length, sizeof(int));
        gaussianToFaceBuffer.SetData(data.gaussianToFace);
        Debug.Log("Initialized Gaussian-to-Face Buffer.");

        // Initialize Face Buffer
        faceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.faces.Length, sizeof(int));
        faceBuffer.SetData(data.faces);
        Debug.Log("Initialized Face Buffer.");
    }

    public GraphicsBuffer GetGaussianToFaceBuffer()
    {
        return gaussianToFaceBuffer;
    }

    public GraphicsBuffer GetFaceBuffer()
    {
        return faceBuffer;
    }

    void OnDestroy()
    {
        // Release GPU buffers
        gaussianToFaceBuffer?.Dispose();
        faceBuffer?.Dispose();
        Debug.Log("Disposed GPU Buffers.");
    }
}
