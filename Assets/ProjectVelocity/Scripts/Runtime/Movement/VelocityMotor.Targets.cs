using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Traversal-target propulsion for <see cref="VelocityMotor"/>: a slingshot through a <see cref="TraversalTarget"/>.
    /// Activating a target turns your current velocity toward it (nothing is zeroed and there is no teleport), carries you
    /// through its centre at Propulsion Speed or faster, and launches you out the far side at that speed, tilted up by the
    /// Upward Bias. Gravity pauses only for the short pull in; after the launch it's ordinary air movement, so you can
    /// steer, boost, wall run, hit another target or land. <see cref="Tick"/> calls into it at a few fixed points.
    /// </summary>
    public sealed partial class VelocityMotor
    {
        // Covering less than this share of the intended distance in a frame means something is in the way.
        const float PullBlockedProgress = 0.5f;

        /// <summary>Raised when a target starts pulling you in.</summary>
        public event Action<TraversalTarget> TargetPullStarted;
        /// <summary>Raised when you pass through a target and are launched out of it.</summary>
        public event Action<TraversalTarget> TargetLaunched;

        public bool IsTargetPulling => targetPulling;
        /// <summary>The target pulling you in right now, or null.</summary>
        public TraversalTarget PullTarget => targetPulling ? pullTarget : null;
        /// <summary>Whether the activate button would launch through a target right now.</summary>
        public bool CanActivateTarget => !targetPulling && targetCooldownTimer <= 0f;
        public float TargetCooldownRemaining => targetCooldownTimer;
        /// <summary>Centre of the body in world space: what targets pull through their centre.</summary>
        public Vector3 BodyCenter => transform.position + (controller != null ? controller.center : Vector3.up);

        bool targetPulling;
        TraversalTarget pullTarget;
        float pullSpeed;
        float pullTime;
        float pullUpwardBias;
        // Whether you've been heading toward the target yet; only then can it be "behind you" (passed).
        bool pullHeadedIn;
        float targetCooldownTimer;

        void TickTargetTimers(float dt)
        {
            targetCooldownTimer = Mathf.Max(0f, targetCooldownTimer - dt);
        }

        /// <summary>Starts the pull through <paramref name="target"/>. Works from the ground, the air, a boost or a wall.</summary>
        void TryStartTargetPull(MovementTuning t, TraversalTarget target)
        {
            if (target == null || !target.isActiveAndEnabled || !target.IsReady || !CanActivateTarget)
                return;

            if (wallRunning)
                StopWallRun(t, WallExit.Targeted);
            EndLunge(); // the target takes over from a lunge

            // Respect the speed you arrive with: a faster entry is kept (per Momentum Keep), a slower one builds up.
            float entrySpeed = (planarVelocity + Vector3.up * verticalSpeed).magnitude;
            pullSpeed = Mathf.Max(t.targetPropulsionSpeed * target.StrengthMultiplier, entrySpeed * t.targetMomentumKeep);
            pullUpwardBias = Mathf.Clamp01(t.targetUpwardBias + target.ExtraUpwardBias);
            pullTime = 0f;
            pullHeadedIn = false;
            pullTarget = target;
            targetPulling = true;

            // The pull takes over from the ground, a jump, a boost or a wall until you're through.
            grounded = false;
            groundNormal = Vector3.up;
            coyoteTimer = 0f;
            jumpRising = false;
            boostTimer = 0f;
            boostHoldsAltitude = false;
            wallJumpCoyoteTimer = 0f;
            wallJumpCommitTimer = 0f;

            if (t.targetRefreshesAirBoost && airBoostsUsed > 0)
                airBoostsUsed--;

            TargetPullStarted?.Invoke(target);
        }

        /// <summary>Planar and vertical motion while being pulled in. Launches you once you're through.</summary>
        void UpdateTargetPull(MovementTuning t, float dt)
        {
            if (pullTarget == null || !pullTarget.isActiveAndEnabled)
            {
                EndTargetPull(t, false);
                return;
            }

            pullTime += dt;
            Vector3 toTarget = pullTarget.Position - BodyCenter;
            float distance = toTarget.magnitude;
            Vector3 velocity = planarVelocity + Vector3.up * verticalSpeed;
            float speed = velocity.magnitude;

            // Swing the direction you're already moving in toward the target, keeping speed through the turn.
            Vector3 toward = distance > 1e-4f ? toTarget / distance : (speed > 1e-4f ? velocity / speed : transform.forward);
            Vector3 direction = speed > StartMovingSpeed ? velocity / speed : toward;
            direction = Vector3.RotateTowards(direction, toward, t.targetRedirectRate * Mathf.Deg2Rad * dt, 0f);
            speed = t.targetPullAcceleration > 0f ? Mathf.MoveTowards(speed, pullSpeed, t.targetPullAcceleration * dt) : pullSpeed;

            // Through it: close enough, about to pass its centre this frame, or you were heading in and it's now beside or
            // behind you (you swept past). Starting out moving away from it is not passing it: the turn just takes longer.
            float ahead = Vector3.Dot(toTarget, direction);
            if (ahead > 0f)
                pullHeadedIn = true;
            if (distance <= Mathf.Max(t.targetArrivalDistance, speed * dt) || (pullHeadedIn && ahead <= 0f))
            {
                LaunchFromTarget(t, direction);
                return;
            }

            planarVelocity = new Vector3(direction.x, 0f, direction.z) * speed;
            verticalSpeed = direction.y * speed;

            if (pullTime >= t.targetMaxPullTime)
                EndTargetPull(t, false); // never reached it: let go, keeping this speed
        }

        /// <summary>The slingshot: out the far side at full pull speed, tilted toward straight up by the upward bias.</summary>
        void LaunchFromTarget(MovementTuning t, Vector3 direction)
        {
            Vector3 launch = direction;
            if (t.targetLaunchLevelsOut && launch.y < 0f)
            {
                launch.y = 0f;
                launch = launch.sqrMagnitude > 1e-4f ? launch.normalized : Flatten(transform.forward);
            }

            // A redirect, not an extra push: the direction tilts up by a share of the angle left to vertical, speed stays.
            float angleToUp = Vector3.Angle(launch, Vector3.up);
            if (angleToUp < 179f)
                launch = Vector3.RotateTowards(launch, Vector3.up, angleToUp * pullUpwardBias * Mathf.Deg2Rad, 0f);

            float exitSpeed = pullSpeed * t.targetExitSpeedKeep;
            planarVelocity = new Vector3(launch.x, 0f, launch.z) * exitSpeed;
            verticalSpeed = launch.y * exitSpeed;
            EndTargetPull(t, true);
        }

        /// <summary>Ends the pull, keeping the current velocity. The target starts its cooldown either way.</summary>
        void EndTargetPull(MovementTuning t, bool launched)
        {
            if (!targetPulling)
                return;

            TraversalTarget target = pullTarget;
            targetPulling = false;
            pullTarget = null;
            jumpRising = false;
            targetCooldownTimer = t.targetReactivationCooldown;

            if (target == null)
                return;
            target.StartCooldown();
            if (launched)
                TargetLaunched?.Invoke(target);
        }

        /// <summary>Called right after the controller moves: a pull that ran into something stops there.</summary>
        void CheckTargetPullProgress(MovementTuning t, Vector3 intended, Vector3 moved)
        {
            if (!targetPulling)
                return;
            float intendedDistance = intended.magnitude;
            if (intendedDistance < 1e-4f)
                return;

            // Blocked by a wall, ceiling or floor: let go so the normal wall and landing logic takes it from here.
            if (Vector3.Dot(moved, intended) / intendedDistance < intendedDistance * PullBlockedProgress)
                EndTargetPull(t, false);
        }

        void ResetTargetState()
        {
            targetPulling = false;
            pullTarget = null;
            pullTime = 0f;
            pullHeadedIn = false;
            targetCooldownTimer = 0f;
        }
    }
}
