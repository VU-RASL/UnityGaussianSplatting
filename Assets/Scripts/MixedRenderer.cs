using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

[ExecuteInEditMode] // Allows updates in Edit mode for visualization
public class MixedRenderer : MonoBehaviour
{
    public RenderTexture meshRT;
    public RenderTexture gsRT;
    public RenderTexture finalRT;
    [Range(0, 1)] public float alpha = 0.5f; // Blending weight

    public Shader shader;
    private Material blendMaterial;

    void OnEnable()
    {
        if (shader == null)
        {
            Debug.LogError("Shader 'Custom/FinalShader' not found. Please ensure the shader file is correctly placed and compiled.");
            return;
        }
        blendMaterial = new Material(shader);
    }

    void Update()
    {
         if (meshRT == null || gsRT == null || finalRT == null || blendMaterial == null)
        {
            Debug.LogWarning("RenderTextures or Material are not set.");
            return;
        }

        // Set shader parameters
        blendMaterial.SetTexture("_MainTex1", meshRT);
        blendMaterial.SetTexture("_MainTex2", gsRT);
        blendMaterial.SetFloat("_Alpha", alpha);

        // Perform the blending operation
        Graphics.Blit(null, finalRT, blendMaterial);


    }
}
