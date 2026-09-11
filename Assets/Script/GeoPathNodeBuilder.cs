using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Loads GeoJSON pathways into a topology-preserving graph.</summary>
public class GeoPathNodeBuilder : MonoBehaviour
{
    [SerializeField] private string geoJsonFileName = "CampusMap.geojson";

    private readonly List<GeoCoordinate> _coordinates = new();
    private readonly List<GeoPathGraphNode> _graphNodes = new();
    private string _rawGeoJsonText;

    // Coordinates are unique graph vertices; GraphNodes also expose their edges.
    public IReadOnlyList<GeoCoordinate> Coordinates => _coordinates;
    public IReadOnlyList<GeoPathGraphNode> GraphNodes => _graphNodes;
    public string RawGeoJsonText => _rawGeoJsonText;
    public bool HasBuilt { get; private set; }
    public event Action OnPathNodesBuilt;

    private void Start() => StartCoroutine(LoadAndParseGeoJson());

    private IEnumerator LoadAndParseGeoJson()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, geoJsonFileName);

#if UNITY_ANDROID && !UNITY_EDITOR
        using (UnityWebRequest request = UnityWebRequest.Get(filePath))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"GeoPathNodeBuilder: Failed to load GeoJSON.\n{request.error}");
                yield break;
            }

            _rawGeoJsonText = request.downloadHandler.text;
        }
#else
        if (!File.Exists(filePath))
        {
            Debug.LogError($"GeoPathNodeBuilder: GeoJSON file not found:\n{filePath}");
            yield break;
        }

        _rawGeoJsonText = File.ReadAllText(filePath);
#endif

        ParseGeoJson();
    }

    private void ParseGeoJson()
    {
        JObject geoJson;
        try
        {
            geoJson = JObject.Parse(_rawGeoJsonText);
        }
        catch (Exception exception)
        {
            Debug.LogError($"GeoPathNodeBuilder: Invalid GeoJSON.\n{exception}");
            return;
        }

        JArray features = geoJson["features"] as JArray;
        if (features == null)
        {
            Debug.LogError("GeoPathNodeBuilder: GeoJSON does not contain a 'features' array.");
            return;
        }

        _coordinates.Clear();
        _graphNodes.Clear();
        Dictionary<CoordinateKey, int> nodeIndices = new();
        int lineCount = 0;

        foreach (JToken feature in features)
        {
            if (feature["geometry"]?["type"]?.ToString() != "LineString")
                continue;

            JArray lineCoordinates = feature["geometry"]?["coordinates"] as JArray;
            if (lineCoordinates == null || lineCoordinates.Count < 2)
                continue;

            double featureElevation = feature["properties"]?["ele_1_2"]?.Value<double?>() ?? 0d;
            int previousIndex = -1;
            bool hasSegment = false;

            foreach (JToken coordinateToken in lineCoordinates)
            {
                JArray coordinate = coordinateToken as JArray;
                if (coordinate == null || coordinate.Count < 2)
                    continue;

                GeoCoordinate geoCoordinate = new(
                    coordinate[1].Value<double>(),
                    coordinate[0].Value<double>(),
                    coordinate.Count >= 3 ? coordinate[2].Value<double>() : featureElevation);
                CoordinateKey key = new(geoCoordinate.latitude, geoCoordinate.longitude);

                if (!nodeIndices.TryGetValue(key, out int currentIndex))
                {
                    currentIndex = _graphNodes.Count;
                    nodeIndices.Add(key, currentIndex);
                    _coordinates.Add(geoCoordinate);
                    _graphNodes.Add(new GeoPathGraphNode(geoCoordinate));
                }

                if (previousIndex >= 0 && previousIndex != currentIndex)
                {
                    _graphNodes[previousIndex].Connect(currentIndex);
                    _graphNodes[currentIndex].Connect(previousIndex);
                    hasSegment = true;
                }

                previousIndex = currentIndex;
            }

            if (hasSegment)
                lineCount++;
        }

        HasBuilt = _graphNodes.Count > 0;
        if (!HasBuilt)
        {
            Debug.LogError("GeoPathNodeBuilder: GeoJSON contains no valid pathway segments.");
            return;
        }

        Debug.Log($"GeoPathNodeBuilder: Parsed {lineCount} LineStrings into {_graphNodes.Count} graph nodes.");
        OnPathNodesBuilt?.Invoke();
    }

    // Seven decimal places is about 1 cm at the equator: enough to merge
    // repeated GeoJSON vertices without merging nearby but distinct paths.
    private readonly struct CoordinateKey : IEquatable<CoordinateKey>
    {
        private const double Precision = 10_000_000d;
        private readonly long _latitude;
        private readonly long _longitude;

        public CoordinateKey(double latitude, double longitude)
        {
            _latitude = (long)Math.Round(latitude * Precision);
            _longitude = (long)Math.Round(longitude * Precision);
        }

        public bool Equals(CoordinateKey other) =>
            _latitude == other._latitude && _longitude == other._longitude;
        public override bool Equals(object obj) => obj is CoordinateKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return (_latitude.GetHashCode() * 397) ^ _longitude.GetHashCode();
            }
        }
    }
}

public readonly struct GeoCoordinate
{
    public readonly double latitude;
    public readonly double longitude;
    public readonly double elevation;

    public GeoCoordinate(double latitude, double longitude, double elevation)
    {
        this.latitude = latitude;
        this.longitude = longitude;
        this.elevation = elevation;
    }
}

public sealed class GeoPathGraphNode
{
    private readonly List<int> _neighborIndices = new();
    public GeoCoordinate Coordinate { get; }
    public IReadOnlyList<int> NeighborIndices => _neighborIndices;

    public GeoPathGraphNode(GeoCoordinate coordinate) => Coordinate = coordinate;

    public void Connect(int neighborIndex)
    {
        if (!_neighborIndices.Contains(neighborIndex))
            _neighborIndices.Add(neighborIndex);
    }
}
