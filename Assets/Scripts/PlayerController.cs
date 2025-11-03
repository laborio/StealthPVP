using UnityEngine;

[RequireComponent(typeof(PlayerInputRouter))]
[RequireComponent(typeof(PlayerLocomotion))]
[RequireComponent(typeof(PlayerClimbController))]
[RequireComponent(typeof(PlayerPresentationController))]
public class PlayerController : MonoBehaviour
{
    [Header("Subsystems")]
    [SerializeField] private PlayerInputRouter inputRouter;
    [SerializeField] private PlayerLocomotion locomotion;
    [SerializeField] private PlayerClimbController climbController;
    [SerializeField] private PlayerPresentationController presentationController;

    private CapsuleCollider _capsuleCollider;
    private bool _isFalling;
    private bool _runIntentActive;

    private void Reset()
    {
        inputRouter = GetComponent<PlayerInputRouter>();
        locomotion = GetComponent<PlayerLocomotion>();
        climbController = GetComponent<PlayerClimbController>();
        presentationController = GetComponent<PlayerPresentationController>();
    }

    private void Awake()
    {
        if (!inputRouter)
        {
            inputRouter = GetComponent<PlayerInputRouter>();
        }

        if (!locomotion)
        {
            locomotion = GetComponent<PlayerLocomotion>();
        }

        if (!climbController)
        {
            climbController = GetComponent<PlayerClimbController>();
        }

        if (!presentationController)
        {
            presentationController = GetComponent<PlayerPresentationController>();
        }

        if (!TryGetComponent(out _capsuleCollider))
        {
            Debug.LogWarning("PlayerController: CapsuleCollider component not found.", this);
        }
        else
        {
            climbController.AttachCollider(_capsuleCollider);
            locomotion.AttachCapsuleCollider(_capsuleCollider);
        }
    }

    private void Update()
    {
        PlayerInputSnapshot inputSnapshot = inputRouter ? inputRouter.PollInput() : default;

        HandleStopCommand(inputSnapshot);
        HandleClickToMove(inputSnapshot);
        HandleClimbRequest(inputSnapshot);

        UpdateMovement(inputSnapshot);
    }

    private void HandleStopCommand(PlayerInputSnapshot snapshot)
    {
        if (!snapshot.StopPressed)
        {
            return;
        }

        locomotion.ApplyImmediateStop();
        locomotion.ClearMoveTarget();

        if (climbController.IsFinishingClimb)
        {
            return;
        }

        if (climbController.IsClimbing)
        {
            climbController.ForceDropFromClimb();
            _isFalling = true;
        }
    }

    private void HandleClickToMove(PlayerInputSnapshot snapshot)
    {
        if (!snapshot.MoveIssued)
        {
            return;
        }

        if (_isFalling || climbController.IsClimbing || climbController.IsFinishingClimb)
        {
            return;
        }

        if (!locomotion.IsGrounded)
        {
            return;
        }

        locomotion.SetMoveTarget(snapshot.MoveTarget);
    }

    private void HandleClimbRequest(PlayerInputSnapshot snapshot)
    {
        if (!snapshot.ClimbPressed)
        {
            return;
        }

        if (climbController.CanBeginClimb)
        {
            locomotion.ApplyImmediateStop();
            locomotion.ClearMoveTarget();
            climbController.BeginClimb();
            _isFalling = false;
        }
    }

    private void UpdateMovement(PlayerInputSnapshot snapshot)
    {
        float deltaTime = Time.deltaTime;
        PlayerPoseState pose = PlayerPoseState.Idle;
        float planarSpeed = 0f;

        _runIntentActive = snapshot.RunHeld && !climbController.IsClimbing && !climbController.IsFinishingClimb;

        if (climbController.IsClimbing)
        {
            locomotion.ApplyImmediateStop();
            locomotion.ClearMoveTarget();
            locomotion.EvaluateGroundedState(true);

            Vector3 climbVelocity = climbController.TickClimb(deltaTime, transform);
            climbController.CheckForClimbStop(transform);
            climbController.UpdateClimbRotation(deltaTime, transform);

            pose = PlayerPoseState.Climbing;
            planarSpeed = new Vector3(climbVelocity.x, 0f, climbVelocity.z).magnitude;
            _isFalling = false;
        }
        else if (climbController.IsFinishingClimb)
        {
            locomotion.ApplyImmediateStop();
            locomotion.ClearMoveTarget();
            locomotion.EvaluateGroundedState(true);

            Vector3 finishVelocity = climbController.TickFinishClimb(deltaTime, transform);
            climbController.UpdateClimbRotation(deltaTime, transform);

            pose = PlayerPoseState.FinishingClimb;
            planarSpeed = new Vector3(finishVelocity.x, 0f, finishVelocity.z).magnitude;
            _isFalling = false;
        }
        else
        {
            locomotion.EvaluateGroundedState(false);

            if (!locomotion.IsGrounded)
            {
                Vector3 fallVelocity = locomotion.TickFalling(deltaTime);
                pose = PlayerPoseState.Falling;
                planarSpeed = new Vector3(fallVelocity.x, 0f, fallVelocity.z).magnitude;
                _isFalling = true;
            }
            else
            {
                bool runActive = _runIntentActive && locomotion.HasMoveTarget;
                LocomotionStep step = locomotion.TickGrounded(deltaTime, runActive);
                locomotion.ApplyRotation(deltaTime);

                planarSpeed = step.PlanarVelocity.magnitude;
                _isFalling = false;

                if (step.IsRunning)
                {
                    pose = PlayerPoseState.Running;
                }
                else if (step.IsMoving)
                {
                    pose = PlayerPoseState.Walking;
                }
                else
                {
                    pose = PlayerPoseState.Idle;
                }
            }
        }

        climbController.UpdateFinishClimbTimer(deltaTime);
        ApplyPresentation(pose, planarSpeed);
    }

    private void ApplyPresentation(PlayerPoseState pose, float planarSpeed)
    {
        if (!presentationController)
        {
            return;
        }

        PlayerPresentationContext context = new PlayerPresentationContext
        {
            Pose = pose,
            PlanarSpeed = planarSpeed,
            WalkSpeed = locomotion ? locomotion.WalkSpeed : 0f,
            RunSpeed = locomotion ? locomotion.RunSpeed : 0f
        };

        presentationController.Apply(context);
    }

    private void OnCollisionEnter(Collision collision)
    {
        climbController.HandleCollisionEnter(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        climbController.HandleCollisionStay(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        climbController.HandleCollisionExit(collision);
    }

    private void OnDisable()
    {
        locomotion.ResetState();
        climbController.ResetAll();
        presentationController.ResetPresentation();
        _isFalling = false;
        _runIntentActive = false;
    }

    public void ResetFinishClimbFlag()
    {
        climbController.ResetFinishClimbState();
        locomotion.ApplyImmediateStop();
        locomotion.ClearMoveTarget();
        _isFalling = false;
        ApplyPresentation(PlayerPoseState.Idle, 0f);
    }
}
