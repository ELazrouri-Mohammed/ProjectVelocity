using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Glue between input, camera, traversal targeting, combat and motor. Each frame it reads device-independent intent,
    /// turns the camera, updates the target selection and the attack, converts movement into camera-relative world space,
    /// drives the motor, then lets the blade land.
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

        [Tooltip("Falling below this height puts you back at the start.")]
        [SerializeField] float killHeight = -30f;

        Vector3 spawnPosition;
        float spawnYaw;

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

            spawnPosition = transform.position;
            spawnYaw = transform.eulerAngles.y;
        }

        void Update()
        {
            PlayerIntent intent = inputSource != null ? inputSource.ReadIntent() : default;
            float dt = Time.deltaTime;

            // Turn the camera first so movement uses this frame's facing.
            if (cameraRig != null)
                cameraRig.AddLookInput(intent.LookDelta, intent.LookRate, intent.LookDegrees, dt);

            Vector3 forward = cameraRig != null ? cameraRig.PlanarForward : transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            // Pick the most likely target before moving; the button only ever launches through that one.
            TraversalTarget activate = targeting != null ? targeting.Tick(intent.TargetPressed) : null;

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
            };
            motor.Tick(command, dt);
            if (combat != null)
                combat.AfterMove();

            if (intent.RespawnPressed || transform.position.y < killHeight)
                Respawn();
        }

        /// <summary>
        /// Back to the start, with motion, combat and every reality stage reset: the level spawn, or the start of the
        /// <see cref="RespawnZone"/> you were in (so failing a section restarts that section).
        /// </summary>
        public void Respawn()
        {
            Vector3 position = spawnPosition;
            float yaw = spawnYaw;
            if (RespawnZone.TryGetRestart(transform.position, out Vector3 restart, out float restartYaw))
            {
                position = restart;
                yaw = restartYaw;
            }

            motor.Teleport(position);
            if (cameraRig != null)
                cameraRig.SnapBehindTarget(yaw);
            if (combat != null)
                combat.ResetCombat();
            Respawned?.Invoke();
        }
    }
}
