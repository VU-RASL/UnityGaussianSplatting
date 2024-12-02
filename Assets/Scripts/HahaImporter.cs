using System;
using System.Data;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

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

        public HahaAvatarData(string path)
        {
            string content = File.ReadAllText(path);

            HahaOutputData data = JsonConvert.DeserializeObject<HahaOutputData>(content);
            betas = getBetas(data._betas);
            // texture = ConvertToTexture(data._trainable_texture);
            vertices = getVertices(data._xyz);
        }

        public float[] getBetas(float[][] _betas)
        {
            int rows = 10;
            float[] betas = new float[rows];
            for(int i = 0; i < rows; i++)
            {   
                betas[i] = _betas[0][i];
            }
            return betas;
        }

        public Vertex[] getVertices(float[][] _xyz)
        {
            int rows = _xyz.GetLength(0);
            Vertex[] verts = new Vertex[rows];
            for(int i = 0; i < rows; i++)
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

        public Texture2D ConvertToTexture(float[][][] data)
        {
            // Get dimensions
            int height = data.Length;        // First dimension: height
            int width = data[0].Length;     // Second dimension: width
            int channels = data[0][0].Length; // Third dimension: RGB channels (should be 3)

            if (channels != 3)
            {
                Debug.LogError("Data must have exactly 3 channels (RGB) per pixel.");
                return null;
            }

            // Create a new texture
            Texture2D texture = new(width, height, TextureFormat.RGB24, false);

            // Set each pixel
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Get RGB values from the array
                    float r = Mathf.Clamp01(data[y][x][0]); // Red
                    float g = Mathf.Clamp01(data[y][x][1]); // Green
                    float b = Mathf.Clamp01(data[y][x][2]); // Blue

                    // Set the pixel color
                    texture.SetPixel(x, y, new Color(r, g, b));
                }
            }

            // Apply changes to the texture
            texture.Apply();

            return texture;
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

    // Start is called before the first frame update
    void Start()
    {
        data = new HahaAvatarData(path);

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

    // Update is called once per frame
    void Update()
    {
        
    }
}
