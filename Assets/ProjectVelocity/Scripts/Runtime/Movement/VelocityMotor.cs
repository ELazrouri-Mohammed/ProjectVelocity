using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// High-speed kinematic character motor built on CharacterController.
    /// It knows nothing about devices or cameras: something calls <see cref="Tick"/> once per frame
    /// with a world-space <see cref="MotorCommand"/>. Wall traversal lives in VelocityMotor.WallRun.cs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class VelocityMotor : MonoBehaviour
    {
        // Pushing more than this many degrees away from the current motion counts as a reversal (hard brake).
        const float ReversalAngle = 140f;
        // Below this speed, movement starts straight in the input direction instead of turning.
        const float StartMovingSpeed = 0.5f;
        const float InputDeadZone = 0.05f;
        // Ground snapping assumes slopes up to ~35° when deciding how far down to look at speed.
        const float SnapSlopeTangent = 0.7f;
        const float GroundContactTolerance = 0.05f;
        // Longest simulated frame; protects against huge jumps after a hitch.
        const float MaxStep = 0.05f;

        [Tooltip("Movement tuning asset. Changes made to it in Play Mode are kept when you stop playing.")]
        [SerializeField] MovementTuning tuning;

        public event Action Jumped;
        /// <summary>Raised on touchdown with the downward speed at impact (m/s).</summary>
        public event Action<float> Landed;
        public event Action BoostStarted;

        public MovementTuning Tuning
        {
            get => tuning;
            set => tuning = value;
        }

        public bool IsGrounded => grounded;
        public bool IsBoosting => boostTimer > 0f;
        /// <summary>Horizontal velocity (m/s).</summary>
        public Vector3 PlanarVelocity => planarVelocity;
        /// <summary>Horizontal speed (m/s).</summary>
        public float Speed => planarVelocity.magnitude;
        /// <summary>Velocity used for the last move, including slope and vertical motion.</summary>
        public Vector3 Velocity => lastMoveVelocity;
        public float BoostCooldownRemaining => boostCooldownTimer;
        public int AirBoostsRemaining => Mathf.Max(0, Settings.maxAirBoosts - airBoostsUsed);

        public MotorState State =>
            wallRunning ? MotorState.Wall : boostTimer > 0f ? MotorState.Boost : grounded ? MotorState.Ground : MotorState.Air;

        CharacterController controller;
        MovementTuning fallbackTuning;

        Vector3 planarVelocity;
        float verticalSpeed;
        Vector3 lastMoveVelocity;

        bool grounded;
        Vector3 groundNormal = Vector3.up;
        float walkableNormalY;

        float coyoteTimer;
        float jumpBufferTimer;
        bool jumpRising;

        float boostTimer;
        float boostCooldownTimer;
        Vector3 boostDirection;
        float boostEntrySpeed;
        bool boostHoldsAltitude;
        int airBoostsUsed;

        MovementTuning Settings
        {
            get
            {
                if (tuning != null)
                    return tuning;
                if (fallbackTuning == null)
                    fallbackTuning = ScriptableObject.CreateInstance<MovementTuning>();
                return fallbackTuning;
            }
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            walkableNormalY = Mathf.Cos(Settings.maxWalkableSlope * Mathf.Deg2Rad);
        }

        void OnDestroy()
        {
            if (fallbackTuning != null)
                Destroy(fallbackTuning);
        }

        /// <summary>Advances the motor by one frame.</summary>
        public void Tick(in MotorCommand command, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float dt = Mathf.Min(deltaTime, MaxStep);
            MovementTuning t = Settings;
            controller.slopeLimit = t.maxWalkableSlope;
            walkableNormalY = Mathf.Cos(t.maxWalkableSlope * Mathf.Deg2Rad);

            boostCooldownTimer = Mathf.Max(0f, boostCooldownTimer - dt);
            coyoteTimer = grounded ? t.coyoteTime : coyoteTimer - dt;
            jumpBufferTimer = command.JumpPressed ? t.jumpBufferTime : jumpBufferTimer - dt;
            TickWallTimers(t, dt);

            Vector3 wishDir = new Vector3(command.MoveDirection.x, 0f, command.MoveDirection.z);
            float wishMagnitude = wishDir.magnitude;
            float wishAmount = 0f;
            if (wishMagnitude > InputDeadZone)
            {
                wishAmount = Mathf.Min(wishMagnitude, 1f);
                wishDir /= wishMagnitude;
            }
            else
            {
                wishDir = Vector3.zero;
            }

            if (command.BoostPressed)
                TryStartBoost(t, wishDir, command.FallbackDirection);

            // Horizontal motion. On a wall, the wall run drives both planar and vertical motion.
            if (wallRunning && KeepWallRun(t, wishDir, wishAmount))
                UpdateWallRun(t, wishDir, wishAmount, dt);
            else if (boostTimer > 0f)
                UpdateBoost(t, wishDir, wishAmount, dt);
            else if (grounded)
                planarVelocity = ApplyControl(planarVelocity, wishDir, wishAmount, t.maxGroundSpeed, t.groundAcceleration,
                    t.groundDeceleration, t.turnResponsiveness, t.reversalBrake, t.groundMomentumDecay, dt);
            else
            {
                float steer = WallJumpSteerScale(t, wishDir); // 1 except right after a wall jump
                planarVelocity = ApplyControl(planarVelocity, wishDir, wishAmount, t.maxAirSpeed, t.airAcceleration,
                    0f, t.airTurnResponsiveness * steer, t.airBrake * steer, t.airMomentumDecay, dt);
            }

            // Jump (buffered input + coyote time). Horizontal speed is never touched, so jumping keeps all momentum.
            bool jumpedThisFrame = false;
            if (jumpBufferTimer > 0f && (grounded || coyoteTimer > 0f))
            {
                // Jumping while running up a ramp adds the ramp's upward motion on top.
                float carry = grounded ? Mathf.Max(0f, lastMoveVelocity.y) : Mathf.Max(0f, verticalSpeed);
                verticalSpeed = Mathf.Sqrt(2f * t.gravity * t.jumpHeight) + carry;
                grounded = false;
                groundNormal = Vector3.up;
                coyoteTimer = 0f;
                jumpBufferTimer = 0f;
                jumpRising = true;
                jumpedThisFrame = true;
                boostHoldsAltitude = false; // a boost still running keeps its speed, but gravity applies to the jump
                Jumped?.Invoke();
            }
            else if (jumpBufferTimer > 0f && CanWallJump)
            {
                WallJump(t, wishDir, wishAmount);
                jumpedThisFrame = true;
            }

            // Gravity (the wall run applies its own while attached).
            if (!grounded && !wallRunning)
            {
                bool gravityPaused = boostTimer > 0f && boostHoldsAltitude;
                if (!gravityPaused)
                {
                    float g = t.gravity;
                    if (verticalSpeed < 0f)
                        g *= t.fallGravityMultiplier;
                    else if (jumpRising && !command.JumpHeld)
                        g *= t.jumpReleaseGravityMultiplier;
                    verticalSpeed = Mathf.Max(verticalSpeed - g * dt, -t.maxFallSpeed);
                }
                if (verticalSpeed <= 0f)
                    jumpRising = false;
            }

            Vector3 moveVelocity;
            if (grounded)
            {
                // Follow the ground surface at full speed: ramps redirect motion instead of slowing it down.
                float speed = planarVelocity.magnitude;
                Vector3 along = Vector3.ProjectOnPlane(planarVelocity, groundNormal);
                moveVelocity = along.sqrMagnitude > 1e-6f ? along * (speed / along.magnitude) : Vector3.zero;
                verticalSpeed = 0f;
            }
            else
            {
                moveVelocity = planarVelocity + Vector3.up * verticalSpeed;
            }

            BeginWallContacts();
            controller.Move(moveVelocity * dt + WallSnapOffset(dt));
            lastMoveVelocity = moveVelocity;

            UpdateGrounding(t, dt, jumpedThisFrame, moveVelocity);
            UpdateWallState(t, wishDir, wishAmount, jumpedThisFrame);
        }

        /// <summary>Instantly moves the character and clears all motion.</summary>
        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;

            planarVelocity = Vector3.zero;
            verticalSpeed = 0f;
            lastMoveVelocity = Vector3.zero;
            grounded = false;
            groundNormal = Vector3.up;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            jumpRising = false;
            boostTimer = 0f;
            boostCooldownTimer = 0f;
            airBoostsUsed = 0;
            ResetWallState();
        }

        /// <summary>
        /// Steering model: the velocity direction rotates toward the input (keeping speed) instead of
        /// accelerating sideways, so turns at speed stay fast. Pushing against the motion brakes hard.
        /// </summary>
        static Vector3 ApplyControl(Vector3 velocity, Vector3 wishDir, float wishAmount, float maxSpeed,
            float acceleration, float idleDeceleration, float turnRate, float reversalBrake, float momentumDecay, float dt)
        {
            float speed = velocity.magnitude;

            if (wishAmount <= 0f)
            {
                // No input: brake to a stop on the ground; in the air keep momentum, only bleeding overspeed.
                if (idleDeceleration > 0f)
                    return Vector3.MoveTowards(velocity, Vector3.zero, idleDeceleration * dt);
                if (speed > maxSpeed)
                    return velocity * (Mathf.MoveTowards(speed, maxSpeed, momentumDecay * dt) / speed);
                return velocity;
            }

            float targetSpeed = maxSpeed * wishAmount;

            if (speed < StartMovingSpeed)
                return wishDir * Mathf.Min(targetSpeed, speed + acceleration * dt);

            Vector3 direction = velocity / speed;
            if (Vector3.Angle(direction, wishDir) > ReversalAngle)
                return Vector3.MoveTowards(velocity, wishDir * targetSpeed, reversalBrake * dt);

            direction = Vector3.RotateTowards(direction, wishDir, turnRate * Mathf.Deg2Rad * dt, 0f);

            if (speed < targetSpeed)
                speed = Mathf.Min(targetSpeed, speed + acceleration * dt);
            else if (speed > maxSpeed)
                speed = Mathf.MoveTowards(speed, maxSpeed, momentumDecay * dt);
            else if (speed > targetSpeed && idleDeceleration > 0f)
                speed = Mathf.MoveTowards(speed, targetSpeed, idleDeceleration * dt);

            return direction * speed;
        }

        void TryStartBoost(MovementTuning t, Vector3 wishDir, Vector3 fallbackDirection)
        {
            if (boostCooldownTimer > 0f || t.boostDuration <= 0f)
                return;
            if (!grounded && airBoostsUsed >= t.maxAirBoosts)
                return;

            if (wallRunning)
            {
                wishDir = WallBoostDirection(wishDir, fallbackDirection);
                StopWallRun(t, WallExit.Boosted);
            }

            // Boost where the player is steering; otherwise keep going the way they are moving; otherwise go forward.
            Vector3 direction = wishDir;
            if (direction.sqrMagnitude < 0.01f)
                direction = planarVelocity.sqrMagnitude > 1f ? planarVelocity : new Vector3(fallbackDirection.x, 0f, fallbackDirection.z);
            if (direction.sqrMagnitude < 1e-4f)
                direction = Vector3.forward;

            boostDirection = direction.normalized;
            boostEntrySpeed = planarVelocity.magnitude;
            boostTimer = t.boostDuration;
            boostCooldownTimer = t.boostCooldown;
            boostHoldsAltitude = !grounded && t.airBoostHoldsAltitude;

            if (!grounded)
            {
                airBoostsUsed++;
                if (boostHoldsAltitude)
                {
                    verticalSpeed = 0f;
                    jumpRising = false;
                }
            }

            BoostStarted?.Invoke();
        }

        void UpdateBoost(MovementTuning t, Vector3 wishDir, float wishAmount, float dt)
        {
            if (wishAmount > 0f)
                boostDirection = Vector3.RotateTowards(boostDirection, wishDir, t.boostSteering * Mathf.Deg2Rad * dt, 0f);

            boostTimer = Mathf.Max(0f, boostTimer - dt);
            float progress = 1f - boostTimer / Mathf.Max(t.boostDuration, 1e-4f);

            // Hit peak speed instantly, hold it, then ease down into the exit speed.
            // Entering faster than the boost never slows you down.
            float peakSpeed = Mathf.Max(t.boostSpeed, boostEntrySpeed);
            float exitSpeed = Mathf.Min(Mathf.Max(t.boostExitSpeed, boostEntrySpeed), peakSpeed);
            planarVelocity = boostDirection * Mathf.Lerp(peakSpeed, exitSpeed, progress * progress);
        }

        void UpdateGrounding(MovementTuning t, float dt, bool jumpedThisFrame, Vector3 moveVelocity)
        {
            bool wasGrounded = grounded;

            if (jumpedThisFrame || verticalSpeed > 0f)
            {
                grounded = false;
                return;
            }

            // While running, look further down so we stay glued over crests and down ramps instead of skipping.
            float allowedGap = controller.skinWidth + GroundContactTolerance;
            if (wasGrounded)
                allowedGap += Mathf.Clamp(planarVelocity.magnitude * dt * SnapSlopeTangent, GroundContactTolerance, t.groundSnapDistance);

            if (ProbeGround(t, allowedGap, out float gap, out Vector3 normal) && normal.y >= walkableNormalY)
            {
                if (wasGrounded && gap > controller.skinWidth + 0.01f)
                    controller.Move(Vector3.down * gap);

                grounded = true;
                groundNormal = normal;

                if (!wasGrounded)
                {
                    float impactSpeed = -verticalSpeed;
                    verticalSpeed = 0f;
                    jumpRising = false;
                    airBoostsUsed = 0;
                    Landed?.Invoke(impactSpeed);
                }

                verticalSpeed = 0f;
            }
            else
            {
                grounded = false;
                groundNormal = Vector3.up;

                // Ran off an edge or a ramp lip: keep the slope's vertical motion, so ramps launch you.
                if (wasGrounded)
                    verticalSpeed = moveVelocity.y;
            }
        }

        bool ProbeGround(MovementTuning t, float maxGap, out float gap, out Vector3 normal)
        {
            float radius = controller.radius;
            Vector3 center = transform.position + controller.center;
            Vector3 bottomSphere = center + Vector3.down * (Mathf.Max(controller.height * 0.5f, radius) - radius);

            // Start a little inside the capsule with a slightly smaller sphere, so the cast never begins inside the floor.
            const float lift = 0.15f;
            float castRadius = radius * 0.9f;
            float startOffset = lift + (radius - castRadius);
            Vector3 origin = bottomSphere + Vector3.up * lift;

            if (Physics.SphereCast(origin, castRadius, Vector3.down, out RaycastHit hit, startOffset + maxGap,
                    t.groundLayers, QueryTriggerInteraction.Ignore))
            {
                gap = hit.distance - startOffset;
                normal = hit.normal;

                // On platform edges the sphere reports a rounded edge normal; use the real surface normal instead.
                if (normal.y < walkableNormalY &&
                    Physics.Raycast(hit.point + Vector3.up * 0.1f, Vector3.down, out RaycastHit surface, 0.2f,
                        t.groundLayers, QueryTriggerInteraction.Ignore) &&
                    surface.normal.y >= walkableNormalY)
                {
                    normal = surface.normal;
                }

                return true;
            }

            gap = float.MaxValue;
            normal = Vector3.up;
            return false;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Vector3 n = hit.normal;
            if (n.y >= walkableNormalY)
                return;

            // Ceiling: stop rising.
            if (n.y < -0.5f)
            {
                if (verticalSpeed > 0f)
                {
                    verticalSpeed = 0f;
                    jumpRising = false;
                }
                return;
            }

            // Contacts below the feet sphere's equator are ledges and seams underfoot, not walls.
            float feetSphereY = transform.position.y + controller.center.y - controller.height * 0.5f + controller.radius;
            if (hit.point.y < feetSphereY - 0.05f)
                return;

            // Wall: drop the velocity pushing into it, so we slide along instead of storing speed in the wall.
            Vector3 wallNormal = new Vector3(n.x, 0f, n.z);
            if (wallNormal.sqrMagnitude < 1e-4f)
                return;
            wallNormal.Normalize();
            RecordWallContact(wallNormal);

            float into = Vector3.Dot(planarVelocity, wallNormal);
            if (into < 0f)
                planarVelocity -= wallNormal * into;

            if (boostTimer > 0f)
            {
                float boostInto = Vector3.Dot(boostDirection, wallNormal);
                if (boostInto < 0f)
                {
                    Vector3 slide = boostDirection - wallNormal * boostInto;
                    if (slide.sqrMagnitude < 0.1f)
                        boostTimer = 0f; // head-on: the boost ends against the wall
                    else
                        boostDirection = slide.normalized;
                }
            }
        }
    }
}
