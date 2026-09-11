using System;
using System.Collections.Generic;
using UnityEngine;

public class DestinationNodeManager : MonoBehaviour
{
    // =====================================================================
    // REFERENCES
    // =====================================================================

    [Header("References")]

    [Tooltip(
        "Parent containing the destination/building NavNode GameObjects."
    )]
    [SerializeField]
    private Transform destinationParent;

    [Tooltip(
        "Creates and connects the pathway NavNodes."
    )]
    [SerializeField]
    private GeoPathNodeBuilder pathBuilder;

    [SerializeField]
    private NodeManager nodeManager;

    // =====================================================================
    // SETTINGS
    // =====================================================================

    [Header("Destination Settings")]
    [Tooltip(
        "Maximum recommended distance between a destination and its nearest " +
        "path node. The destination will still be connected if it exceeds " +
        "this value, but a warning will be displayed."
    )]
    [SerializeField]
    private float snapDistance = 15f;


    // =====================================================================
    // PUBLIC STATE
    // =====================================================================

    /// <summary>
    /// All destination NavNodes managed by this component.
    ///
    /// These are normally the building destination nodes.
    /// </summary>
    public List<NavNode> DestinationNodes { get; } =
        new List<NavNode>();

    public IReadOnlyList<GeoCoordinate> DestinationCoordinates =>
        DestinationNodes.ConvertAll(node =>
            new GeoCoordinate(node.latitude, node.longitude, node.elevation));


    /// <summary>
    /// True after destination nodes have been discovered,
    /// positioned, and connected to the path graph.
    /// </summary>
    public bool HasInitialized { get; private set; }


    /// <summary>
    /// Fired after all destination nodes have been initialized.
    /// </summary>
    public event Action OnDestinationsReady;


    // =====================================================================
    // START
    // =====================================================================

    private void Start()
    {
        if (nodeManager == null)
            nodeManager = FindFirstObjectByType<NodeManager>();
        if (nodeManager != null)
            nodeManager.OnPathGraphReady += InitializeDestinations;

        ValidateReferences();

        if (pathBuilder == null)
            return;


        // -------------------------------------------------------------
        // If the path builder has already finished, initialize now.
        // -------------------------------------------------------------

        if (pathBuilder.HasBuilt)
        {
            InitializeDestinations();
        }
        else
        {
            // Otherwise wait for GeoPathNodeBuilder.
            pathBuilder.OnPathNodesBuilt +=
                InitializeDestinations;
        }
    }


    // =====================================================================
    // CLEANUP
    // =====================================================================

    private void OnDestroy()
    {
        if (pathBuilder != null)
        {
            pathBuilder.OnPathNodesBuilt -=
                InitializeDestinations;
        }
        if (nodeManager != null)
            nodeManager.OnPathGraphReady -= InitializeDestinations;
    }


    // =====================================================================
    // REFERENCE VALIDATION
    // =====================================================================

    private void ValidateReferences()
    {
        if (destinationParent == null)
        {
            Debug.LogError(
                "DestinationNodeManager: " +
                "Destination Parent is not assigned."
            );
        }


        if (pathBuilder == null)
        {
            Debug.LogError(
                "DestinationNodeManager: " +
                "GeoPathNodeBuilder is not assigned."
            );
        }

    }


    // =====================================================================
    // INITIALIZATION
    // =====================================================================

    /// <summary>
    /// Initializes all destination NavNodes.
    ///
    /// IMPORTANT:
    /// BEFORE the destination is snapped to the pathway.
    ///
    /// Therefore the order is:
    ///
    /// 1. Find destination nodes
    /// 2. Register them in DestinationNodes.
    /// 4. Snap/connect them to the path graph.
    /// 5. Notify other systems.
    /// </summary>
    private void InitializeDestinations()
    {
        // Prevent accidental double initialization.
        if (HasInitialized)
            return;

        if (destinationParent == null)
        {
            Debug.LogError(
                "DestinationNodeManager: " +
                "Cannot initialize because Destination Parent is missing."
            );

            return;
        }

        if (pathBuilder == null)
        {
            Debug.LogError(
                "DestinationNodeManager: " +
                "Cannot initialize because GeoPathNodeBuilder is missing."
            );

            return;
        }

        if (!pathBuilder.HasBuilt)
        {
            Debug.LogWarning(
                "DestinationNodeManager: " +
                "Path graph is not ready yet."
            );

            return;
        }

        if (nodeManager == null || !nodeManager.HasPathNodes)
            return;

        // ---------------------------------------------------------
        // CLEAR OLD DESTINATIONS
        // ---------------------------------------------------------

        DestinationNodes.Clear();

        // ---------------------------------------------------------
        // FIND DESTINATION NAVNODES
        // ---------------------------------------------------------

        foreach (Transform child in destinationParent)
        {
            if (child == null)
                continue;

            NavNode node = child.GetComponent<NavNode>();

            if (node == null)
            {
                Debug.LogWarning(
                    $"DestinationNodeManager: '{child.name}' " +
                    "does not contain a NavNode component. Skipping."
                );

                continue;
            }

            // This tells the navigation system that this
            // NavNode represents a destination/building.
            node.isDestination = true;

            DestinationNodes.Add(node);
        }

        foreach (NavNode destination in DestinationNodes)
        {
            ConnectDestinationToNearestPath(destination);
        }

        Debug.Log(
            $"DestinationNodeManager: Found " +
            $"{DestinationNodes.Count} destination NavNodes."
        );

        HasInitialized = true;
        nodeManager.AddDestinationNodes(DestinationNodes);

        // Notify route selection and rendering systems that destinations exist.
        OnDestinationsReady?.Invoke();

        Debug.Log(
            "DestinationNodeManager: Destination nodes are ready."
        );
    }


    // =====================================================================
    // CONNECT DESTINATION TO PATH
    // =====================================================================

    /// <summary>
    /// Finds the closest path NavNode and connects the destination
    /// bidirectionally to that node.
    ///
    /// The destination's GPS coordinates are never modified.
    /// Only its Unity position and graph connections are affected.
    /// </summary>
    private void ConnectDestinationToNearestPath(
        NavNode destination)
    {
        if (destination == null)
            return;


        if (nodeManager == null ||
            nodeManager.PathNodes == null ||
            nodeManager.PathNodes.Count == 0)
        {
            Debug.LogWarning(
                $"DestinationNodeManager: " +
                $"No path nodes available for {destination.name}."
            );

            return;
        }


        NavNode nearestPathNode = null;

        float nearestDistance =
            float.MaxValue;


        // -------------------------------------------------------------
        // SEARCH PATH GRAPH
        // -------------------------------------------------------------

        foreach (NavNode pathNode in nodeManager.PathNodes)
        {
            if (pathNode == null)
                continue;


            // Do not accidentally connect a destination to itself.
            if (pathNode == destination)
                continue;


            float distance = AStarRouteService.HaversineMeters(
                destination.latitude,
                destination.longitude,
                pathNode.latitude,
                pathNode.longitude);


            if (distance < nearestDistance)
            {
                nearestDistance =
                    distance;

                nearestPathNode =
                    pathNode;
            }
        }


        // -------------------------------------------------------------
        // NO PATH NODE FOUND
        // -------------------------------------------------------------

        if (nearestPathNode == null)
        {
            Debug.LogWarning(
                $"DestinationNodeManager: " +
                $"Could not find a path node for {destination.name}."
            );

            return;
        }


        // -------------------------------------------------------------
        // DISTANCE WARNING
        // -------------------------------------------------------------

        if (nearestDistance > snapDistance)
        {
            Debug.LogWarning(
                $"DestinationNodeManager: " +
                $"{destination.name} is " +
                $"{nearestDistance:F2}m from its nearest path node. " +
                $"Configured snap distance = {snapDistance:F2}m."
            );
        }


        // -------------------------------------------------------------
        // CONNECT DESTINATION <-> PATH NODE
        // -------------------------------------------------------------

        destination.ConnectBidirectional(nearestPathNode);

        // Debug.Log(
        //     $"DestinationNodeManager: " +
        //     $"{destination.name} connected to " +
        //     $"{nearestPathNode.name} " +
        //     $"({nearestDistance:F2}m)."
        // );
    }


    // =====================================================================
    // PUBLIC REFRESH
    // =====================================================================

    /// <summary>
    /// Rebuilds destination positioning and connections.
    ///
    /// Useful if:
    /// - WPS altitude changes significantly.
    /// - Ground calibration changes.
    /// - Path nodes are rebuilt.
    ///
    /// Existing destination nodes are not recreated.
    /// </summary>
    public void RefreshDestinations()
    {
        HasInitialized = false;


        // Remove destination-to-path connections first.
        DisconnectDestinationNodes();

        foreach (NavNode destination in DestinationNodes)
        {
            if (destination == null)
                continue;


            ConnectDestinationToNearestPath(
                destination
            );
        }


        HasInitialized = true;


        OnDestinationsReady?.Invoke();


        Debug.Log(
            "DestinationNodeManager: " +
            "Destination nodes refreshed."
        );
    }


    // =====================================================================
    // DISCONNECT DESTINATIONS
    // =====================================================================

    /// <summary>
    /// Removes destination-to-path connections.
    ///
    /// This does not remove normal path-to-path connections.
    /// </summary>
    private void DisconnectDestinationNodes()
    {
        foreach (NavNode destination in DestinationNodes)
        {
            if (destination == null)
                continue;


            // Remove this destination from every path node.
            foreach (NavNode pathNode in nodeManager.PathNodes)
            {
                if (pathNode == null)
                    continue;


                pathNode.neighbors.Remove(
                    destination
                );
            }


            // Remove existing destination connections.
            destination.neighbors.Clear();
        }
    }
}
