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
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stoppingDistance = 0.5f;

    [Header("Guide Side Offset")]
    [SerializeField] private float sideOffset = 1f;
    [SerializeField] private bool offsetToRightSide = true;

    [Header("User Distance Control")]
    [Tooltip("Start slowing the guide at this distance.")]
    [SerializeField] private float slowDownDistanceFromUser = 2f;

    [Tooltip("Preferred distance from the user.")]
    [SerializeField] private float followDistance = 1f;

    [Tooltip("ABSOLUTE maximum horizontal distance between user and guide.")]
    [SerializeField] private float maxDistanceFromUser = 3f;

    [Header("User Following")]
    [SerializeField] private float userMovementThreshold = 0.005f;
    [SerializeField] private float verticalAdjustmentSpeed = 1.5f;
    [SerializeField] private Transform userTransform;

    [Header("Audio")]
    [SerializeField] private AudioSource destinationAudioSource;
    [SerializeField] private AudioClip destinationVoiceClip;
    private bool destinationAudioPlayed = false;
    private bool destinationSequenceTriggered = false;

    private Vector3 previousUserPosition;
    private Vector3 userMovementDelta;
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

    [Header("Visual Indicators")]
    [SerializeField] private GameObject waitingBubble; 

    private bool isFacingUserAtDestination = false;
    private bool destinationReached = false;
    private bool destinationWaiting = false;

    private Vector3 finalDestinationPosition;
    private bool finalDestinationPositionCalculated = false;

    [Header("Grounding")]
    [SerializeField] private bool keepGrounded = true;
    [SerializeField] private float groundOffset = -0.2f;

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

        if (destinationAudioSource == null)
        {
            destinationAudioSource = GetComponentInChildren<AudioSource>(true);
        }

        if (destinationVoiceClip == null && destinationAudioSource != null)
        {
            destinationVoiceClip = destinationAudioSource.clip;
        }

        Debug.Log(
            $"[AUDIO INIT] Character={name}, " +
            $"SourceFound={destinationAudioSource != null}, " +
            $"ClipFound={destinationVoiceClip != null}"
        );
    }


    // =========================================================
    // USER MOVEMENT
    // =========================================================

    private void UpdateUserMovement()
    {
        if (userTransform == null)
            return;

        Vector3 currentUserPosition = userTransform.position;

        userMovementDelta = currentUserPosition - previousUserPosition;
        userMovementDelta.y = 0f;

        float movementDistance = userMovementDelta.magnitude;

        userIsMoving = movementDistance >= userMovementThreshold;

        previousUserPosition = currentUserPosition;
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
        destinationWaiting = false;
        isFacingUserAtDestination = false;
        destinationAudioPlayed = false;
        destinationSequenceTriggered = false;

        finalDestinationPositionCalculated = false;

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


    private void UpdateAnimator(bool isActuallyMoving)
    {
        if (animator == null)
            return;

        if (isActuallyMoving)
        {
            // User is moving and character is moving
            stoppedTimer = 0f;

            SetAnimBool("IsWalking", true);
            SetAnimBool("IsStop", false);
            if (waitingBubble != null) 
                waitingBubble.SetActive(false);
        }
        else
        {
            // Character has stopped moving
            SetAnimBool("IsWalking", false);

            if (!userIsMoving)
            {
                // User is also stopped → count the idle time
                stoppedTimer += Time.deltaTime;

                if (stoppedTimer >= idleAfterSeconds)
                {
                    SetAnimBool("IsStop", true);
                    FaceUser();
                    // Show waiting indicator when character stops and faces user
                    if (waitingBubble != null) waitingBubble.SetActive(true);
                }
                else
                {
                    SetAnimBool("IsStop", false);
                    // hide waiting indicator
                    if (waitingBubble != null) waitingBubble.SetActive(false);
                }
            }
            else
            {
                // User started moving again
                stoppedTimer = 0f;
                SetAnimBool("IsStop", false);
                if (waitingBubble != null) 
                    waitingBubble.SetActive(false);
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

        if (directionToUser.sqrMagnitude < 0.001f)
            return;

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

         // DESTINATION STATE HAS PRIORITY OVER NORMAL IDLE
        if (destinationWaiting)
        {
            // Never allow normal IsStop behavior during arrival.
            SetAnimBool("IsWalking", false);
            SetAnimBool("IsStop", false);

            if (waitingBubble != null)
                waitingBubble.SetActive(false);

            // Keep the character facing the user.
            FaceUser();

            stoppedTimer += Time.deltaTime;

            // Trigger the arrival animation and voice only once.
            if (stoppedTimer >= 2f && !destinationSequenceTriggered)
            {
                destinationSequenceTriggered = true;
                SetAnimBool("isDestination", true);

                Debug.Log("[DESTINATION] Arrival animation triggered once.");

            if (!destinationAudioPlayed)
            {
                if (destinationAudioSource == null)
                {
                    Debug.LogError(
                        "[DESTINATION AUDIO] AudioSource is missing!"
                    );
                }
                else if (destinationVoiceClip == null)
                {
                    Debug.LogError(
                        "[DESTINATION AUDIO] Voice clip is missing!"
                    );
                }
                else if (!destinationAudioSource.isActiveAndEnabled)
                {
                    Debug.LogError(
                        "[DESTINATION AUDIO] AudioSource is disabled!"
                    );
                }
                else
                {
                    destinationAudioSource.PlayOneShot(
                        destinationVoiceClip
                    );

                    destinationAudioPlayed = true;

                    Debug.Log(
                        $"[DESTINATION AUDIO] Playing: " +
                        $"{destinationVoiceClip.name}, " +
                        $"Volume: {destinationAudioSource.volume}"
                    );
                }
            }
            }

            return;
        }

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
            // StopGuiding();
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
        // DESTINATION SIDE OFFSET
        // -----------------------------------------------------
        // Calculate the final destination offset ONLY ONCE.
        // Do NOT recalculate it from the character's current
        // position every frame, otherwise the character can
        // orbit/circle around the destination.

        if (currentWaypointIndex == waypoints.Count - 1)
        {
            if (!finalDestinationPositionCalculated)
            {
                Vector3 travelDirection;

                // Use the direction of the final route segment.
                if (currentWaypointIndex > 0 &&
                    waypoints[currentWaypointIndex - 1] != null)
                {
                    travelDirection =
                        target.position -
                        waypoints[currentWaypointIndex - 1].position;
                }
                else
                {
                    travelDirection =
                        target.position -
                        transform.position;
                }

                travelDirection.y = 0f;

                if (travelDirection.sqrMagnitude > 0.001f)
                {
                    travelDirection.Normalize();

                    Vector3 sideDirection =
                        Vector3.Cross(
                            Vector3.up,
                            travelDirection
                        ).normalized;

                    if (!offsetToRightSide)
                        sideDirection = -sideDirection;

                    finalDestinationPosition =
                        target.position +
                        sideDirection * sideOffset;
                }
                else
                {
                    finalDestinationPosition = target.position;
                }

                finalDestinationPositionCalculated = true;

                Debug.Log(
                    $"ARGuideCharacter: Final destination position calculated. " +
                    $"Offset = {sideOffset}m"
                );
            }

            targetPosition = finalDestinationPosition;
        }


        // -----------------------------------------------------
        // KEEP CHARACTER GROUNDED
        // -----------------------------------------------------

        if (keepGrounded)
        {
            targetPosition.y =
                target.position.y +
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
        // Reach Destination
        // -----------------------------------------------------

        if (distance <= stoppingDistance)
        {
             Debug.Log(
                        $"[DESTINATION CHECK] Character reached target. " +
                        $"Distance = {distance:F3}m, " +
                        $"Stopping Distance = {stoppingDistance:F3}m, " +
                        $"Waypoint = {currentWaypointIndex}/{waypoints.Count - 1}"
                    );

            currentWaypointIndex++;

           if (currentWaypointIndex >= waypoints.Count)
            {
                // Only initialize destination state once
                if (!destinationWaiting)
                {
                    destinationReached = true;
                    destinationWaiting = true;

                    SetAnimBool("IsWalking", false);
                    SetAnimBool("IsStop", false);

                    Debug.Log("[DESTINATION] Final waypoint reached. " + "Starting destination behavior.");
                }

                Debug.Log("ARGuideCharacter: Destination reached.");
                return;
            }

            return;
        }


        // =====================================================
        // MOVEMENT DECISION ORDER
        // =====================================================
        // 1. Destination reached has already been handled above.
        // 2. If the user is not moving, the guide must stop.
        // 3. If the guide has reached the max allowed distance, stop
        //    and wait for the user to close back in to the comfort range.
        // 4. If the guide is approaching the max distance, slow down.
        // 5. Otherwise the guide moves along the route while the user is
        //    walking and the guide is within the allowed range.

        // The guide follows exactly the same horizontal distance the user
        // travels each frame. This prevents the guide from chasing the user.


        // -----------------------------------------------------
        // ROTATE TOWARD TARGET
        // -----------------------------------------------------

        // Only turn toward the route while the USER is walking.
        // When the user stops, the guide keeps its current facing for the
        // 5-second wait, then FaceUser() takes over.
        // if (userIsMoving && direction.sqrMagnitude > 0.001f)
        // {
        //     Quaternion targetRotation =
        //         Quaternion.LookRotation(
        //             direction.normalized,
        //             Vector3.up
        //         );

        //     transform.rotation =
        //         Quaternion.Slerp(
        //             transform.rotation,
        //             targetRotation,
        //             rotationSpeed *
        //             Time.deltaTime
        //         );
        // }
        // -----------------------------------------------------
        // ROTATE TOWARD DESTINATION
        // -----------------------------------------------------

        if (userIsMoving && direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * 90f * Time.deltaTime
            );
        }


        // =====================================================
        // CALCULATE NEXT POSITION
        // =====================================================
        Vector3 previousPosition = transform.position;

        // Move horizontally toward the route waypoint.
        Vector3 horizontalTarget = targetPosition;
        horizontalTarget.y = transform.position.y;

        float stepDistance = userIsMoving
            ? userMovementDelta.magnitude
            : 0f;

        Vector3 nextPosition = Vector3.MoveTowards(
            transform.position,
            horizontalTarget,
            stepDistance
        );

        // Adjust height independently, even when the user is standing still.
        nextPosition.y = Mathf.MoveTowards(
            transform.position.y,
            targetPosition.y,
            verticalAdjustmentSpeed * Time.deltaTime
        );

        // Vector3 previousPosition =
        //     transform.position;

        // // Advance by the same horizontal distance the user travelled this frame.
        // // The guide still follows the route, so the user cannot make it chase them.
        // float stepDistance = userIsMoving ? userMovementDelta.magnitude : 0f;

        // Vector3 nextPosition =
        //     Vector3.MoveTowards(
        //         transform.position,
        //         targetPosition,
        //         stepDistance
        //     );


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
        UpdateAnimator(isActuallyMoving );
    }

    public void PlayDestinationVoice()
    {
        if (destinationAudioSource == null)
        {
            Debug.LogWarning("ARGuideCharacter: Destination Audio Source is not assigned.");
            return;
        }

        if (destinationVoiceClip == null)
        {
            Debug.LogWarning(
                "ARGuideCharacter: Destination Voice Clip is not assigned."
            );
            return;
        }

        destinationAudioSource.PlayOneShot(destinationVoiceClip);

        Debug.Log(
            "ARGuideCharacter: Destination voice played."
        );
    }
}