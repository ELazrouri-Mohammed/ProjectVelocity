using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Tether for <see cref="VelocityMotor"/>: a controlled swing around a <see cref="TetherAnchor"/>, or a zip straight to it.
    /// The rope never pulls you backwards and never stops you: it only removes motion that would carry you further from the
    /// anchor than the rope is long, and turns that motion into speed along the arc (Speed Keep), so connecting fast swings
    /// fast. Gravity pulls you through the arc; the stick turns its heading around the rope; letting go keeps every bit of the
    /// velocity and adds a small release push. Connecting slower than Min Swing Speed sets you to it along your heading, so a
    /// tether is always FLY → CONNECT → ARC → RELEASE, never stop → hang. Feel values live in <see cref="TetherTuning"/>.
    /// <see cref="Tick"/> calls into it at a few fixed points.
    /// </summary>
    public sealed partial class VelocityMotor
    {
        // Longest positional correction (m) the rope applies in one frame, so a hitch never teleports you onto the sphere.
        const float MaxRopeCorrection = 1.5f;
        // How fast a zip turns your direction of travel toward the anchor (degrees per second).
        const float ZipTurnRate = 1440f;
        // How fast a zip builds toward its speed (m/s²).
        const float ZipAcceleration = 260f;

        public enum TetherExit
        {
            /// <summary>The LINK button was let go.</summary>
            Released,
            /// <summary>Swung up past the auto-release angle.</summary>
            Launched,
            /// <summary>Jump pressed while swinging.</summary>
            Jumped,
            /// <summary>Boost pressed while swinging.</summary>
            Boosted,
            /// <summary>A zip reached its anchor.</summary>
            Arrived,
            /// <summary>Ran out of time.</summary>
            Expired,
            /// <summary>Hit the ground.</summary>
            Landed,
            /// <summary>Ran into something, the anchor went away, or something else took over (respawn, knockback).</summary>
            Lost,
        }

        /// <summary>Raised when the rope hooks onto an anchor.</summary>
        public event Action<TetherAnchor> TetherAttached;
        /// <summary>Raised when the rope lets go, with how.</summary>
        public event Action<TetherAnchor, TetherExit> TetherReleased;

        public bool IsTethering => tethering;
        /// <summary>The anchor the rope is hooked onto, or null.</summary>
        public TetherAnchor TetherAnchorPoint => tethering ? tetherAnchor : null;
        /// <summary>Whether a tether could attach right now.</summary>
        public bool CanTether => !tethering && !targetPulling && tetherCooldownTimer <= 0f;
        /// <summary>Current rope length (m) while tethering.</summary>
        public float RopeLength => tethering ? ropeLength : 0f;

        bool tethering;
        bool tetherZip;
        bool tetherHeadedIn;
        TetherAnchor tetherAnchor;
        TetherTuning tetherSettings;
        float ropeLength;
        float ropeTarget;
        float tetherTime;
        float tetherCooldownTimer;
        float zipSpeed;
        bool tetherDescended;
        bool tetherRising;
        Vector3 tetherCorrection;

        void TickTetherTimers(float dt)
        {
            tetherCooldownTimer = Mathf.Max(0f, tetherCooldownTimer - dt);
        }

        void TryStartTether(MovementTuning t, in MotorTether request, Vector3 fallbackDirection)
        {
            TetherAnchor anchor = request.Anchor;
            if (!request.IsRequested || !anchor.isActiveAndEnabled || !anchor.IsReady || !CanTether)
                return;

            TetherTuning s = request.Settings;
            if (wallRunning)
                StopWallRun(t, WallExit.Targeted);
            EndLunge();
            boostTimer = 0f;
            boostHoldsAltitude = false;

            Vector3 toAnchor = anchor.Position - BodyCenter;
            float distance = toAnchor.magnitude;
            Vector3 velocity = planarVelocity + Vector3.up * verticalSpeed;

            tetherSettings = s;
            tetherAnchor = anchor;
            tetherZip = anchor.PullsIn;
            tetherHeadedIn = false;
            tetherTime = 0f;
            tetherDescended = false;
            tetherRising = false;
            ropeLength = distance;
            ropeTarget = Mathf.Clamp(distance * s.lengthFactor, s.minLength, s.maxLength);
            tetherCorrection = Vector3.zero;
            tethering = true;

            if (tetherZip)
            {
                zipSpeed = Mathf.Max(s.pullSpeed, velocity.magnitude);
            }
            else
            {
                // Never a dead hang: below Min Swing Speed, set the heading's horizontal speed to it, cushioning a falling catch.
                var flat = new Vector3(velocity.x, 0f, velocity.z);
                if (flat.magnitude < s.minSwingSpeed)
                {
                    var flatToAnchor = new Vector3(toAnchor.x, 0f, toAnchor.z);
                    var fallback = new Vector3(fallbackDirection.x, 0f, fallbackDirection.z);
                    Vector3 heading = flat.sqrMagnitude > 4f ? flat.normalized
                        : flatToAnchor.sqrMagnitude > 1f ? flatToAnchor.normalized
                        : fallback.sqrMagnitude > 1e-4f ? fallback.normalized : Vector3.forward;
                    flat = heading * s.minSwingSpeed;
                }
                float rise = Mathf.Max(verticalSpeed, -s.maxCatchFallSpeed);
                if (grounded)
                    rise = Mathf.Max(rise, s.groundAttachLift);
                planarVelocity = flat;
                verticalSpeed = rise;
            }

            // The rope takes over from the ground, a jump or a wall until it lets go.
            grounded = false;
            groundNormal = Vector3.up;
            coyoteTimer = 0f;
            jumpRising = false;
            wallJumpCoyoteTimer = 0f;
            wallJumpCommitTimer = 0f;
            if (s.refreshesAirBoost && airBoostsUsed > 0)
                airBoostsUsed--;

            anchor.SetAttached(true);
            TetherAttached?.Invoke(anchor);
        }

        /// <summary>Planar and vertical motion while tethered. Sets <see cref="tetherCorrection"/> for this frame's move.</summary>
        void UpdateTether(Vector3 wishDir, float wishAmount, float dt)
        {
            tetherCorrection = Vector3.zero;
            if (tetherAnchor == null || !tetherAnchor.isActiveAndEnabled)
            {
                EndTether(TetherExit.Lost);
                return;
            }

            TetherTuning s = tetherSettings;
            tetherTime += dt;
            Vector3 anchor = tetherAnchor.Position;
            Vector3 body = BodyCenter;
            Vector3 toBody = body - anchor;
            float distance = toBody.magnitude;
            Vector3 velocity = planarVelocity + Vector3.up * verticalSpeed;

            if (tetherZip)
            {
                UpdateZip(s, anchor, body, velocity, dt);
                return;
            }

            ropeLength = Mathf.MoveTowards(ropeLength, ropeTarget, s.reelSpeed * dt);

            // Gravity, plus a push along the arc while it heads down.
            velocity.y -= s.gravity * dt;
            float speed = velocity.magnitude;
            if (speed > StartMovingSpeed && velocity.y < 0f)
                velocity += velocity / speed * (s.swingAcceleration * dt);

            // Steering turns the swing's heading around the rope, so the velocity stays along the arc.
            Vector3 radial = distance > 1e-3f ? toBody / distance : Vector3.down;
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            if (wishAmount > 0f && flat.sqrMagnitude > 1f && s.steerRate > 0f)
            {
                float angle = Vector3.SignedAngle(flat, wishDir, Vector3.up);
                float limit = s.steerRate * wishAmount * dt;
                velocity = Quaternion.AngleAxis(Mathf.Clamp(angle, -limit, limit), radial) * velocity;
            }

            speed = velocity.magnitude;
            if (speed > s.maxSwingSpeed)
                velocity *= s.maxSwingSpeed / speed;

            // The rope: motion that would carry you past its length is turned along the arc instead of lost.
            Vector3 next = body + velocity * dt;
            Vector3 fromAnchor = next - anchor;
            float nextDistance = fromAnchor.magnitude;
            if (nextDistance > ropeLength && nextDistance > 1e-3f)
            {
                Vector3 n = fromAnchor / nextDistance;
                float outward = Vector3.Dot(velocity, n);
                if (outward > 0f)
                {
                    float before = velocity.magnitude;
                    velocity -= n * outward;
                    float after = velocity.magnitude;
                    if (after > 1e-3f)
                        velocity *= Mathf.Lerp(after, before, s.speedKeep) / after;
                }
                tetherCorrection = Vector3.ClampMagnitude(anchor + n * ropeLength - (body + velocity * dt), MaxRopeCorrection);
            }

            planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            verticalSpeed = velocity.y;

            // Swung up past the anchor's height while still rising, or reached the top of the arc: the rope launches you
            // (a swing never hangs you up there, it keeps you flying).
            // Only a rise that follows the swing's own descent counts (not the lift of a ground attach or a jump).
            float elevation = Mathf.Asin(Mathf.Clamp(radial.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (velocity.y < -3f)
                tetherDescended = true;
            if (tetherDescended && velocity.y > s.apexReleaseSpeed + 2f)
                tetherRising = true;
            if (elevation > s.autoReleaseAngle && velocity.y > 0f)
                EndTether(TetherExit.Launched);
            else if (tetherRising && velocity.y < s.apexReleaseSpeed && elevation > -60f)
                EndTether(TetherExit.Launched);
            else if (tetherTime >= s.maxDuration)
                EndTether(TetherExit.Expired);
        }

        /// <summary>Zip: reeled straight in at speed, turning the current velocity toward the anchor (never zeroing it).</summary>
        void UpdateZip(TetherTuning s, Vector3 anchor, Vector3 body, Vector3 velocity, float dt)
        {
            Vector3 toAnchor = anchor - body;
            float distance = toAnchor.magnitude;
            float speed = velocity.magnitude;
            Vector3 toward = distance > 1e-4f ? toAnchor / distance : Vector3.up;
            Vector3 direction = speed > StartMovingSpeed ? velocity / speed : toward;
            direction = Vector3.RotateTowards(direction, toward, ZipTurnRate * Mathf.Deg2Rad * dt, 0f);
            speed = Mathf.MoveTowards(speed, zipSpeed, ZipAcceleration * dt);
            ropeLength = distance;

            float ahead = Vector3.Dot(toAnchor, direction);
            if (ahead > 0f)
                tetherHeadedIn = true;

            planarVelocity = new Vector3(direction.x, 0f, direction.z) * speed;
            verticalSpeed = direction.y * speed;

            if (distance <= Mathf.Max(s.pullArrivalDistance, speed * dt) || (tetherHeadedIn && ahead <= 0f))
                EndTether(TetherExit.Arrived);
            else if (tetherTime >= s.pullMaxDuration)
                EndTether(TetherExit.Expired);
        }

        /// <summary>Jump while swinging: let go with an upward kick.</summary>
        void TetherJump()
        {
            TetherTuning s = tetherSettings;
            if (s != null)
                verticalSpeed = Mathf.Max(verticalSpeed, s.jumpReleaseUpSpeed);
            jumpBufferTimer = 0f;
            EndTether(TetherExit.Jumped);
            Jumped?.Invoke();
        }

        /// <summary>Lets go, keeping the current velocity, plus the release push when you let go on purpose.</summary>
        void EndTether(TetherExit exit)
        {
            if (!tethering)
                return;

            TetherAnchor anchor = tetherAnchor;
            TetherTuning s = tetherSettings;
            tethering = false;
            tetherZip = false;
            tetherAnchor = null;
            tetherCorrection = Vector3.zero;
            jumpRising = false;

            if (s != null)
            {
                tetherCooldownTimer = s.reattachCooldown;
                bool voluntary = exit == TetherExit.Released || exit == TetherExit.Launched || exit == TetherExit.Jumped ||
                                 exit == TetherExit.Arrived;
                if (voluntary)
                {
                    Vector3 velocity = planarVelocity + Vector3.up * verticalSpeed;
                    float speed = velocity.magnitude;
                    if (speed > StartMovingSpeed)
                        velocity += velocity / speed * s.releaseBoost;
                    if (velocity.y > -4f)
                        velocity.y += s.releaseLift;
                    planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
                    verticalSpeed = velocity.y;
                }
            }

            if (anchor != null)
            {
                anchor.SetAttached(false);
                TetherReleased?.Invoke(anchor, exit);
            }
        }

        /// <summary>Called right after the controller moves: a swing that slams into something lets go there.</summary>
        void CheckTetherProgress(Vector3 intended, Vector3 moved)
        {
            if (!tethering)
                return;
            float intendedDistance = intended.magnitude;
            if (intendedDistance < 0.02f)
                return;
            if (Vector3.Dot(moved, intended) / intendedDistance < intendedDistance * PullBlockedProgress)
                EndTether(TetherExit.Lost);
        }

        /// <summary>Lets go of the tether from outside (the LINK button released, a respawn...).</summary>
        public void ReleaseTether(bool voluntary)
        {
            EndTether(voluntary ? TetherExit.Released : TetherExit.Lost);
        }

        void ResetTetherState()
        {
            if (tethering && tetherAnchor != null)
                tetherAnchor.SetAttached(false);
            tethering = false;
            tetherZip = false;
            tetherAnchor = null;
            tetherSettings = null;
            tetherCorrection = Vector3.zero;
            tetherCooldownTimer = 0f;
        }
    }
}
