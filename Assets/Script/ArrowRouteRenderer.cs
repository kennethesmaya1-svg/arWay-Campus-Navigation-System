using System;
using System.Collections.Generic;
using UnityEngine;
using Niantic.Lightship.AR.WorldPositioning;

/// <summary>
/// Renders the active A* route as evenly spaced, forward-facing arrows.
/// The first arrow is always anchored to the route's nearest path node.
/// </summary>
public class ArrowRouteRenderer : MonoBehaviour
{
    [SerializeField] private AStarRouteService _routeService;
    [SerializeField] private LocationServiceManager _locationService;
    [SerializeField] private ARWorldPositioningObjectHelper _objectHelper;
    [SerializeField] private GameObject _arrowPrefab;
    [SerializeField, Min(1f)] private float _arrowSpacingMeters = 5f;
    [SerializeField, Min(0.1f)] private float _refreshIntervalSeconds = 2f;

    private readonly List<GameObject> _arrows = new();
    private float _refreshTimer;
    private bool _missingReferenceWarningShown;

    private void Awake()
    {
        if (_routeService == null)
            _routeService = FindFirstObjectByType<AStarRouteService>();
        if (_locationService == null)
            _locationService = FindFirstObjectByType<LocationServiceManager>();
        if (_objectHelper == null)
            _objectHelper = FindFirstObjectByType<ARWorldPositioningObjectHelper>();
    }

    private void OnEnable()
    {
        if (_routeService != null)
        {
            _routeService.OnRouteComputed += RenderRoute;
            _routeService.OnRouteCleared += ClearArrows;
        }
        if (_routeService != null && _routeService.HasRoute)
            RenderRoute(_routeService.CurrentRoute);
    }

    private void OnDisable()
    {
        if (_routeService != null)
        {
            _routeService.OnRouteComputed -= RenderRoute;
            _routeService.OnRouteCleared -= ClearArrows;
        }

        ClearArrows();
    }

    private void Update()
    {
        if (_routeService == null || !_routeService.HasRoute)
            return;

        _refreshTimer += Time.deltaTime;
        if (_refreshTimer < _refreshIntervalSeconds)
            return;

        _refreshTimer = 0f;
        if (_routeService.RouteDestination != null)
            _routeService.TryBuildRoute(_routeService.RouteDestination);
    }

    private void RenderRoute(IReadOnlyList<NavNode> route)
    {
        ClearArrows();

        if (route == null || route.Count < 2)
            return;

        if (_arrowPrefab == null || _objectHelper == null)
        {
            WarnMissingReferences();
            return;
        }

        List<RoutePoint> points = BuildArrowPoints(route);
        for (int index = 0; index < points.Count; index++)
        {
            RoutePoint point = points[index];
            GameObject arrow = Instantiate(_arrowPrefab, transform);
            arrow.name = $"RouteArrow_{index:D3}";
            // arrow.transform.rotation = Quaternion.Euler(0f, point.Bearing, 0f);
            // Rotate arrow so its TIP points toward the destination
            float arrowBearing = point.Bearing + 180f;

            arrow.transform.rotation = Quaternion.Euler(
                0f,
                arrowBearing,
                0f
            );
            _objectHelper.AddOrUpdateObject(
                arrow,
                point.Latitude,
                point.Longitude,
                0f,
                arrow.transform.rotation);
            _arrows.Add(arrow);
        }
    }

    private List<RoutePoint> BuildArrowPoints(IReadOnlyList<NavNode> route)
    {
        var points = new List<RoutePoint> { CreatePoint(route[0], route[1]) };
        double distanceUntilNextArrow = _arrowSpacingMeters;

        for (int index = 0; index < route.Count - 1; index++)
        {
            NavNode from = route[index];
            NavNode to = route[index + 1];
            double segmentLength = AStarRouteService.HaversineMeters(
                from.latitude,
                from.longitude,
                to.latitude,
                to.longitude);
            double segmentOffset = 0d;

            while (segmentOffset + distanceUntilNextArrow < segmentLength)
            {
                segmentOffset += distanceUntilNextArrow;
                double fraction = segmentOffset / segmentLength;
                double latitude = Mathf.Lerp(
                    (float)from.latitude,
                    (float)to.latitude,
                    (float)fraction);
                double longitude = Mathf.Lerp(
                    (float)from.longitude,
                    (float)to.longitude,
                    (float)fraction);
                points.Add(new RoutePoint(latitude, longitude, Bearing(from, to)));
                distanceUntilNextArrow = _arrowSpacingMeters;
            }

            distanceUntilNextArrow -= segmentLength - segmentOffset;
            if (distanceUntilNextArrow <= 0d)
                distanceUntilNextArrow = _arrowSpacingMeters;
        }

        return points;
    }

    private static RoutePoint CreatePoint(NavNode from, NavNode to) =>
        new(from.latitude, from.longitude, Bearing(from, to));

    private static float Bearing(NavNode from, NavNode to)
    {
        double latitudeA = from.latitude * Math.PI / 180d;
        double latitudeB = to.latitude * Math.PI / 180d;
        double longitudeDelta = (to.longitude - from.longitude) * Math.PI / 180d;
        double y = Math.Sin(longitudeDelta) * Math.Cos(latitudeB);
        double x = Math.Cos(latitudeA) * Math.Sin(latitudeB) -
                   Math.Sin(latitudeA) * Math.Cos(latitudeB) * Math.Cos(longitudeDelta);
        return (float)((Math.Atan2(y, x) * 180d / Math.PI + 360d) % 360d);
    }

    private void ClearArrows()
    {
        foreach (GameObject arrow in _arrows)
        {
            if (arrow != null)
                Destroy(arrow);
        }

        _arrows.Clear();
    }

    private void WarnMissingReferences()
    {
        if (_missingReferenceWarningShown)
            return;

        _missingReferenceWarningShown = true;
        Debug.LogError(
            "ArrowRouteRenderer requires an arrow prefab and " +
            "ARWorldPositioningObjectHelper.");
    }

    private readonly struct RoutePoint
    {
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly float Bearing;

        public RoutePoint(double latitude, double longitude, float bearing)
        {
            Latitude = latitude;
            Longitude = longitude;
            Bearing = bearing;
        }
    }
}
