using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Niantic.Lightship.AR.WorldPositioning;

/// <summary>
/// Controls the AR guide character from the A* route.
///
/// Workflow:
/// 1. DestinationDropdown selects a destination.
/// 2. AStarRouteService builds the route.
/// 3. AStarRouteService raises OnRouteComputed.
/// 4. ARGuideManager receives the route.
/// 5. The guide is placed using the same
///    ARWorldPositioningObjectHelper approach as ArrowRouteRenderer.
/// 6. The character starts moving along the computed route.
///
/// The guide is NOT spawned from the camera position.
/// Its world position comes from the route latitude/longitude.
/// </summary>
public class ARGuideManager : MonoBehaviour
{
    [Header("Guide")]
    [SerializeField] private GameObject guideCharacterPrefab;

    [Header("Character Guide UI")]
    [SerializeField] private Image CharacterBtnImage;
    [SerializeField] private Text CharacterBtnLabel;
    [SerializeField] private Sprite CharacterON;
    [SerializeField] private Sprite CharacterOFF;
    [SerializeField] private GameObject travelUI;

    [Header("World Positioning")]
    [SerializeField] private ARWorldPositioningObjectHelper _objectHelper;
    [SerializeField] private LocationServiceManager _locationService;

    [Tooltip("Distance the guide spawns ahead of the user along the route.")]
    [SerializeField, Min(0f)] private float spawnDistanceMeters = 2f;

    [Tooltip("Same altitude convention used by ArrowRouteRenderer.")]
    [SerializeField] private float characterYOffset = 0;

    [Header("Route")]
    [SerializeField] private AStarRouteService _routeService;

    private GameObject activeGuide;
    private ARGuideCharacter guideController;

    private bool destinationSelected;

    public bool DestinationSelected => destinationSelected;
    public bool HasGuide => activeGuide != null;

    private void Awake()
    {
        if (_routeService == null)
            _routeService = FindFirstObjectByType<AStarRouteService>();

        if (_objectHelper == null)
            _objectHelper = FindFirstObjectByType<ARWorldPositioningObjectHelper>();

        if (_locationService == null)
            _locationService = FindFirstObjectByType<LocationServiceManager>();
    }

    private void OnEnable()
    {
        if (_routeService == null)
            return;

        _routeService.OnRouteComputed += OnRouteComputed;
        _routeService.OnRouteCleared += OnRouteCleared;

        // Useful if this object is enabled after a route already exists.
        if (_routeService.HasRoute)
            OnRouteComputed(_routeService.CurrentRoute);
    }

    private void OnDisable()
    {
        if (_routeService != null)
        {
            _routeService.OnRouteComputed -= OnRouteComputed;
            _routeService.OnRouteCleared -= OnRouteCleared;
        }

        RemoveGuide(false);
    }

    /// <summary>
    /// Called when AStarRouteService successfully computes a route.
    /// </summary>
    private void OnRouteComputed(IReadOnlyList<NavNode> route)
    {
        if (route == null || route.Count < 2)
        {
            Debug.LogWarning(
                "ARGuideManager: Cannot spawn guide. " +
                "The computed route contains fewer than 2 nodes."
            );

            RemoveGuide();
            return;
        }

        if (guideCharacterPrefab == null)
        {
            Debug.LogError(
                "ARGuideManager: Guide Character Prefab is not assigned."
            );
            return;
        }

        if (_objectHelper == null)
        {
            Debug.LogError(
                "ARGuideManager: ARWorldPositioningObjectHelper is not assigned."
            );
            return;
        }

        List<Transform> path = BuildPath(route);

        if (path.Count < 2)
        {
            Debug.LogWarning(
                "ARGuideManager: Computed route does not contain enough valid NavNodes."
            );
            return;
        }

        destinationSelected = true;

        // The guide always starts 2 meters in front of the AR user/camera.
        // It no longer depends on GPS being available for its spawn position.
        SpawnGuideOnRoute(route, path);

        if (guideController == null)
        {
            Debug.LogError(
                "ARGuideManager: Guide controller was not created."
            );
            return;
        }

        // Find the route node closest to the actual spawned character position,
        // then continue toward the next node. This also works when GPS is unavailable.
        int firstTargetIndex = FindNearestPathIndex(path, activeGuide.transform.position);
        firstTargetIndex = Mathf.Clamp(
            firstTargetIndex + 1,
            0,
            path.Count - 1
        );

        guideController.StartGuiding(path, firstTargetIndex);

        UpdateCharacterButton(true);
        if (travelUI != null)
            travelUI.SetActive(false);

        Debug.Log(
            $"ARGuideManager: A* route received. " +
            $"Guide spawned {spawnDistanceMeters:0.##}m ahead of the user " +
            $"along the route. Nodes = {path.Count}"
        );
    }

    /// <summary>
    /// Converts the NavNode route to the Transform path used
    /// by ARGuideCharacter for movement.
    /// </summary>
    private static List<Transform> BuildPath(
        IReadOnlyList<NavNode> route)
    {
        List<Transform> path = new();

        foreach (NavNode node in route)
        {
            if (node != null && node.transform != null)
                path.Add(node.transform);
        }

        return path;
    }

    /// <summary>
    /// Spawns the character using GPS coordinates, matching
    /// the positioning approach used by ArrowRouteRenderer.
    ///
    /// ArrowRouteRenderer does:
    ///
    /// _objectHelper.AddOrUpdateObject(
    ///     arrow,
    ///     latitude,
    ///     longitude,
    ///     0f,
    ///     rotation);
    ///
    /// The guide uses the same idea.
    /// </summary>
    private void SpawnGuideOnRoute(
        IReadOnlyList<NavNode> route,
        List<Transform> path)
    {
        RemoveGuide(false);

        if (route == null || route.Count < 2 || path == null || path.Count < 2)
            return;

        Camera arCamera = Camera.main;

        if (arCamera == null)
        {
            Debug.LogError(
                "ARGuideManager: Main AR Camera was not found. Cannot spawn guide 2m ahead of user."
            );
            return;
        }

        // Spawn exactly 2 meters ahead of the user on the horizontal plane.
        Vector3 forward = arCamera.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        forward.Normalize();

        Vector3 spawnPosition = arCamera.transform.position + forward * spawnDistanceMeters;

        // Find the route node closest to the character's spawn position.
        int nearestIndex = FindNearestPathIndex(path, spawnPosition);
        // Match the character's initial Y to the route/arrow level.
        spawnPosition.y = path[nearestIndex].position.y + characterYOffset;  

        // Face the character toward the next route node.
        int targetIndex = Mathf.Clamp(nearestIndex + 1, 0, path.Count - 1);

        Vector3 lookDirection = path[targetIndex].position - spawnPosition;
        lookDirection.y = 0f;

        Quaternion rotation = lookDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
            : arCamera.transform.rotation;

        activeGuide = Instantiate(
            guideCharacterPrefab,
            spawnPosition,
            rotation,
            transform
        );

        guideController =
            activeGuide.GetComponent<ARGuideCharacter>();

        if (guideController == null)
            guideController = activeGuide.AddComponent<ARGuideCharacter>();

        activeGuide.SetActive(true);

        Debug.Log(
            $"ARGuideManager: Guide spawned {spawnDistanceMeters:0.##}m ahead of the user. " +
            $"GPS is not required for spawning. Nearest route index = {nearestIndex}."
        );
    }

    private static int FindNearestPathIndex(
        List<Transform> path,
        Vector3 worldPosition)
    {
        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < path.Count; i++)
        {
            if (path[i] == null)
                continue;

            Vector3 nodePosition = path[i].position;
            nodePosition.y = worldPosition.y;

            float distance =
                (nodePosition - worldPosition).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }


    private void OnRouteCleared()
    {
        RemoveGuide();
        Debug.Log(
            "ARGuideManager: A* route cleared. Guide removed."
        );
    }

    /// <summary>
    /// Compatibility method for existing UI/code.
    /// Normally route events control the guide automatically.
    /// </summary>
  
    public void CharacterButtonClicked()
    {
        if (!destinationSelected)
        {
            Debug.LogWarning("ARGuideManager: Cannot toggle character because " + "no destination has been selected.");
            return;
        }

        if (_routeService == null || !_routeService.HasRoute)
        {
            Debug.LogWarning("ARGuideManager: Cannot toggle character because " + "there is no active A* route.");
            return;
        }

        // ---------------------------------------------------------
        // CHARACTER EXISTS AND IS VISIBLE
        // → HIDE CHARACTER
        // ---------------------------------------------------------

        if (activeGuide != null && activeGuide.activeSelf)
        {
            activeGuide.SetActive(false);

            UpdateCharacterButton(false);

            Debug.Log(
                "ARGuideManager: Character hidden."
            );
            //show the travel distance if character is hidden
            if (travelUI != null)
                travelUI.SetActive(true);

            return;
        }

        // ---------------------------------------------------------
        // CHARACTER IS HIDDEN / DOES NOT EXIST
        // → SPAWN CHARACTER AGAIN ON A* ROUTE
        // ---------------------------------------------------------

        Debug.Log("ARGuideManager: Spawning character again " + "on the current A* route.");

        OnRouteComputed(_routeService.CurrentRoute);

        if (activeGuide != null)
        {
            activeGuide.SetActive(true);

            UpdateCharacterButton(true);
            //hide the travel distance if character is shown
            if (travelUI != null)
                travelUI.SetActive(false);
        }
    }

    /// <summary>
    /// Compatibility method used by DestinationDropdown.
    /// </summary>
    public void SetDestinationSelected(bool selected)
    {
        destinationSelected = selected;

        if (!selected)
        {
            RemoveGuide();
        }

        Debug.Log(
            $"ARGuideManager: Destination selected = {destinationSelected}"
        );
    }

    /// <summary>
    /// Compatibility method for scripts that already call StartGuide().
    ///
    /// The preferred workflow is:
    /// DestinationDropdown -> AStarRouteService -> OnRouteComputed.
    /// </summary>
    public void StartGuide(List<Transform> path)
    {
        if (path == null || path.Count < 2)
        {
            Debug.LogWarning(
                "ARGuideManager: Cannot start guide. " +
                "No valid route path was provided."
            );
            return;
        }

        Debug.Log(
            "ARGuideManager: StartGuide(List<Transform>) is deprecated. " +
            "Use AStarRouteService.OnRouteComputed instead."
        );

        // This method intentionally does not create a second
        // positioning system. The A* route event is the source
        // of truth for guide spawning.
    }

    /// <summary>
    /// Removes the active guide.
    /// </summary>
    public void RemoveGuide()
    {
        RemoveGuide(true);
    }

    private void RemoveGuide(bool resetDestination)
    {
        if (guideController != null)
            guideController.StopGuiding();

        if (activeGuide != null)
            Destroy(activeGuide);

        activeGuide = null;
        guideController = null;

        if (resetDestination)
            destinationSelected = false;

        UpdateCharacterButton(false);
    }

    private void UpdateCharacterButton(bool isVisible)
    {
        if (CharacterBtnImage != null)
        {
            CharacterBtnImage.sprite =
                isVisible ? CharacterON : CharacterOFF;
        }

        if (CharacterBtnLabel != null)
        {
            CharacterBtnLabel.text =
                isVisible
                    ? "Hide Character"
                    : "Show Character";
        }
    }


}
