using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Turns the body to face its motion and leans it with speed and into turns, so direction changes are readable; tilts it
    /// with its feet to the wall on a wall run, and hangs it from the rope on the tether. Purely visual: it never affects movement.
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

        [Tooltip("Tilt (degrees) toward the wall on a wall run, feet to the wall.")]
        [SerializeField, Range(0f, 60f)] float wallTilt = 0f;

        [Tooltip("How much the body hangs from the rope on the tether (0 = stays upright, 1 = head points at the anchor).")]
        [SerializeField, Range(0f, 1f)] float ropeHang = 0f;

        float yaw;
        float bank;
        float wallRoll;
        float hang;
        Vector3 hangUp = Vector3.up;

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

        /// <summary>Sets the slice's extra presentation (used by the vertical slice builder).</summary>
        public void SetStyle(float lean, float leanSpeed, float tiltOnWalls, float hangFromRope)
        {
            forwardLean = lean;
            fullLeanSpeed = Mathf.Max(0.1f, leanSpeed);
            wallTilt = tiltOnWalls;
            ropeHang = hangFromRope;
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

            // Feet to the wall while running on it.
            float wantedWallRoll = 0f;
            if (motor.IsWallRunning && wallTilt > 0f)
            {
                Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                wantedWallRoll = -Mathf.Sign(Vector3.Dot(motor.WallNormal, right)) * wallTilt;
            }
            wallRoll = Mathf.Lerp(wallRoll, wantedWallRoll, 1f - Mathf.Exp(-14f * dt));

            Quaternion upright = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, bank + wallRoll);

            // Hanging from the rope: the body's up turns toward the anchor.
            TetherAnchor anchor = motor.TetherAnchorPoint;
            hang = Mathf.Lerp(hang, anchor != null ? ropeHang : 0f, 1f - Mathf.Exp(-10f * dt));
            if (anchor != null)
            {
                Vector3 toAnchor = anchor.Position - visualRoot.position;
                if (toAnchor.sqrMagnitude > 1e-3f)
                    hangUp = toAnchor.normalized;
            }
            if (hang > 0.001f)
            {
                Quaternion hanging = Quaternion.FromToRotation(Vector3.up, hangUp) * Quaternion.Euler(0f, yaw, 0f);
                visualRoot.rotation = Quaternion.Slerp(upright, hanging, hang);
            }
            else
            {
                visualRoot.rotation = upright;
            }
        }
    }
}
