using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Placeholder sword-arm animation and slash effect. Purely visual: <see cref="CombatController"/> says when to draw back
    /// (during a lunge), when to swing and whether the swing connected. The arm and blade swing together from the shoulder in
    /// the body's frame, and <see cref="HumanoidVisual"/> twists the torso along with it; the slash arc is aimed along the attack
    /// itself, so it reads even before the body has turned. Allocation-free: poses are interpolated by hand and the arc fades
    /// through a material property block.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BladeVisual : MonoBehaviour
    {
        enum Phase
        {
            Rest,
            Windup,
            Swing,
            Hold,
            Return,
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Parts")]
        [Tooltip("Turned to swing the blade: the sword arm's shoulder. The arm, the hand and the blade all point along its +Z.")]
        [SerializeField] Transform bladePivot;

        [Tooltip("The slash arc: a flat crescent opening along +Z, a child of the player at chest height. Hidden between swings.")]
        [SerializeField] Renderer slashArc;

        [Tooltip("Weapon trail at the blade tip: emits while the blade is moving. Optional.")]
        [SerializeField] TrailRenderer trail;

        [Tooltip("Also leave the trail while boosting (the blade streaks behind you). Needs the motor.")]
        [SerializeField] VelocityMotor trailMotor;

        [Header("Sword Arm Poses (shoulder rotation, degrees)")]
        [Tooltip("Held back and low while running.")]
        [SerializeField] Vector3 restPose = new Vector3(35f, 160f, 0f);

        [Tooltip("Drawn back to the right, ready to cut. Held while lunging.")]
        [SerializeField] Vector3 windupPose = new Vector3(-8f, 115f, -25f);

        [Tooltip("End of the cut, across to the left. The swing sweeps through the front, the return carries on round the back.")]
        [SerializeField] Vector3 swingEndPose = new Vector3(10f, -55f, 25f);

        [Tooltip("How far (degrees) the arm lifts on the way back to rest, so the sword goes over the shoulder, not through the body.")]
        [SerializeField, Range(0f, 120f)] float returnLift = 80f;

        [Header("Timing (s)")]
        [SerializeField, Min(0.01f)] float windupTime = 0.05f;
        [SerializeField, Min(0.01f)] float swingTime = 0.07f;
        [SerializeField, Min(0f)] float holdTime = 0.05f;
        [SerializeField, Min(0.01f)] float returnTime = 0.18f;

        [Header("Slash Arc")]
        [Tooltip("How long (s) the arc shows, fading out.")]
        [SerializeField, Min(0.01f)] float slashTime = 0.18f;

        [Tooltip("How far (degrees) the arc sweeps across the attack direction, right to left.")]
        [SerializeField] float slashSweep = 50f;

        [Tooltip("Tilt of the cut (degrees). 0 = flat.")]
        [SerializeField] float slashTilt = 15f;

        [SerializeField] Color missColor = new Color(0.65f, 0.92f, 1f, 0.6f);
        [SerializeField] Color hitColor = new Color(1f, 0.85f, 0.35f, 0.9f);

        [Tooltip("Size of an arc that connected, relative to a miss.")]
        [SerializeField, Min(0f)] float hitScale = 1.25f;

        Phase phase = Phase.Rest;
        float phaseTime;
        // Current sword-arm rotation as Euler angles, with yaw unwrapped so a swing always sweeps the intended way round.
        Vector3 pose;
        Vector3 poseFrom;
        // Torso twist that goes with the pose: +1 drawn back (sword shoulder back), -1 at the end of the cut, 0 at rest.
        float twist;
        float twistFrom;
        MaterialPropertyBlock block;
        Vector3 slashDirection = Vector3.forward;
        float slashTimer;
        bool slashHit;
        float slashTiltNow;
        float slashSweepNow;
        float slashScaleNow = 1f;

        /// <summary>Whether the sword arm is anywhere but at rest (drawn back, swinging or returning).</summary>
        public bool IsSwinging => phase != Phase.Rest;

        /// <summary>How far the torso should twist with the swing: +1 drawn back, -1 at the end of the cut, 0 at rest.</summary>
        public float BodyTwist => twist;

        /// <summary>Hooks up the parts (used by the movement test builder).</summary>
        public void SetParts(Transform swordArm, Renderer arc)
        {
            bladePivot = swordArm;
            slashArc = arc;
        }

        /// <summary>Hooks up the weapon trail (used by the vertical slice builder).</summary>
        public void SetTrail(TrailRenderer tipTrail, VelocityMotor motor)
        {
            trail = tipTrail;
            trailMotor = motor;
        }

        /// <summary>Draws the blade back, ready to cut, and holds it there until <see cref="PlaySlash"/>.</summary>
        public void PlayWindup()
        {
            poseFrom = Unwrapped(pose, windupPose.y);
            twistFrom = twist;
            Enter(Phase.Windup);
        }

        /// <summary>Swings the blade and shows the slash arc along <paramref name="direction"/> (world space).</summary>
        public void PlaySlash(Vector3 direction)
        {
            PlaySlash(direction, CombatController.AttackKind.Slash);
        }

        /// <summary>
        /// Swings the blade, with an arc shaped for the kind of attack: a flat cut on the ground, a wide vertical arc in the air,
        /// a long thin streak for a dash strike, a big diagonal for a tether strike.
        /// </summary>
        public void PlaySlash(Vector3 direction, CombatController.AttackKind kind)
        {
            switch (kind)
            {
                case CombatController.AttackKind.Aerial:
                    slashTiltNow = 72f;
                    slashSweepNow = 140f;
                    slashScaleNow = 1.15f;
                    break;
                case CombatController.AttackKind.Dash:
                    slashTiltNow = 6f;
                    slashSweepNow = 18f;
                    slashScaleNow = 1.45f;
                    break;
                case CombatController.AttackKind.Strike:
                    slashTiltNow = -55f;
                    slashSweepNow = 160f;
                    slashScaleNow = 1.35f;
                    break;
                default:
                    slashTiltNow = slashTilt;
                    slashSweepNow = slashSweep;
                    slashScaleNow = 1f;
                    break;
            }

            poseFrom = Unwrapped(pose, swingEndPose.y);
            twistFrom = twist;
            Enter(Phase.Swing);

            if (slashArc == null)
                return;
            if (Vector3.Cross(direction, Vector3.up).sqrMagnitude > 1e-4f)
                slashDirection = direction.normalized;
            slashTimer = 0f;
            slashHit = false;
            slashArc.enabled = true;
            ApplySlash(0f);
        }

        /// <summary>The current swing connected: the arc flares brighter and bigger.</summary>
        public void ShowHit()
        {
            slashHit = true;
        }

        /// <summary>Back to rest at once, arc hidden (on respawn).</summary>
        public void ResetPose()
        {
            phase = Phase.Rest;
            twist = 0f;
            SetPose(restPose);
            if (slashArc != null)
                slashArc.enabled = false;
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            slashTiltNow = slashTilt;
            slashSweepNow = slashSweep;
            ResetPose();
            if (trail != null)
                trail.emitting = false;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            UpdateBlade(dt);
            UpdateSlash(dt);
            if (trail != null)
            {
                bool swinging = phase == Phase.Windup || phase == Phase.Swing || phase == Phase.Hold ||
                                (phase == Phase.Return && phaseTime < returnTime * 0.5f);
                bool streaking = trailMotor != null && (trailMotor.IsBoosting || trailMotor.State == MotorState.Lunge);
                bool emit = swinging || streaking;
                if (trail.emitting != emit)
                    trail.emitting = emit;
            }
        }

        void UpdateBlade(float dt)
        {
            if (phase == Phase.Rest)
                return;

            phaseTime += dt;
            switch (phase)
            {
                case Phase.Windup:
                    float draw = EaseOut(phaseTime / windupTime);
                    SetPose(Vector3.Lerp(poseFrom, windupPose, draw));
                    twist = Mathf.Lerp(twistFrom, 1f, draw);
                    break;
                case Phase.Swing:
                    float swing = phaseTime / swingTime;
                    SetPose(Vector3.Lerp(poseFrom, swingEndPose, EaseOut(swing)));
                    twist = Mathf.Lerp(twistFrom, -1f, EaseOut(swing));
                    if (swing >= 1f)
                        Enter(Phase.Hold);
                    break;
                case Phase.Hold:
                    if (phaseTime >= holdTime)
                        Enter(Phase.Return);
                    break;
                case Phase.Return:
                    // Carry on round the back to rest, the way the cut was going, lifting the sword over the shoulder.
                    float back = EaseInOut(phaseTime / returnTime);
                    Vector3 rest = restPose;
                    rest.y -= 360f;
                    Vector3 returning = Vector3.Lerp(swingEndPose, rest, back);
                    returning.x -= returnLift * Mathf.Sin(back * Mathf.PI);
                    SetPose(returning);
                    twist = Mathf.Lerp(-1f, 0f, back);
                    if (phaseTime >= returnTime)
                    {
                        phase = Phase.Rest;
                        twist = 0f;
                        SetPose(restPose);
                    }
                    break;
            }
        }

        void UpdateSlash(float dt)
        {
            if (slashArc == null || !slashArc.enabled)
                return;
            slashTimer += dt;
            float u = slashTimer / slashTime;
            if (u >= 1f)
            {
                slashArc.enabled = false;
                return;
            }
            ApplySlash(u);
        }

        void ApplySlash(float u)
        {
            float sweep = EaseOut(u);
            Quaternion aim = Quaternion.LookRotation(slashDirection, Vector3.up);
            slashArc.transform.rotation = aim * Quaternion.Euler(0f, Mathf.Lerp(slashSweepNow, -slashSweepNow, sweep) * 0.5f, slashTiltNow);
            float size = (slashHit ? hitScale : 1f) * slashScaleNow * Mathf.Lerp(0.85f, 1.05f, sweep);
            slashArc.transform.localScale = new Vector3(size, 1f, size);

            Color color = slashHit ? hitColor : missColor;
            color.a *= 1f - u * u;
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            slashArc.SetPropertyBlock(block);
        }

        void Enter(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        void SetPose(Vector3 euler)
        {
            pose = euler;
            if (bladePivot != null)
                bladePivot.localRotation = Quaternion.Euler(euler);
        }

        /// <summary>
        /// The same rotation with its yaw shifted by whole turns to sit within one turn above <paramref name="toYaw"/>, so
        /// interpolating down to it always turns the same way round as the cut (right to left).
        /// </summary>
        static Vector3 Unwrapped(Vector3 euler, float toYaw)
        {
            while (euler.y < toYaw)
                euler.y += 360f;
            while (euler.y > toYaw + 360f)
                euler.y -= 360f;
            return euler;
        }

        static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
