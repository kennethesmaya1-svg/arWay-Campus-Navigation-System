using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Calculates shortest campus routes over the NavNode graph.</summary>
public class AStarRouteService : MonoBehaviour
{
    private const double EarthRadiusMeters = 6_371_000d;

    [SerializeField] private NodeManager _nodeManager;
    [SerializeField] private LocationServiceManager _locationService;

    private readonly List<NavNode> _currentRoute = new();

    public IReadOnlyList<NavNode> CurrentRoute => _currentRoute;
    public bool HasRoute => _currentRoute.Count > 0;
    public NavNode RouteStart { get; private set; }
    public NavNode RouteDestination { get; private set; }
    public string LastFailure { get; private set; }

    public event Action<IReadOnlyList<NavNode>> OnRouteComputed;
    public event Action OnRouteCleared;

    private void Awake()
    {
        if (_nodeManager == null)
            _nodeManager = FindFirstObjectByType<NodeManager>();
        if (_locationService == null)
            _locationService = FindFirstObjectByType<LocationServiceManager>();
    }

    /// <summary>Builds a route from the live GPS fix to a selected destination.</summary>
    public bool TryBuildRoute(NavNode destination)
    {
        ClearRoute();
        if (destination == null)
            return Fail("No destination was selected.");
        if (_nodeManager == null || !_nodeManager.IsNavigationGraphReady)
            return Fail("The campus navigation graph is not ready.");
        if (!TryGetCurrentLocation(out double latitude, out double longitude))
            return false;

        NavNode start = FindNearestNode(_nodeManager.PathNodes, latitude, longitude);
        if (start == null)
            return Fail("No pathway node is available near the current location.");

        if (!TryFindPath(start, destination, out List<NavNode> route))
            return Fail("No connected route exists to this destination.");

        RouteStart = start;
        RouteDestination = destination;
        _currentRoute.AddRange(route);
        LastFailure = null;
        OnRouteComputed?.Invoke(_currentRoute);
        return true;
    }

    public bool TryBuildRouteToBuilding(int buildingId)
    {
        if (_nodeManager == null)
            _nodeManager = FindFirstObjectByType<NodeManager>();

        if (_nodeManager != null)
        {
            foreach (NavNode node in _nodeManager.NavigationNodes)
            {
                if (node != null && node.isDestination && node.buildingId == buildingId)
                    return TryBuildRoute(node);
            }
        }

        return Fail($"Destination building {buildingId} was not found.");
    }

    public void ClearRoute()
    {
        bool hadRoute = _currentRoute.Count > 0;
        _currentRoute.Clear();
        RouteStart = null;
        RouteDestination = null;
        if (hadRoute)
            OnRouteCleared?.Invoke();
    }

    public bool TryFindPath(NavNode start, NavNode destination, out List<NavNode> route)
    {
        route = null;
        if (start == null || destination == null)
            return false;

        List<NavNode> openSet = new() { start };
        HashSet<NavNode> closedSet = new();
        Dictionary<NavNode, float> gScore = new() { [start] = 0f };
        Dictionary<NavNode, NavNode> cameFrom = new();

        while (openSet.Count > 0)
        {
            NavNode current = RemoveBestNode(openSet, gScore, destination);
            if (current == destination)
            {
                route = ReconstructRoute(cameFrom, current);
                return true;
            }

            closedSet.Add(current);
            foreach (NavNode neighbor in current.neighbors)
            {
                if (neighbor == null || closedSet.Contains(neighbor))
                    continue;

                float tentativeGScore = gScore[current] + DistanceMeters(current, neighbor);
                if (!gScore.TryGetValue(neighbor, out float knownGScore) ||
                    tentativeGScore < knownGScore)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    neighbor.parent = current;
                    neighbor.gCost = tentativeGScore;
                    neighbor.hCost = DistanceMeters(neighbor, destination);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return false;
    }

    public static float DistanceMeters(NavNode from, NavNode to) =>
        HaversineMeters(from.latitude, from.longitude, to.latitude, to.longitude);

    public static float HaversineMeters(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        double latitudeDelta = DegreesToRadians(latitudeB - latitudeA);
        double longitudeDelta = DegreesToRadians(longitudeB - longitudeA);
        double latitudeARadians = DegreesToRadians(latitudeA);
        double latitudeBRadians = DegreesToRadians(latitudeB);
        double sinLatitude = Math.Sin(latitudeDelta / 2d);
        double sinLongitude = Math.Sin(longitudeDelta / 2d);
        double a = sinLatitude * sinLatitude +
                   Math.Cos(latitudeARadians) * Math.Cos(latitudeBRadians) *
                   sinLongitude * sinLongitude;
        return (float)(2d * EarthRadiusMeters * Math.Asin(Math.Min(1d, Math.Sqrt(a))));
    }

    private bool TryGetCurrentLocation(out double latitude, out double longitude)
    {
        latitude = 0d;
        longitude = 0d;
        if (_locationService != null && !_locationService.HasUsableFix)
            return Fail("GPS is still acquiring a usable location fix.");
        if (_locationService == null || !_locationService.IsRunning)
            return Fail("GPS is not running.");

        if (!_locationService.HasUsableFix)
            return Fail("GPS did not provide a horizontal accuracy value.");

        LocationInfo location = _locationService.LastLocation;
        latitude = location.latitude;
        longitude = location.longitude;
        return true;
    }

    private static NavNode FindNearestNode(IReadOnlyList<NavNode> nodes, double latitude, double longitude)
    {
        NavNode nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (NavNode node in nodes)
        {
            if (node == null)
                continue;

            float distance = HaversineMeters(latitude, longitude, node.latitude, node.longitude);
            if (distance < nearestDistance)
            {
                nearest = node;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private static NavNode RemoveBestNode(List<NavNode> openSet, Dictionary<NavNode, float> gScore, NavNode destination)
    {
        int bestIndex = 0;
        float bestScore = float.PositiveInfinity;
        for (int index = 0; index < openSet.Count; index++)
        {
            NavNode candidate = openSet[index];
            float score = gScore[candidate] + DistanceMeters(candidate, destination);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = index;
            }
        }

        NavNode best = openSet[bestIndex];
        openSet.RemoveAt(bestIndex);
        return best;
    }

    private static List<NavNode> ReconstructRoute(Dictionary<NavNode, NavNode> cameFrom, NavNode destination)
    {
        List<NavNode> route = new() { destination };
        while (cameFrom.TryGetValue(route[route.Count - 1], out NavNode previous))
            route.Add(previous);

        route.Reverse();
        return route;
    }

    private bool Fail(string message)
    {
        LastFailure = message;
        Debug.LogWarning($"AStarRouteService: {message}");
        return false;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
