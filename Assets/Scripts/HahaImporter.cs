using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Unity.Mathematics;


// namespace GSAvatar.Runtime
// {
[ExecuteInEditMode]
public class HahaImporter : MonoBehaviour
{
    public string path = "Assets/HahaData/state_dict.json";
    public string texSavePath = "Assets/HahaData/extracted_texture.png";
    public string gaussianToFaceSavePath = "Assets/HahaData/GaussianToFace.txt";
    public string colorsSavePath = "Assets/HahaData/Colors.txt";

    public HahaAvatarData data;

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
        // ExportGaussianToFace();
        // ExportColors();
    }

    void ExportGaussianToFace()
    {
        if (data.gaussianToFace == null || data.gaussianToFace.Length == 0)
        {
            Debug.LogError("GaussianToFace data is missing or empty.");
            return;
        }

        try
        {
            using (StreamWriter writer = new StreamWriter(gaussianToFaceSavePath))
            {
                foreach (int faceIndex in data.gaussianToFace)
                {
                    writer.WriteLine(faceIndex);
                }
            }
            Debug.Log($"GaussianToFace mapping exported to: {gaussianToFaceSavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to export GaussianToFace mapping: {e.Message}");
        }
    }
    void ExportColors()
    {
        if (data.colors == null || data.colors.Length == 0)
        {
            Debug.LogError("Color data is missing or empty.");
            return;
        }

        try
        {
            using (StreamWriter writer = new StreamWriter(colorsSavePath))
            {
                foreach (float3 color in data.colors)
                {
                    writer.WriteLine($"{color.x},{color.y},{color.z}");
                }
            }
            Debug.Log($"Colors exported to: {colorsSavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to export colors: {e.Message}");
        }
    }
    void InitializeBuffers()
    {
        if (data.gaussianToFace != null && data.gaussianToFace.Length > 0)
        {
            gaussianToFaceBuffer = new ComputeBuffer(data.gaussianToFace.Length, sizeof(int));
            gaussianToFaceBuffer.SetData(data.gaussianToFace);
            // Debug.Log("Initialized GaussianToFaceBuffer.");
        }

        if (data.offsets != null && data.offsets.Length > 0)
        {
            haha_xyzBuffer = new ComputeBuffer(data.offsets.Length, sizeof(float) * 3);
            haha_xyzBuffer.SetData(data.offsets);
            // Debug.Log("Initialized HahaXyzBuffer.");
        }

        if (data.facesToVerts != null && data.facesToVerts.Length > 0)
        {
            faceBuffer = new ComputeBuffer(data.facesToVerts.Length, sizeof(int) * 3);
            faceBuffer.SetData(data.facesToVerts);
            // Debug.Log("Initialized FaceBuffer.");
        }

        //  scaling
        haha_scalingBuffer = new ComputeBuffer(data.offsets.Length, sizeof(float) * 3);
        haha_scalingBuffer.SetData(data.scaling);
        // rotation
        haha_rotationBuffer = new ComputeBuffer(data.offsets.Length, sizeof(float) * 4);
        haha_rotationBuffer.SetData(data.rotations);
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
    public float3[] GetHAHAScaling()
    {
        return data.scaling;
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
        // Debug.Log("Haha Importer Disposed GPU Buffers.");
    }
}

[Serializable]
public class HahaAvatarData
{
    public int splatCount;
    public int faceCount;         
    public float[] betas; // SMPLX betas
    public float3[] offsets; 
    public float3[] colors; // rgb 
    public float4[] rotations; // quaternion
    public float3[] scaling; 
    public float[] opacities; 
    public int[] gaussianToFace; // Mapping from Gaussian to face
    public int3[] facesToVerts; // Face indices (N x 3 array)
    public Texture2D texture;

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

    public HahaAvatarData(string path)
    {
        string content = File.ReadAllText(path);

        HahaOutputData data = JsonConvert.DeserializeObject<HahaOutputData>(content);
        // data = SwitchHandedness(data);
        splatCount = data._xyz.Length;
        faceCount = data._faces.Length;
        betas = processF1Data(data._betas[0]);
        offsets = processF3Data(data._xyz);
        colors = processF3Data(data._color);
        rotations = processF4Data(data._rotation);
        scaling = processF3Data(data._scaling);
        opacities = processF1Data(ConvertTo1D(data._opacity));
        gaussianToFace = processInt1Data(data._gaussian_to_face);
        facesToVerts = processInt3Data(data._faces);
        texture = ConvertToTexture(data._trainable_texture);
    }

    public HahaAvatarData(string path, string texSavePath): this(path)
    {
        File.WriteAllBytes(texSavePath, texture.EncodeToPNG());
    }

    // convert from SMPLX right-handed coordinates to unity left-handed coordinates
    HahaOutputData SwitchHandedness(HahaOutputData data)
    {
        int rowCount = data._xyz.Length;
        for(int i = 0; i < rowCount; i++)
        {
            data._xyz[i][0] *= -1;
            data._rotation[i][0] *= -1;
            data._rotation[i][3] *= -1;
        }
        return data;
    }

    float[] ConvertTo1D(float[][] input)
    {
        int rowCount = input.Length;
        float[] result = new float[rowCount];

        for (int i = 0; i < rowCount; i++)
        {
            result[i] = input[i][0]; // Extract the single element in the inner array
        }

        return result;
    }
    

    public int[] processInt1Data(int[] inputArray)
    {
        return (int[])inputArray.Clone(); 
    }

    public float[] processF1Data(float[] inputArray)
    {
        return (float[])inputArray.Clone(); 
    }

    public int3[] processInt3Data(int[][] inputNestedArray)
    {
        int rowCount = inputNestedArray.Length;
        int3[] outputArray = new int3[rowCount];
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            outputArray[rowIndex] = new int3(
                inputNestedArray[rowIndex][0], 
                inputNestedArray[rowIndex][1], 
                inputNestedArray[rowIndex][2]);
        }
        return outputArray;
    }

    public float3[] processF3Data(float[][] inputNestedMatrix)
    {
        int rowCount = inputNestedMatrix.Length;
        float3[] outputArray = new float3[rowCount];
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            outputArray[rowIndex] = new float3(
                inputNestedMatrix[rowIndex][0], 
                inputNestedMatrix[rowIndex][1], 
                inputNestedMatrix[rowIndex][2]);
        }
        return outputArray;
    }

    public float4[] processF4Data(float[][] inputNestedMatrix)
    {
        int rowCount = inputNestedMatrix.Length;
        float4[] outputArray = new float4[rowCount];
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            outputArray[rowIndex] = new float4(
                inputNestedMatrix[rowIndex][0], 
                inputNestedMatrix[rowIndex][1], 
                inputNestedMatrix[rowIndex][2],
                inputNestedMatrix[rowIndex][3]);
        }
        return outputArray;
    }

    public Texture2D ConvertToTexture(float[][][] data)
    {
        // Get dimensions
        int size = data[0].Length;
        int channels = data.Length;

        if (channels != 3)
        {
            // Debug.LogError("Data must have exactly 3 channels (RGB) per pixel.");
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

    

    
}


// }

