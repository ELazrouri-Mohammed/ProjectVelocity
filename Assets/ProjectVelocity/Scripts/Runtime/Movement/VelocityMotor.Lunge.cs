using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Lunge for <see cref="VelocityMotor"/>: a short dash that closes in on a point, used by the blade attack.
    /// <see cref="CombatController"/> picks the point and decides what counts as a hit; the motor only moves.
    /// Like a traversal-target pull it turns your current velocity toward the point instead of replacing it: nothing is
    /// zeroed, there's no teleport, and arriving faster than the lunge speed keeps your speed. Started on the ground it stays
    /// horizontal (jumping mid-lunge still works); started in the air it flies straight at the point with gravity paused.
    /// It ends when told to, when it reaches or passes the point, when something blocks it, or when its time runs out,
    /// always keeping its speed. Also holds the small movement rewards combat hands out.
    /// <see cref="Tick"/> calls into it at a few fixed points.
    /// </summary>
    public sealed partial class VelocityMotor
    {
        // Within this distance (m) of the point the lunge is over, so it never circles a point it can't quite reach.
        const float LungeArrivalDistance = 0.5f;

        public bool IsLunging => lunging;
        /// <summary>
        /// Whether a lunge can start right now: not while a traversal target pulls you in, nor while swinging (attacks during a
        /// swing are cut in passing, without leaving the rope).
        /// </summary>
        public bool CanLunge => !targetPulling && !tethering;

        bool lunging;
        Vector3 lungeGoal;
        float lungeSpeed;
        float lungeTimeLeft;
        float lungeTurnRate;
        // Horizontal only: started on the ground (or landed during it).
        bool lungeFlat;
        // Whether you've been heading toward the point yet; only then can it be "behind you" (passed).
        bool lungeHeadedIn;

        bool LungeHoldsAltitude => lunging && !lungeFlat;

        void TryStartLunge(MovementTuning t, in MotorLunge request)
        {
            if (!request.IsRequested || !CanLunge)
                return;

            if (wallRunning)
                StopWallRun(t, WallExit.Lunged);

            lungeFlat = grounded;
            Vector3 velocity = lungeFlat ? planarVelocity : planarVelocity + Vector3.up * verticalSpeed;
            lungeSpeed = Mathf.Max(request.Speed, velocity.magnitude);
            lungeGoal = request.Goal;
            lungeTimeLeft = request.Duration;
            lungeTurnRate = request.TurnRate;
            lungeHeadedIn = false;
            lunging = true;

            // The lunge takes over from a boost, keeping its speed.
            boostTimer = 0f;
            boostHoldsAltitude = false;

            if (!lungeFlat)
            {
                // In the air it commits you until it's over: no late ground or wall jump halfway through.
                jumpRising = false;
                coyoteTimer = 0f;
                wallJumpCoyoteTimer = 0f;
                wallJumpCommitTimer = 0f;
            }
        }

        /// <summary>Planar (and, in the air, vertical) motion while lunging.</summary>
        void UpdateLunge(float dt)
        {
            lungeTimeLeft -= dt;
            if (grounded)
                lungeFlat = true; // landed mid-lunge: carry on along the ground

            Vector3 toGoal = lungeGoal - BodyCenter;
            if (lungeFlat)
                toGoal.y = 0f;
            float distance = toGoal.magnitude;
            Vector3 velocity = lungeFlat ? planarVelocity : planarVelocity + Vector3.up * verticalSpeed;
            float speed = velocity.magnitude;

            // Swing the direction you're already moving in toward the point, at full lunge speed.
            Vector3 toward = distance > 1e-4f ? toGoal / distance : speed > 1e-4f ? velocity / speed : Vector3.forward;
            Vector3 direction = speed > StartMovingSpeed ? velocity / speed : toward;
            direction = Vector3.RotateTowards(direction, toward, lungeTurnRate * Mathf.Deg2Rad * dt, 0f);

            float ahead = Vector3.Dot(toGoal, direction);
            if (ahead > 0f)
                lungeHeadedIn = true;
            if (lungeTimeLeft <= 0f || distance <= LungeArrivalDistance || (lungeHeadedIn && ahead <= 0f))
            {
                EndLunge(); // over: carry on with the speed you have
                return;
            }

            planarVelocity = new Vector3(direction.x, 0f, direction.z) * lungeSpeed;
            if (!lungeFlat)
                verticalSpeed = direction.y * lungeSpeed;
        }

        /// <summary>Ends the lunge, keeping the current velocity. Combat calls it when the blade connects.</summary>
        public void EndLunge()
        {
            lunging = false;
            lungeHeadedIn = false;
            lungeTimeLeft = 0f;
        }

        /// <summary>Called right after the controller moves: a lunge that ran into something stops there.</summary>
        void CheckLungeProgress(Vector3 intended, Vector3 moved)
        {
            if (!lunging)
                return;
            float intendedDistance = intended.magnitude;
            if (intendedDistance < 1e-4f)
                return;

            if (Vector3.Dot(moved, intended) / intendedDistance < intendedDistance * PullBlockedProgress)
                EndLunge();
        }

        /// <summary>Gives back one spent air boost. False when none was spent: boosts never stack past the maximum.</summary>
        public bool RefreshAirBoost()
        {
            if (airBoostsUsed <= 0)
                return false;
            airBoostsUsed--;
            return true;
        }

        /// <summary>Skips what's left of the boost cooldown, so a boost can start straight away.</summary>
        public void ReadyBoost()
        {
            boostCooldownTimer = 0f;
        }

        /// <summary>In the air, rise at least this fast (m/s), keeping horizontal speed (an aerial kill's pop). Not on the ground.</summary>
        public void PopUp(float upSpeed)
        {
            if (grounded || wallRunning || targetPulling || tethering)
                return;
            verticalSpeed = Mathf.Max(verticalSpeed, upSpeed);
            jumpRising = false;
        }

        /// <summary>Adds speed (m/s) along the current horizontal direction of travel. Does nothing while standing still.</summary>
        public void AddMomentum(float speed)
        {
            float current = planarVelocity.magnitude;
            if (speed <= 0f || current < StartMovingSpeed)
                return;
            planarVelocity *= (current + speed) / current;
        }
    }
}
