using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Procedural poses for the placeholder humanoid: a run cycle (legs and free arm) that quickens with speed, a tucked
    /// airborne pose, a streamlined dash pose for boosts, lunges and target pulls, and a torso twist that follows the sword
    /// swing. Presentation only: it reads the motor and turns empty joint pivots, and never touches movement, colliders or
    /// physics. <see cref="CharacterVisual"/> still turns and leans the whole body; <see cref="BladeVisual"/> swings the
    /// sword arm. Allocation-free, a dozen rotations a frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HumanoidVisual : MonoBehaviour
    {
        /// <summary>
        /// The joints it poses: empty pivots with an identity rest rotation, limbs hanging along -Y from them, facing +Z.
        /// Rotating a limb joint about X by a positive angle swings it backward.
        /// </summary>
        [Serializable]
        public sealed class Rig
        {
            public Transform hips;
            public Transform spine;
            public Transform head;
            public Transform leftShoulder;
            public Transform leftElbow;

            [Tooltip("Holds the sword arm. Swayed while running, left still while attacking (Blade Visual swings the arm itself).")]
            public Transform rightShoulderMount;

            public Transform leftHip;
            public Transform leftKnee;
            public Transform leftAnkle;
            public Transform rightHip;
            public Transform rightKnee;
            public Transform rightAnkle;
        }

        [SerializeField] VelocityMotor motor;

        [Tooltip("Sword arm animation: the torso twists with its swing. Optional.")]
        [SerializeField] BladeVisual blade;

        [SerializeField] Rig rig = new Rig();

        [Header("Run Cycle")]
        [Tooltip("Distance (m) covered by one stride cycle (two steps) at moderate speed.")]
        [SerializeField, Min(0.1f)] float strideLength = 4f;

        [Tooltip("Fastest stride cycle (cycles per second), so the legs stay readable at high speed instead of blurring.")]
        [SerializeField, Min(0.1f)] float maxStrideRate = 3.2f;

        [Tooltip("Speed (m/s) at which the run reaches its full swing.")]
        [SerializeField, Min(0.1f)] float fullRunSpeed = 12f;

        [Tooltip("Leg swing forward and back from the hip (degrees).")]
        [SerializeField, Range(0f, 90f)] float legSwing = 50f;

        [Tooltip("Knee bend as the leg comes through (degrees).")]
        [SerializeField, Range(0f, 130f)] float kneeBend = 90f;

        [Tooltip("Free arm swing (degrees).")]
        [SerializeField, Range(0f, 90f)] float armSwing = 45f;

        [Tooltip("Sword arm sway while running (degrees). Stops while attacking.")]
        [SerializeField, Range(0f, 30f)] float swordArmSway = 8f;

        [Tooltip("Extra forward lean of the torso at full run (degrees), on top of the whole-body lean.")]
        [SerializeField, Range(0f, 30f)] float runLean = 8f;

        [Tooltip("Hip bob per step (m).")]
        [SerializeField, Min(0f)] float bobHeight = 0.04f;

        [Header("Poses")]
        [Tooltip("Torso lean in the dash pose (boost, lunge, target pull), degrees.")]
        [SerializeField, Range(0f, 45f)] float dashLean = 18f;

        [Tooltip("Torso twist at the extremes of a slash (degrees).")]
        [SerializeField, Range(0f, 60f)] float slashTwist = 25f;

        [Tooltip("How quickly the body blends between standing, running, airborne and dashing (per second).")]
        [SerializeField, Min(0.1f)] float blendRate = 12f;

        float phase;
        float run;
        float air;
        float dash;
        float calm = 1f;
        Vector3 hipsRest;

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        public BladeVisual Blade
        {
            get => blade;
            set => blade = value;
        }

        /// <summary>Hooks up the joints (used by the movement test builder).</summary>
        public void SetRig(Rig joints)
        {
            rig = joints ?? new Rig();
            if (rig.hips != null)
                hipsRest = rig.hips.localPosition;
        }

        void Awake()
        {
            if (motor == null)
                motor = GetComponentInParent<VelocityMotor>();
            if (blade == null)
                blade = GetComponentInParent<BladeVisual>();
            if (rig.hips != null)
                hipsRest = rig.hips.localPosition;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (motor == null || dt <= 0f)
                return;

            // What the body is doing. On a wall the legs keep running, upward speed included.
            MotorState state = motor.State;
            bool onWall = state == MotorState.Wall;
            bool onFeet = motor.IsGrounded || onWall;
            bool dashing = state == MotorState.Boost || state == MotorState.Lunge || state == MotorState.Target;
            float speed = onWall ? motor.Velocity.magnitude : motor.Speed;

            float blend = 1f - Mathf.Exp(-blendRate * dt);
            run = Mathf.Lerp(run, onFeet ? Mathf.Clamp01(speed / fullRunSpeed) : 0f, blend);
            air = Mathf.Lerp(air, onFeet ? 0f : 1f, blend);
            dash = Mathf.Lerp(dash, dashing ? (motor.IsGrounded ? 0.5f : 1f) : 0f, blend);
            float twist = blade != null ? blade.BodyTwist : 0f;
            calm = Mathf.Lerp(calm, blade != null && blade.IsSwinging ? 0f : 1f, blend);

            if (onFeet)
                phase = Mathf.Repeat(phase + Mathf.Min(speed / strideLength, maxStrideRate) * dt, 1f);

            // Pose weights: dashing wins over airborne, airborne over running; what's left is standing.
            float wDash = dash;
            float wAir = air * (1f - wDash);
            float wRun = run * (1f - air) * (1f - wDash);

            float cycle = phase * 2f * Mathf.PI;
            float sin = Mathf.Sin(cycle);
            float cos = Mathf.Cos(cycle);

            // Legs. The left leg is forward at sin = 1; each knee bends most while its leg swings through.
            float leftHip = wRun * -legSwing * sin + wAir * -45f + wDash * 25f;
            float rightHip = wRun * legSwing * sin + wAir * 15f + wDash * 40f;
            float leftKnee = wRun * (10f + kneeBend * Mathf.Max(0f, cos)) + wAir * 80f + wDash * 45f;
            float rightKnee = wRun * (10f + kneeBend * Mathf.Max(0f, -cos)) + wAir * 45f + wDash * 70f;
            float pointToes = (wAir + wDash) * 25f;
            SetPitch(rig.leftHip, leftHip);
            SetPitch(rig.rightHip, rightHip);
            SetPitch(rig.leftKnee, leftKnee);
            SetPitch(rig.rightKnee, rightKnee);
            // Running, the feet stay roughly level; in the air they point.
            SetPitch(rig.leftAnkle, -(leftHip + leftKnee) * wRun * 0.6f + pointToes);
            SetPitch(rig.rightAnkle, -(rightHip + rightKnee) * wRun * 0.6f + pointToes);

            // Free (left) arm: swings against the left leg, out for balance in the air, trailing in a dash, and reaching
            // forward as the sword is drawn back (pulling back as it cuts).
            if (rig.leftShoulder != null)
            {
                float swing = wRun * armSwing * sin + wAir * -25f + wDash * 55f - twist * 30f;
                float spread = -10f - wAir * 40f;
                rig.leftShoulder.localRotation = Quaternion.Euler(swing, 0f, spread);
            }
            SetPitch(rig.leftElbow, -(15f + wRun * 75f + wAir * 45f + wDash * 15f));

            // Sword arm mount: a gentle sway while running, still while the arm swings.
            SetPitch(rig.rightShoulderMount, -swordArmSway * sin * wRun * calm);

            // Torso: leans with speed and in a dash, counter-rotates against the hips while running, twists with the cut.
            if (rig.hips != null)
            {
                rig.hips.localPosition = hipsRest + Vector3.up * (wRun * bobHeight * Mathf.Cos(2f * cycle));
                rig.hips.localRotation = Quaternion.Euler(0f, wRun * 8f * sin, 0f);
            }

            float spinePitch = wRun * runLean + wDash * dashLean + wAir * 6f;
            float spineYaw = -wRun * 14f * sin + twist * slashTwist;
            if (rig.spine != null)
                rig.spine.localRotation = Quaternion.Euler(spinePitch, spineYaw, 0f);

            // The head steadies itself, so the visor keeps facing where you're going.
            if (rig.head != null)
                rig.head.localRotation = Quaternion.Euler(-spinePitch * 0.7f, -spineYaw * 0.6f, 0f);
        }

        static void SetPitch(Transform joint, float degrees)
        {
            if (joint != null)
                joint.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }
    }
}
