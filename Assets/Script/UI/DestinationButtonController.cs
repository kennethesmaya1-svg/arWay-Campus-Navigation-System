using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates destination buttons from Firestore building records
/// and maps each selected building to its physical Unity NavNode.
/// </summary>
public class DestinationButtonController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private DestinationButton destinationButtonPrefab;
    [SerializeField] private Transform destinationContent;

    [Header("Data")]
    [SerializeField] private BuildingDataService buildingDataService;

    [Header("Navigation")]
    [SerializeField] private AStarRouteService routeService;

    private readonly List<DestinationButton> spawnedButtons =
        new List<DestinationButton>();

    private readonly List<BuildingInfo> buildings =
        new List<BuildingInfo>();

    public BuildingInfo SelectedBuilding { get; private set; }

    public NavNode SelectedDestination { get; private set; }


    // =========================================================
    // UNITY
    // =========================================================

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

        Debug.Log($"[Time Check] 1. Requesting Firebase data at: {Time.realtimeSinceStartup}");

    }


    // =========================================================
    // BUILDINGS LOADED
    // =========================================================

    private void OnBuildingsLoaded(List<BuildingInfo> result)
    {
        buildings.Clear();

        Debug.Log($"[Time Check] 2. Firebase data received. Starting to clone buttons at: {Time.realtimeSinceStartup}");

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

            // Clone the destination button.
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

        // Destination metadata is already available, so render the list
        // immediately. Image loading continues independently in the
        // background and does not delay button creation.
        buildingDataService.LoadBuildingImagesInBackground(
            buildings
        );
    }


    // =========================================================
    // LOAD ERROR
    // =========================================================

    private void OnBuildingsLoadFailed(string error)
    {
        Debug.LogError("[DestinationButtonController] " + "Failed to load buildings: " + error);
    }

    // =========================================================
    // BUTTON CLICK
    // =========================================================

    public void SelectDestination(int buildingId, string buildingNodeId)
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
        // BUILD A* ROUTE
        // -----------------------------------------------------

        if (routeService != null)
        {
            routeService.TryBuildRoute(destination);
        }
        else
        {
            Debug.LogWarning(
                "[DestinationButtonController] " +
                "AStarRouteService is not assigned. " +
                "Destination was selected but route was not built."
            );
        }
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
        NavNode[] nodes = FindObjectsByType<NavNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);

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
    // CLEAR BUTTONS
    // =========================================================

    public void ClearButtons()
    {
        foreach (DestinationButton button in spawnedButtons)
        {
            if (button != null)
                Destroy(button.gameObject);
        }

        spawnedButtons.Clear();
    }
}