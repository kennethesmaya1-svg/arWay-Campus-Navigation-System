using UnityEngine;
using TMPro;
using System;
using System.Collections;
using UnityEngine.UI;

public class ARWarmupManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject homePanel;
    public TextMeshProUGUI statusText;
    public Slider progressBar;
    public Button retryButton;

    [Header("References")]
    [SerializeField] private NodeManager nodeManager;

    // ---------------------------------------------------------
    // EVENTS
    // ---------------------------------------------------------

    /// <summary>
    /// Fired once GPS + WPS + localization stability are ready.
    /// </summary>
    public event Action OnLocalizationReady;

    /// <summary>
    /// True when localization has successfully completed.
    /// </summary>
    public bool IsReady { get; private set; }

    // ---------------------------------------------------------
    // INTERNAL
    // ---------------------------------------------------------

    private Coroutine _warmupCoroutine;

    // ---------------------------------------------------------
    // UNITY
    // ---------------------------------------------------------

    private void Awake()
    {
        if (nodeManager == null)
            nodeManager = FindFirstObjectByType<NodeManager>();

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(false);
            retryButton.onClick.AddListener(OnRetry);
        }
    }

    // ---------------------------------------------------------
    // START WARMUP
    // ---------------------------------------------------------

    public void StartLoading()
    {
        Debug.Log("ARWarmupManager: Starting localization warmup.");

        IsReady = false;

        gameObject.SetActive(true);

        if (retryButton != null)
            retryButton.gameObject.SetActive(false);

        if (progressBar != null)
            progressBar.value = 0f;

        if (_warmupCoroutine != null)
            StopCoroutine(_warmupCoroutine);

        _warmupCoroutine = StartCoroutine(WarmupRoutine());
    }

    // ---------------------------------------------------------
    // WARMUP
    // ---------------------------------------------------------

    private IEnumerator WarmupRoutine()
    {
#if UNITY_EDITOR

        // =====================================================
        // EDITOR MOCK
        // =====================================================

        float editorTimer = 0f;
        float editorTarget =
            nodeManager != null
                ? nodeManager.StableSeconds
                : 5f;

        while (editorTimer < editorTarget)
        {
            editorTimer += Time.deltaTime;

            if (statusText != null)
            {
                statusText.text =
                    $"[Editor] Simulating localization...\n" +
                    $"{editorTimer:F1}/{editorTarget:F1}s";
            }

            if (progressBar != null)
                progressBar.value =
                    Mathf.Clamp01(editorTimer / editorTarget);

            yield return null;
        }

        if (statusText != null)
            statusText.text = "Localization ready!";

        if (progressBar != null)
            progressBar.value = 1f;

        yield return new WaitForSeconds(0.5f);

        FinishWarmup();

#else

        // =====================================================
        // DEVICE MODE
        // =====================================================

        if (nodeManager == null)
        {
            ShowError("NodeManager was not found.");
            yield break;
        }

        // -----------------------------------------------------
        // PHASE 1: GPS
        // -----------------------------------------------------

        if (statusText != null)
            statusText.text = "Acquiring GPS signal...";

        if (progressBar != null)
            progressBar.value = 0f;

        float gpsTimeout = 15f;
        float gpsWait = 0f;

        while (
            Input.location.status != LocationServiceStatus.Running &&
            gpsWait < gpsTimeout)
        {
            gpsWait += Time.deltaTime;

            if (statusText != null)
            {
                statusText.text =
                    $"Acquiring GPS signal...\n" +
                    $"{gpsTimeout - gpsWait:F0}s remaining";
            }

            if (progressBar != null)
            {
                progressBar.value =
                    Mathf.Lerp(
                        0f,
                        0.2f,
                        gpsWait / gpsTimeout);
            }

            yield return null;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            ShowError(
                "GPS unavailable.\n" +
                "Move outdoors and try again.");

            yield break;
        }

        // -----------------------------------------------------
        // PHASE 2: WPS
        // -----------------------------------------------------

        if (statusText != null)
            statusText.text =
                "Connecting to AR positioning...";

        float wpsTimeout = 30f;
        float wpsWait = 0f;

        while (
            !nodeManager.IsWpsStable &&
            wpsWait < wpsTimeout)
        {
            wpsWait += Time.deltaTime;

            float accuracy =
                nodeManager.GpsAccuracy;

            string accuracyText =
                accuracy >= 0f
                    ? $"{accuracy:F1}m"
                    : "--";

            if (statusText != null)
            {
                statusText.text =
                    $"Connecting to AR positioning...\n" +
                    $"GPS accuracy: {accuracyText}";
            }

            if (progressBar != null)
            {
                progressBar.value =
                    0.2f +
                    (wpsWait / wpsTimeout * 0.3f);
            }

            yield return null;
        }

        if (!nodeManager.IsWpsStable)
        {
            ShowError(
                "AR positioning failed.\n" +
                "Move to an open area and try again.");

            yield break;
        }

        // -----------------------------------------------------
        // PHASE 3: GPS + WPS STABILITY
        // -----------------------------------------------------

        float stableTarget =
            nodeManager.StableSeconds;

        float threshold =
            nodeManager.GpsAccuracyThreshold;

        while (true)
        {
            bool ready =
                nodeManager.IsLocalizationReady();

            float accuracy =
                nodeManager.GpsAccuracy;

            float stableDuration =
                nodeManager.StableDuration;

            if (!ready)
            {
                if (statusText != null)
                {
                    if (!nodeManager.IsWpsStable)
                    {
                        statusText.text =
                            "AR positioning lost.\n" +
                            "Reconnecting...";
                    }
                    else
                    {
                        string accuracyText =
                            accuracy >= 0f
                                ? $"{accuracy:F1}m"
                                : "--";

                        statusText.text =
                            $"Improving GPS accuracy...\n" +
                            $"Accuracy: {accuracyText}\n" +
                            $"Required: < {threshold:F0}m";
                    }
                }

                if (progressBar != null)
                    progressBar.value = 0.5f;
            }
            else
            {
                if (statusText != null)
                {
                    statusText.text =
                        $"Stabilizing AR...\n" +
                        $"{stableDuration:F1}/{stableTarget:F1}s\n" +
                        $"GPS accuracy: {accuracy:F1}m";
                }

                if (progressBar != null)
                {
                    float stabilityProgress =
                        Mathf.Clamp01(
                            stableDuration /
                            stableTarget);

                    progressBar.value =
                        0.5f +
                        stabilityProgress * 0.5f;
                }

                if (stableDuration >= stableTarget)
                {
                    if (statusText != null)
                        statusText.text =
                            "Localization ready!";

                    if (progressBar != null)
                        progressBar.value = 1f;

                    yield return new WaitForSeconds(0.5f);

                    FinishWarmup();

                    yield break;
                }
            }

            yield return null;
        }

#endif
    }

    // ---------------------------------------------------------
    // FINISH
    // ---------------------------------------------------------

    private void FinishWarmup()
    {
        if (IsReady)
            return;

        IsReady = true;

        Debug.Log(
            "ARWarmupManager: GPS/WPS localization READY.");

        OnLocalizationReady?.Invoke();

        // IMPORTANT:
        // Do NOT hide HomePanel here.
        //
        // The navigation sequence will hide it only
        // after AR objects and route are successfully ready.

        gameObject.SetActive(false);
    }

    // ---------------------------------------------------------
    // ERROR
    // ---------------------------------------------------------

    private void ShowError(string message)
    {
        if (statusText != null)
            statusText.text = message;

        if (progressBar != null)
            progressBar.value = 0f;

        if (retryButton != null)
            retryButton.gameObject.SetActive(true);

        Debug.LogWarning(
            "ARWarmupManager: " + message);
    }

    public void HideHomePanel()
    {
        if (homePanel != null)
            homePanel.SetActive(false);
    }

    // ---------------------------------------------------------
    // RETRY
    // ---------------------------------------------------------

    private void OnRetry()
    {
        IsReady = false;

        if (nodeManager != null)
            nodeManager.ClearNavigation();

        if (homePanel != null)
            homePanel.SetActive(true);

        StartLoading();
    }
}