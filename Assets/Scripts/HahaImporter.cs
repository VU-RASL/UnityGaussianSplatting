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
            // texture = ConvertToTexture(data._trainable_texture);
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

        // public Texture2D ConvertToTexture(float[][][] data)
        // {
        //     int originalHeight = data.Length;  // Original height of the 512x512 texture
        //     int originalWidth = data[0].Length; // Original width of the 512x512 texture

        //     // Create a Texture2D with the original 512x512 data
        //     Texture2D smallTexture = new Texture2D(originalWidth, originalHeight, TextureFormat.RGBA32, false);

        //     for (int y = 0; y < originalHeight; y++)
        //     {
        //         for (int x = 0; x < originalWidth; x++)
        //         {
        //             float r = Mathf.Clamp01(data[y][x][0]); // Red channel
        //             float g = Mathf.Clamp01(data[y][x][1]); // Green channel
        //             float b = Mathf.Clamp01(data[y][x][2]); // Blue channel
        //             float a = 1.0f; // Alpha (optional)

        //             smallTexture.SetPixel(x, y, new Color(r, g, b, a));
        //         }
        //     }

        //     smallTexture.Apply();

        //     // Resize the 512x512 texture to 4096x4096
        //     Texture2D resizedTexture = new Texture2D(4096, 4096, TextureFormat.RGBA32, false);
        //     Graphics.CopyTexture(smallTexture, 0, 0, 0, 0, smallTexture.width, smallTexture.height, resizedTexture, 0, 0, 0, 0);

        //     // Optionally reorganize UV layout here if necessary (e.g., custom remapping logic)

        //     resizedTexture.Apply();
        //     return resizedTexture;
        // }
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

        // Set SMPL-X texture
        // SetSMPLXTexture(data.texture);

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

    // void SetSMPLXTexture(Texture2D texture)
    // {
    //     SkinnedMeshRenderer smr = FindObjectOfType<SkinnedMeshRenderer>();
    //     if (smr == null)
    //     {
    //         Debug.LogError("SkinnedMeshRenderer not found in the scene!");
    //         return;
    //     }

    //     if (smr.material != null)
    //     {
    //         smr.material.mainTexture = texture;
    //         Debug.Log("SMPL-X texture has been set successfully.");
    //     }
    //     else
    //     {
    //         Debug.LogError("Material not assigned to SkinnedMeshRenderer!");
    //     }
    // }

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
