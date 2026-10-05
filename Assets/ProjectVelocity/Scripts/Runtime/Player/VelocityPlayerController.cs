using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Glue between input, camera, traversal targeting, tether, combat and motor. Each frame it reads device-independent intent,
    /// turns the camera, updates the target and anchor selections and the attack, converts movement into camera-relative world
    /// space, drives the motor, then lets the blade land. LINK (ACTION) is contextual: it hooks the tether onto the selected
    /// anchor or launches through the selected traversal target, whichever the player more likely means.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VelocityMotor))]
    public sealed class VelocityPlayerController : MonoBehaviour
    {
        [Tooltip("Where player intent comes from (keyboard/mouse or touch controls). Set at startup by the Input Source Selector, if there is one.")]
        [SerializeField] VelocityInputSource inputSource;

        [SerializeField] VelocityMotor motor;

        [Tooltip("Camera rig. Movement is relative to its facing.")]
        [SerializeField] VelocityCamera cameraRig;

        [Tooltip("Traversal target soft lock. The activate button launches through its selection. Optional.")]
        [SerializeField] TraversalTargeting targeting;

        [Tooltip("Blade combat: the attack, its soft targeting and the kill reward. Optional.")]
        [SerializeField] CombatController combat;

        [Tooltip("Tether: LINK hooks onto anchors. Optional.")]
        [SerializeField] TetherController tether;

        [Tooltip("Shield and death. Optional: without it, failing puts you straight back at the start.")]
        [SerializeField] PlayerHealth health;

        [Tooltip("Falling below this height puts you back at the start (a Slice Director in the scene overrides it per checkpoint).")]
        [SerializeField] float killHeight = -30f;

        Vector3 spawnPosition;
        float spawnYaw;
        bool frozen;

        /// <summary>Raised after <see cref="Respawn"/> has put the player back at the start (R / RESET, or a fall).</summary>
        public event Action Respawned;

        public VelocityInputSource InputSource
        {
            get => inputSource;
            set => inputSource = value;
        }

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        public VelocityCamera CameraRig
        {
            get => cameraRig;
            set => cameraRig = value;
        }

        public TraversalTargeting Targeting
        {
            get => targeting;
            set => targeting = value;
        }

        public CombatController Combat
        {
            get => combat;
            set => combat = value;
        }

        public TetherController Tether
        {
            get => tether;
            set => tether = value;
        }

        public PlayerHealth Health
        {
            get => health;
            set => health = value;
        }

        /// <summary>While frozen (the death beat, the end screen), input is read but ignored and the motor doesn't run.</summary>
        public bool Frozen
        {
            get => frozen;
            set => frozen = value;
        }

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<VelocityMotor>();
            if (inputSource == null)
                inputSource = GetComponent<VelocityInputSource>();
            if (cameraRig == null && Camera.main != null)
                cameraRig = Camera.main.GetComponent<VelocityCamera>();
            if (targeting == null)
                targeting = GetComponent<TraversalTargeting>();
            if (combat == null)
                combat = GetComponent<CombatController>();
            if (tether == null)
                tether = GetComponent<TetherController>();
            if (health == null)
                health = GetComponent<PlayerHealth>();

            spawnPosition = transform.position;
            spawnYaw = transform.eulerAngles.y;
        }

        void Update()
        {
            PlayerIntent intent = inputSource != null ? inputSource.ReadIntent() : default;
            if (frozen)
                return;
            float dt = Time.deltaTime;

            // Turn the camera first so movement uses this frame's facing.
            if (cameraRig != null)
                cameraRig.AddLookInput(intent.LookDelta, intent.LookRate, intent.LookDegrees, dt);

            Vector3 forward = cameraRig != null ? cameraRig.PlanarForward : transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            // Pick the most likely target and anchor before moving; LINK only ever uses one of those two.
            TraversalTarget selectedTarget = null;
            float targetScore = 1f;
            if (targeting != null)
            {
                targeting.Tick(false);
                selectedTarget = targeting.Selected;
                targetScore = targeting.SelectedAngle / Mathf.Max(1f, motor.Settings.targetSelectionAngle);
            }
            LinkCommand link = tether != null
                ? tether.Tick(intent.TargetPressed, intent.TargetHeld, selectedTarget, targetScore)
                : new LinkCommand { UseTarget = intent.TargetPressed && selectedTarget != null };
            TraversalTarget activate = link.UseTarget && selectedTarget != null && motor.CanActivateTarget ? selectedTarget : null;

            // The attack picks its enemy the same way; reaching for one beyond the blade becomes a lunge for the motor.
            MotorLunge lunge = combat != null ? combat.Tick(intent.AttackPressed, forward, dt) : default;

            var command = new MotorCommand
            {
                MoveDirection = Vector3.ClampMagnitude(right * intent.Move.x + forward * intent.Move.y, 1f),
                FallbackDirection = forward,
                JumpPressed = intent.JumpPressed,
                JumpHeld = intent.JumpHeld,
                BoostPressed = intent.BoostPressed,
                ActivateTarget = activate,
                Lunge = lunge,
                Tether = link.Attach,
                ReleaseTether = link.Release,
            };
            motor.Tick(command, dt);
            if (combat != null)
                combat.AfterMove();

            float limit = SliceDirector.Current != null ? SliceDirector.Current.KillHeight : killHeight;
            if (intent.RespawnPressed)
                Respawn();
            else if (transform.position.y < limit)
                Fail("fell");
        }

        /// <summary>
        /// The player failed (fell, was crushed or hit by a hazard): dies through <see cref="PlayerHealth"/> when there is one
        /// (a short death beat, then the checkpoint), otherwise straight back to the start.
        /// </summary>
        public void Fail(string reason)
        {
            if (health != null && health.isActiveAndEnabled)
                health.Kill(reason);
            else
                Respawn();
        }

        /// <summary>
        /// Back to the start, with motion, combat and the reality stages reset: the last checkpoint when a
        /// <see cref="SliceDirector"/> runs the level, else the start of the <see cref="RespawnZone"/> you were in (so failing a
        /// section restarts that section), else the level spawn.
        /// </summary>
        public void Respawn()
        {
            frozen = false;
            Vector3 position = spawnPosition;
            float yaw = spawnYaw;
            if (SliceDirector.Current != null && SliceDirector.Current.TryGetRestart(out Vector3 checkpoint, out float checkpointYaw))
            {
                position = checkpoint;
                yaw = checkpointYaw;
            }
            else if (RespawnZone.TryGetRestart(transform.position, out Vector3 restart, out float restartYaw))
            {
                position = restart;
                yaw = restartYaw;
            }

            motor.Teleport(position);
            if (cameraRig != null)
                cameraRig.SnapBehindTarget(yaw);
            if (combat != null)
                combat.ResetCombat();
            if (tether != null)
                tether.ResetTether();
            Respawned?.Invoke();
        }
    }
}
