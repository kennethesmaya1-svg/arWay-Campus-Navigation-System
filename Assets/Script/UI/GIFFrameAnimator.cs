using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a sequence of PNG sprites as a frame-by-frame animation.
/// Designed for Unity UI Image components.
/// </summary>
public class GIFFrameAnimator : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Image targetImage;

    [SerializeField] private Sprite[] frames;

    [SerializeField] private float framesPerSecond = 8f;

    [SerializeField] private bool loop = true;

    private int currentFrame;
    private float timer;

    private void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }

    private void Start()
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning(
                "[GIF Animator] No frames assigned."
            );

            return;
        }

        currentFrame = 0;
        timer = 0f;

        targetImage.sprite = frames[0];
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0)
            return;

        if (targetImage == null)
            return;

        timer += Time.deltaTime;

        float frameDuration =
            1f / Mathf.Max(framesPerSecond, 0.01f);

        if (timer >= frameDuration)
        {
            timer -= frameDuration;

            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                }
                else
                {
                    currentFrame = frames.Length - 1;
                }
            }

            targetImage.sprite = frames[currentFrame];
        }
    }

    public void Play()
    {
        enabled = true;
    }

    public void Stop()
    {
        enabled = false;
    }

    public void ResetAnimation()
    {
        currentFrame = 0;
        timer = 0f;

        if (targetImage != null &&
            frames != null &&
            frames.Length > 0)
        {
            targetImage.sprite = frames[0];
        }
    }
}