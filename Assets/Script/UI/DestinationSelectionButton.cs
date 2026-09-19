using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates destination buttons from Firestore BuildingInfo records.
///
/// IMPORTANT:
/// Every Firestore document is treated as one destination.
///
/// buildingId:
///     Identifies the Firestore information record.
///
/// buildingNodeId:
///     Identifies the physical Unity NavNode.
///
/// Facilities:
///     Information only. They are displayed by BuildingInfoPanel.
///     They are NOT separate destination buttons.
///
/// Destination click:
///     1. Stores the selected BuildingInfo.
///     2. Finds the NavNode using buildingNodeId.
///     3. Sets SelectedDestination.
///     4. Starts navigation.
///
/// Info button:
///     Calls ShowSelectedBuildingInfo().
///     The stored BuildingInfo is passed to BuildingInfoPanel.
/// </summary>
public class DestinationSelectionButtons : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private DestinationNodeManager destinationManager;

    [SerializeField]
    private AStarRouteService routeService;

    [SerializeField]
    private NodeManager nodeManager;

    [SerializeField]
    private BuildingDataService buildingDataService;

    [SerializeField]
    private BuildingInfoPanelController buildingInfoPanel;

    [SerializeField]
    private ARGuideManager arGuideManager;

    [SerializeField]
    private ARWarmupManager arWarmupManager;

    [Header("Destination Button UI")]
    [Tooltip("Content object inside the Scroll View.")]
    [SerializeField]
    private Transform destinationContent;

    [Tooltip("Button prefab that will be cloned.")]
    [SerializeField]
    private GameObject destinationButtonPrefab;

    [Tooltip("Optional TMP text reference. Usually leave empty so the script finds the text inside each cloned button.")]
    [SerializeField]
    private TMP_Text buttonLabel;
    private Image destinationPanel;

    [Header("UI")]
    [SerializeField]
    private string loadingText =
        "Loading destinations...";

    // =========================================================
    // STATE
    // =========================================================

    /// <summary>
    /// The NavNode currently selected as the navigation destination.
    /// </summary>
    public NavNode SelectedDestination
    {
        get;
        private set;
    }

    /// <summary>
    /// The Firestore BuildingInfo corresponding to the selected
    /// destination button.
    ///
    /// This is what the Info button uses.
    /// </summary>
    public BuildingInfo SelectedBuilding
    {
        get;
        private set;
    }

    /// <summary>
    /// Optional public access to the selected Firestore ID.
    /// </summary>
    public int SelectedBuildingId
    {
        get
        {
            return SelectedBuilding != null
                ? SelectedBuilding.buildingId
                : -1;
        }
    }

    /// <summary>
    /// Optional public access to the selected building_node_id.
    /// </summary>
    public string SelectedBuildingNodeId
    {
        get
        {
            return SelectedBuilding != null
                ? SelectedBuilding.buildingNodeId
                : string.Empty;
        }
    }

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (destinationManager == null)
        {
            destinationManager =
                FindFirstObjectByType<
                    DestinationNodeManager>();
        }

        if (routeService == null)
        {
            routeService =
                FindFirstObjectByType<
                    AStarRouteService>();
        }

        if (nodeManager == null)
        {
            nodeManager =
                FindFirstObjectByType<
                    NodeManager>();
        }

        if (buildingDataService == null)
        {
            buildingDataService =
                FindFirstObjectByType<
                    BuildingDataService>();
        }

        if (buildingInfoPanel == null)
        {
            buildingInfoPanel =
                FindFirstObjectByType<
                    BuildingInfoPanelController>();
        }

        if (arGuideManager == null)
        {
            arGuideManager =
                FindFirstObjectByType<
                    ARGuideManager>();
        }

        if (arWarmupManager == null)
        {
            arWarmupManager =
                FindFirstObjectByType<
                    ARWarmupManager>();
        }
    }

    private void OnEnable()
    {
        if (nodeManager != null)
        {
            nodeManager.OnPathNodesReady +=
                RefreshDestinations;
        }

        if (nodeManager == null ||
            nodeManager.IsNavigationGraphReady)
        {
            RefreshDestinations();
        }
    }

    private void OnDisable()
    {
        if (nodeManager != null)
        {
            nodeManager.OnPathNodesReady -=
                RefreshDestinations;
        }
    }

    // =========================================================
    // REFRESH DESTINATIONS
    // =========================================================

    private void RefreshDestinations()
    {
        ClearButtons();

        if (destinationContent == null)
        {
            Debug.LogError(
                "DestinationButtons: " +
                "Destination Content is not assigned."
            );

            return;
        }

        if (destinationButtonPrefab == null)
        {
            Debug.LogError(
                "DestinationButtons: " +
                "Destination Button Prefab is not assigned."
            );

            return;
        }

        if (buildingDataService == null)
        {
            Debug.LogError(
                "DestinationButtons: " +
                "BuildingDataService is not assigned."
            );

            return;
        }

        LoadBuildings();
    }

    // =========================================================
    // LOAD ALL FIRESTORE DESTINATIONS
    // =========================================================

    private void LoadBuildings()
    {
        ShowLoadingButton();

        buildingDataService.FetchAllBuildings(
            buildings =>
            {
                ClearButtons();

                if (buildings == null ||
                    buildings.Count == 0)
                {
                    Debug.LogWarning(
                        "DestinationButtons: " +
                        "No destinations found in Firestore."
                    );

                    return;
                }

                CreateButtons(
                    buildings
                );
            },

            error =>
            {
                Debug.LogError(
                    "DestinationButtons: Failed to load " +
                    $"destinations: {error}"
                );

                ClearButtons();
            }
        );
    }

    // =========================================================
    // CREATE DESTINATION BUTTONS
    // =========================================================

    /// <summary>
    /// Creates exactly ONE button for every Firestore
    /// BuildingInfo record.
    ///
    /// Facilities are deliberately NOT converted into buttons.
    /// </summary>
    private void CreateButtons(
        List<BuildingInfo> buildings)
    {
        foreach (BuildingInfo building in buildings)
        {
            if (building == null)
                continue;

            // A destination needs a NavNode ID.
            if (string.IsNullOrWhiteSpace(
                    building.buildingNodeId))
            {
                Debug.LogWarning(
                    $"DestinationButtons: " +
                    $"'{building.title}' " +
                    $"(buildingId={building.buildingId}) " +
                    "has no building_node_id. " +
                    "Destination button was skipped."
                );

                continue;
            }

            CreateBuildingButton(
                building
            );
        }

        Debug.Log(
            $"DestinationButtons: Created " +
            $"{destinationContent.childCount} " +
            "destination buttons."
        );
    }

    // =========================================================
    // CREATE ONE DESTINATION BUTTON
    // =========================================================

    private void CreateBuildingButton(
        BuildingInfo building)
    {
        GameObject buttonObject =
            Instantiate(
                destinationButtonPrefab,
                destinationContent
            );

        Button button =
            buttonObject.GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError(
                "DestinationButtons: " +
                "Destination Button prefab does not " +
                "have a Button component."
            );

            Destroy(buttonObject);

            return;
        }

        TMP_Text label =
            FindLabel(
                buttonObject
            );

        if (label != null)
        {
            label.text =
                building.title;
        }

        button.onClick.RemoveAllListeners();

        // Capture the current BuildingInfo.
        BuildingInfo selectedBuilding =
            building;

        button.onClick.AddListener(
            () =>
            {
                SelectBuilding(
                    selectedBuilding
                );
            }
        );
    }

    // =========================================================
    // SELECT DESTINATION
    // =========================================================

    /// <summary>
    /// Called when the user clicks a destination button.
    ///
    /// Example:
    ///
    /// OSAS
    /// buildingId = 11
    /// buildingNodeId = "Node-01"
    ///
    /// The BuildingInfo is stored for the Info button.
    /// The buildingNodeId is used to find the Unity NavNode.
    /// </summary>
    private void SelectBuilding(BuildingInfo building)
    {
        if (building == null)
        {
            Debug.LogError(
                "DestinationSelectionButtons: Selected building is null."
            );
            return;
        }

        // =========================================================
        // STEP 1
        // Store the selected BuildingInfo
        // =========================================================

        SelectedBuilding = building;

        Debug.Log(
            $"DestinationSelectionButtons: Selected " +
            $"{building.title} (buildingId={building.buildingId})"
        );

        // =========================================================
        // STEP 2
        // Find the physical NavNode using building_node_id
        // =========================================================

        if (string.IsNullOrWhiteSpace(building.buildingNodeId))
        {
            Debug.LogError(
                $"DestinationSelectionButtons: " +
                $"{building.title} has no building_node_id."
            );

            return;
        }

        NavNode destinationNode =
            FindNavNodeByBuildingNodeId(building.buildingNodeId);

        if (destinationNode == null)
        {
            Debug.LogError(
                $"DestinationSelectionButtons: Could not find NavNode " +
                $"with buildingNodeId='{building.buildingNodeId}' " +
                $"for '{building.title}'."
            );

            return;
        }

        // =========================================================
        // STEP 3
        // Store selected destination
        // =========================================================

        SelectedDestination = destinationNode;

        Debug.Log(
            $"DestinationSelectionButtons: Selected destination " +
            $"{GetNodeLabel(SelectedDestination)} " +
            $"(buildingId={building.buildingId})"
        );

        // =========================================================
        // STEP 4
        // Tell ARGuideManager a destination was selected
        // =========================================================

        if (arGuideManager != null)
        {
            arGuideManager.SetDestinationSelected(true);
        }

        // =========================================================
        // STEP 5
        // Hide destination selection panel
        // =========================================================

        gameObject.SetActive(false);

        // =========================================================
        // STEP 6
        // Start AR warmup
        // =========================================================

        if (arWarmupManager == null)
        {
            Debug.LogError(
                "DestinationSelectionButtons: " +
                "ARWarmupManager is not assigned."
            );

            return;
        }

        Debug.Log(
            "DestinationSelectionButtons: " +
            "Destination selected. Starting AR warmup."
        );

        arWarmupManager.StartLoading();
    }

    // =========================================================
    // FIND NAV NODE
    // =========================================================

    /// <summary>
    /// Finds the Unity NavNode whose buildingNodeId matches
    /// the Firestore building_node_id.
    ///
    /// Firestore:
    ///     building_node_id = "Node-01"
    ///
    /// Unity:
    ///     NavNode.buildingNodeId = "Node-01"
    ///
    /// Match:
    ///     That NavNode becomes the destination.
    /// </summary>
    private NavNode FindNavNodeByBuildingNodeId(
        string buildingNodeId)
    {
        if (string.IsNullOrWhiteSpace(
                buildingNodeId))
        {
            return null;
        }

        // -----------------------------------------------------
        // FIRST: DestinationNodeManager
        // -----------------------------------------------------

        if (destinationManager != null &&
            destinationManager.DestinationNodes != null)
        {
            foreach (NavNode node in
                     destinationManager.DestinationNodes)
            {
                if (node == null)
                    continue;

                if (string.Equals(
                        node.buildingNodeId,
                        buildingNodeId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }
        }

        // -----------------------------------------------------
        // FALLBACK: SEARCH ALL NAV NODES
        // -----------------------------------------------------

        NavNode[] allNodes =
            FindObjectsByType<NavNode>(
                FindObjectsSortMode.None
            );

        foreach (NavNode node in allNodes)
        {
            if (node == null)
                continue;

            if (string.Equals(
                    node.buildingNodeId,
                    buildingNodeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        return null;
    }

    // =========================================================
    // SHOW SELECTED BUILDING INFORMATION
    // =========================================================

    /// <summary>
    /// Connect this method to the UI Info button.
    ///
    /// The destination button does NOT open the information panel.
    ///
    /// User flow:
    ///
    ///     Click OSAS
    ///         ↓
    ///     SelectedBuilding = OSAS
    ///
    ///     Click Info
    ///         ↓
    ///     SetBuildingInfo(OSAS)
    ///         ↓
    ///     Show()
    /// </summary>
    public void ShowSelectedBuildingInfo()
    {
        if (SelectedBuilding == null)
        {
            Debug.LogWarning(
                "DestinationButtons: " +
                "No destination has been selected. " +
                "Cannot show building information."
            );

            return;
        }

        if (buildingInfoPanel == null)
        {
            Debug.LogError(
                "DestinationButtons: " +
                "BuildingInfoPanelController is not assigned."
            );

            return;
        }

        Debug.Log(
            $"DestinationButtons: Showing information for " +
            $"{SelectedBuilding.title} " +
            $"(buildingId={SelectedBuilding.buildingId})."
        );

        // The BuildingInfo was already loaded from Firestore.
        // No second Firestore request is required.
        buildingInfoPanel.SetBuildingInfo(
            SelectedBuilding
        );

        buildingInfoPanel.Show();
    }

    // =========================================================
    // START NAVIGATION
    // =========================================================

    private void StartNavigation()
    {
        if (SelectedDestination == null)
        {
            Debug.LogWarning(
                "DestinationButtons: " +
                "SelectedDestination is null."
            );

            return;
        }

        if (arGuideManager != null)
        {
            arGuideManager.SetDestinationSelected(
                true
            );
        }

        if (arWarmupManager == null)
        {
            Debug.LogError(
                "DestinationButtons: " +
                "ARWarmupManager is not assigned."
            );

            return;
        }

        Debug.Log(
            $"DestinationButtons: Selected " +
            $"{GetNodeLabel(SelectedDestination)}. " +
            "Starting AR warmup."
        );

        arWarmupManager.StartLoading();
    }

    // =========================================================
    // LOCALIZATION READY
    // =========================================================

    /// <summary>
    /// This should be called by your localization system when
    /// AR/world positioning is ready.
    /// </summary>
    private void OnLocalizationReady()
    {
        Debug.Log(
            "DestinationSelectionButtons: Localization is ready."
        );

        if (SelectedDestination == null)
        {
            Debug.LogWarning(
                "DestinationSelectionButtons: No destination selected."
            );

            return;
        }

        // =========================================================
        // STEP 1
        // Place AR navigation objects
        // =========================================================

        if (nodeManager == null)
        {
            Debug.LogError(
                "DestinationSelectionButtons: NodeManager is missing."
            );

            return;
        }

        Debug.Log(
            "DestinationSelectionButtons: " +
            "Starting WPS placement of AR objects."
        );

        nodeManager.PlaceARObjects();

        Debug.Log(
            "DestinationSelectionButtons: " +
            "Finished WPS placement request."
        );

        // =========================================================
        // STEP 2
        // Set selected destination marker
        // =========================================================

        if (!nodeManager.SetActiveDestination(
                SelectedDestination))
        {
            Debug.LogError(
                "DestinationSelectionButtons: " +
                "Failed to set destination."
            );

            return;
        }

        Debug.Log(
            $"DestinationSelectionButtons: " +
            $"Destination marker set for " +
            $"{SelectedBuilding.title} " +
            $"(buildingId={SelectedBuilding.buildingId})."
        );

        // =========================================================
        // STEP 3
        // Build A* route
        // =========================================================

        if (routeService == null)
        {
            Debug.LogError(
                "DestinationSelectionButtons: " +
                "AStarRouteService is missing."
            );

            return;
        }

        if (!routeService.TryBuildRoute(
                SelectedDestination))
        {
            Debug.LogWarning(
                "DestinationSelectionButtons: " +
                "Route could not be built.\n" +
                routeService.LastFailure
            );

            return;
        }

        // =========================================================
        // STEP 4
        // Navigation successfully started
        // =========================================================

        Debug.Log(
            "DestinationSelectionButtons: " +
            "Navigation route successfully built."
        );

        // =========================================================
        // STEP 5
        // Hide HomePanel
        // =========================================================

        if (arWarmupManager != null)
        {
            arWarmupManager.HideHomePanel();
        }
    }

    // =========================================================
    // FIND BUTTON LABEL
    // =========================================================

    private TMP_Text FindLabel(
        GameObject buttonObject)
    {
        // Do not use a scene-wide reference for cloned buttons.
        // Find the TMP_Text inside the newly cloned prefab.

        TMP_Text label =
            buttonObject.GetComponentInChildren<TMP_Text>(
                true
            );

        if (label == null)
        {
            Debug.LogWarning(
                "DestinationButtons: " +
                "No TMP_Text found inside destination button prefab."
            );
        }

        return label;
    }

    // =========================================================
    // CLEAR BUTTONS
    // =========================================================

    private void ClearButtons()
    {
        if (destinationContent == null)
            return;

        for (
            int i = destinationContent.childCount - 1;
            i >= 0;
            i--)
        {
            Destroy(
                destinationContent
                    .GetChild(i)
                    .gameObject
            );
        }
    }

    // =========================================================
    // LOADING BUTTON
    // =========================================================

    private void ShowLoadingButton()
    {
        if (destinationContent == null ||
            destinationButtonPrefab == null)
        {
            return;
        }

        GameObject buttonObject =
            Instantiate(
                destinationButtonPrefab,
                destinationContent
            );

        Button button =
            buttonObject.GetComponent<Button>();

        if (button != null)
        {
            button.interactable = false;
        }

        TMP_Text label =
            FindLabel(
                buttonObject
            );

        if (label != null)
        {
            label.text =
                loadingText;
        }
    }

    // =========================================================
    // FALLBACK NODE LABEL
    // =========================================================

    private static string GetNodeLabel(
        NavNode node)
    {
        if (node == null)
            return "Unknown destination";

        if (!string.IsNullOrWhiteSpace(
                node.nodeName))
        {
            return node.nodeName;
        }

        if (node.gameObject != null)
        {
            return node.gameObject.name;
        }

        return
            $"Building {node.buildingId}";
    }
}