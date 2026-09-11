using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuildingInfoPanelController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private RectTransform panel;

    [Header("Building UI")]
    [SerializeField] private Image buildingImage;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Transform facilitiesContainer;
    [SerializeField] private GameObject facilityRowPrefab;

    [Header("Buttons")]
    [SerializeField] private Button dragButton;

    [Header("Animation")]
    [SerializeField] private float slideDistance = 700f;
    [SerializeField] private float animationDuration = 0.35f;

    private readonly List<GameObject> spawnedRows =
        new List<GameObject>();

    private Vector2 shownPosition;
    private Vector2 hiddenPosition;

    private Coroutine animationCoroutine;

    private BuildingInfo currentInfo;

    private void Awake()
    {
        if (panel == null)
            panel = GetComponent<RectTransform>();

        if (dragButton != null)
            dragButton.onClick.AddListener(Hide);

        shownPosition = panel.anchoredPosition;

        hiddenPosition =
            shownPosition + Vector2.down * slideDistance;

        // Start hidden.
        panel.anchoredPosition = hiddenPosition;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (dragButton != null)
            dragButton.onClick.RemoveListener(Hide);
    }

    // =========================================================
    // SET BUILDING INFORMATION
    // =========================================================

    public void SetBuildingInfo(BuildingInfo info)
    {
        if (info == null)
        {
            Debug.LogWarning(
                "[InfoPanel] SetBuildingInfo called with null BuildingInfo."
            );

            currentInfo = null;
            return;
        }

        currentInfo = info;

        Debug.Log(
            $"[InfoPanel] Displaying building: {info.title}"
        );

        // -----------------------------------------------------
        // IMAGE
        // -----------------------------------------------------

        if (buildingImage != null)
        {
            buildingImage.sprite = info.buildingImage;
            buildingImage.enabled = info.buildingImage != null;
        }

        // -----------------------------------------------------
        // TITLE
        // -----------------------------------------------------

        if (titleText != null)
            titleText.text = info.title ?? string.Empty;

        // -----------------------------------------------------
        // DESCRIPTION
        // -----------------------------------------------------

        if (descriptionText != null)
            descriptionText.text =
                info.description ?? string.Empty;

        // -----------------------------------------------------
        // FACILITIES
        // -----------------------------------------------------

        ClearFacilityRows();

        if (facilitiesContainer == null)
        {
            Debug.LogWarning(
                "[InfoPanel] Facilities Container is not assigned."
            );

            return;
        }

        if (facilityRowPrefab == null)
        {
            Debug.LogWarning(
                "[InfoPanel] Facility Row Prefab is not assigned."
            );

            return;
        }

        if (info.facilities == null)
            return;

        foreach (FacilityEntry facility in info.facilities)
        {
            if (facility == null)
                continue;

            GameObject row =
                Instantiate(
                    facilityRowPrefab,
                    facilitiesContainer
                );

            row.SetActive(true);

            Transform labelTransform =
                row.transform.Find("Label");

            if (labelTransform != null)
            {
                Text label =
                    labelTransform.GetComponent<Text>();

                if (label != null)
                    label.text =
                        facility.label ?? string.Empty;
            }
            else
            {
                Debug.LogWarning(
                    "[InfoPanel] Facility row prefab has no " +
                    "child named 'Label'."
                );
            }

            spawnedRows.Add(row);
        }
    }

    // =========================================================
    // SHOW
    // =========================================================

    /// <summary>
    /// Called by ShowInfoBtn.
    /// </summary>
    public void Show()
    {
        if (currentInfo == null)
        {
            Debug.LogWarning(
                "[InfoPanel] No building information has been loaded."
            );

            return;
        }

        gameObject.SetActive(true);

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        animationCoroutine =
            StartCoroutine(
                AnimatePanel(
                    panel.anchoredPosition,
                    shownPosition
                )
            );
    }

    // =========================================================
    // HIDE
    // =========================================================

    /// <summary>
    /// Called by DragButton.
    /// </summary>
    public void Hide()
    {
        if (!gameObject.activeSelf)
            return;

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        animationCoroutine =
            StartCoroutine(HideRoutine());
    }

    // =========================================================
    // CLEAR FACILITIES
    // =========================================================

    private void ClearFacilityRows()
    {
        foreach (GameObject row in spawnedRows)
        {
            if (row != null)
                Destroy(row);
        }

        spawnedRows.Clear();
    }

    // =========================================================
    // HIDE ANIMATION
    // =========================================================

    private IEnumerator HideRoutine()
    {
        yield return AnimatePanel(
            panel.anchoredPosition,
            hiddenPosition
        );

        gameObject.SetActive(false);
        animationCoroutine = null;
    }

    // =========================================================
    // PANEL ANIMATION
    // =========================================================

    private IEnumerator AnimatePanel(
        Vector2 start,
        Vector2 target)
    {
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / animationDuration
                );

            // Ease out.
            t = 1f - Mathf.Pow(1f - t, 3f);

            panel.anchoredPosition =
                Vector2.Lerp(start, target, t);

            yield return null;
        }

        panel.anchoredPosition = target;
        animationCoroutine = null;
    }
}