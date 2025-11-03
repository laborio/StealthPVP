using UnityEngine;

/// <summary>
/// Centralizes Animator parameter updates and presentation-side effects for the player character.
/// </summary>
[DisallowMultipleComponent]
public class PlayerPresentationController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float walkAnimationBaseSpeed = 1f;
    [SerializeField] private float runAnimationBaseSpeed = 1f;
    [SerializeField] private float climbAnimationBaseSpeed = 1f;
    [SerializeField] private float finishClimbAnimationBaseSpeed = 1f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem runParticleSystem;

    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int IsIdleHash = Animator.StringToHash("isIdle");
    private static readonly int IsClimbingHash = Animator.StringToHash("isClimbing");
    private static readonly int FinishClimbingHash = Animator.StringToHash("FinishClimbing");
    private static readonly int IsFallingHash = Animator.StringToHash("isFalling");

    public void Apply(PlayerPresentationContext context)
    {
        if (!animator)
        {
            return;
        }

        bool isRunning = context.Pose == PlayerPoseState.Running;
        bool isWalking = context.Pose == PlayerPoseState.Walking;
        bool isIdle = context.Pose == PlayerPoseState.Idle;
        bool isClimbing = context.Pose == PlayerPoseState.Climbing;
        bool isFinishingClimb = context.Pose == PlayerPoseState.FinishingClimb;
        bool isFalling = context.Pose == PlayerPoseState.Falling;

        animator.SetBool(IsRunningHash, isRunning);
        animator.SetBool(IsWalkingHash, isWalking);
        animator.SetBool(IsIdleHash, isIdle);
        animator.SetBool(IsClimbingHash, isClimbing);
        animator.SetBool(FinishClimbingHash, isFinishingClimb);
        animator.SetBool(IsFallingHash, isFalling);

        switch (context.Pose)
        {
            case PlayerPoseState.Running:
            {
                float runSpeed = Mathf.Max(0.001f, context.RunSpeed);
                float normalized = Mathf.Clamp(context.PlanarSpeed / runSpeed, 0f, 2f);
                animator.speed = runAnimationBaseSpeed * normalized;
                UpdateRunParticles(true);
                break;
            }
            case PlayerPoseState.Walking:
            {
                float normalized = Mathf.Clamp(context.PlanarSpeed / Mathf.Max(0.001f, context.WalkSpeed), 0f, 2f);
                animator.speed = walkAnimationBaseSpeed * normalized;
                UpdateRunParticles(false);
                break;
            }
            case PlayerPoseState.Idle:
                animator.speed = 1f;
                UpdateRunParticles(false);
                break;
            case PlayerPoseState.Climbing:
                animator.speed = climbAnimationBaseSpeed;
                UpdateRunParticles(false);
                break;
            case PlayerPoseState.FinishingClimb:
                animator.speed = finishClimbAnimationBaseSpeed;
                UpdateRunParticles(false);
                break;
            case PlayerPoseState.Falling:
                animator.speed = 1f;
                UpdateRunParticles(false);
                break;
        }
    }

    public void ResetPresentation()
    {
        UpdateRunParticles(false);
        if (!animator)
        {
            return;
        }

        animator.SetBool(IsRunningHash, false);
        animator.SetBool(IsWalkingHash, false);
        animator.SetBool(IsIdleHash, false);
        animator.SetBool(IsClimbingHash, false);
        animator.SetBool(FinishClimbingHash, false);
        animator.SetBool(IsFallingHash, false);
        animator.speed = 1f;
    }

    private void UpdateRunParticles(bool shouldBeActive)
    {
        if (!runParticleSystem)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = runParticleSystem.emission;
        emission.enabled = shouldBeActive;
    }

    private void OnDisable()
    {
        UpdateRunParticles(false);
    }

    private void OnValidate()
    {
        walkAnimationBaseSpeed = Mathf.Max(0.01f, walkAnimationBaseSpeed);
        runAnimationBaseSpeed = Mathf.Max(0.01f, runAnimationBaseSpeed);
        climbAnimationBaseSpeed = Mathf.Max(0.01f, climbAnimationBaseSpeed);
        finishClimbAnimationBaseSpeed = Mathf.Max(0.01f, finishClimbAnimationBaseSpeed);
    }
}

public struct PlayerPresentationContext
{
    public PlayerPoseState Pose;
    public float PlanarSpeed;
    public float WalkSpeed;
    public float RunSpeed;
}

public enum PlayerPoseState
{
    Idle,
    Walking,
    Running,
    Climbing,
    FinishingClimb,
    Falling
}
