using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ARGuideCharacter : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stoppingDistance = 0.5f;

    [Header("Grounding")]
    [SerializeField] private bool keepGrounded = true;
    [SerializeField] private float groundOffset = 0f;

    private List<Transform> waypoints = new List<Transform>();
    private int currentWaypointIndex;
    private bool isGuiding;

    public bool IsGuiding => isGuiding;

    public void StartGuiding(List<Transform> path)
    {
        StopGuiding();

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("ARGuideCharacter: No path was provided.");
            return;
        }

        waypoints = new List<Transform>(path);
        currentWaypointIndex = 0;
        isGuiding = true;
    }

    public void StopGuiding()
    {
        isGuiding = false;
        waypoints.Clear();
        currentWaypointIndex = 0;
    }

    private void Update()
    {
        if (!isGuiding)
            return;

        if (waypoints == null || waypoints.Count == 0)
        {
            StopGuiding();
            return;
        }

        if (currentWaypointIndex >= waypoints.Count)
        {
            StopGuiding();
            return;
        }

        Transform target = waypoints[currentWaypointIndex];

        if (target == null)
        {
            currentWaypointIndex++;
            return;
        }

        Vector3 targetPosition = target.position;

        if (keepGrounded)
        {
            targetPosition.y = transform.position.y + groundOffset;
        }

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance <= stoppingDistance)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Count)
            {
                StopGuiding();
            }

            return;
        }

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction.normalized, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );
    }
}