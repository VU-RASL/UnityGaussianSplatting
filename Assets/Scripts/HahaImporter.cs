using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class HahaImporter : MonoBehaviour
{
    public string path = "Assets/HahaData/state_dict.json";
    public string texSavePath = "Assets/HahaData/extracted_texture.png";

    public HahaAvatarData data;

    [Serializable]
    public class HahaAvatarData
    {
        public float[] betas;
        public Vector3[] positions; // xyz of gaussians
        public Texture2D texture;
        public int[] gaussianToFace; // Mapping from Gaussian to face
        public Face[] facesToVerts;         // Face indices

        public HahaAvatarData(string path, string texSavePath)
        {
            string content = File.ReadAllText(path);

            HahaOutputData data = JsonConvert.DeserializeObject<HahaOutputData>(content);
            betas = getBetas(data._betas);
            gaussianToFace = getGaussianToFace(data._gaussian_to_face);
            facesToVerts = getFaces(data._faces);
            positions = getVertices(data._xyz);
            texture = ConvertToTexture(data._trainable_texture);
            File.WriteAllBytes(texSavePath, texture.EncodeToPNG());
            Debug.Log("Texture save to: " + texSavePath);
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

        public int[] getGaussianToFace(int[] _gaussian_to_face)
        {
            int[] result = new int[_gaussian_to_face.Length];
            for (int i = 0; i < _gaussian_to_face.Length; i++)
            {
                result[i] = _gaussian_to_face[i]; // Convert float to int
            }
            return result;
        }

        public Face[] getFaces(int[][] _faces)
        {
            int rows = _faces.Length;
            Face[] faces = new Face[rows];
            for (int i = 0; i < rows; i++)
            {
                faces[i] = new Face {
                    v1 = _faces[i][0],
                    v2 = _faces[i][1],
                    v3 = _faces[i][2]
                };
            }
            return faces;
        }

        public Vector3[] getVertices(float[][] _xyz)
        {
            int rows = _xyz.GetLength(0);
            Vector3[] verts = new Vector3[rows];
            for (int i = 0; i < rows; i++)
            {
                verts[i] = new Vector3
                (
                    _xyz[i][0],
                    _xyz[i][1],
                    _xyz[i][2]
                );
            }
            return verts;
        }

        public Texture2D ConvertToTexture(float[][][] data)
        {
            // Get dimensions
            int size = data[0].Length;
            int channels = data.Length;

            if (channels != 3)
            {
                Debug.LogError("Data must have exactly 3 channels (RGB) per pixel.");
                return null;
            }
            Debug.Log("Size: " + size.ToString());

            // Create a new texture
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);

            // Set each pixel
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Get RGB values from the array
                    float r = Mathf.Clamp01(data[0][x][y]); // Red
                    float g = Mathf.Clamp01(data[1][x][y]); // Green
                    float b = Mathf.Clamp01(data[2][x][y]); // Blue

                    // Set the pixel color
                    texture.SetPixel(y, x, new Color(r, g, b)); // y,x to rotate
                }
            }

            // Apply changes to the texture
            texture.Apply();

            return texture;
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

        [Serializable]
        public class Face
        {
            public int v1;
            public int v2;
            public int v3;
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
            public int[] _gaussian_to_face;
            public int[][] _faces;
        }
    }

    // GPU Buffers
    private GraphicsBuffer gaussianToFaceBuffer;
    private GraphicsBuffer faceBuffer;

    void Start()
    {
        // Load data from the file
        data = new HahaAvatarData(path, texSavePath);

        // Initialize GPU buffers
        // InitializeBuffers();

        // Set SMPL-X texture
        // SetSMPLXTexture(data.texture);

        // // Find the PoseController in the scene and pass the betas
        // PoseController poseController = FindObjectOfType<PoseController>();
        // if (poseController != null)
        // {
        //     poseController.UpdateBetas(data.betas);
        // }
        // else
        // {
        //     Debug.LogError("PoseController not found in the scene!");
        // }
    }

    void InitializeBuffers()
    {
        // Initialize Gaussian-to-Face Buffer
        gaussianToFaceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.gaussianToFace.Length, sizeof(int));
        gaussianToFaceBuffer.SetData(data.gaussianToFace);
        Debug.Log("Initialized Gaussian-to-Face Buffer.");

        // Initialize Face Buffer
        faceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.facesToVerts.Length, sizeof(int));
        faceBuffer.SetData(data.facesToVerts);
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
