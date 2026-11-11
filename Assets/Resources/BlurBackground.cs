using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class BlurBackground : MonoBehaviour
{
    [Header("Blur Settings")]
    public Material blurMaterial;
    [Range(0, 10)] public float blurStrength = 3f;

    [Header("Color Overlay")]
    public Color overlayColor = new Color(0, 0, 0, 0.5f);

    private RawImage rawImage;
    private RenderTexture renderTexture;

    void Start()
    {
        rawImage = GetComponent<RawImage>();

        // Create RenderTexture
        renderTexture = new RenderTexture(
            Screen.width,
            Screen.height,
            24
        );

        renderTexture.Create();

        // Camera renders into the RenderTexture
        Camera.main.targetTexture = renderTexture;

        // Display RenderTexture
        rawImage.texture = renderTexture;

        // IMPORTANT:
        // Make the RawImage actually use the blur shader
        if (blurMaterial != null)
        {
            rawImage.material = blurMaterial;
            blurMaterial.SetFloat("_BlurSize", blurStrength);
        }

        // Color/tint
        rawImage.color = overlayColor;
    }

    void Update()
    {
        if (blurMaterial != null)
        {
            blurMaterial.SetFloat("_BlurSize", blurStrength);
        }
    }

    void OnDestroy()
    {
        if (Camera.main != null)
        {
            Camera.main.targetTexture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}