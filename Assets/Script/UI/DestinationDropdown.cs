using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Populates a dropdown with destination NavNodes.
///
/// IMPORTANT:
/// - "Select a destination" is ONLY displayed as the caption.
/// - It is NOT added to Dropdown.options.
/// - The first real destination is index 0.
/// - Building names are loaded from Firebase.
/// </summary>
[RequireComponent(typeof(Dropdown))]
public class DestinationDropdown : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DestinationNodeManager destinationManager;
    [SerializeField] private AStarRouteService routeService;
    [SerializeField] private NodeManager nodeManager;
    [SerializeField] private BuildingDataService buildingDataService;
    [SerializeField] private BuildingInfoPanelController buildingInfoPanel;
    [SerializeField] private ARGuideManager arGuideManager;
    [SerializeField] private ARWarmupManager arWarmupManager;

    [Header("UI")]
    [SerializeField] private string placeholderText = "Select a destination";
    [SerializeField] private string loadingText = "Loading destinations...";

    private readonly List<NavNode> _destinations = new();

    private Dropdown _dropdown;

    public NavNode SelectedDestination { get; private set; }

    // ---------------------------------------------------------
    // UNITY LIFECYCLE
    // ---------------------------------------------------------

    private void Awake()
    {
        _dropdown = GetComponent<Dropdown>();

        if (destinationManager == null)
            destinationManager = FindFirstObjectByType<DestinationNodeManager>();

        if (routeService == null)
            routeService = FindFirstObjectByType<AStarRouteService>();

        if (nodeManager == null)
            nodeManager = FindFirstObjectByType<NodeManager>();

        if (buildingDataService == null)
            buildingDataService = FindFirstObjectByType<BuildingDataService>();

        if (arGuideManager == null)
            arGuideManager = FindFirstObjectByType<ARGuideManager>();

        if (arWarmupManager == null)
            arWarmupManager =
                FindFirstObjectByType<ARWarmupManager>();

        _dropdown.onValueChanged.AddListener(OnDropdownValueChanged);

        if (arWarmupManager != null)
            arWarmupManager.OnLocalizationReady +=
                OnLocalizationReady;
    }

    private void OnEnable()
    {
        if (nodeManager != null)
            nodeManager.OnPathNodesReady += RefreshDestinations;

        if (nodeManager == null || nodeManager.IsNavigationGraphReady)
            RefreshDestinations();
    }

    private void OnDisable()
    {
        if (nodeManager != null)
            nodeManager.OnPathNodesReady -= RefreshDestinations;
    }

    private void OnDestroy()
    {
        if (_dropdown != null)
            _dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);

        if (arWarmupManager != null)
            arWarmupManager.OnLocalizationReady -=
                OnLocalizationReady;
    }

    // ---------------------------------------------------------
    // DESTINATION REFRESH
    // ---------------------------------------------------------

    private void RefreshDestinations()
    {
        if (_dropdown == null)
            return;

        _destinations.Clear();

        // -----------------------------------------------------
        // Get destinations from DestinationNodeManager
        // -----------------------------------------------------

        if (destinationManager != null)
        {
            foreach (NavNode node in destinationManager.DestinationNodes)
            {
                if (node != null &&
                    node.isDestination &&
                    node.buildingId >= 0)
                {
                    _destinations.Add(node);
                }
            }
        }
        else
        {
            // -------------------------------------------------
            // Fallback: find all NavNodes in the scene
            // -------------------------------------------------

            foreach (NavNode node in FindObjectsByType<NavNode>(
                         FindObjectsSortMode.None))
            {
                if (node != null &&
                    node.isDestination &&
                    node.buildingId >= 0)
                {
                    _destinations.Add(node);
                }
            }
        }

        // -----------------------------------------------------
        // Clear dropdown.
        //
        // IMPORTANT:
        // No placeholder is added here.
        // No loading entry is added here.
        // -----------------------------------------------------

        _dropdown.ClearOptions();

        _dropdown.interactable = false;

        SelectedDestination = null;

        // -----------------------------------------------------
        // Display loading text ONLY as caption.
        //
        // It is NOT a dropdown option.
        // -----------------------------------------------------

        SetCaption(loadingText);

        // -----------------------------------------------------
        // Load Firebase building names.
        // -----------------------------------------------------

        LoadBuildingNames();
    }

    // ---------------------------------------------------------
    // LOAD BUILDING NAMES
    // ---------------------------------------------------------

    private void LoadBuildingNames()
    {
        // -----------------------------------------------------
        // No Firebase service
        // -----------------------------------------------------

        if (buildingDataService == null)
        {
            Debug.LogError(
                "DestinationDropdown: BuildingDataService is not assigned."
            );

            PopulateUsingNodeNames();
            return;
        }

        // -----------------------------------------------------
        // No destinations
        // -----------------------------------------------------

        if (_destinations.Count == 0)
        {
            _dropdown.ClearOptions();

            _dropdown.interactable = false;

            SelectedDestination = null;

            SetCaption(placeholderText);

            return;
        }

        // -----------------------------------------------------
        // Clear dropdown before adding destinations.
        //
        // There is NO placeholder option.
        // -----------------------------------------------------

        _dropdown.ClearOptions();

        // -----------------------------------------------------
        // Add ONLY destinations.
        //
        // index 0 = first destination
        // index 1 = second destination
        // index 2 = third destination
        // -----------------------------------------------------

        foreach (NavNode node in _destinations)
        {
            _dropdown.AddOptions(
                new List<string>
                {
                    GetFallbackLabel(node)
                }
            );
        }

        // -----------------------------------------------------
        // Initially show placeholder.
        //
        // The placeholder is caption-only.
        // -----------------------------------------------------

        SetCaption(placeholderText);

        // Keep the actual dropdown value at 0 internally.
        // We prevent the event from firing.
        _dropdown.SetValueWithoutNotify(0);

        // Restore placeholder because index 0 is a real building.
        SetCaption(placeholderText);

        _dropdown.interactable = false;

        // -----------------------------------------------------
        // Firebase loading counter
        // -----------------------------------------------------

        int remaining = _destinations.Count;

        for (int i = 0; i < _destinations.Count; i++)
        {
            NavNode node = _destinations[i];

            // Capture the current index.
            int destinationIndex = i;

            buildingDataService.FetchBuildingName(
                node.buildingId,

                // -------------------------------------------------
                // SUCCESS
                // -------------------------------------------------

                buildingName =>
                {
                    if (destinationIndex >= _destinations.Count)
                        return;

                    string label =
                        !string.IsNullOrWhiteSpace(buildingName)
                            ? buildingName
                            : GetFallbackLabel(node);

                    // -------------------------------------------------
                    // IMPORTANT:
                    //
                    // There is NO +1.
                    //
                    // Dropdown index directly matches destination index.
                    //
                    // destination 0 -> dropdown 0
                    // destination 1 -> dropdown 1
                    // destination 2 -> dropdown 2
                    // -------------------------------------------------

                    int dropdownIndex = destinationIndex;

                    if (dropdownIndex >= 0 &&
                        dropdownIndex < _dropdown.options.Count)
                    {
                        _dropdown.options[dropdownIndex].text = label;
                    }

                    // -------------------------------------------------
                    // Keep placeholder visible until user selects
                    // an actual destination.
                    // -------------------------------------------------

                    if (SelectedDestination == null)
                        SetCaption(placeholderText);

                    _dropdown.RefreshShownValue();

                    remaining--;

                    if (remaining <= 0)
                    {
                        _dropdown.interactable =
                            _destinations.Count > 0;

                        // Keep placeholder visible initially.
                        if (SelectedDestination == null)
                            SetCaption(placeholderText);

                        Debug.Log(
                            $"DestinationDropdown: Loaded " +
                            $"{_destinations.Count} destination names."
                        );
                    }
                },

                // -------------------------------------------------
                // ERROR
                // -------------------------------------------------

                error =>
                {
                    Debug.LogWarning(
                        $"DestinationDropdown: Failed to load building " +
                        $"{node.buildingId}: {error}"
                    );

                    // -------------------------------------------------
                    // Use NavNode name as fallback.
                    // -------------------------------------------------

                    int dropdownIndex = destinationIndex;

                    if (dropdownIndex >= 0 &&
                        dropdownIndex < _dropdown.options.Count)
                    {
                        _dropdown.options[dropdownIndex].text =
                            GetFallbackLabel(node);
                    }

                    if (SelectedDestination == null)
                        SetCaption(placeholderText);

                    _dropdown.RefreshShownValue();

                    remaining--;

                    if (remaining <= 0)
                    {
                        _dropdown.interactable =
                            _destinations.Count > 0;

                        if (SelectedDestination == null)
                            SetCaption(placeholderText);
                    }
                }
            );
        }
    }

    // ---------------------------------------------------------
    // FALLBACK POPULATION
    // ---------------------------------------------------------

    private void PopulateUsingNodeNames()
    {
        _dropdown.ClearOptions();

        // -----------------------------------------------------
        // ONLY actual destinations.
        //
        // No placeholder.
        // -----------------------------------------------------

        var options = new List<string>();

        foreach (NavNode node in _destinations)
        {
            options.Add(GetFallbackLabel(node));
        }

        _dropdown.AddOptions(options);

        _dropdown.interactable =
            _destinations.Count > 0;

        // -----------------------------------------------------
        // Display placeholder as caption only.
        // -----------------------------------------------------

        if (_destinations.Count > 0)
        {
            _dropdown.SetValueWithoutNotify(0);
        }

        SetCaption(placeholderText);

        _dropdown.RefreshShownValue();
    }

    // ---------------------------------------------------------
    // DROPDOWN SELECTION
    // ---------------------------------------------------------

    private void OnDropdownValueChanged(int optionIndex)
    {
        int destinationIndex = optionIndex;

        if (destinationIndex < 0 ||
            destinationIndex >= _destinations.Count)
        {
            SelectedDestination = null;

            nodeManager?.SetActiveDestination(null);

            if (arGuideManager != null)
            {
                arGuideManager.SetDestinationSelected(false);
            }

            SetCaption(placeholderText);

            return;
        }

        SelectedDestination =
            _destinations[destinationIndex];

        Debug.Log(
                $"DestinationDropdown: Selected " +
                $"{GetFallbackLabel(SelectedDestination)} " +
                $"(buildingId={SelectedDestination.buildingId})"
);

        // Tell ARGuideManager that a destination has been selected.
        if (arGuideManager != null)
        {
            arGuideManager.SetDestinationSelected(true);
        }

        if (_dropdown.options.Count > optionIndex)
        {
            SetCaption(
                _dropdown.options[optionIndex].text
            );
        }

        Debug.Log(
            $"DestinationDropdown: Selected " +
            $"{GetFallbackLabel(SelectedDestination)} " +
            $"(buildingId={SelectedDestination.buildingId})"
        );

        LoadSelectedBuildingInfo();

        if (arWarmupManager == null)
        {
            Debug.LogError(
                "DestinationDropdown: ARWarmupManager is not assigned.");

            return;
        }

        Debug.Log(
            "Destination selected. Starting AR warmup.");

        arWarmupManager.StartLoading();

        // if (nodeManager != null &&
        //     !nodeManager.SetActiveDestination(SelectedDestination))
        // {
        //     return;
        // }

        // Build route
        // if (routeService == null)
        //     return;

        // if (!routeService.TryBuildRoute(SelectedDestination))
        // {
        //     Debug.LogWarning(
        //         $"DestinationDropdown: Could not build a route to " +
        //         $"{GetFallbackLabel(SelectedDestination)}. " +
        //         routeService.LastFailure
        //     );
        // }
    }

    private void OnLocalizationReady()
    {
        Debug.Log(
            "DestinationDropdown: Localization is ready.");

        if (SelectedDestination == null)
        {
            Debug.LogWarning(
                "DestinationDropdown: No destination selected.");

            return;
        }

        // STEP 1
        // Place the AR navigation objects
        if (nodeManager == null)
        {
            Debug.LogError(
                "DestinationDropdown: NodeManager is missing.");

            return;
        }

        nodeManager.PlaceARObjects();

        // STEP 2
        // Set the selected destination marker
        if (nodeManager != null)
        {
            if (!nodeManager.SetActiveDestination(
                    SelectedDestination))
            {
                Debug.LogError(
                    "DestinationDropdown: Failed to set destination.");

                return;
            }
        }

        // STEP 3
        // Build the A* route
        if (routeService == null)
        {
            Debug.LogError(
                "DestinationDropdown: AStarRouteService is missing.");

            return;
        }

        if (!routeService.TryBuildRoute(
                SelectedDestination))
        {
            Debug.LogWarning(
                "DestinationDropdown: Route could not be built.\n" +
                routeService.LastFailure);

            return;
        }

        // STEP 4
        Debug.Log("DestinationDropdown: Navigation route successfully built.");

        // STEP 5
        // Hide HomePanel
        if (arWarmupManager != null)
        {
            arWarmupManager.HideHomePanel();
        }
    }


    // ---------------------------------------------------------
    // FALLBACK LABEL
    // ---------------------------------------------------------

    private static string GetFallbackLabel(NavNode node)
    {
        if (node == null)
            return "Unknown destination";

        if (!string.IsNullOrWhiteSpace(node.nodeName))
            return node.nodeName;

        if (node.gameObject != null)
            return node.gameObject.name;

        return $"Building {node.buildingId}";
    }

    // ---------------------------------------------------------
    // SET CAPTION
    // ---------------------------------------------------------

    private void SetCaption(string text)
    {
        if (_dropdown == null)
            return;

        if (_dropdown.captionText != null)
        {
            _dropdown.captionText.text = text;
        }
    }

    // ---------------------------------------------------------
    // LOAD SELECTED BUILDING INFORMATION
    // ---------------------------------------------------------

    private void LoadSelectedBuildingInfo()
    {
        if (SelectedDestination == null)
            return;

        if (buildingInfoPanel == null)
        {
            Debug.LogError(
                "DestinationDropdown: " +
                "BuildingInfoPanelController is not assigned."
            );

            return;
        }

        if (buildingDataService == null)
        {
            Debug.LogError(
                "DestinationDropdown: " +
                "BuildingDataService is not assigned."
            );

            return;
        }

        int buildingId =
            SelectedDestination.buildingId;

        if (buildingId < 0)
        {
            Debug.LogWarning(
                $"DestinationDropdown: NavNode " +
                $"'{SelectedDestination.name}' " +
                "does not have a valid buildingId."
            );

            return;
        }

        Debug.Log(
            $"DestinationDropdown: Loading building information " +
            $"for buildingId={buildingId}"
        );

        buildingDataService.FetchBuilding(
            buildingId,

            // -------------------------------------------------
            // SUCCESS
            // -------------------------------------------------

            info =>
            {
                if (info == null)
                {
                    Debug.LogWarning(
                        $"DestinationDropdown: Firebase returned " +
                        $"null BuildingInfo for buildingId={buildingId}."
                    );

                    return;
                }

                Debug.Log(
                    $"DestinationDropdown: Building information loaded: " +
                    $"{info.title}"
                );

                // Store Firebase information in the panel.
                buildingInfoPanel.SetBuildingInfo(info);

                // IMPORTANT:
                // Do NOT call buildingInfoPanel.Show() here.
            },

            // -------------------------------------------------
            // ERROR
            // -------------------------------------------------

            error =>
            {
                Debug.LogWarning(
                    $"DestinationDropdown: Failed to load building " +
                    $"{buildingId}: {error}"
                );
            }
        );
    }
}
