using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves the guide character through the NavNode route generated
/// by AStarRouteService.
///
/// ARGuideManager is responsible for placing the character in the
/// real world. This class is responsible only for movement along
/// the already-positioned route nodes.
/// </summary>
public class ARGuideCharacter : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stoppingDistance = 0.5f;

    [Header("Grounding")]
    [SerializeField] private bool keepGrounded = true;
    [SerializeField] private float groundOffset = 0f;
    private Animator guideAnimator;

    private List<Transform> waypoints = new();
    private int currentWaypointIndex;
    private bool isGuiding;
    public bool IsGuiding => isGuiding;

    /// Starts movement from the specified waypoint index.
    public void StartGuiding(List<Transform> path)
    {
        StartGuiding(path, 0);
    }

    /// <summary>
    /// Starts movement along a route.
    /// firstTargetIndex is important because ARGuideManager
    /// can spawn the character a few meters after route[0].
    /// The character therefore starts by walking toward the next
    /// route node instead of walking backward to route[0].
    /// </summary>
    public void StartGuiding(
        List<Transform> path,
        int firstTargetIndex)
    {
        StopGuiding();

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning(
                "ARGuideCharacter: No path was provided."
            );
            return;
        }

        waypoints = new List<Transform>(path);

        currentWaypointIndex =
            Mathf.Clamp(
                firstTargetIndex,
                0,
                waypoints.Count - 1
            );

        isGuiding = true;

        Debug.Log(
            $"ARGuideCharacter: Guiding started. " +
            $"First target index = {currentWaypointIndex}, " +
            $"Waypoints = {waypoints.Count}"
        );
    }

    //=========================================================
    /// <summary>
    /// Guide movements and functions
    /// </summary>
    /// ===========================================================
    bool HasParameter(Animator anim, string paramName)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == paramName) return true;
        return false;
    }

    void SetAnimBool(string paramName, bool value)
    {
        if (HasParameter(guideAnimator, paramName))
            guideAnimator.SetBool(paramName, value);
    }

    void CharacterMovement(bool userIsWalking)
    {
        if (userIsWalking)
        {
            SetAnimBool("IsWalking", true);
            SetAnimBool("IsStop", false);
            SetAnimBool("IsIdle", false);
        }

        SetAnimBool("IsWalking", false);

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

        Transform target =
            waypoints[currentWaypointIndex];

        if (target == null)
        {
            currentWaypointIndex++;
            return;
        }

        Vector3 targetPosition =
            target.position;

        if (keepGrounded)
        {
            targetPosition.y =
                transform.position.y +
                groundOffset;
        }

        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0f;

        float distance =
            direction.magnitude;

        if (distance <= stoppingDistance)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Count)
            {
                StopGuiding();

                Debug.Log("ARGuideCharacter: Destination reached.");
            }

            return;
        }

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up
                );

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed *
                    Time.deltaTime
                );
        }

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed *
                Time.deltaTime
            );
    }
}
