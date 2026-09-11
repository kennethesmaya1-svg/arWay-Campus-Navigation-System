using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A vertex in the campus navigation graph. Pathway and destination nodes use
/// the same model so A* can traverse either without coordinate conversion.
/// </summary>
public class NavNode : MonoBehaviour
{
    [Header("A* Data")]
    public List<NavNode> neighbors = new();
    public float gCost;
    public float hCost;
    public NavNode parent;
    public float fCost => gCost + hCost;

    [Header("Destination")]
    public bool isDestination;

    [Header("Database")]
    public int buildingId = -1;
    public string nodeName;

    [Header("GPS")]
    [Tooltip("WGS84 latitude in decimal degrees.")]
    public double latitude;
    [Tooltip("WGS84 longitude in decimal degrees.")]
    public double longitude;
    [Tooltip("Source elevation above mean sea level, in meters.")]
    public double elevation;

    [Header("Guidance")]
    public float targetBearing;
    public float relativeBearing;
    public string arrowDirection;

    public void AddNeighbor(NavNode node)
    {
        if (node != null && node != this && !neighbors.Contains(node))
            neighbors.Add(node);
    }

    public void RemoveNeighbor(NavNode node)
    {
        if (node != null)
            neighbors.Remove(node);
    }

    public void ConnectBidirectional(NavNode node)
    {
        if (node == null || node == this)
            return;

        AddNeighbor(node);
        node.AddNeighbor(this);
    }

    public void DisconnectBidirectional(NavNode node)
    {
        if (node == null)
            return;

        RemoveNeighbor(node);
        node.RemoveNeighbor(this);
    }

    public void ClearNeighbors() => neighbors.Clear();

    public void FindNeighbors(NavNode[] allNodes, float maxDistance)
    {
        neighbors.Clear();
        if (allNodes == null || maxDistance <= 0f)
            return;

        float maxDistanceSquared = maxDistance * maxDistance;
        foreach (NavNode node in allNodes)
        {
            if (node == null || node == this)
                continue;

            if ((node.transform.position - transform.position).sqrMagnitude <= maxDistanceSquared)
                AddNeighbor(node);
        }
    }

    public void ResetPathfindingState()
    {
        gCost = 0f;
        hCost = 0f;
        parent = null;
    }

    public float HorizontalDistanceTo(NavNode other)
    {
        if (other == null)
            return Mathf.Infinity;

        Vector3 from = transform.position;
        Vector3 to = other.transform.position;
        from.y = 0f;
        to.y = 0f;
        return Vector3.Distance(from, to);
    }
}
