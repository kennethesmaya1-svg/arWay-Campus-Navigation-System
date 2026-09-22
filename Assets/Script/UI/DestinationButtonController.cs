using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Creates destination buttons from Firestore building records
/// and handles destination selection using the same AR navigation
/// workflow as DestinationDropdown.
///
/// Workflow:
/// 
/// Button selected
///     ↓
/// Store SelectedBuilding / SelectedDestination
///     ↓
/// Notify ARGuideManager
///     ↓
/// Load Firebase building information
///     ↓
/// Start AR warmup
///     ↓
/// Wait for localization
///     ↓
/// PlaceARObjects()
///     ↓
/// SetActiveDestination()
///     ↓
/// TryBuildRoute()
///     ↓
/// HideHomePanel()
/// </summary>
public class DestinationButtonController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private DestinationButton destinationButtonPrefab;
    [SerializeField] private Transform destinationContent;
    [SerializeField] private GameObject homePanel;
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private GameObject travelUi;
    [SerializeField] private TMP_Text travelDistanceText;
    [SerializeField] private TMP_Text destinationTextName;

    [Header("Data")]
    [SerializeField] private BuildingDataService buildingDataService;

    [Header("Navigation")]
    [SerializeField] private AStarRouteService routeService;
    [SerializeField] private NodeManager nodeManager;

    [Header("AR Navigation")]
    [SerializeField] private ARGuideManager arGuideManager;
    [SerializeField] private ARWarmupManager arWarmupManager;
    [SerializeField] private BuildingInfoPanelController buildingInfoPanel;

    private readonly List<DestinationButton> spawnedButtons =
        new List<DestinationButton>();

    private readonly List<BuildingInfo> buildings =
        new List<BuildingInfo>();

    public BuildingInfo SelectedBuilding { get; private set; }

    public NavNode SelectedDestination { get; private set; }
    [SerializeField] private float distanceUpdateStep = 5f;
    [SerializeField] private float arrivalDistance = 2f;
    private Coroutine travelDistanceCoroutine;
    private int lastDisplayedDistance = -1;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        // Optional automatic lookup.
        // Inspector references are still preferred.

        if (buildingDataService == null)
        {
            buildingDataService =
                FindFirstObjectByType<BuildingDataService>();
        }

        if (routeService == null)
        {
            routeService =
                FindFirstObjectByType<AStarRouteService>();
        }

        if (nodeManager == null)
        {
            nodeManager =
                FindFirstObjectByType<NodeManager>();
        }

        if (arGuideManager == null)
        {
            arGuideManager =
                FindFirstObjectByType<ARGuideManager>();
        }

        if (arWarmupManager == null)
        {
            arWarmupManager =
                FindFirstObjectByType<ARWarmupManager>();
        }

        if (buildingInfoPanel == null)
        {
            buildingInfoPanel =
                FindFirstObjectByType<BuildingInfoPanelController>();
        }

        // Search
        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(FilterDestinations);
        }
    }


    private void OnEnable()
    {
        if (arWarmupManager != null)
        {
            arWarmupManager.OnLocalizationReady +=
                OnLocalizationReady;
        }
    }


    private void OnDisable()
    {
        if (arWarmupManager != null)
        {
            arWarmupManager.OnLocalizationReady -=
                OnLocalizationReady;
        }
    }


    private void Start()
    {
        LoadDestinationButtons();
    }


    // =========================================================
    // LOAD DESTINATION BUTTONS
    // =========================================================

    public void LoadDestinationButtons()
    {
        ClearButtons();

        if (buildingDataService == null)
        {
            Debug.LogError(
                "[DestinationButtonController] " +
                "BuildingDataService is not assigned."
            );

            return;
        }

        if (destinationButtonPrefab == null)
        {
            Debug.LogError(
                "[DestinationButtonController] " +
                "Destination Button Prefab is not assigned."
            );

            return;
        }

        if (destinationContent == null)
        {
            Debug.LogError(
                "[DestinationButtonController] " +
                "Destination Content is not assigned."
            );

            return;
        }

        buildingDataService.FetchAllBuildings(
            OnBuildingsLoaded,
            OnBuildingsLoadFailed
        );

        Debug.Log(
            $"[Time Check] 1. Requesting Firebase data at: " +
            $"{Time.realtimeSinceStartup}"
        );
    }


    // =========================================================
    // BUILDINGS LOADED
    // =========================================================

    private void OnBuildingsLoaded(List<BuildingInfo> result)
    {
        buildings.Clear();

        Debug.Log(
            $"[Time Check] 2. Firebase data received. " +
            $"Starting to clone buttons at: " +
            $"{Time.realtimeSinceStartup}"
        );

        if (result == null)
        {
            Debug.LogWarning(
                "[DestinationButtonController] " +
                "No buildings were returned."
            );

            return;
        }

        foreach (BuildingInfo building in result)
        {
            if (building == null)
                continue;

            // A destination must have a physical NavNode.
            if (string.IsNullOrWhiteSpace(
                    building.buildingNodeId))
            {
                Debug.LogWarning(
                    $"[DestinationButtonController] " +
                    $"Building {building.buildingId} " +
                    $"({building.title}) has no " +
                    $"building_node_id. Button was not created."
                );

                continue;
            }

            buildings.Add(building);

            // Clone destination button.
            DestinationButton button =
                Instantiate(
                    destinationButtonPrefab,
                    destinationContent
                );

            // Set button information.
            button.Setup(
                building.buildingId,
                building.buildingNodeId,
                building.title,
                this
            );

            spawnedButtons.Add(button);
        }

        Debug.Log(
            $"[DestinationButtonController] " +
            $"Created {spawnedButtons.Count} destination buttons."
        );

        // Load images independently in the background.
        buildingDataService.LoadBuildingImagesInBackground(
            buildings
        );

        // Hide the original/template button
        if (destinationButtonPrefab.gameObject.activeSelf)
        {
            destinationButtonPrefab.gameObject.SetActive(false);
        }
    }


    // =========================================================
    // LOAD ERROR
    // =========================================================

    private void OnBuildingsLoadFailed(string error)
    {
        Debug.LogError(
            "[DestinationButtonController] " +
            "Failed to load buildings: " +
            error
        );
    }


    // =========================================================
    // BUTTON CLICK
    // =========================================================

    public void SelectDestination(
        int buildingId,
        string buildingNodeId)
    {
        Debug.Log(
            $"[DestinationButtonController] " +
            $"Button clicked:\n" +
            $"Building ID = {buildingId}\n" +
            $"Building Node ID = {buildingNodeId}"
        );


        // -----------------------------------------------------
        // FIND BUILDING INFORMATION
        // -----------------------------------------------------

        BuildingInfo building =
            FindBuilding(buildingId);

        if (building == null)
        {
            Debug.LogError(
                $"[DestinationButtonController] " +
                $"Building ID {buildingId} was not found."
            );

            return;
        }


        // -----------------------------------------------------
        // FIND PHYSICAL NAVNODE
        // -----------------------------------------------------

        NavNode destination =
            FindNavNode(buildingNodeId);

        if (destination == null)
        {
            Debug.LogError(
                $"[DestinationButtonController] " +
                $"No NavNode found with buildingNodeId " +
                $"'{buildingNodeId}'."
            );

            return;
        }


        // -----------------------------------------------------
        // SAVE SELECTION
        // -----------------------------------------------------

        SelectedBuilding = building;
        SelectedDestination = destination;

        // Hide Home Panel after selecting destination
        if (homePanel != null)
        {
            homePanel.SetActive(false);
        }


        Debug.Log(
            $"[DestinationButtonController] " +
            $"Destination selected:\n" +
            $"Name = {building.title}\n" +
            $"Building ID = {buildingId}\n" +
            $"Building Node ID = {buildingNodeId}\n" +
            $"NavNode = {destination.gameObject.name}\n" +
            $"Latitude = {destination.latitude}\n" +
            $"Longitude = {destination.longitude}\n" +
            $"Elevation = {destination.elevation}"
        );


        // -----------------------------------------------------
        // TELL AR GUIDE THAT DESTINATION WAS SELECTED
        // -----------------------------------------------------

        if (arGuideManager != null)
        {
            arGuideManager.SetDestinationSelected(true);

            Debug.Log(
                "[DestinationButtonController] " +
                "ARGuideManager notified that destination " +
                "was selected."
            );
        }
        else
        {
            Debug.LogWarning(
                "[DestinationButtonController] " +
                "ARGuideManager is not assigned."
            );
        }


        // -----------------------------------------------------
        // LOAD BUILDING INFORMATION
        // -----------------------------------------------------

        LoadSelectedBuildingInfo();


        // -----------------------------------------------------
        // START AR WARMUP
        // -----------------------------------------------------

        if (arWarmupManager == null)
        {
            Debug.LogError(
                "[DestinationButtonController] " +
                "ARWarmupManager is not assigned."
            );

            return;
        }

        Debug.Log(
            "[DestinationButtonController] " +
            "Destination selected. Starting AR warmup."
        );

        // -----------------------------------------------------
        // SHOW TRAVEL UI
        // -----------------------------------------------------
        if (travelUi != null)
        {
            travelUi.SetActive(true);
        }

         if (destinationTextName != null)
        {
            destinationTextName.text = building.title;
        }

        // Start tracking distance to destination
        StartTravelDistanceTracking();

        arWarmupManager.StartLoading();


        // IMPORTANT:
        //
        // DO NOT BUILD THE ROUTE HERE.
        //
        // We wait for localization first.
        //
        // OnLocalizationReady() will perform:
        //
        // 1. PlaceARObjects()
        // 2. SetActiveDestination()
        // 3. TryBuildRoute()
        // 4. HideHomePanel()
    }

    public void SelectAgainDestination()
    {
        // Stop old distance tracking.
        if (travelDistanceCoroutine != null)
        {
            StopCoroutine(travelDistanceCoroutine);
            travelDistanceCoroutine = null;
        }

        // Show destination selection.
        if (homePanel != null)
        {
            homePanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[DestinationButtonController] " +
                "Home Panel is not assigned."
            );
        }
    }
    //Hide home panel if back button is clicked
    public void BackToCamera()
    {
        // Show Home Panel
        if (homePanel != null)
        {
            homePanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning(
                "[DestinationButtonController] Home Panel is not assigned."
            );
        }
    }


    // =========================================================
    // AR LOCALIZATION READY
    // =========================================================

    private void OnLocalizationReady()
    {
        Debug.Log(
            "[DestinationButtonController] " +
            "Localization is ready."
        );


        // -----------------------------------------------------
        // MAKE SURE DESTINATION STILL EXISTS
        // -----------------------------------------------------

        if (SelectedDestination == null)
        {
            Debug.LogWarning(
                "[DestinationButtonController] " +
                "No destination selected."
            );

            return;
        }


        // -----------------------------------------------------
        // STEP 1
        // PLACE AR NAVIGATION OBJECTS
        // -----------------------------------------------------

        if (nodeManager == null)
        {
            Debug.LogError(
                "[DestinationButtonController] " +
                "NodeManager is missing."
            );

            return;
        }

        Debug.Log(
            "[DestinationButtonController] " +
            "Step 1: Placing AR navigation objects."
        );

        nodeManager.PlaceARObjects();


        // -----------------------------------------------------
        // STEP 2
        // SET ACTIVE DESTINATION
        // -----------------------------------------------------

        Debug.Log(
            "[DestinationButtonController] " +
            "Step 2: Setting active destination."
        );

        if (!nodeManager.SetActiveDestination(
                SelectedDestination))
        {
            Debug.LogError(
                "[DestinationButtonController] " +
                "Failed to set active destination."
            );

            return;
        }


        // -----------------------------------------------------
        // STEP 3
        // BUILD A* ROUTE
        // -----------------------------------------------------

        if (routeService == null)
        {
            Debug.LogError(
                "[DestinationButtonController] " +
                "AStarRouteService is missing."
            );

            return;
        }

        Debug.Log(
            "[DestinationButtonController] " +
            "Step 3: Building A* route."
        );

        if (!routeService.TryBuildRoute(
                SelectedDestination))
        {
            Debug.LogWarning(
                "[DestinationButtonController] " +
                "Route could not be built.\n" +
                routeService.LastFailure
            );

            return;
        }


        // -----------------------------------------------------
        // STEP 4
        // ROUTE SUCCESSFULLY BUILT
        // -----------------------------------------------------

        Debug.Log(
            "[DestinationButtonController] " +
            "Step 4: Navigation route successfully built."
        );


        // -----------------------------------------------------
        // STEP 5
        // HIDE HOME PANEL
        // -----------------------------------------------------

        if (arWarmupManager != null)
        {
            Debug.Log(
                "[DestinationButtonController] " +
                "Step 5: Hiding Home Panel."
            );

            arWarmupManager.HideHomePanel();
        }


        // -----------------------------------------------------
        // NAVIGATION BEGINS
        // -----------------------------------------------------

        Debug.Log(
            "[DestinationButtonController] " +
            "Navigation workflow completed."
        );
    }


    // =========================================================
    // FIND BUILDING
    // =========================================================

    private BuildingInfo FindBuilding(int buildingId)
    {
        foreach (BuildingInfo building in buildings)
        {
            if (building != null &&
                building.buildingId == buildingId)
            {
                return building;
            }
        }

        return null;
    }


    // =========================================================
    // FIND NAVNODE
    // =========================================================

    private NavNode FindNavNode(
        string buildingNodeId)
    {
        NavNode[] nodes =
            FindObjectsByType<NavNode>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (NavNode node in nodes)
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
    // LOAD SELECTED BUILDING INFORMATION
    // =========================================================

    private void LoadSelectedBuildingInfo()
    {
        if (SelectedDestination == null)
            return;

        if (buildingInfoPanel == null)
        {
            Debug.LogWarning(
                "[DestinationButtonController] " +
                "BuildingInfoPanelController is not assigned."
            );

            return;
        }

        if (buildingDataService == null)
        {
            Debug.LogError(
                "[DestinationButtonController] " +
                "BuildingDataService is not assigned."
            );

            return;
        }
        // IMPORTANT:
        // Use the Firebase BuildingInfo ID.
        // Do NOT use the NavNode/location ID.
        int buildingId = SelectedBuilding.buildingId;

        if (buildingId < 0)
        {
            Debug.LogWarning(
                $"[DestinationButtonController] " +
                $"NavNode '{SelectedDestination.name}' " +
                $"does not have a valid buildingId."
            );

            return;
        }

        Debug.Log(
        $"[DestinationButtonController] " +
        $"Loading Firebase information for " +
        $"buildingId={buildingId}, " +
        $"buildingName={SelectedBuilding.title}"
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
                        $"[DestinationButtonController] " +
                        $"Firebase returned null BuildingInfo " +
                        $"for buildingId={buildingId}."
                    );

                    return;
                }

                Debug.Log(
                    $"[DestinationButtonController] " +
                    $"Building information loaded: " +
                    $"{info.title}"
                );

                // Store Firebase information in the panel.
                buildingInfoPanel.SetBuildingInfo(info);

                // Same behavior as DestinationDropdown:
                // Do NOT automatically show the panel here.
            },

            // -------------------------------------------------
            // ERROR
            // -------------------------------------------------

            error =>
            {
                Debug.LogWarning(
                    $"[DestinationButtonController] " +
                    $"Failed to load building " +
                    $"{buildingId}: {error}"
                );
            }
        );
    }

    // =========================================================
    // TRAVEL DISTANCE
    // =========================================================

    private void StartTravelDistanceTracking()
    {
        if (SelectedDestination == null)
        {
            Debug.LogWarning(
                "[DestinationButtonController] " +
                "Cannot track distance because " +
                "SelectedDestination is null."
            );

            return;
        }

        // Stop previous distance tracking.
        if (travelDistanceCoroutine != null)
        {
            StopCoroutine(travelDistanceCoroutine);
        }

        lastDisplayedDistance = -1;

        // Display the initial distance immediately.
        UpdateTravelDistance();

        // Start continuous tracking.
        travelDistanceCoroutine =
            StartCoroutine(TrackTravelDistance());

        Debug.Log(
            "[DestinationButtonController] " +
            "Started travel distance tracking."
        );
    }


    private IEnumerator TrackTravelDistance()
    {
        while (SelectedDestination != null)
        {
            UpdateTravelDistance();

            // Check GPS every 0.5 seconds.
            yield return new WaitForSeconds(0.5f);
        }

        travelDistanceCoroutine = null;
    }


        private void UpdateTravelDistance()
    {
        float distance =
            CalculateDistanceToDestination();

        if (distance < 0f)
            return;

        // -----------------------------------------------------
        // ARRIVAL
        // -----------------------------------------------------

        if (distance <= arrivalDistance)
        {
            ShowArrived();
            return;
        }


        // -----------------------------------------------------
        // ROUND TO 5-METER STEPS
        // -----------------------------------------------------

        int displayDistance =
            Mathf.CeilToInt(
                distance / distanceUpdateStep
            ) * Mathf.RoundToInt(distanceUpdateStep);


        // -----------------------------------------------------
        // DO NOT ALLOW THE DISPLAY TO INCREASE
        //
        // This helps prevent GPS fluctuations such as:
        //
        // 20m → 15m → 18m → 15m
        //
        // Instead:
        //
        // 20m → 15m → 15m
        // -----------------------------------------------------

        if (lastDisplayedDistance >= 0 &&
            displayDistance > lastDisplayedDistance)
        {
            displayDistance = lastDisplayedDistance;
        }


        // -----------------------------------------------------
        // UPDATE TEXT ONLY WHEN VALUE CHANGES
        // -----------------------------------------------------

        if (displayDistance != lastDisplayedDistance)
        {
            lastDisplayedDistance = displayDistance;

            if (travelDistanceText != null)
            {
                travelDistanceText.text =
                    $"{displayDistance} m";
            }

            Debug.Log(
                $"[DestinationButtonController] " +
                $"Distance to destination: " +
                $"{displayDistance}m"
            );
        }
    }

    private float CalculateDistanceToDestination()
    {
        if (SelectedDestination == null)
            return -1f;

        // GPS must be enabled.
        if (!Input.location.isEnabledByUser)
            return -1f;

        // GPS must be running.
        if (Input.location.status !=
            LocationServiceStatus.Running)
        {
            return -1f;
        }

        double userLatitude =
            Input.location.lastData.latitude;

        double userLongitude =
            Input.location.lastData.longitude;

        double destinationLatitude =
            SelectedDestination.latitude;

        double destinationLongitude =
            SelectedDestination.longitude;

        return CalculateGpsDistance(
            userLatitude,
            userLongitude,
            destinationLatitude,
            destinationLongitude
        );
    }

    private float CalculateGpsDistance(
    double latitude1,
    double longitude1,
    double latitude2,
    double longitude2)
    {
        const double EarthRadius = 6371000.0;

        double lat1 =
            latitude1 * Mathf.Deg2Rad;

        double lat2 =
            latitude2 * Mathf.Deg2Rad;

        double deltaLat =
            (latitude2 - latitude1) * Mathf.Deg2Rad;

        double deltaLon =
            (longitude2 - longitude1) * Mathf.Deg2Rad;

        double a =
            Math.Sin(deltaLat / 2.0) *
            Math.Sin(deltaLat / 2.0) +
            Math.Cos(lat1) *
            Math.Cos(lat2) *
            Math.Sin(deltaLon / 2.0) *
            Math.Sin(deltaLon / 2.0);

        double c =
            2.0 *
            Math.Atan2(
                Math.Sqrt(a),
                Math.Sqrt(1.0 - a)
            );

        return (float)(EarthRadius * c);
    }

    private void ShowArrived()
    {
        if (travelDistanceText != null)
        {
            travelDistanceText.text =
                "You Arrive";
        }

        Debug.Log(
            "[DestinationButtonController] " +
            "User has arrived at the destination."
        );

        if (travelDistanceCoroutine != null)
        {
            StopCoroutine(travelDistanceCoroutine);
            travelDistanceCoroutine = null;
        }
    }


    // =========================================================
    // CLEAR BUTTONS
    // =========================================================

    public void ClearButtons()
    {
        foreach (DestinationButton button in spawnedButtons)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        spawnedButtons.Clear();
    }

    private void FilterDestinations(string searchText)
    {
        searchText = searchText.Trim();

        foreach (DestinationButton button in spawnedButtons)
        {
            if (button == null)
                continue;

            bool matches = button.MatchesSearch(searchText);

            button.gameObject.SetActive(matches);
        }
    }

    private void OnDestroy()
{
    if (searchInput != null)
    {
        searchInput.onValueChanged.RemoveListener(
            FilterDestinations
        );
    }
}
}