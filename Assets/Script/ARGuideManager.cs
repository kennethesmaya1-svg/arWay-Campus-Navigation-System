using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ARGuideManager : MonoBehaviour
{
    [Header("Guide")]
    [SerializeField] private GameObject guideCharacterPrefab;

    [Header("Character Guide UI")]
    [SerializeField] private Image CharacterBtnImage;
    [SerializeField] private Text CharacterBtnLabel;
    [SerializeField] private Sprite CharacterON;
    [SerializeField] private Sprite CharacterOFF;

    [Header("Spawn")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float spawnDistance = 2f;
    [SerializeField] private float spawnHeightOffset = 0f;

    private GameObject activeGuide;
    private ARGuideCharacter guideController;
    private bool destinationSelected;
    public bool DestinationSelected => destinationSelected;

    [Header("Route")]
    [SerializeField] private AStarRouteService _routeService;
    public bool HasGuide => activeGuide != null;

    private void Awake()
    {
        if (_routeService == null)
            _routeService = FindFirstObjectByType<AStarRouteService>();
    }

    public void SpawnGuide()
    {
        RemoveGuide();

        if (guideCharacterPrefab == null)
        {
            Debug.LogError("ARGuideManager: Guide Character Prefab is not assigned.");
            return;
        }

        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
                cameraTransform = mainCamera.transform;
        }

        if (cameraTransform == null)
        {
            Debug.LogError("ARGuideManager: Camera Transform is missing.");
            return;
        }

        Vector3 spawnPosition =
            cameraTransform.position +
            cameraTransform.forward * spawnDistance;

        spawnPosition.y += spawnHeightOffset;

        Quaternion spawnRotation = cameraTransform.rotation;
        spawnRotation.x = 0f;
        spawnRotation.z = 0f;

        activeGuide = Instantiate(
            guideCharacterPrefab,
            spawnPosition,
            spawnRotation
        );

        guideController =
            activeGuide.GetComponent<ARGuideCharacter>();

        if (guideController == null)
        {
            guideController =
                activeGuide.AddComponent<ARGuideCharacter>();
        }

        activeGuide.SetActive(true);
        UpdateCharacterButton(true);
        Debug.Log("ARGuideManager: Guide character spawned.");
    }

    public void CharacterButtonClicked()
    {
        if (!destinationSelected)
        {
            Debug.LogWarning("[ARGuideManager] Cannot toggle character because no destination is selected.");
            return;
        }

        if (activeGuide == null)
        {
            Debug.LogWarning("[ARGuideManager] Cannot toggle character because it has not been spawned." );
            return;
        }

        bool isVisible = !activeGuide.activeSelf;

        activeGuide.SetActive(isVisible);

        UpdateCharacterButton(isVisible);

        Debug.Log(isVisible? "ARGuideManager: Guide character shown.": "ARGuideManager: Guide character hidden.");
    }

    public void StartGuide(List<Transform> path)
    {
        if (path == null || path.Count < 2)
        {
            Debug.LogWarning(
                "ARGuideManager: Cannot start guide. No valid destination path."
            );

            destinationSelected = false;
            return;
        }

        destinationSelected = true;

        Debug.Log(
            $"ARGuideManager: Destination selected. Path points = {path.Count}"
        );

        // Spawn character on the path, 2 meters ahead of the camera.
        SpawnGuideOnPath(path, 2f);

        if (guideController == null)
        {
            Debug.LogError(
                "ARGuideManager: Guide controller was not created."
            );
            return;
        }

        guideController.StartGuiding(path);
    }
    public void SetDestinationSelected(bool selected)
    {
        destinationSelected = selected;

        Debug.Log(
            $"ARGuideManager: Destination selected = {destinationSelected}"
        );
    }

    private void SpawnGuideOnPath(List<Transform> path, float distanceAhead)
    {
        RemoveGuide();

        if (guideCharacterPrefab == null)
        {
            Debug.LogError(
                "ARGuideManager: Guide Character Prefab is not assigned."
            );
            return;
        }

        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
                cameraTransform = mainCamera.transform;
        }

        if (cameraTransform == null)
        {
            Debug.LogError(
                "ARGuideManager: Camera Transform is missing."
            );
            return;
        }

        Vector3 cameraPosition = cameraTransform.position;

        // ---------------------------------------------------------
        // 1. Find the closest point on the navigation path
        // ---------------------------------------------------------

        int closestSegment = -1;
        float closestDistance = float.MaxValue;
        float closestT = 0f;

        for (int i = 0; i < path.Count - 1; i++)
        {
            if (path[i] == null || path[i + 1] == null)
                continue;

            Vector3 a = path[i].position;
            Vector3 b = path[i + 1].position;

            // Ignore height when finding the nearest path position.
            Vector3 cameraFlat = cameraPosition;
            cameraFlat.y = 0f;

            a.y = 0f;
            b.y = 0f;

            Vector3 segment = b - a;

            if (segment.sqrMagnitude < 0.0001f)
                continue;

            float t = Vector3.Dot(
                cameraFlat - a,
                segment
            ) / segment.sqrMagnitude;

            t = Mathf.Clamp01(t);

            Vector3 closestPoint = Vector3.Lerp(a, b, t);

            float distance =
                Vector3.Distance(cameraFlat, closestPoint);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestSegment = i;
                closestT = t;
            }
        }

        if (closestSegment < 0)
        {
            Debug.LogError(
                "ARGuideManager: Could not find nearest path segment."
            );
            return;
        }

        // ---------------------------------------------------------
        // 2. Get the nearest position ON the path
        // ---------------------------------------------------------

        Vector3 segmentStart =
            path[closestSegment].position;

        Vector3 segmentEnd =
            path[closestSegment + 1].position;

        Vector3 nearestPathPosition =
            Vector3.Lerp(
                segmentStart,
                segmentEnd,
                closestT
            );

        // ---------------------------------------------------------
        // 3. Move 2 meters forward along the path
        // ---------------------------------------------------------

        float remainingDistance = distanceAhead;

        Vector3 spawnPosition = nearestPathPosition;

        int segmentIndex = closestSegment;

        // First move along the current segment.
        Vector3 currentTarget =
            path[segmentIndex + 1].position;

        float distanceToCurrentTarget =
            Vector3.Distance(
                spawnPosition,
                currentTarget
            );

        if (remainingDistance <= distanceToCurrentTarget)
        {
            Vector3 direction =
                (currentTarget - spawnPosition).normalized;

            spawnPosition +=
                direction * remainingDistance;

            remainingDistance = 0f;
        }
        else
        {
            remainingDistance -= distanceToCurrentTarget;
            spawnPosition = currentTarget;

            // Continue through following path segments.
            segmentIndex++;

            while (
                remainingDistance > 0f &&
                segmentIndex < path.Count - 1
            )
            {
                Vector3 nextPoint =
                    path[segmentIndex + 1].position;

                Vector3 direction =
                    nextPoint - spawnPosition;

                float segmentDistance =
                    direction.magnitude;

                if (segmentDistance < 0.001f)
                {
                    segmentIndex++;
                    continue;
                }

                direction.Normalize();

                if (remainingDistance <= segmentDistance)
                {
                    spawnPosition +=
                        direction * remainingDistance;

                    remainingDistance = 0f;
                }
                else
                {
                    spawnPosition = nextPoint;
                    remainingDistance -= segmentDistance;
                    segmentIndex++;
                }
            }
        }

        // ---------------------------------------------------------
        // 4. Calculate character facing direction
        // ---------------------------------------------------------

        Vector3 forwardDirection;

        if (segmentIndex < path.Count - 1)
        {
            forwardDirection =
                path[segmentIndex + 1].position -
                spawnPosition;
        }
        else
        {
            forwardDirection =
                path[path.Count - 1].position -
                path[Mathf.Max(0, path.Count - 2)].position;
        }

        forwardDirection.y = 0f;

        if (forwardDirection.sqrMagnitude < 0.001f)
        {
            forwardDirection = cameraTransform.forward;
            forwardDirection.y = 0f;
        }

        forwardDirection.Normalize();

        Quaternion spawnRotation =
            Quaternion.LookRotation(
                forwardDirection,
                Vector3.up
            );

        // ---------------------------------------------------------
        // 5. Spawn
        // ---------------------------------------------------------

        activeGuide = Instantiate(
            guideCharacterPrefab,
            spawnPosition,
            spawnRotation
        );

        guideController =
            activeGuide.GetComponent<ARGuideCharacter>();

        if (guideController == null)
        {
            guideController =
                activeGuide.AddComponent<ARGuideCharacter>();
        }

        activeGuide.SetActive(true);

        UpdateCharacterButton(true);

        Debug.Log(
            $"ARGuideManager: Character spawned ON PATH " +
            $"2m ahead. Position = {spawnPosition}"
        );
    }

    private void OnEnable()
    {
        if (_routeService != null)
            _routeService.OnRouteComputed += OnRouteComputed;
    }

    private void OnDisable()
    {
        if (_routeService != null)
            _routeService.OnRouteComputed -= OnRouteComputed;
    }

    private void OnRouteComputed(IReadOnlyList<NavNode> route)
    {
        if (route == null || route.Count == 0)
        {
            Debug.LogWarning("[ARGuideManager] Route is empty.");
            return;
        }

        List<Transform> path = new List<Transform>();

        foreach (NavNode node in route)
        {
            if (node != null)
                path.Add(node.transform);
        }

        if (path.Count == 0)
        {
            Debug.LogWarning("[ARGuideManager] No valid path transforms.");
            return;
        }

        destinationSelected = true;

        // Spawn the character on the route.
        SpawnGuideOnPath(path, spawnDistance);

        if (guideController != null)
        {
            guideController.StartGuiding(path);
        }

        // IMPORTANT:
        // Character starts HIDDEN after destination selection.
        if (activeGuide != null)
            activeGuide.SetActive(false);

        UpdateCharacterButton(false);

        Debug.Log("[ARGuideManager] Destination selected. Guide spawned but hidden.");
    }

    public void RemoveGuide()
    {
        if (activeGuide != null)
        {
            Destroy(activeGuide);
        }

        activeGuide = null;
        guideController = null;
        destinationSelected = false;
        UpdateCharacterButton(false);
        Debug.Log("ARGuideManager: Destination cleared. " + "Character toggle disabled.");
    }

    private void UpdateCharacterButton(bool isVisible)
    {
        if (CharacterBtnImage != null)
        {
            CharacterBtnImage.sprite = isVisible ? CharacterON : CharacterOFF;
        }

        if (CharacterBtnLabel != null)
        {
            CharacterBtnLabel.text = isVisible ? "Hide Character" : "Show Character";
        }
    }
}