using System.Collections.Generic;
using Niantic.Lightship.AR.WorldPositioning;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.Serialization;

public class NodeManager : MonoBehaviour
{
    [Header("Localization")]
    [SerializeField] private float _stableSeconds = 10f;
    [SerializeField] private float _gpsAccuracyThreshold = 8f;

    [SerializeField] private List<Material> _materials = new();
    [SerializeField] private List<GameObject> _possibleObjectPlacer = new();
    [SerializeField] private GameObject _destinationMarkerPrefab;
    [SerializeField] private List<LatLong> _latlong = new();
    [SerializeField] private GeoPathNodeBuilder _pathBuilder;
    [SerializeField] private DestinationNodeManager _destinationManager;
    [SerializeField] private LocationServiceManager _locationService;
    [SerializeField] private ARWorldPositioningManager _positioningManager;
    [SerializeField] private ARWorldPositioningObjectHelper _objectHelper;

    private readonly List<GameObject> _instantiatedObjects = new();
    private readonly List<NavNode> _pathNodes = new();
    private readonly List<NavNode> _navigationNodes = new();
    private GameObject _activeDestinationMarker;
    private float _stableDuration;
    private bool _pathGraphReady;
    private bool _navigationGraphReady;

    public IReadOnlyList<NavNode> NavigationNodes => _navigationNodes;
    public IReadOnlyList<NavNode> PathNodes => _pathNodes;
    public bool HasPathNodes => _pathNodes.Count > 0;
    public bool IsPathGraphReady => _pathGraphReady;
    public bool IsNavigationGraphReady => _navigationGraphReady;
    public NavNode ActiveDestination { get; private set; }
    /// <summary>Raised when path nodes have been built and connected.</summary>
    public event System.Action OnPathGraphReady;
    /// <summary>Raised only when the path and destination graphs are complete.</summary>
    public event System.Action OnPathNodesReady;

    public float StableSeconds => _stableSeconds;
    public float GpsAccuracyThreshold => _gpsAccuracyThreshold;
    public float StableDuration => _stableDuration;
    public float GpsAccuracy =>
        _locationService != null && _locationService.HasUsableFix
            ? _locationService.HorizontalAccuracy
            : -1f;
    public bool IsWpsStable => _positioningManager != null && _positioningManager.IsAvailable;

    private void Start()
    {
        if (_locationService != null)
            _locationService.StartLocationService();

        if (_pathBuilder != null)
        {
            if (_objectHelper == null || _possibleObjectPlacer.Count == 0)
            {
                Debug.LogError(
                    "NodeManager requires an object helper and at least one object prefab.");
                enabled = false;
                return;
            }

            _pathBuilder.OnPathNodesBuilt += InitializeNavigationGraph;
            if (_destinationManager != null)
                _destinationManager.OnDestinationsReady += InitializeNavigationGraph;

            InitializeNavigationGraph();
            return;
        }

        if (_objectHelper == null || _positioningManager == null)
        {
            Debug.LogError("NodeManager requires both an object helper and a positioning manager.");
            enabled = false;
            return;
        }

        if (_possibleObjectPlacer.Count == 0)
        {
            Debug.LogError("NodeManager requires at least one object prefab to place.");
            enabled = false;
            return;
        }

        for (var index = 0; index < _latlong.Count; index++)
        {
            var gpsCoord = _latlong[index];
            var objectToPlace = _possibleObjectPlacer[index % _possibleObjectPlacer.Count];
            if (objectToPlace == null)
            {
                Debug.LogError($"NodeManager has a missing prefab at index {index % _possibleObjectPlacer.Count}.");
                continue;
            }

            var newObject = Instantiate(objectToPlace);
            _instantiatedObjects.Add(newObject);
            _objectHelper.AddOrUpdateObject(
                newObject,
                gpsCoord.latitude,
                gpsCoord.longitude,
                0f,
                Quaternion.identity);

            Debug.Log(
                $"Added {newObject.name} with latitude {gpsCoord.latitude} and longitude {gpsCoord.longitude}.");
        }

        _positioningManager.OnStatusChanged += OnStatusChanged;
    }

    private void InitializeNavigationGraph()
    {
        if (_pathBuilder == null || !_pathBuilder.HasBuilt)
            return;

        if (!_pathGraphReady)
        {
            BuildPathNodes(_pathBuilder.GraphNodes);
            if (_pathNodes.Count == 0)
                return;

            _pathGraphReady = true;
            OnPathGraphReady?.Invoke();
        }

        if (_destinationManager != null && _destinationManager.HasInitialized)
        {
            AddNodes(_destinationManager.DestinationNodes);
        }
        else if (_destinationManager != null)
        {
            return;
        }

        if (_navigationGraphReady)
            return;

        Debug.Log($"NodeManager initialized {_navigationNodes.Count} navigation nodes.");
        _navigationGraphReady = true;
        OnPathNodesReady?.Invoke();
    }

    private void BuildPathNodes(IReadOnlyList<GeoPathGraphNode> graphNodes)
    {
        ClearPathNodes();

        for (var index = 0; index < graphNodes.Count; index++)
        {
            if (_possibleObjectPlacer.Count == 0)
            {
                Debug.LogError("NodeManager requires at least one object prefab to place.");
                return;
            }

            GameObject prefab = _possibleObjectPlacer[index % _possibleObjectPlacer.Count];
            if (prefab == null)
            {
                Debug.LogError($"NodeManager has a missing path-node prefab at index {index % _possibleObjectPlacer.Count}.");
                ClearPathNodes();
                enabled = false;
                return;
            }

            GeoCoordinate coordinate = graphNodes[index].Coordinate;
            GameObject nodeObject = Instantiate(prefab, transform);
            nodeObject.name = $"PathNode_{index:D3}";
            NavNode node = nodeObject.GetComponent<NavNode>();
            if (node == null)
                node = nodeObject.AddComponent<NavNode>();

            node.latitude = coordinate.latitude;
            node.longitude = coordinate.longitude;
            node.elevation = coordinate.elevation;
            node.isDestination = false;
            node.ClearNeighbors();

            _instantiatedObjects.Add(nodeObject);
            _pathNodes.Add(node);
            _navigationNodes.Add(node);
            _objectHelper.AddOrUpdateObject(
                nodeObject,
                coordinate.latitude,
                coordinate.longitude,
                0f,
                Quaternion.identity);
        }

        for (var index = 0; index < graphNodes.Count; index++)
        {
            foreach (int neighborIndex in graphNodes[index].NeighborIndices)
            {
                if (neighborIndex >= 0 && neighborIndex < _pathNodes.Count)
                    _pathNodes[index].ConnectBidirectional(_pathNodes[neighborIndex]);
            }
        }
    }

    private void AddNodes(IEnumerable<NavNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node != null && !_navigationNodes.Contains(node))
                _navigationNodes.Add(node);
        }
    }

    public void AddDestinationNodes(IEnumerable<NavNode> nodes)
    {
        AddNodes(nodes);
    }

    /// <summary>
    /// Replaces the single marker shown for the selected destination.
    /// </summary>
    public bool SetActiveDestination(NavNode destination)
    {
        if (destination == null)
        {
            ClearActiveDestinationMarker();
            ActiveDestination = null;

            Debug.Log("NodeManager: Destination deselected.");
            return true;
        }

        if (!destination.isDestination)
        {
            Debug.LogWarning("NodeManager cannot mark a node that is not a destination.");
            return false;
        }

        if (_destinationMarkerPrefab == null)
        {
            Debug.LogError("NodeManager requires a destination marker prefab.");
            return false;
        }

        if (_objectHelper == null)
        {
            Debug.LogError("NodeManager cannot place a destination marker without an object helper.");
            return false;
        }

        ClearActiveDestinationMarker();
        ActiveDestination = null;

        _activeDestinationMarker = Instantiate(_destinationMarkerPrefab, transform);
        _activeDestinationMarker.name = $"DestinationMarker_{GetDestinationLabel(destination)}";
        _objectHelper.AddOrUpdateObject(
            _activeDestinationMarker,
            destination.latitude,
            destination.longitude,
            0f,
            Quaternion.identity);
        ActiveDestination = destination;

          Debug.Log(
        $"NodeManager: Spawned destination marker for " +
        $"{GetDestinationLabel(destination)} " +
        $"(ID: {destination.buildingId}) " +
        $"at GPS ({destination.latitude:F7}, {destination.longitude:F7}, " +
        $"{destination.elevation:F2}m)."
    );
    
        return true;
    }

    private void ClearActiveDestinationMarker()
    {
        if (_activeDestinationMarker != null)
            Destroy(_activeDestinationMarker);
        _activeDestinationMarker = null;
    }

    private static string GetDestinationLabel(NavNode destination)
    {
        if (!string.IsNullOrWhiteSpace(destination.nodeName))
            return destination.nodeName;

        return destination.gameObject.name;
    }

    private void Update()
    {
        if (IsLocalizationReady())
        {
            _stableDuration += Time.deltaTime;
        }
        else
        {
            _stableDuration = 0f;
        }
    }

    public bool IsLocalizationReady()
    {
        return IsWpsStable &&
               _locationService != null &&
               _locationService.IsRunning &&
               GpsAccuracy >= 0f &&
               GpsAccuracy <= _gpsAccuracyThreshold;
    }

    public void ClearNavigation()
    {

        ClearPathNodes();
    }

    private void ClearPathNodes()
    {
        ClearActiveDestinationMarker();
        ActiveDestination = null;

        foreach (NavNode navigationNode in _navigationNodes)
            navigationNode?.neighbors.RemoveAll(node => _pathNodes.Contains(node));

        foreach (var instantiatedObject in _instantiatedObjects)
        {
            if (instantiatedObject != null)
            {
                Destroy(instantiatedObject);
            }
        }

        _instantiatedObjects.Clear();
        _pathNodes.Clear();
        _navigationNodes.Clear();
        _stableDuration = 0f;
        _pathGraphReady = false;
        _navigationGraphReady = false;
    }

    private void OnDestroy()
    {
        if (_pathBuilder != null)
            _pathBuilder.OnPathNodesBuilt -= InitializeNavigationGraph;
        if (_destinationManager != null)
            _destinationManager.OnDestinationsReady -= InitializeNavigationGraph;

        if (_positioningManager != null)
        {
            _positioningManager.OnStatusChanged -= OnStatusChanged;
        }
    }

    private void OnStatusChanged(WorldPositioningStatus status)
    {
        Debug.Log($"Status changed to {status}");
    }

    [System.Serializable]
    private struct LatLong
    {
        [FormerlySerializedAs("latitude")]
        public double latitude;
        [FormerlySerializedAs("longitude")]
        public double longitude;
    }
}
