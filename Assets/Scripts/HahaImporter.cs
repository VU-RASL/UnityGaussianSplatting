using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

public class HahaImporter : MonoBehaviour
{
    public string path = "Assets/HahaData/state_dict.json";
    public string texSavePath = "Assets/HahaData/extracted_texture.png";
    [SerializeField] public SMPLX smplx; // Reference to the SMPL-X model

    public HahaAvatarData data;

    [Serializable]
    public class HahaAvatarData
    {
        public float[] betas;
        public Vector3[] positions; // xyz of gaussians
        public Vector3[] scaling; // scales of gaussians
        public Vector4[] rotation;  // original rotation of gaussians
        public float[] opacity;
        public Texture2D texture;
        public int[] gaussianToFace; // Mapping from Gaussian to face
        public Face[] facesToVerts; // Face indices (N x 3 array)

        public HahaAvatarData(string path, string texSavePath)
        {
            string content = File.ReadAllText(path);

            HahaOutputData data = JsonConvert.DeserializeObject<HahaOutputData>(content);
            betas = getBetas(data._betas);
            gaussianToFace = getGaussianToFace(data._gaussian_to_face);
            facesToVerts = getFaces(data._faces);
            positions = getVertices(data._xyz);
            scaling = getVertices(data._scaling);
            rotation = getRotation(data._rotation);
            opacity = getOpacity(data._opacity);
            texture = ConvertToTexture(data._trainable_texture);
            File.WriteAllBytes(texSavePath, texture.EncodeToPNG());
            Debug.Log("Texture saved to: " + texSavePath);
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
        public float[] getOpacity(float[][] _opacity)
        {
            int rows = _opacity.Length;
            float[] opacity = new float[rows];
            for (int i = 0; i < rows; i++)
            {
                opacity[i] = _opacity[i][0];
            }
            return opacity;
        }

        public int[] getGaussianToFace(int[] _gaussian_to_face)
        {
            return (int[])_gaussian_to_face.Clone();
        }

        public Face[] getFaces(int[][] _faces)
        {
            int rows = _faces.Length;
            Face[] faces = new Face[rows];
            for (int i = 0; i < rows; i++)
            {
                faces[i] = new Face(_faces[i][0], _faces[i][1], _faces[i][2]);
            }
            return faces;
        }

        public Vector3[] getVertices(float[][] _xyz)
        {
            int rows = _xyz.Length;
            Vector3[] verts = new Vector3[rows];
            for (int i = 0; i < rows; i++)
            {
                verts[i] = new Vector3(_xyz[i][0], _xyz[i][1], _xyz[i][2]);
            }
            return verts;
        }

        public Vector4[] getRotation(float[][] _rotation)
        {
            int rows = _rotation.Length;
            Vector4[] verts = new Vector4[rows];
            for (int i = 0; i < rows; i++)
            {
                verts[i] = new Vector4(_rotation[i][0], _rotation[i][1], _rotation[i][2], _rotation[i][3]);
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
                    texture.SetPixel(y, x, new Color(r, g, b)); // Note: y,x to rotate
                }
            }

            // Apply changes to the texture
            texture.Apply();
            return texture;
        }

        [Serializable]
        public struct Face
        {
            public int v1; // First vertex index
            public int v2; // Second vertex index
            public int v3; // Third vertex index

            public Face(int vertex1, int vertex2, int vertex3)
            {
                v1 = vertex1;
                v2 = vertex2;
                v3 = vertex3;
            }
        }

        public class HahaOutputData
        {
            public float[][] _betas;
            public float[][][] _trainable_texture;
            public float[][] _xyz;
            public float[][] _scaling;
            public int[] _gaussian_to_face;
            public int[][] _faces;
            public float[][] _rotation;
            public float[][] _opacity;
        }
    }

    // GPU Buffers
    public ComputeBuffer gaussianToFaceBuffer;
    public ComputeBuffer haha_xyzBuffer;
    public ComputeBuffer haha_scalingBuffer;
    public ComputeBuffer faceBuffer;
    public ComputeBuffer haha_rotationBuffer;
    void Start()
    {
        // Load data from the file
        data = new HahaAvatarData(path, texSavePath);
        
        // Initialize GPU buffers
        InitializeBuffers();
    }

    void InitializeBuffers()
    {
        if (data.gaussianToFace != null && data.gaussianToFace.Length > 0)
        {
            gaussianToFaceBuffer = new ComputeBuffer(data.gaussianToFace.Length, sizeof(int));
            gaussianToFaceBuffer.SetData(data.gaussianToFace);
            Debug.Log("Initialized GaussianToFaceBuffer.");
        }

        if (data.positions != null && data.positions.Length > 0)
        {
            haha_xyzBuffer = new ComputeBuffer(data.positions.Length, sizeof(float) * 3);
            haha_xyzBuffer.SetData(data.positions);
            Debug.Log("Initialized HahaXyzBuffer.");
        }

        if (data.facesToVerts != null && data.facesToVerts.Length > 0)
        {
            faceBuffer = new ComputeBuffer(data.facesToVerts.Length, sizeof(int) * 3);
            faceBuffer.SetData(data.facesToVerts);
            Debug.Log("Initialized FaceBuffer.");
        }

        //  scaling
        haha_scalingBuffer = new ComputeBuffer(data.positions.Length, sizeof(float) * 3);
        haha_scalingBuffer.SetData(data.scaling);
        // rotation
        haha_rotationBuffer = new ComputeBuffer(data.positions.Length, sizeof(float) * 4);
        haha_rotationBuffer.SetData(data.rotation);
    }

    public ComputeBuffer GetHahaXyzBuffer()
    {
        return haha_xyzBuffer;
    }

    public ComputeBuffer GetGaussianToFaceBuffer()
    {
        return gaussianToFaceBuffer;
    }

    public ComputeBuffer GetFaceBuffer()
    {
        return faceBuffer;
    }
    public ComputeBuffer GetHahaScalingBuffer()
    {
        return haha_scalingBuffer;
    }

    public ComputeBuffer GetHahaRotationBuffer()
    {
        return haha_rotationBuffer;
    }


    void OnDestroy()
    {
        // Release GPU buffers
        gaussianToFaceBuffer?.Dispose();
        haha_scalingBuffer?.Dispose();
        haha_xyzBuffer?.Dispose();
        haha_rotationBuffer?.Dispose();
        faceBuffer?.Dispose();
        Debug.Log("Haha Importer Disposed GPU Buffers.");
    }
}
