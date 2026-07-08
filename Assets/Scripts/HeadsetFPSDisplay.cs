using UnityEngine;

[DefaultExecutionOrder(500)]
public sealed class HeadsetFPSDisplay : MonoBehaviour
{
    const int TextureWidth = 256;
    const int TextureHeight = 72;
    const int GlyphWidth = 5;
    const int GlyphHeight = 7;
    const int Scale = 6;

    [SerializeField] float updateInterval = 0.25f;
    [SerializeField] Vector3 cameraLocalPosition = new(-0.32f, 0.18f, 0.9f);
    [SerializeField] Vector2 displaySizeMeters = new(0.24f, 0.0675f);

    Texture2D texture;
    Color32[] pixelBuffer;
    Material material;
    Renderer quadRenderer;
    Camera targetCamera;
    float accumulatedTime;
    int accumulatedFrames;
    float nextUpdateTime;
    string lastText;

    static readonly Color32 Clear = new(0, 0, 0, 0);
    static readonly Color32 Background = new(6, 10, 12, 170);
    static readonly Color32 Good = new(80, 255, 140, 255);
    static readonly Color32 Okay = new(255, 220, 80, 255);
    static readonly Color32 Bad = new(255, 90, 80, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateForQuest()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!GeneralOperator.GetSceneHeadsetFpsEnabled())
            return;

        if (FindObjectOfType<HeadsetFPSDisplay>() != null)
            return;

        GameObject go = new("Headset FPS Display");
        DontDestroyOnLoad(go);
        go.AddComponent<HeadsetFPSDisplay>();
#endif
    }

    void Awake()
    {
        BuildDisplay();
    }

    void OnDestroy()
    {
        if (material != null)
            Destroy(material);
        if (texture != null)
            Destroy(texture);
    }

    void LateUpdate()
    {
        AttachToCamera();
        AccumulateAndRefresh();
    }

    void BuildDisplay()
    {
        texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        pixelBuffer = new Color32[TextureWidth * TextureHeight];

        Shader shader = Shader.Find("GSAC/Headset FPS Overlay");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Standard");

        material = new Material(shader)
        {
            mainTexture = texture,
            renderQueue = 5000,
        };

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "FPS Quad";
        quad.transform.SetParent(transform, false);
        quad.transform.localScale = new Vector3(displaySizeMeters.x, displaySizeMeters.y, 1.0f);

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        quadRenderer = quad.GetComponent<Renderer>();
        quadRenderer.sharedMaterial = material;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;

        DrawText("FPS --", Good);
    }

    void AttachToCamera()
    {
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            targetCamera = Camera.main;

        if (targetCamera == null || transform.parent == targetCamera.transform)
            return;

        transform.SetParent(targetCamera.transform, false);
        transform.localPosition = cameraLocalPosition;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    void AccumulateAndRefresh()
    {
        accumulatedTime += Time.unscaledDeltaTime;
        accumulatedFrames++;

        if (Time.unscaledTime < nextUpdateTime || accumulatedTime <= 0.0f)
            return;

        float fps = accumulatedFrames / accumulatedTime;
        accumulatedTime = 0.0f;
        accumulatedFrames = 0;
        nextUpdateTime = Time.unscaledTime + updateInterval;

        string text = $"FPS {Mathf.RoundToInt(fps),2}";
        if (text == lastText)
            return;

        lastText = text;
        DrawText(text, ColorForFps(fps));
    }

    static Color32 ColorForFps(float fps)
    {
        if (fps >= 68.0f)
            return Good;
        if (fps >= 50.0f)
            return Okay;
        return Bad;
    }

    void DrawText(string text, Color32 textColor)
    {
        Color32[] pixels = pixelBuffer;
        if (pixels == null || pixels.Length != TextureWidth * TextureHeight)
        {
            pixels = new Color32[TextureWidth * TextureHeight];
            pixelBuffer = pixels;
        }

        for (int i = 0; i < pixels.Length; ++i)
            pixels[i] = Clear;

        FillRect(pixels, 0, 0, TextureWidth, TextureHeight, Background);

        int textPixelWidth = MeasureText(text) * Scale;
        int x = Mathf.Max(8, (TextureWidth - textPixelWidth) / 2);
        int y = (TextureHeight - GlyphHeight * Scale) / 2;

        foreach (char character in text.ToUpperInvariant())
        {
            if (character == ' ')
            {
                x += 4 * Scale;
                continue;
            }

            string[] glyph = GetGlyph(character);
            if (glyph != null)
                DrawGlyph(pixels, x, y, glyph, textColor);

            x += (GlyphWidth + 1) * Scale;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    static int MeasureText(string text)
    {
        int width = 0;
        foreach (char character in text)
            width += character == ' ' ? 4 : GlyphWidth + 1;
        return Mathf.Max(0, width - 1);
    }

    static void FillRect(Color32[] pixels, int x, int y, int width, int height, Color32 color)
    {
        for (int py = y; py < y + height; ++py)
        {
            for (int px = x; px < x + width; ++px)
            {
                if (px < 0 || px >= TextureWidth || py < 0 || py >= TextureHeight)
                    continue;
                pixels[py * TextureWidth + px] = color;
            }
        }
    }

    static void DrawGlyph(Color32[] pixels, int originX, int originY, string[] glyph, Color32 color)
    {
        for (int row = 0; row < glyph.Length; ++row)
        {
            for (int column = 0; column < glyph[row].Length; ++column)
            {
                if (glyph[row][column] != '1')
                    continue;

                int pixelX = originX + column * Scale;
                int pixelY = originY + (GlyphHeight - 1 - row) * Scale;
                FillRect(pixels, pixelX, pixelY, Scale, Scale, color);
            }
        }
    }

    static string[] GetGlyph(char character)
    {
        switch (character)
        {
            case 'F':
                return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" };
            case 'P':
                return new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" };
            case 'S':
                return new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" };
            case '0':
                return new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" };
            case '1':
                return new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" };
            case '2':
                return new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" };
            case '3':
                return new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" };
            case '4':
                return new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" };
            case '5':
                return new[] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" };
            case '6':
                return new[] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" };
            case '7':
                return new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" };
            case '8':
                return new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" };
            case '9':
                return new[] { "01110", "10001", "10001", "01111", "00001", "00001", "01110" };
            case '-':
                return new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" };
            default:
                return null;
        }
    }
}
