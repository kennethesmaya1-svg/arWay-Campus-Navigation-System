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
    [SerializeField] private LocationServiceManager _locationService;

    [Tooltip("Distance the guide spawns ahead of the user along the route.")]
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

        RouteSpawnPoint spawnPoint = BuildSpawnPointForUser(route, spawnDistanceMeters);

        SpawnGuideOnRoute(route, path, spawnPoint);

        if (guideController == null)
        {
            Debug.LogError(
                "ARGuideManager: Guide controller was not created."
            );
            return;
        }

        // Move toward the next route node after the nearest route node.
        // This keeps the guide walking forward even when the user is
        // already close to the destination.
        int firstTargetIndex = CalculateFirstTargetIndex(
            route,
            spawnPoint.NearestIndex
        );

        guideController.StartGuiding(path, firstTargetIndex);

        UpdateCharacterButton(true);

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
        List<Transform> path,
        RouteSpawnPoint spawnPoint)
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
    /// Spawns the guide relative to the user's current GPS fix.
    ///
    /// Logic:
    /// 1. Find the closest route node to the user.
    /// 2. Use the next node in the route as the forward direction.
    /// 3. Place the guide a configured distance ahead of the user.
    /// 4. If the user is already at the destination, place the guide at the
    ///    destination itself and use the previous route segment for bearing.
    /// </summary>
    private RouteSpawnPoint BuildSpawnPointForUser(
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
                0f,
                0
            );
        }

        if (!TryGetCurrentLocation(out double userLatitude, out double userLongitude))
        {
            Debug.LogWarning(
                "ARGuideManager: No valid GPS fix available. Falling back to route-based spawn positioning."
            );

            return BuildSpawnPoint(route, distanceMeters);
        }

        int nearestIndex = FindNearestRouteIndex(route, userLatitude, userLongitude);
        int forwardIndex = nearestIndex + 1;

        if (forwardIndex >= route.Count)
        {
            NavNode last = route[route.Count - 1];
            NavNode previous = route[route.Count - 2];

            return new RouteSpawnPoint(
                last.latitude,
                last.longitude,
                Bearing(previous, last),
                nearestIndex
            );
        }

        NavNode forwardNode = route[forwardIndex];
        float bearing = Bearing(userLatitude, userLongitude, forwardNode.latitude, forwardNode.longitude);
        (double latitude, double longitude) = MoveTowards(
            userLatitude,
            userLongitude,
            bearing,
            distanceMeters
        );

        return new RouteSpawnPoint(
            latitude,
            longitude,
            bearing,
            nearestIndex
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
                0f,
                0
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
                    bearing,
                    i
                );
            }

            remaining -= segmentLength;
        }

        NavNode last = route[route.Count - 1];
        NavNode previous = route[route.Count - 2];

        return new RouteSpawnPoint(
            last.latitude,
            last.longitude,
            Bearing(previous, last),
            route.Count - 1
        );
    }

    /// <summary>
    /// Returns the first NavNode the character should move toward.
    ///
    /// Example:
    /// route 0 -> 1 -> 2 -> 3
    ///
    /// If the user is nearest to route[2], the guide should start by walking
    /// toward route[3]. If the user is on the final destination, it stays at
    /// the destination and stops.
    /// </summary>
    private static int CalculateFirstTargetIndex(
        IReadOnlyList<NavNode> route,
        int nearestIndex)
    {
        if (route == null || route.Count == 0)
            return 0;

        int nextIndex = nearestIndex + 1;
        return Mathf.Clamp(nextIndex, 0, route.Count - 1);
    }

    private static double LerpDouble(
        double a,
        double b,
        double t)
    {
        return a + (b - a) * t;
    }

    private bool TryGetCurrentLocation(
        out double latitude,
        out double longitude)
    {
        latitude = 0d;
        longitude = 0d;

        if (_locationService == null)
            _locationService = FindFirstObjectByType<LocationServiceManager>();

        if (_locationService == null || !_locationService.IsRunning)
            return false;

        if (!_locationService.HasUsableFix)
            return false;

        LocationInfo location = _locationService.LastLocation;
        latitude = location.latitude;
        longitude = location.longitude;
        return true;
    }

    private static int FindNearestRouteIndex(
        IReadOnlyList<NavNode> route,
        double latitude,
        double longitude)
    {
        int nearestIndex = 0;
        double nearestDistance = double.MaxValue;

        for (int i = 0; i < route.Count; i++)
        {
            NavNode node = route[i];
            if (node == null)
                continue;

            double distance =
                AStarRouteService.HaversineMeters(
                    latitude,
                    longitude,
                    node.latitude,
                    node.longitude
                );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private static (double latitude, double longitude) MoveTowards(
        double startLatitude,
        double startLongitude,
        float bearingDegrees,
        float distanceMeters)
    {
        const double EarthRadiusMeters = 6_371_000d;

        double radiansBearing = bearingDegrees * Math.PI / 180d;
        double distanceRatio = Math.Max(0d, distanceMeters) / EarthRadiusMeters;

        double startLatRadians = startLatitude * Math.PI / 180d;
        double startLonRadians = startLongitude * Math.PI / 180d;

        double destinationLatRadians = Math.Asin(
            Math.Sin(startLatRadians) * Math.Cos(distanceRatio) +
            Math.Cos(startLatRadians) * Math.Sin(distanceRatio) * Math.Cos(radiansBearing)
        );

        double destinationLonRadians = startLonRadians + Math.Atan2(
            Math.Sin(radiansBearing) * Math.Sin(distanceRatio) * Math.Cos(startLatRadians),
            Math.Cos(distanceRatio) - Math.Sin(startLatRadians) * Math.Sin(destinationLatRadians)
        );

        double latitude = destinationLatRadians * 180d / Math.PI;
        double longitude = destinationLonRadians * 180d / Math.PI;

        return (latitude, longitude);
    }

    private static float Bearing(
        NavNode from,
        NavNode to)
    {
        return Bearing(
            from.latitude,
            from.longitude,
            to.latitude,
            to.longitude
        );
    }

    private static float Bearing(
        double fromLatitude,
        double fromLongitude,
        double toLatitude,
        double toLongitude)
    {
        double latitudeA =
            fromLatitude * Math.PI / 180d;

        double latitudeB =
            toLatitude * Math.PI / 180d;

        double longitudeDelta =
            (toLongitude - fromLongitude) *
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
        public readonly int NearestIndex;

        public RouteSpawnPoint(
            double latitude,
            double longitude,
            float bearing,
            int nearestIndex)
        {
            Latitude = latitude;
            Longitude = longitude;
            Bearing = bearing;
            NearestIndex = nearestIndex;
        }
    }
}
