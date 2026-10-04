using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Wall traversal for <see cref="VelocityMotor"/>: wall run, upward wall run (climb) and wall jump.
    /// It shares the motor's momentum state; <see cref="Tick"/> calls into it at a few fixed points.
    /// There is no wall button: walls are caught automatically when airborne, fast enough and heading into them,
    /// and touching a wall redirects your momentum instead of resetting it.
    /// </summary>
    public sealed partial class VelocityMotor
    {
        // Two surfaces are the same wall when their normals are within ~25° of each other.
        const float SameWallDot = 0.9f;
        // While attached, the run follows the wall around bends of up to ~45° per frame.
        const float WallBendDot = 0.7f;
        // Upper and lower probe hits further apart than this (m), beyond what the allowed tilt explains, are a step, not one wall.
        const float WallPlaneTolerance = 0.15f;
        // Moving toward a wall slower than this (m/s) does not count as heading into it.
        const float WallApproachSpeed = 0.5f;
        // The stick pointing at least ~10° into a wall counts as steering into it.
        const float WallIntentDot = 0.17f;
        // Stick deflection needed before pointing it away from the wall releases you.
        const float WallReleaseMinInput = 0.25f;
        // The stick pointing this far back against the run (~120°) brakes.
        const float WallBrakeDot = -0.5f;
        // To be worth running on, a wall must continue ahead for this long at the current speed (clamped, m).
        const float WallAheadTime = 0.1f;
        const float MinWallAhead = 1f;
        const float MaxWallAhead = 3f;
        // How fast the body closes the gap to a wall it caught from a little way off (m/s).
        const float WallSnapSpeed = 8f;
        // Sideways nudge off the wall when a run runs out, so you visibly peel away (m/s).
        const float WallDropOffPush = 2f;
        // Pop when a climb reaches the top of a wall, so you clear the edge instead of hanging just below it (m/s).
        const float ClimbTopOutUpSpeed = 6f;
        const float ClimbTopOutForwardSpeed = 5f;

        // Wall jump assist (see TryAssistWallJump).
        // Aim the head this far below a lower wall's top edge (m).
        const float AssistTopMargin = 0.5f;
        // Vertical spacing of the rays that look for the destination wall, and how far below the feet they reach (m).
        const float AssistScanStep = 1f;
        const float AssistScanDepth = 6f;
        // You must be steering, and heading, at least ~20° into the destination wall.
        const float AssistAimDot = 0.34f;
        // A top at least this deep (m) is somewhere to land rather than a wall to sail past: no assist.
        const float AssistLandingDepth = 2f;
        // Hits within this distance (m) of each other belong to the same wall face.
        const float AssistFaceTolerance = 0.5f;
        // You may reach the wall up to this much later than the straight-line estimate, since turning takes time.
        const float AssistTimeSlack = 1.35f;

        enum WallExit
        {
            Lost,
            Released,
            Expired,
            Jumped,
            Boosted,
            Landed,
            Targeted,
            Lunged,
        }

        public event Action WallRunStarted;
        public event Action WallRunEnded;
        public event Action WallJumped;

        public bool IsWallRunning => wallRunning;
        /// <summary>Horizontal normal of the wall being run on, pointing out of it. Zero when not on a wall.</summary>
        public Vector3 WallNormal => wallRunning ? wallRunNormal : Vector3.zero;
        /// <summary>Seconds left before the current wall run runs out.</summary>
        public float WallRunTimeRemaining => wallRunning ? Mathf.Max(0f, Settings.maxWallRunDuration - wallRunTime) : 0f;
        /// <summary>Upward speed of the last wall jump (m/s), negative before the first one. Debug readout.</summary>
        public float LastWallJumpUpSpeed => lastWallJumpUpSpeed;
        /// <summary>Whether the wall jump assist lowered the last wall jump to reach a lower wall. Debug readout.</summary>
        public bool LastWallJumpAssisted => lastWallJumpAssisted;

        bool wallRunning;
        // The wall being run on, or the last one this airtime (forgotten on landing).
        Vector3 wallRunNormal;
        Collider wallCollider;
        float wallGap;
        bool wallTopReached;
        // Time on that wall this airtime, and how much of it was spent rising.
        float wallRunTime;
        float wallRiseTime;

        float wallReattachTimer;
        float wallJumpCoyoteTimer;
        float wallJumpCommitTimer;
        float wallMaxNormalY;
        float lastWallJumpUpSpeed = -1f;
        bool lastWallJumpAssisted;

        // Collected while the controller moves, used right after.
        Vector3 preMovePlanarVelocity;
        bool wallContact;
        Vector3 wallContactNormal;
        float wallContactScore;

        bool CanWallJump => !grounded && (wallRunning || wallJumpCoyoteTimer > 0f);

        void TickWallTimers(MovementTuning t, float dt)
        {
            wallReattachTimer = Mathf.Max(0f, wallReattachTimer - dt);
            wallJumpCoyoteTimer = Mathf.Max(0f, wallJumpCoyoteTimer - dt);
            wallJumpCommitTimer = Mathf.Max(0f, wallJumpCommitTimer - dt);
            wallMaxNormalY = Mathf.Sin(t.maxWallTilt * Mathf.Deg2Rad);
        }

        /// <summary>Checks the run can go on this frame; ends it (keeping momentum) when it can't.</summary>
        bool KeepWallRun(MovementTuning t, Vector3 wishDir, float wishAmount)
        {
            bool wasAtTop = wallTopReached;
            if (!ProbeAttachedWall(t))
            {
                // The wall is gone: ran off its end, or climbed over its top.
                bool climbedOver = wasAtTop && verticalSpeed > 0f;
                StopWallRun(t, WallExit.Lost);
                if (climbedOver)
                {
                    verticalSpeed = Mathf.Max(verticalSpeed, ClimbTopOutUpSpeed);
                    planarVelocity -= wallRunNormal * ClimbTopOutForwardSpeed;
                }
                return false;
            }

            float alongSpeed = (planarVelocity - wallRunNormal * Vector3.Dot(planarVelocity, wallRunNormal)).magnitude;
            if (wallRunTime >= t.maxWallRunDuration || (alongSpeed < t.wallRunMinSpeed && verticalSpeed <= 0f))
            {
                StopWallRun(t, WallExit.Expired);
                return false;
            }

            // Steering out of the wall peels you off it; air control takes over with all your speed.
            if (wishAmount > WallReleaseMinInput &&
                Vector3.Dot(wishDir, wallRunNormal) > Mathf.Sin(t.wallReleaseAngle * Mathf.Deg2Rad))
            {
                StopWallRun(t, WallExit.Released);
                return false;
            }

            return true;
        }

        /// <summary>Planar and vertical motion while attached to a wall.</summary>
        void UpdateWallRun(MovementTuning t, Vector3 wishDir, float wishAmount, float dt)
        {
            wallRunTime += dt;

            Vector3 n = wallRunNormal;
            Vector3 along = planarVelocity - n * Vector3.Dot(planarVelocity, n);
            float alongSpeed = along.magnitude;

            Vector3 direction;
            if (alongSpeed > StartMovingSpeed)
            {
                direction = along / alongSpeed;
            }
            else
            {
                // Climbing straight up: steering along the wall picks a direction to run in.
                Vector3 wishAlong = wishDir - n * Vector3.Dot(wishDir, n);
                direction = wishAmount > 0f && wishAlong.sqrMagnitude > 0.09f ? wishAlong.normalized : Vector3.zero;
            }

            // The run keeps a strong pace by itself; pulling back against it brakes.
            float alongInput = wishAmount > 0f ? Vector3.Dot(wishDir, direction) : 0f;
            if (alongInput < WallBrakeDot)
                alongSpeed = Mathf.MoveTowards(alongSpeed, 0f, t.wallBrake * dt);
            else if (alongSpeed > t.wallRunSpeed)
                alongSpeed = Mathf.MoveTowards(alongSpeed, t.wallRunSpeed, t.wallRunMomentumDecay * dt);
            else if (direction != Vector3.zero)
                alongSpeed = Mathf.Min(t.wallRunSpeed, alongSpeed + t.wallRunAcceleration * dt);

            planarVelocity = direction * alongSpeed;

            // Low gravity, but rising only stays cheap for a short time per wall, and sinking speeds up as the run ages.
            float g = t.wallGravity;
            if (verticalSpeed > 0f)
            {
                wallRiseTime += dt;
                if (wallRiseTime > t.wallClimbDuration)
                    g = t.gravity;
            }
            float runProgress = t.maxWallRunDuration > 0f ? Mathf.Clamp01(wallRunTime / t.maxWallRunDuration) : 1f;
            float maxSink = t.wallMaxSlideSpeed * runProgress;
            verticalSpeed = Mathf.Max(verticalSpeed - g * dt, Mathf.Min(-maxSink, verticalSpeed));
        }

        /// <summary>Extra displacement that eases the body onto a wall caught from a little way off.</summary>
        Vector3 WallSnapOffset(float dt)
        {
            if (!wallRunning || wallGap <= 0f)
                return Vector3.zero;
            return -wallRunNormal * Mathf.Min(wallGap, WallSnapSpeed * dt);
        }

        void WallJump(MovementTuning t, Vector3 wishDir, float wishAmount)
        {
            Vector3 n = wallRunNormal;
            if (wallRunning)
                StopWallRun(t, WallExit.Jumped);
            wallJumpCoyoteTimer = 0f;
            wallReattachTimer = t.wallReattachCooldown;

            // Kick away from the wall on top of the speed you had along it.
            float away = Vector3.Dot(planarVelocity, n);
            Vector3 along = planarVelocity - n * away;
            planarVelocity = along * t.wallJumpMomentumKeep + n * Mathf.Max(away, t.wallJumpAwayForce);

            float upSpeed = Mathf.Max(verticalSpeed, t.wallJumpUpForce);
            lastWallJumpAssisted = TryAssistWallJump(t, n, wishDir, wishAmount, ref upSpeed);
            lastWallJumpUpSpeed = upSpeed;
            verticalSpeed = upSpeed;

            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            jumpRising = false; // always full strength: releasing Jump early does not cut a wall jump short
            boostTimer = 0f;
            boostHoldsAltitude = false;
            wallJumpCommitTimer = t.wallJumpCommitTime;
            WallJumped?.Invoke();
        }

        /// <summary>
        /// Wall jump assist. When you steer toward a nearby wall whose top edge the normal jump would carry your head over,
        /// lowers the upward launch speed just enough to arrive at body height on its face, so the chain connects.
        /// It only ever lowers the upward speed: direction, horizontal speed and steering are untouched, nothing pulls
        /// you toward the wall, and when the normal jump already connects (or can't be helped) nothing changes.
        /// Runs once per wall jump, only while steering: about 15-20 raycasts on that one frame.
        /// </summary>
        bool TryAssistWallJump(MovementTuning t, Vector3 fromNormal, Vector3 wishDir, float wishAmount, ref float upSpeed)
        {
            float range = t.wallJumpAssistRange;
            float speed = planarVelocity.magnitude;
            if (range <= 0f || wishAmount <= WallReleaseMinInput || speed < StartMovingSpeed || upSpeed <= 0f)
                return false;

            // Where you'll head: your steering if it points further from the old wall than the kick does, otherwise the
            // kick itself (turning back toward the old wall is damped right after a wall jump).
            Vector3 launchDir = planarVelocity / speed;
            Vector3 travelDir = Vector3.Dot(wishDir, fromNormal) >= Vector3.Dot(launchDir, fromNormal) ? wishDir : launchDir;

            GetWallProbeOrigins(out Vector3 upper, out Vector3 lower);
            float feet = lower.y - controller.radius;
            float upperOffset = upper.y - feet;
            float lowerOffset = lower.y - feet;
            float gUp = t.gravity;
            float gDown = t.gravity * t.fallGravityMultiplier;

            // The nearest wall ahead, scanning down from the highest your head could get to.
            float scanTop = upper.y + upSpeed * upSpeed / (2f * gUp);
            int rayCount = Mathf.FloorToInt((scanTop - (feet - AssistScanDepth)) / AssistScanStep) + 1;
            bool found = false;
            bool reachesAboveJump = false;
            RaycastHit face = default;
            float faceY = 0f;
            for (int i = 0; i < rayCount; i++)
            {
                float y = scanTop - i * AssistScanStep;
                if (!CastWall(t, new Vector3(upper.x, y, upper.z), travelDir, range, out RaycastHit hit))
                    continue;
                if (found && hit.distance > face.distance - AssistFaceTolerance)
                    continue; // the same wall lower down, or one behind it
                found = true;
                face = hit;
                faceY = y;
                reachesAboveJump = i == 0;
            }
            // Nothing ahead, or a wall too tall to sail over: the normal jump is right.
            if (!found || reachesAboveJump)
                return false;

            Vector3 n = Flatten(face.normal);
            if (-Vector3.Dot(travelDir, n) < AssistAimDot || -Vector3.Dot(wishDir, n) < AssistAimDot || IsLastWall(face.collider, n))
                return false;

            // Its top edge lies between the highest ray that hit it and the one above.
            float top = faceY;
            Vector3 aboveTop = face.point - n * 0.05f + Vector3.up * AssistScanStep;
            if (Physics.Raycast(aboveTop, Vector3.down, out RaycastHit topHit, AssistScanStep + 0.05f, t.wallRunLayers,
                    QueryTriggerInteraction.Ignore) && topHit.normal.y > 0.5f)
                top = Mathf.Max(faceY, topHit.point.y);

            // A deep top is somewhere to land on, not a wall to sail past: leave the jump alone.
            Vector3 behindTop = face.point - n * AssistLandingDepth;
            behindTop.y = top + 0.5f;
            if (Physics.Raycast(behindTop, Vector3.down, out RaycastHit roof, 1.25f, t.groundLayers, QueryTriggerInteraction.Ignore) &&
                roof.normal.y >= walkableNormalY)
                return false;

            // When you'd reach it (straight-line estimate, up to AssistTimeSlack later while turning).
            float travel = face.distance - (controller.radius + controller.skinWidth) / -Vector3.Dot(travelDir, n);
            if (travel <= 0f)
                return false;
            float arriveSoon = travel / speed;
            float arriveLate = arriveSoon * AssistTimeSlack;

            // Aim the head just under the top edge. Only ever lower the launch, and only when that can work.
            float targetRise = top - AssistTopMargin - upperOffset - feet;
            if (HighestRise(upSpeed, arriveSoon, arriveLate, gUp, gDown) <= targetRise)
                return false; // the normal jump already arrives below the edge
            if (HighestRise(0f, arriveSoon, arriveLate, gUp, gDown) > targetRise)
                return false; // too close and too low to reach even with a flat jump

            // Highest launch that still keeps the head under the edge on arrival (the gentlest change).
            float low = 0f;
            float high = upSpeed;
            for (int i = 0; i < 16; i++)
            {
                float mid = 0.5f * (low + high);
                if (HighestRise(mid, arriveSoon, arriveLate, gUp, gDown) > targetRise)
                    high = mid;
                else
                    low = mid;
            }

            // Only if the wall really spans you at the arrival heights, and you could catch it there (not just above the ground).
            float lowestRise = Mathf.Min(Rise(low, arriveSoon, gUp, gDown), Rise(low, arriveLate, gUp, gDown));
            if (!IsAssistFaceAt(t, upper, feet + targetRise + upperOffset, travelDir, face, n) ||
                !IsAssistFaceAt(t, upper, feet + lowestRise + lowerOffset, travelDir, face, n))
                return false;
            Vector3 catchPoint = face.point + n * (controller.radius + controller.skinWidth);
            catchPoint.y = feet + lowestRise + lowerOffset;
            if (Physics.Raycast(catchPoint, Vector3.down, controller.radius + t.wallMinHeight, t.groundLayers, QueryTriggerInteraction.Ignore))
                return false;

            upSpeed = low;
            return true;
        }

        bool IsAssistFaceAt(MovementTuning t, Vector3 upper, float height, Vector3 direction, RaycastHit face, Vector3 faceNormal)
        {
            return CastWall(t, new Vector3(upper.x, height, upper.z), direction, t.wallJumpAssistRange, out RaycastHit hit) &&
                   Vector3.Dot(Flatten(hit.normal), faceNormal) > SameWallDot &&
                   Mathf.Abs(hit.distance - face.distance) < AssistFaceTolerance;
        }

        /// <summary>Height gained after <paramref name="time"/> seconds when launched upward at <paramref name="v0"/> (air gravity, faster when falling).</summary>
        static float Rise(float v0, float time, float gUp, float gDown)
        {
            if (v0 <= 0f)
                return v0 * time - 0.5f * gDown * time * time;
            float apexTime = v0 / gUp;
            if (time <= apexTime)
                return v0 * time - 0.5f * gUp * time * time;
            float fall = time - apexTime;
            return 0.5f * v0 * apexTime - 0.5f * gDown * fall * fall;
        }

        /// <summary>Highest point of the jump arc between two times.</summary>
        static float HighestRise(float v0, float from, float to, float gUp, float gDown)
        {
            float apexTime = v0 > 0f ? v0 / gUp : 0f;
            if (apexTime > from && apexTime < to)
                return Rise(v0, apexTime, gUp, gDown);
            return Mathf.Max(Rise(v0, from, gUp, gDown), Rise(v0, to, gUp, gDown));
        }

        /// <summary>
        /// Right after a wall jump, turning back toward that wall is damped so the kick isn't undone instantly.
        /// Steering away from it, or with no wall jump in progress, is never affected.
        /// </summary>
        float WallJumpSteerScale(MovementTuning t, Vector3 wishDir)
        {
            if (wallJumpCommitTimer <= 0f || t.wallJumpCommitTime <= 0f)
                return 1f;
            float speed = planarVelocity.magnitude;
            if (speed < StartMovingSpeed || Vector3.Dot(wishDir, wallRunNormal) >= Vector3.Dot(planarVelocity, wallRunNormal) / speed)
                return 1f;
            float k = 1f - wallJumpCommitTimer / t.wallJumpCommitTime;
            return k * k;
        }

        /// <summary>Boosting from a wall leaves it: along or away from the wall, never into it.</summary>
        Vector3 WallBoostDirection(Vector3 wishDir, Vector3 fallbackDirection)
        {
            Vector3 n = wallRunNormal;
            Vector3 direction = wishDir.sqrMagnitude > 0.01f ? wishDir
                : planarVelocity.sqrMagnitude > 1f ? planarVelocity.normalized
                : new Vector3(fallbackDirection.x, 0f, fallbackDirection.z);
            float into = Vector3.Dot(direction, n);
            if (into < 0f)
                direction -= n * into;
            return direction.sqrMagnitude > 0.01f ? direction.normalized : n;
        }

        /// <summary>Called just before the controller moves.</summary>
        void BeginWallContacts()
        {
            preMovePlanarVelocity = planarVelocity;
            wallContact = false;
        }

        /// <summary>Called from <see cref="OnControllerColliderHit"/> for wall-like contacts. Keeps the most head-on one.</summary>
        void RecordWallContact(Vector3 normal)
        {
            if (wallRunning && Vector3.Dot(normal, wallRunNormal) > SameWallDot)
                return; // the wall we're already running on

            float score = -Vector3.Dot(preMovePlanarVelocity, normal);
            if (wallContact && score <= wallContactScore)
                return;
            wallContact = true;
            wallContactNormal = normal;
            wallContactScore = score;
        }

        /// <summary>Called after the move and the ground check: lands, transfers to another wall, or catches a new one.</summary>
        void UpdateWallState(MovementTuning t, Vector3 wishDir, float wishAmount, bool jumpedThisFrame)
        {
            if (grounded)
            {
                if (wallRunning)
                    StopWallRun(t, WallExit.Landed);
                ForgetWalls();
                return;
            }

            // Momentum from before this frame's collisions, so an impact is redirected instead of lost.
            Vector3 velocity = preMovePlanarVelocity;
            float speed = velocity.magnitude;
            if (jumpedThisFrame || speed < t.wallRunMinEntrySpeed || speed < StartMovingSpeed)
                return;

            Vector3 normal;
            Collider collider;
            float gap;

            // Ran into a wall this frame: head-on, at an angle, or into an inside corner while already wall running.
            if (wallContact && TryFindWall(t, wallContactNormal, velocity, wishDir, wishAmount, out normal, out collider, out gap))
            {
                StartWallRun(t, normal, collider, gap, velocity);
                return;
            }
            if (wallRunning)
                return;

            // A wall just beside us, to the left or right of the direction of travel.
            GetWallProbeOrigins(out Vector3 upper, out _);
            Vector3 right = new Vector3(velocity.z, 0f, -velocity.x) / speed;
            float reach = WallReach(t);
            for (int side = -1; side <= 1; side += 2)
            {
                if (CastWall(t, upper, right * side, reach, out RaycastHit hit) &&
                    TryFindWall(t, Flatten(hit.normal), velocity, wishDir, wishAmount, out normal, out collider, out gap))
                {
                    StartWallRun(t, normal, collider, gap, velocity);
                    return;
                }
            }
        }

        /// <summary>Validates a possible wall in the direction of <paramref name="normal"/> and checks the player is going for it.</summary>
        bool TryFindWall(MovementTuning t, Vector3 normal, Vector3 velocity, Vector3 wishDir, float wishAmount,
            out Vector3 wallNormalOut, out Collider collider, out float gap)
        {
            wallNormalOut = normal;
            collider = null;
            gap = 0f;
            if (normal == Vector3.zero)
                return false;

            // Intent: moving into it or steering into it, and not steering away from it.
            float approach = -Vector3.Dot(velocity, normal);
            float wishInto = wishAmount > 0f ? -Vector3.Dot(wishDir, normal) : 0f;
            if (-wishInto > Mathf.Sin(t.wallReleaseAngle * Mathf.Deg2Rad))
                return false;
            if (approach < WallApproachSpeed && wishInto < WallIntentDot)
                return false;

            // A real wall: near-vertical, flat and at least body-tall where we are (not a kerb, ledge or slab edge).
            GetWallProbeOrigins(out Vector3 upper, out Vector3 lower);
            float reach = WallReach(t);
            if (!CastWall(t, upper, -normal, reach, out RaycastHit upperHit) ||
                !CastWall(t, lower, -normal, reach, out RaycastHit lowerHit))
                return false;

            Vector3 n = Flatten(upperHit.normal);
            if (Vector3.Dot(n, normal) < SameWallDot || Vector3.Dot(Flatten(lowerHit.normal), n) < SameWallDot)
                return false;
            float tolerance = WallPlaneTolerance + (upper.y - lower.y) * Mathf.Tan(t.maxWallTilt * Mathf.Deg2Rad);
            if (Mathf.Abs(upperHit.distance - lowerHit.distance) > tolerance)
                return false;

            // The wall we just left: only after the cooldown, and only while it still has run time left this airtime.
            if (IsLastWall(upperHit.collider, n) && (wallReattachTimer > 0f || wallRunTime >= t.maxWallRunDuration))
                return false;

            // When the hit would carry you along the wall, it has to continue ahead (skips pillars and wall ends).
            float into = Mathf.Max(0f, -Vector3.Dot(velocity, n));
            Vector3 along = velocity - n * Vector3.Dot(velocity, n);
            float alongSpeed = along.magnitude;
            if (alongSpeed > StartMovingSpeed && Mathf.Atan2(into, alongSpeed) * Mathf.Rad2Deg < t.wallClimbMinAngle)
            {
                float ahead = Mathf.Clamp(alongSpeed * WallAheadTime, MinWallAhead, MaxWallAhead);
                if (!CastWall(t, upper + along * (ahead / alongSpeed), -n, reach, out _))
                    return false;
            }

            // Falling just above the ground: let the landing happen instead.
            if (verticalSpeed <= 0f && t.wallMinHeight > 0f &&
                Physics.Raycast(lower, Vector3.down, controller.radius + t.wallMinHeight, t.groundLayers, QueryTriggerInteraction.Ignore))
                return false;

            wallNormalOut = n;
            collider = upperHit.collider;
            gap = Mathf.Max(0f, upperHit.distance - controller.radius - controller.skinWidth);
            return true;
        }

        /// <summary>Attaches to a wall, redirecting the incoming momentum along it and, for steep hits, up it.</summary>
        void StartWallRun(MovementTuning t, Vector3 normal, Collider collider, float gap, Vector3 velocity)
        {
            // Each wall has its own time budget per airtime; coming back to the same one doesn't refill it.
            if (!IsLastWall(collider, normal))
            {
                wallRunTime = 0f;
                wallRiseTime = 0f;
            }

            float speed = velocity.magnitude;
            float into = Mathf.Max(0f, -Vector3.Dot(velocity, normal));
            Vector3 along = velocity - normal * Vector3.Dot(velocity, normal);
            float alongSpeed = along.magnitude;

            // 0° = skimming the wall, 90° = head-on. Steep hits send part of the impact upward.
            float approachAngle = Mathf.Atan2(into, alongSpeed) * Mathf.Rad2Deg;
            float climbShare = t.wallClimbMinAngle < 90f ? Mathf.InverseLerp(t.wallClimbMinAngle, 90f, approachAngle) : 0f;
            float redirected = into * t.wallEntryRedirect;

            float newAlongSpeed = Mathf.Min(speed, alongSpeed + redirected * (1f - climbShare));
            planarVelocity = alongSpeed > 1e-3f ? along * (newAlongSpeed / alongSpeed) : Vector3.zero;

            // Falling is caught; rising is kept. The climb adds on top, up to the climb speed.
            float rise = Mathf.Max(verticalSpeed, t.wallRunEntryLift) + redirected * climbShare;
            verticalSpeed = Mathf.Min(rise, Mathf.Max(verticalSpeed, t.wallClimbSpeed));

            wallRunning = true;
            wallRunNormal = normal;
            wallCollider = collider;
            wallGap = gap;
            wallTopReached = false;

            // The wall takes over from the boost; its speed has just been redirected.
            boostTimer = 0f;
            boostHoldsAltitude = false;
            jumpRising = false;
            coyoteTimer = 0f;
            wallJumpCoyoteTimer = 0f;
            wallJumpCommitTimer = 0f;
            if (t.wallRunRefreshesAirBoosts)
                airBoostsUsed = 0;

            WallRunStarted?.Invoke();
        }

        void StopWallRun(MovementTuning t, WallExit exit)
        {
            if (!wallRunning)
                return;

            wallRunning = false;
            wallGap = 0f;
            wallReattachTimer = t.wallReattachCooldown;
            bool canStillKick = exit == WallExit.Lost || exit == WallExit.Released || exit == WallExit.Expired;
            wallJumpCoyoteTimer = canStillKick ? t.wallJumpCoyoteTime : 0f;
            if (exit == WallExit.Expired)
                planarVelocity += wallRunNormal * WallDropOffPush;

            WallRunEnded?.Invoke();
        }

        /// <summary>Finds the attached wall again from the current position and follows it if it bends.</summary>
        bool ProbeAttachedWall(MovementTuning t)
        {
            GetWallProbeOrigins(out Vector3 upper, out Vector3 lower);
            float reach = WallReach(t);
            Vector3 toWall = -wallRunNormal;

            bool upperHit = CastWall(t, upper, toWall, reach, out RaycastHit high) &&
                            Vector3.Dot(Flatten(high.normal), wallRunNormal) > WallBendDot;
            bool lowerHit = CastWall(t, lower, toWall, reach, out RaycastHit low) &&
                            Vector3.Dot(Flatten(low.normal), wallRunNormal) > WallBendDot;
            if (!upperHit && !lowerHit)
                return false;

            RaycastHit hit = upperHit ? high : low;
            wallRunNormal = Flatten(hit.normal);
            wallCollider = hit.collider;
            wallGap = Mathf.Max(0f, hit.distance - controller.radius - controller.skinWidth);
            wallTopReached = lowerHit && !upperHit;
            return true;
        }

        bool IsLastWall(Collider collider, Vector3 normal)
        {
            return wallCollider != null && collider == wallCollider && Vector3.Dot(normal, wallRunNormal) > SameWallDot;
        }

        void ForgetWalls()
        {
            wallCollider = null;
            wallRunTime = 0f;
            wallRiseTime = 0f;
            wallReattachTimer = 0f;
            wallJumpCoyoteTimer = 0f;
            wallJumpCommitTimer = 0f;
        }

        void ResetWallState()
        {
            wallRunning = false;
            wallGap = 0f;
            wallTopReached = false;
            wallContact = false;
            ForgetWalls();
        }

        /// <summary>Probe heights: near the top of the body and at the feet sphere, so short ledges never count as walls.</summary>
        void GetWallProbeOrigins(out Vector3 upper, out Vector3 lower)
        {
            Vector3 center = transform.position + controller.center;
            float halfHeight = Mathf.Max(controller.height * 0.5f, controller.radius);
            float feet = center.y - halfHeight;
            upper = new Vector3(center.x, feet + halfHeight * 1.8f, center.z);
            lower = new Vector3(center.x, feet + controller.radius, center.z);
        }

        float WallReach(MovementTuning t)
        {
            return controller.radius + controller.skinWidth + t.wallDetectionDistance;
        }

        bool CastWall(MovementTuning t, Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            return Physics.Raycast(origin, direction, out hit, distance, t.wallRunLayers, QueryTriggerInteraction.Ignore) &&
                   Mathf.Abs(hit.normal.y) <= wallMaxNormalY && hit.normal.y < walkableNormalY;
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }
    }
}
