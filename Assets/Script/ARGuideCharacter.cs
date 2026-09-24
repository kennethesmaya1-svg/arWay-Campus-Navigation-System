using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves the guide character through the NavNode route generated
/// by AStarRouteService.
///
/// Distance behavior:
/// - 0–2 meters from user: normal movement.
/// - 2–5 meters: gradually slows down.
/// - 5 meters: hard stop.
/// - The guide will NEVER move beyond maxDistanceFromUser.
/// </summary>
public class ARGuideCharacter : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 0.26f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stoppingDistance = 0.5f;

    [Header("Guide Side Offset")]
    [SerializeField] private float sideOffset = 0.7f;
    [SerializeField] private bool offsetToRightSide = true;

    [Header("User Distance Control")]
    [Tooltip("Start slowing the guide at this distance.")]
    [SerializeField] private float slowDownDistanceFromUser = 2f;

    [Tooltip("Preferred distance from the user.")]
    [SerializeField] private float followDistance = 1.5f;

    [Tooltip("ABSOLUTE maximum horizontal distance between user and guide.")]
    [SerializeField] private float maxDistanceFromUser = 5f;

    [Header("User Following")]
    [SerializeField] private float userMovementThreshold = 0.02f;
    [SerializeField] private Transform userTransform;

    private Vector3 previousUserPosition;
    private bool userIsMoving;

    [Header("Guide Animator")]
    [SerializeField] private float idleAfterSeconds = 5f;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    private float stoppedTimer = 0f;

    [Header("Face User")]
    [SerializeField] private float faceUserAfterSeconds = 5f;
    [SerializeField] private float faceUserRotationSpeed = 5f;

    [Header("Destination Rotation")]
    [SerializeField] private float destinationFaceRotationSpeed = 5f;
    [SerializeField] private float destinationRotationTolerance = 5f;

    private bool isFacingUserAtDestination = false;
    private bool destinationReached = false;

    [Header("Grounding")]
    [SerializeField] private bool keepGrounded = true;
    [SerializeField] private float groundOffset = 0f;

    private List<Transform> waypoints = new();
    private int currentWaypointIndex;
    private bool isGuiding;

    public bool IsGuiding => isGuiding;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        Camera arCamera = Camera.main;

        if (arCamera != null)
        {
            userTransform = arCamera.transform;
            previousUserPosition = userTransform.position;
        }
        else
        {
            Debug.LogWarning(
                "ARGuideCharacter: Main Camera was not found."
            );
        }

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>(true);
        }

        if (animator == null)
        {
            Debug.LogWarning(
                "ARGuideCharacter: Animator was not found in this character."
            );
        }
    }


    // =========================================================
    // USER MOVEMENT
    // =========================================================

    private void UpdateUserMovement()
    {
        if (userTransform == null)
            return;

        Vector3 currentUserPosition =
            userTransform.position;

        Vector3 movement =
            currentUserPosition -
            previousUserPosition;

        movement.y = 0f;

        float movementDistance =
            movement.magnitude;

        userIsMoving =
            movementDistance >=
            userMovementThreshold;

        previousUserPosition =
            currentUserPosition;
    }


    // =========================================================
    // START GUIDING
    // =========================================================

    public void StartGuiding(List<Transform> path)
    {
        StartGuiding(path, 0);
    }


    public void StartGuiding(
        List<Transform> path,
        int firstTargetIndex)
    {
        StopGuiding();

        if (path == null ||
            path.Count == 0)
        {
            Debug.LogWarning(
                "ARGuideCharacter: No path was provided."
            );

            return;
        }

        waypoints =
            new List<Transform>(path);

        currentWaypointIndex =
            Mathf.Clamp(
                firstTargetIndex,
                0,
                waypoints.Count - 1
            );

        isGuiding = true;

        stoppedTimer = 0f;

        destinationReached = false;
        isFacingUserAtDestination = false;

        SetAnimBool(
            "IsWalking",
            false
        );

        SetAnimBool(
            "IsStop",
            false
        );

        SetAnimBool(
            "isDestination",
            false
        );

        Debug.Log(
            $"ARGuideCharacter: Guiding started. " +
            $"First target index = {currentWaypointIndex}, " +
            $"Waypoints = {waypoints.Count}"
        );
    }


    // =========================================================
    // ANIMATOR
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

        foreach (
            AnimatorControllerParameter p
            in anim.parameters)
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
        if (HasParameter(
            animator,
            paramName))
        {
            animator.SetBool(
                paramName,
                value
            );
        }
    }


    private void UpdateAnimator(
        bool isActuallyMoving)
    {
        if (animator == null)
            return;

        if (isActuallyMoving)
        {
            stoppedTimer = 0f;

            SetAnimBool(
                "IsWalking",
                true
            );

            SetAnimBool(
                "IsStop",
                false
            );
        }
        else
        {
            SetAnimBool(
                "IsWalking",
                false
            );

            stoppedTimer +=
                Time.deltaTime;

            if (stoppedTimer <
                idleAfterSeconds)
            {
                SetAnimBool(
                    "IsStop",
                    true
                );
            }
            else
            {
                SetAnimBool(
                    "IsStop",
                    false
                );
            }

            if (stoppedTimer >=
                faceUserAfterSeconds)
            {
                FaceUser();
            }
        }
    }


    // =========================================================
    // FACE USER
    // =========================================================

    private void FaceUser()
    {
        if (userTransform == null)
            return;

        Vector3 directionToUser =
            userTransform.position -
            transform.position;

        directionToUser.y = 0f;

        if (directionToUser.sqrMagnitude <
            0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                directionToUser.normalized,
                Vector3.up
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                faceUserRotationSpeed *
                Time.deltaTime
            );
    }


    // =========================================================
    // STOP GUIDING
    // =========================================================

    public void StopGuiding()
    {
        isGuiding = false;

        waypoints.Clear();

        currentWaypointIndex = 0;

        stoppedTimer = 0f;

        SetAnimBool(
            "IsWalking",
            false
        );

        SetAnimBool(
            "IsStop",
            false
        );

        SetAnimBool(
            "isDestination",
            false
        );
    }


    // =========================================================
    // MAIN MOVEMENT
    // =========================================================

    private void Update()
    {
        // -----------------------------------------------------
        // DESTINATION STATE
        // -----------------------------------------------------

        if (!isGuiding)
        {
            if (destinationReached)
            {
                RotateToUserAtDestination();
            }

            return;
        }


        // -----------------------------------------------------
        // CHECK USER
        // -----------------------------------------------------

        if (userTransform == null)
        {
            Debug.LogWarning(
                "ARGuideCharacter: User Transform is missing."
            );

            UpdateAnimator(false);

            return;
        }


        UpdateUserMovement();


        // -----------------------------------------------------
        // HORIZONTAL DISTANCE
        // -----------------------------------------------------

        Vector3 guidePosition =
            transform.position;

        Vector3 userPosition =
            userTransform.position;

        // Ignore height difference.
        guidePosition.y = 0f;
        userPosition.y = 0f;

        float distanceFromUser =
            Vector3.Distance(
                guidePosition,
                userPosition
            );


        // -----------------------------------------------------
        // CHECK ROUTE
        // -----------------------------------------------------

        if (waypoints == null ||
            waypoints.Count == 0)
        {
            StopGuiding();
            return;
        }

        if (currentWaypointIndex >=
            waypoints.Count)
        {
            StopGuiding();
            return;
        }


        // -----------------------------------------------------
        // GET CURRENT TARGET
        // -----------------------------------------------------

        Transform target =
            waypoints[
                currentWaypointIndex
            ];

        if (target == null)
        {
            currentWaypointIndex++;
            return;
        }


        Vector3 targetPosition =
            target.position;


        // -----------------------------------------------------
        // SIDE OFFSET
        // -----------------------------------------------------

        Vector3 travelDirection =
            target.position -
            transform.position;

        travelDirection.y = 0f;

        if (travelDirection.sqrMagnitude >
            0.001f)
        {
            travelDirection.Normalize();

            Vector3 sideDirection =
                Vector3.Cross(
                    Vector3.up,
                    travelDirection
                ).normalized;

            if (!offsetToRightSide)
            {
                sideDirection =
                    -sideDirection;
            }

            targetPosition +=
                sideDirection *
                sideOffset;
        }


        // -----------------------------------------------------
        // KEEP CHARACTER GROUNDED
        // -----------------------------------------------------

        if (keepGrounded)
        {
            targetPosition.y =
                transform.position.y +
                groundOffset;
        }


        // -----------------------------------------------------
        // DIRECTION TO TARGET
        // -----------------------------------------------------

        Vector3 direction =
            targetPosition -
            transform.position;

        direction.y = 0f;

        float distance =
            direction.magnitude;


        // -----------------------------------------------------
        // REACHED WAYPOINT
        // -----------------------------------------------------

        if (distance <=
            stoppingDistance)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >=
                waypoints.Count)
            {
                destinationReached = true;

                isFacingUserAtDestination =
                    false;

                SetAnimBool(
                    "IsWalking",
                    false
                );

                SetAnimBool(
                    "IsStop",
                    false
                );

                SetAnimBool(
                    "isDestination",
                    false
                );

                isGuiding = false;

                Debug.Log(
                    "ARGuideCharacter: Destination reached."
                );

                return;
            }

            return;
        }


        // -----------------------------------------------------
        // ROTATE TOWARD TARGET
        // -----------------------------------------------------

        if (direction.sqrMagnitude >
            0.001f)
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


        // =====================================================
        // DISTANCE-BASED SPEED
        // =====================================================

        float currentMoveSpeed =
            moveSpeed;


        // -----------------------------------------------------
        // HARD STOP AT MAXIMUM DISTANCE
        // -----------------------------------------------------

        if (distanceFromUser >=
            maxDistanceFromUser)
        {
            currentMoveSpeed = 0f;
        }


        // -----------------------------------------------------
        // SLOW DOWN NEAR USER LIMIT
        // -----------------------------------------------------

        else if (
            distanceFromUser >=
            slowDownDistanceFromUser)
        {
            float slowdownAmount =
                Mathf.InverseLerp(
                    maxDistanceFromUser,
                    slowDownDistanceFromUser,
                    distanceFromUser
                );

            currentMoveSpeed =
                moveSpeed *
                slowdownAmount;
        }


        // -----------------------------------------------------
        // NORMAL SPEED
        // -----------------------------------------------------

        else
        {
            currentMoveSpeed =
                moveSpeed;
        }


        // =====================================================
        // CALCULATE NEXT POSITION
        // =====================================================

        Vector3 previousPosition =
            transform.position;

        Vector3 nextPosition =
            Vector3.MoveTowards(
                transform.position,
                targetPosition,
                currentMoveSpeed *
                Time.deltaTime
            );


        // =====================================================
        // HARD MAXIMUM DISTANCE PROTECTION
        // =====================================================

        Vector3 horizontalNextPosition =
            nextPosition;

        Vector3 horizontalUserPosition =
            userPosition;

        horizontalNextPosition.y = 0f;
        horizontalUserPosition.y = 0f;

        float nextDistanceFromUser =
            Vector3.Distance(
                horizontalNextPosition,
                horizontalUserPosition
            );


        // -----------------------------------------------------
        // CANCEL MOVEMENT IF NEXT POSITION EXCEEDS 5M
        // -----------------------------------------------------

        if (nextDistanceFromUser >=
            maxDistanceFromUser)
        {
            nextPosition =
                transform.position;

            currentMoveSpeed = 0f;
        }


        // =====================================================
        // APPLY MOVEMENT
        // =====================================================

        transform.position =
            nextPosition;


        // =====================================================
        // DETERMINE ACTUAL MOVEMENT
        // =====================================================

        float movementAmount =
            Vector3.Distance(
                previousPosition,
                transform.position
            );

        bool isActuallyMoving =
            movementAmount > 0.0001f;


        // =====================================================
        // UPDATE ANIMATOR
        // =====================================================

        UpdateAnimator(
            isActuallyMoving
        );
    }


    // =========================================================
    // FACE USER AT DESTINATION
    // =========================================================

    private void RotateToUserAtDestination()
    {
        if (userTransform == null)
            return;

        Vector3 directionToUser =
            userTransform.position -
            transform.position;

        directionToUser.y = 0f;


        // -----------------------------------------------------
        // USER IS VERY CLOSE
        // -----------------------------------------------------

        if (directionToUser.sqrMagnitude <
            0.001f)
        {
            SetAnimBool(
                "isDestination",
                true
            );

            destinationReached = false;

            Debug.Log(
                "ARGuideCharacter: Facing user. " +
                "Playing destination animation."
            );

            return;
        }


        // -----------------------------------------------------
        // ROTATE TOWARD USER
        // -----------------------------------------------------

        Quaternion targetRotation =
            Quaternion.LookRotation(
                directionToUser.normalized,
                Vector3.up
            );

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                destinationFaceRotationSpeed *
                Time.deltaTime
            );


        // -----------------------------------------------------
        // CHECK ROTATION
        // -----------------------------------------------------

        float angle =
            Quaternion.Angle(
                transform.rotation,
                targetRotation
            );


        if (angle <=
            destinationRotationTolerance)
        {
            transform.rotation =
                targetRotation;

            SetAnimBool(
                "isDestination",
                true
            );

            destinationReached = false;

            Debug.Log(
                "ARGuideCharacter: Finished facing user. " +
                "Playing isDestination animation."
            );
        }
    }
}

