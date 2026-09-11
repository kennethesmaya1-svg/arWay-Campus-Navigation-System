using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InstructionPanel : MonoBehaviour
{
    [Header("Slides")]
    public RectTransform slidesHolder;
    public float slideWidth = 978f;

    [Header("Dots")]
    public Image[] dots;
    public float activeDotWidth = 60f;
    public float inactiveDotWidth = 16f;
    public Color activeDotColor;
    public Color inactiveDotColor;

    [Header("Buttons")]
    public Button btnNext;
    public Button btnBack;
    public TextMeshProUGUI txtNext;

    private int currentSlide = 0;
    private int totalSlides = 3;
    private bool isAnimating = false;

    void Start()
    {
        if (slidesHolder == null)
        { Debug.LogError("MISSING: Slides Holder not assigned!"); return; }

        if (btnNext == null)
        { Debug.LogError("MISSING: BTN_Next not assigned!"); return; }

        if (txtNext == null)
        { Debug.LogError("MISSING: TXT_Next not assigned!"); return; }

        if (dots == null || dots.Length == 0)
        { Debug.LogError("MISSING: Dots not assigned!"); return; }

        btnNext.onClick.AddListener(OnNextClicked);
        btnBack.onClick.AddListener(OnBackClicked);
        GoToSlide(0, false);
    }

    void OnNextClicked()
    {
        if (isAnimating) return;

        if (currentSlide < totalSlides - 1)
        {
            GoToSlide(currentSlide + 1, true);
        }
        else
        {
            LaunchApp();
        }
    }

    void OnBackClicked()
    {
        if (isAnimating) return;

        if (currentSlide > 0)
        {
            GoToSlide(currentSlide - 1, true);
        }
    }

    void GoToSlide(int index, bool animate)
    {
        index = Mathf.Clamp(index, 0, totalSlides - 1);

        currentSlide = index;
        float targetX = -index * slideWidth;

        if (animate)
        {
            isAnimating = true;
            StartCoroutine(AnimateSlide(targetX));
        }
        else
        {
            slidesHolder.anchoredPosition =
                new Vector2(targetX, slidesHolder.anchoredPosition.y);
        }

        UpdateDots(index);
        UpdateButton(index);
    }

    System.Collections.IEnumerator AnimateSlide(float targetX)
    {
        float startX = slidesHolder.anchoredPosition.x;
        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);
            float newX = Mathf.Lerp(startX, targetX, t);
            slidesHolder.anchoredPosition =
                new Vector2(newX, slidesHolder.anchoredPosition.y);
            yield return null;
        }

        slidesHolder.anchoredPosition =
            new Vector2(targetX, slidesHolder.anchoredPosition.y);
        isAnimating = false;
    }

    void UpdateDots(int activeIndex)
    {
        if (dots == null || dots.Length == 0)
    {
        Debug.LogWarning("InstructionPanel: Dots array is empty.");
        return;
    }

    for (int i = 0; i < dots.Length; i++)
    {
        if (dots[i] == null)
        {
            Debug.LogError($"InstructionPanel: Dots element {i} is not assigned in the Inspector.");
            continue;
        }

        bool isActive = (i == activeIndex);

        dots[i].color = isActive ? activeDotColor : inactiveDotColor;

        RectTransform rt = dots[i].GetComponent<RectTransform>();

        if (rt == null)
        {
            Debug.LogError($"InstructionPanel: Dot {i} does not have a RectTransform.");
            continue;
        }

        rt.sizeDelta = new Vector2(
            isActive ? activeDotWidth : inactiveDotWidth,
            rt.sizeDelta.y
        );
    }
    }

    void UpdateButton(int index)
    {
        bool isLast = (index == totalSlides - 1);
        txtNext.text = isLast ? "Let's Go!" : "Next";
    }

    void LaunchApp()
    {
        gameObject.SetActive(false);
    }
}