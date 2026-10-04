using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Turns the placeholder body to face its motion and leans it with speed and into turns,
    /// so direction changes are readable. Purely visual: it never affects movement.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterVisual : MonoBehaviour
    {
        [SerializeField] VelocityMotor motor;

        [Tooltip("The child that holds the body meshes.")]
        [SerializeField] Transform visualRoot;

        [Tooltip("How fast the body turns to face the movement direction (degrees per second).")]
        [SerializeField, Min(0f)] float turnSpeed = 1440f;

        [Tooltip("Forward lean at full lean speed (degrees).")]
        [SerializeField, Range(0f, 30f)] float forwardLean = 10f;

        [Tooltip("Speed (m/s) at which the forward lean is at its maximum.")]
        [SerializeField, Min(0.1f)] float fullLeanSpeed = 40f;

        [Tooltip("Maximum sideways lean into turns (degrees).")]
        [SerializeField, Range(0f, 45f)] float maxBank = 20f;

        [Tooltip("Sideways lean (degrees) per m/s² of turning force.")]
        [SerializeField, Min(0f)] float bankPerTurnForce = 0.4f;

        float yaw;
        float bank;

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        public Transform VisualRoot
        {
            get => visualRoot;
            set => visualRoot = value;
        }

        void Awake()
        {
            if (motor == null)
                motor = GetComponentInParent<VelocityMotor>();
            if (visualRoot != null)
                yaw = visualRoot.eulerAngles.y;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (motor == null || visualRoot == null || dt <= 0f)
                return;

            Vector3 velocity = motor.PlanarVelocity;
            float speed = velocity.magnitude;

            float previousYaw = yaw;
            if (speed > 0.5f)
                yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg, turnSpeed * dt);

            float yawRate = Mathf.DeltaAngle(previousYaw, yaw) / dt * Mathf.Deg2Rad;
            float turnForce = yawRate * speed; // centripetal acceleration, m/s²
            float targetBank = Mathf.Clamp(-turnForce * bankPerTurnForce, -maxBank, maxBank);
            bank = Mathf.Lerp(bank, targetBank, 1f - Mathf.Exp(-12f * dt));

            float pitch = forwardLean * Mathf.Clamp01(speed / fullLeanSpeed);
            visualRoot.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, bank);
        }
    }
}
