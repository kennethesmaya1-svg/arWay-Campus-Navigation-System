using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class SplashReminderController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private RectTransform cardTransform;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Toggle dontShowAgainToggle;

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private TMP_Text loadingPercentText;

    [Header("Splash Settings")]
    [SerializeField] private float splashDelay = 0.9f;

    [Header("Animation Settings")]
    [SerializeField] private float animDuration = 0.35f;
    [SerializeField] private AnimationCurve popInCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Scene Transition")]
    [SerializeField] private string mainSceneName = "MainScene";

    [SerializeField] private string playerPrefsKey =
        "HideSplashReminder";

    private bool isTransitioning = false;

    private void Start()
    {
        // Splash background visible
        popupCanvasGroup.alpha = 1f;

        // Reminder hidden
        cardTransform.localScale = Vector3.zero;

        // Loading UI hidden
        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        // Button enabled
        acceptButton.interactable = true;

        acceptButton.onClick.AddListener(OnAcceptPressed);

        // Start splash
        StartCoroutine(StartSplashSequence());
    }

    private IEnumerator StartSplashSequence()
    {
        // Show splash
        popupCanvasGroup.alpha = 1f;

        // Hide reminder
        cardTransform.localScale = Vector3.zero;

        // Wait for splash
        yield return new WaitForSeconds(splashDelay);

        // Check preference
        bool hideReminder =
            PlayerPrefs.GetInt(playerPrefsKey, 0) == 1;

        if (hideReminder)
        {
            // User previously selected
            // "Don't Show Again".

            // No reminder needed.
            // Proceed directly to loading.
            StartCoroutine(AnimatePopOutAndLoad());
        }
        else
        {
            // Show reminder
            StartCoroutine(AnimatePopIn());
        }
    }

    private IEnumerator AnimatePopIn()
    {
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;

            float percent =
                Mathf.Clamp01(elapsed / animDuration);

            float scaleValue =
                popInCurve.Evaluate(percent);

            cardTransform.localScale =
                Vector3.one * scaleValue;

            yield return null;
        }

        cardTransform.localScale = Vector3.one;
    }

    private void OnAcceptPressed()
    {
        if (isTransitioning)
            return;

        isTransitioning = true;

        // Save "Don't Show Again"
        if (dontShowAgainToggle != null &&
            dontShowAgainToggle.isOn)
        {
            PlayerPrefs.SetInt(playerPrefsKey, 1);
            PlayerPrefs.Save();
        }

        // Hide reminder first
        StartCoroutine(AnimatePopOutAndLoad());
    }

    private IEnumerator AnimatePopOutAndLoad()
    {
        acceptButton.interactable = false;

        float elapsed = 0f;

        Vector3 initialScale =
            cardTransform.localScale;

        // Pop reminder out
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;

            float percent =
                Mathf.Clamp01(elapsed / animDuration);

            cardTransform.localScale =
                Vector3.Lerp(
                    initialScale,
                    Vector3.zero,
                    percent
                );

            yield return null;
        }

        cardTransform.localScale = Vector3.zero;

        // NOW start loading MainScene
        StartCoroutine(LoadMainSceneAsync());
    }

    private IEnumerator LoadMainSceneAsync()
    {
        // Show loading panel
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        // Reset progress
        if (loadingSlider != null)
        {
            loadingSlider.minValue = 0f;
            loadingSlider.maxValue = 1f;
            loadingSlider.value = 0f;
        }

        if (loadingPercentText != null)
            loadingPercentText.text = "0%";

        // Begin loading scene
        AsyncOperation operation =
            SceneManager.LoadSceneAsync(mainSceneName);

        // Do not open scene yet
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            // Unity loads up to 90% internally
            float progress =
                Mathf.Clamp01(operation.progress / 0.9f);

            // Update progress bar
            if (loadingSlider != null)
                loadingSlider.value = progress;

            // Update percentage
            if (loadingPercentText != null)
            {
                int percentage =
                    Mathf.RoundToInt(progress * 100f);

                loadingPercentText.text =
                    percentage + "%";
            }

            // Scene is ready
            if (operation.progress >= 0.9f)
            {
                // Show 100%
                if (loadingSlider != null)
                    loadingSlider.value = 1f;

                if (loadingPercentText != null)
                    loadingPercentText.text = "100%";

                // Small delay so 100% is visible
                yield return new WaitForSeconds(0.2f);

                // NOW open MainScene
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    [ContextMenu("Reset 'Don't Show Again' Preference")]
    public void ResetPreference()
    {
        PlayerPrefs.DeleteKey(playerPrefsKey);
        PlayerPrefs.Save();

        Debug.Log(
            "<color=green>" +
            "Splash reminder preference reset successfully!" +
            "</color>"
        );
    }
}