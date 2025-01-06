using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRenderer : MonoBehaviour
{
    public RenderTexture replacement;

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        // To overwrite the entire screen
        // Graphics.Blit(replacement, null);

        // Or to overwrite only what this specific Camera renders
        Graphics.Blit(replacement, dest);
    }
}
