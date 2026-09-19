using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves the guide character through the NavNode route generated
/// by AStarRouteService.
///
/// ARGuideManager is responsible for placing the character in the
/// real world. This class is responsible only for movement along
/// the already-positioned route nodes.
///
/// Movement rule:
/// - User is within 5 meters  -> Guide can move and plays Walking.
/// - User is farther than 5m  -> Guide stops and plays Stop.
/// - Guide remains stopped for 5 seconds -> Guide returns to Idle.
/// </summary>
public class ARGuideCharacter : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.8f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stoppingDistance = 0.5f;

    [Tooltip("Maximum allowed distance between the user and the guide.")]
    [SerializeField] private float maxDistanceFromUser = 5f;

    [SerializeField] private Transform userTransform;

    [Header("Guide Animator")]
    [SerializeField] private float idleAfterSeconds = 5f;

    private float stoppedTimer = 0f;
    private Animator animator;

    [Header("Grounding")]
    [SerializeField] private bool keepGrounded = true;
    [SerializeField] private float groundOffset = 0f;

    private List<Transform> waypoints = new();
    private int currentWaypointIndex;
    private bool isGuiding;

    public bool IsGuiding => isGuiding;

    private void Awake()
    {
        Camera arCamera = Camera.main;

        if (arCamera != null)
        {
            userTransform = arCamera.transform;
        }
        else
        {
            Debug.LogWarning(
                "ARGuideCharacter: Main Camera was not found."
            );
        }

        animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning(
                "ARGuideCharacter: Animator was not found in this character."
            );
        }
    }

    /// <summary>
    /// Starts movement from the specified waypoint index.
    /// </summary>
    public void StartGuiding(List<Transform> path)
    {
        StartGuiding(path, 0);
    }

    /// <summary>
    /// Starts movement along a route.
    ///
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

        // Reset the stopped timer whenever a new guide starts.
        stoppedTimer = 0f;

        // Start in a neutral/idle state.
        SetAnimBool("IsWalking", false);
        SetAnimBool("IsStop", false);

        Debug.Log(
            $"ARGuideCharacter: Guiding started. " +
            $"First target index = {currentWaypointIndex}, " +
            $"Waypoints = {waypoints.Count}"
        );
    }

    // =========================================================
    // Animator Functions
    // =========================================================

    private bool HasParameter(
        Animator anim,
        string paramName)
    {
        if (anim == null ||
            anim.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.name == paramName)
                return true;
        }

        return false;
    }

    private void SetAnimBool(
        string paramName,
        bool value)
    {
        if (HasParameter(animator, paramName))
        {
            animator.SetBool(paramName, value);
        }
    }

    /// <summary>
    /// Updates the walking/stopping animation according to
    /// whether the guide is actually moving.
    /// </summary>
    private void UpdateAnimator(bool isActuallyMoving)
    {
        if (animator == null)
            return;

        if (isActuallyMoving)
        {
            // ---------------------------------------------
            // GUIDE IS MOVING
            // ---------------------------------------------

            stoppedTimer = 0f;

            SetAnimBool("IsWalking", true);
            SetAnimBool("IsStop", false);
        }
        else
        {
            // ---------------------------------------------
            // GUIDE IS NOT MOVING
            // ---------------------------------------------

            SetAnimBool("IsWalking", false);

            stoppedTimer += Time.deltaTime;

            if (stoppedTimer < idleAfterSeconds)
            {
                // Stopped for less than 5 seconds.
                SetAnimBool("IsStop", true);
            }
            else
            {
                // Stopped for 5 seconds or more.
                SetAnimBool("IsStop", false);
            }
        }
    }

    // =========================================================
    // Movement
    // =========================================================

    public void StopGuiding()
    {
        isGuiding = false;

        waypoints.Clear();

        currentWaypointIndex = 0;

        stoppedTimer = 0f;

        SetAnimBool("IsWalking", false);
        SetAnimBool("IsStop", false);
    }

    private void Update()
    {
        if (!isGuiding)
            return;

        // -----------------------------------------------------
        // Check user distance
        // -----------------------------------------------------

        if (userTransform == null)
        {
            Debug.LogWarning(
                "ARGuideCharacter: User Transform is missing."
            );

            UpdateAnimator(false);
            return;
        }

        /*
         * Use horizontal distance only.
         *
         * This prevents the user's camera height and the
         * character's Y position from affecting the 5-meter rule.
         */
        Vector3 guidePosition = transform.position;
        Vector3 userPosition = userTransform.position;

        guidePosition.y = 0f;
        userPosition.y = 0f;

        float distanceFromUser =
            Vector3.Distance(
                guidePosition,
                userPosition
            );

        // -----------------------------------------------------
        // USER IS TOO FAR FROM GUIDE
        // -----------------------------------------------------

        if (distanceFromUser > maxDistanceFromUser)
        {
            /*
             * IMPORTANT:
             * Do NOT call MoveTowards().
             *
             * The guide physically stops here.
             */

            UpdateAnimator(false);

            return;
        }

        // -----------------------------------------------------
        // Check route
        // -----------------------------------------------------

        if (waypoints == null ||
            waypoints.Count == 0)
        {
            StopGuiding();
            return;
        }

        if (currentWaypointIndex >= waypoints.Count)
        {
            StopGuiding();
            return;
        }

        // -----------------------------------------------------
        // Get current target
        // -----------------------------------------------------

        Transform target =
            waypoints[currentWaypointIndex];

        if (target == null)
        {
            currentWaypointIndex++;
            return;
        }

        Vector3 targetPosition =
            target.position;

        // -----------------------------------------------------
        // Keep character at its current ground height
        // -----------------------------------------------------

        if (keepGrounded)
        {
            targetPosition.y =
                transform.position.y +
                groundOffset;
        }

        // -----------------------------------------------------
        // Calculate direction
        // -----------------------------------------------------

        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0f;

        float distance =
            direction.magnitude;

        // -----------------------------------------------------
        // Reached current waypoint
        // -----------------------------------------------------

        if (distance <= stoppingDistance)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Count)
            {
                StopGuiding();

                Debug.Log(
                    "ARGuideCharacter: Destination reached."
                );

                return;
            }

            return;
        }

        // -----------------------------------------------------
        // Rotate toward next waypoint
        // -----------------------------------------------------

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

        // -----------------------------------------------------
        // MOVE CHARACTER
        // -----------------------------------------------------

        Vector3 previousPosition =
            transform.position;

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed *
                Time.deltaTime
            );

        // -----------------------------------------------------
        // Determine if the character actually moved
        // -----------------------------------------------------

        float movementAmount =
            Vector3.Distance(
                previousPosition,
                transform.position
            );

        bool isActuallyMoving =
            movementAmount > 0.0001f;

        // -----------------------------------------------------
        // Update Animator
        // -----------------------------------------------------

        UpdateAnimator(isActuallyMoving);
    }
}