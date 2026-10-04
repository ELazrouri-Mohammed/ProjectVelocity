using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Placeholder blade animation and slash effect. Purely visual: <see cref="CombatController"/> says when to draw back
    /// (during a lunge), when to swing and whether the swing connected. The blade swings in the body's frame; the slash arc is
    /// aimed along the attack itself, so it reads even before the body has turned. Allocation-free: poses are interpolated by
    /// hand and the arc fades through a material property block.
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
        [Tooltip("Turned to swing the blade: the hand, a child of the character's visual root. The blade points along its +Z.")]
        [SerializeField] Transform bladePivot;

        [Tooltip("The slash arc: a flat crescent opening along +Z, a child of the player at chest height. Hidden between swings.")]
        [SerializeField] Renderer slashArc;

        [Header("Blade Poses (hand rotation, degrees)")]
        [Tooltip("Held back and low while running.")]
        [SerializeField] Vector3 restPose = new Vector3(27f, 157f, 0f);

        [Tooltip("Drawn back to the right, ready to cut. Held while lunging.")]
        [SerializeField] Vector3 windupPose = new Vector3(-8f, 115f, -25f);

        [Tooltip("End of the cut, across to the left. The swing sweeps through the front, the return carries on round the back.")]
        [SerializeField] Vector3 swingEndPose = new Vector3(10f, -65f, 25f);

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
        // Current hand rotation as Euler angles, with yaw unwrapped so a swing always sweeps the intended way round.
        Vector3 pose;
        Vector3 poseFrom;
        MaterialPropertyBlock block;
        Vector3 slashDirection = Vector3.forward;
        float slashTimer;
        bool slashHit;

        /// <summary>Hooks up the parts (used by the movement test builder).</summary>
        public void SetParts(Transform hand, Renderer arc)
        {
            bladePivot = hand;
            slashArc = arc;
        }

        /// <summary>Draws the blade back, ready to cut, and holds it there until <see cref="PlaySlash"/>.</summary>
        public void PlayWindup()
        {
            poseFrom = Unwrapped(pose, windupPose.y);
            Enter(Phase.Windup);
        }

        /// <summary>Swings the blade and shows the slash arc along <paramref name="direction"/> (world space).</summary>
        public void PlaySlash(Vector3 direction)
        {
            poseFrom = Unwrapped(pose, swingEndPose.y);
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
            SetPose(restPose);
            if (slashArc != null)
                slashArc.enabled = false;
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            ResetPose();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            UpdateBlade(dt);
            UpdateSlash(dt);
        }

        void UpdateBlade(float dt)
        {
            if (phase == Phase.Rest)
                return;

            phaseTime += dt;
            switch (phase)
            {
                case Phase.Windup:
                    SetPose(Vector3.Lerp(poseFrom, windupPose, EaseOut(phaseTime / windupTime)));
                    break;
                case Phase.Swing:
                    float swing = phaseTime / swingTime;
                    SetPose(Vector3.Lerp(poseFrom, swingEndPose, EaseOut(swing)));
                    if (swing >= 1f)
                        Enter(Phase.Hold);
                    break;
                case Phase.Hold:
                    if (phaseTime >= holdTime)
                        Enter(Phase.Return);
                    break;
                case Phase.Return:
                    // Carry on round the back to rest, the way the cut was going.
                    float back = phaseTime / returnTime;
                    Vector3 rest = restPose;
                    rest.y -= 360f;
                    SetPose(Vector3.Lerp(swingEndPose, rest, EaseInOut(back)));
                    if (back >= 1f)
                    {
                        phase = Phase.Rest;
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
            slashArc.transform.rotation = aim * Quaternion.Euler(0f, Mathf.Lerp(slashSweep, -slashSweep, sweep) * 0.5f, slashTilt);
            float size = (slashHit ? hitScale : 1f) * Mathf.Lerp(0.85f, 1.05f, sweep);
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
