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

    [Header("World Positioning")]
    [SerializeField] private ARWorldPositioningObjectHelper _objectHelper;

    [Tooltip("Distance from the beginning of the computed route where the guide is spawned.")]
    [SerializeField, Min(0f)] private float spawnDistanceMeters = 2f;

    [Tooltip("Same altitude convention used by ArrowRouteRenderer.")]
    [SerializeField] private float spawnAltitudeMeters = 0f;

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

        SpawnGuideOnRoute(route, path);

        if (guideController == null)
        {
            Debug.LogError(
                "ARGuideManager: Guide controller was not created."
            );
            return;
        }

        // Start at the route node immediately after the guide's
        // spawn position. This prevents the guide from walking
        // backward to route[0] after being spawned a few meters
        // along the route.
        int firstTargetIndex = CalculateFirstTargetIndex(
            route,
            spawnDistanceMeters
        );

        guideController.StartGuiding(path, firstTargetIndex);

        UpdateCharacterButton(true);

        Debug.Log(
            $"ARGuideManager: A* route received. " +
            $"Guide spawned on route at {spawnDistanceMeters:0.##}m " +
            $"from RouteStart. Nodes = {path.Count}"
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

        if (route == null || route.Count < 2)
            return;

        if (_objectHelper == null)
        {
            Debug.LogError(
                "ARGuideManager: Cannot position guide because " +
                "ARWorldPositioningObjectHelper is missing."
            );
            return;
        }

        RouteSpawnPoint spawnPoint =
            BuildSpawnPoint(route, spawnDistanceMeters);

        Quaternion rotation = Quaternion.Euler(
            0f,
            spawnPoint.Bearing,
            0f
        );

        activeGuide = Instantiate(
            guideCharacterPrefab,
            Vector3.zero,
            rotation,
            transform
        );

        guideController =
            activeGuide.GetComponent<ARGuideCharacter>();

        if (guideController == null)
        {
            guideController =
                activeGuide.AddComponent<ARGuideCharacter>();
        }

        // IMPORTANT:
        // Do NOT use cameraTransform.position here.
        // The ObjectHelper places the character at the
        // latitude/longitude of the route.
        _objectHelper.AddOrUpdateObject(
            activeGuide,
            spawnPoint.Latitude,
            spawnPoint.Longitude,
            spawnAltitudeMeters,
            rotation
        );

        activeGuide.SetActive(true);

        Debug.Log(
            $"ARGuideManager: Guide positioned on A* route. " +
            $"Lat={spawnPoint.Latitude:F8}, " +
            $"Lon={spawnPoint.Longitude:F8}, " +
            $"Bearing={spawnPoint.Bearing:F1}°"
        );
    }

    /// <summary>
    /// Finds the GPS position a specified distance along the
    /// computed route, starting at route[0].
    ///
    /// This is the GPS equivalent of moving forward along
    /// the route before spawning the character.
    /// </summary>
    private static RouteSpawnPoint BuildSpawnPoint(
        IReadOnlyList<NavNode> route,
        float distanceMeters)
    {
        if (route == null || route.Count == 0)
            return default;

        if (route.Count == 1)
        {
            return new RouteSpawnPoint(
                route[0].latitude,
                route[0].longitude,
                0f
            );
        }

        double remaining = Mathf.Max(0f, distanceMeters);

        for (int i = 0; i < route.Count - 1; i++)
        {
            NavNode from = route[i];
            NavNode to = route[i + 1];

            if (from == null || to == null)
                continue;

            double segmentLength =
                AStarRouteService.HaversineMeters(
                    from.latitude,
                    from.longitude,
                    to.latitude,
                    to.longitude
                );

            if (segmentLength <= 0.0001d)
                continue;

            float bearing = Bearing(from, to);

            if (remaining <= segmentLength)
            {
                double fraction = remaining / segmentLength;

                double latitude = LerpDouble(
                    from.latitude,
                    to.latitude,
                    fraction
                );

                double longitude = LerpDouble(
                    from.longitude,
                    to.longitude,
                    fraction
                );

                return new RouteSpawnPoint(
                    latitude,
                    longitude,
                    bearing
                );
            }

            remaining -= segmentLength;
        }

        // If spawnDistance is longer than the whole route,
        // place the guide at the destination.
        NavNode last = route[route.Count - 1];
        NavNode previous = route[route.Count - 2];

        return new RouteSpawnPoint(
            last.latitude,
            last.longitude,
            Bearing(previous, last)
        );
    }

    /// <summary>
    /// Returns the first NavNode the character should move toward.
    ///
    /// Example:
    /// route 0 -> 1 -> 2 -> 3
    ///
    /// Guide is spawned 2m after route 0.
    /// The first movement target is route 1.
    /// </summary>
    private static int CalculateFirstTargetIndex(
        IReadOnlyList<NavNode> route,
        float distanceMeters)
    {
        if (route == null || route.Count <= 1)
            return 0;

        double remaining = Mathf.Max(0f, distanceMeters);

        for (int i = 0; i < route.Count - 1; i++)
        {
            NavNode from = route[i];
            NavNode to = route[i + 1];

            if (from == null || to == null)
                continue;

            double segmentLength =
                AStarRouteService.HaversineMeters(
                    from.latitude,
                    from.longitude,
                    to.latitude,
                    to.longitude
                );

            if (segmentLength <= 0.0001d)
                continue;

            if (remaining <= segmentLength)
                return i + 1;

            remaining -= segmentLength;
        }

        return route.Count - 1;
    }

    private static double LerpDouble(
        double a,
        double b,
        double t)
    {
        return a + (b - a) * t;
    }

    private static float Bearing(
        NavNode from,
        NavNode to)
    {
        double latitudeA =
            from.latitude * Math.PI / 180d;

        double latitudeB =
            to.latitude * Math.PI / 180d;

        double longitudeDelta =
            (to.longitude - from.longitude) *
            Math.PI / 180d;

        double y =
            Math.Sin(longitudeDelta) *
            Math.Cos(latitudeB);

        double x =
            Math.Cos(latitudeA) *
            Math.Sin(latitudeB) -
            Math.Sin(latitudeA) *
            Math.Cos(latitudeB) *
            Math.Cos(longitudeDelta);

        return (float)(
            (Math.Atan2(y, x) *
             180d / Math.PI + 360d) % 360d
        );
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
            Debug.LogWarning(
            
    "ARGuideManager: Cannot toggle character because " +
                "no destination has been selected."
            );
            return;
        }

        if (_routeService == null || !_routeService.HasRoute)
        {
            Debug.LogWarning(
                "ARGuideManager: Cannot toggle character because " +
                "there is no active A* route."
            );
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

            return;
        }

        // ---------------------------------------------------------
        // CHARACTER IS HIDDEN / DOES NOT EXIST
        // → SPAWN CHARACTER AGAIN ON A* ROUTE
        // ---------------------------------------------------------

        Debug.Log(
            "ARGuideManager: Spawning character again " +
            "on the current A* route."
        );

        OnRouteComputed(_routeService.CurrentRoute);

        if (activeGuide != null)
        {
            activeGuide.SetActive(true);

            UpdateCharacterButton(true);
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

    private readonly struct RouteSpawnPoint
    {
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly float Bearing;

        public RouteSpawnPoint(
            double latitude,
            double longitude,
            float bearing)
        {
            Latitude = latitude;
            Longitude = longitude;
            Bearing = bearing;
        }
    }
}
