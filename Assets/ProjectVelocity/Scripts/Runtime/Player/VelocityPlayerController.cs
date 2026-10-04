using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Glue between input, camera, traversal targeting and motor. Each frame it reads device-independent intent,
    /// turns the camera, updates the target selection, converts movement into camera-relative world space and drives the motor.
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

        [Tooltip("Falling below this height puts you back at the start.")]
        [SerializeField] float killHeight = -30f;

        Vector3 spawnPosition;
        float spawnYaw;

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

            var command = new MotorCommand
            {
                MoveDirection = Vector3.ClampMagnitude(right * intent.Move.x + forward * intent.Move.y, 1f),
                FallbackDirection = forward,
                JumpPressed = intent.JumpPressed,
                JumpHeld = intent.JumpHeld,
                BoostPressed = intent.BoostPressed,
                ActivateTarget = activate,
            };
            motor.Tick(command, dt);

            if (intent.RespawnPressed || transform.position.y < killHeight)
                Respawn();
        }

        public void Respawn()
        {
            motor.Teleport(spawnPosition);
            if (cameraRig != null)
                cameraRig.SnapBehindTarget(spawnYaw);
        }
    }
}
