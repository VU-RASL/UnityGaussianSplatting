using UnityEngine;
using System.Collections.Generic;
using System.IO;
public class PassMeshTextureToSplats : MonoBehaviour
{
    [SerializeField]public Renderer meshRenderer; // Assign your mesh's renderer in the Inspector
    [SerializeField]public Material splatsMaterial; // Assign your splats shader material in the Inspector

    void Start()
    {
        // Check if the mesh has a material
        if (meshRenderer != null && meshRenderer.material != null)
        {
            // Fetch the main texture from the mesh material
            Texture meshTexture = meshRenderer.material.GetTexture("_MainTex");

            // Assign the fetched texture to the splats material
            if (splatsMaterial != null && meshTexture != null)
            {
                splatsMaterial.SetTexture("_UVTex", meshTexture);
                Debug.Log("Texture passed to splats shader.");
            }
            else
            {
                Debug.LogWarning("Splats material or texture is missing.");
            }
        }
        else
        {
            Debug.LogWarning("Mesh renderer or material is missing.");
        }
    }
}
