using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// One piece of reality-changing architecture. When its <see cref="RealityTransformSequence"/> plays, it first warns
    /// (its seams glow violet and it trembles for <see cref="Anticipation"/> seconds), then moves its own transform from an
    /// initial pose to a destination pose (local position and rotation, relative to its parent) over
    /// <see cref="Duration"/> seconds along its easing curve, and stays there until the sequence is reset.
    /// This object is the pivot: put its origin where the piece should hinge, fold or roll around (it can be in mid-air),
    /// and its geometry and colliders in children. A piece can sit inside another piece to chain a second move onto the
    /// first (e.g. swing up in one stage, fold away again in a later one).
    /// Its colliders are plain colliders moved by the transform: no Rigidbody, so physics never pushes anything with them,
    /// and the character controller's overlap recovery treats them like the rest of the level.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RealityChunk : MonoBehaviour
    {
        // Shakes per second while warning.
        const float TrembleRate = 14f;

        static readonly Color InitialGizmoColor = new Color(0.75f, 0.45f, 1f, 0.8f);
        static readonly Color DestinationGizmoColor = new Color(0.3f, 0.95f, 1f, 0.9f);

        [Header("Poses (local to the parent; this object is the pivot)")]
        [Tooltip("Where the pivot starts, and where Reset puts it back.")]
        [SerializeField] Vector3 initialPosition;

        [Tooltip("Rotation it starts at (Euler degrees). Interpolated per axis, so turns past 180° work.")]
        [SerializeField] Vector3 initialRotation;

        [Tooltip("Where the pivot ends up.")]
        [SerializeField] Vector3 destinationPosition;

        [Tooltip("Rotation it ends at (Euler degrees).")]
        [SerializeField] Vector3 destinationRotation;

        [Header("Timing")]
        [Tooltip("When the move starts (s after the sequence starts), on top of the sequence's Start Delay and this piece's place in the order × Stagger. Can be negative.")]
        [SerializeField] float delay;

        [Tooltip("How long the move takes (s). The sequence's Duration Scale scales it.")]
        [SerializeField, Min(0.01f)] float duration = 1.5f;

        [Tooltip("Progress over the move: time 0-1 → pose 0 (initial) - 1 (destination). Values past 1 overshoot. The move always ends exactly on the destination. The shape is the piece's personality: a fast eruption, a heavy turn, a collapse...")]
        [SerializeField] AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Anticipation (the readable warning before the move)")]
        [Tooltip("How long (s) before its move the piece warns: its seams switch to the warning glow and it trembles. Taken out of the wait before the move, so it never delays it; cut short when the move starts sooner.")]
        [SerializeField, Min(0f)] float anticipation = 0.3f;

        [Tooltip("How far (m) it shakes while warning, along its direction of travel (along the parent's X when it only turns). 0 = glow only.")]
        [SerializeField, Min(0f)] float tremble = 0.1f;

        [Header("Placeholder Look")]
        [Tooltip("Glowing seams that switch material with the piece's state.")]
        [SerializeField] Renderer[] accentRenderers;

        [Tooltip("Before it does anything.")]
        [SerializeField] Material dormantMaterial;

        [Tooltip("While it warns (anticipation).")]
        [SerializeField] Material warningMaterial;

        [Tooltip("While it moves, and briefly after it lands.")]
        [SerializeField] Material shiftingMaterial;

        [Tooltip("Once it has landed.")]
        [SerializeField] Material settledMaterial;

        // Progress of the pose last written to the transform (0-1), or negative when it has to be written again.
        float appliedProgress = -1f;
        Material shownMaterial;
        Collider[] colliders;

        public float Delay
        {
            get => delay;
            set => delay = value;
        }

        public float Duration
        {
            get => duration;
            set => duration = Mathf.Max(0.01f, value);
        }

        public AnimationCurve Easing
        {
            get => easing;
            set => easing = value;
        }

        public float Anticipation
        {
            get => anticipation;
            set => anticipation = Mathf.Max(0f, value);
        }

        public float Tremble
        {
            get => tremble;
            set => tremble = Mathf.Max(0f, value);
        }

        /// <summary>Every collider that moves with this piece (its own, and any inner piece's).</summary>
        public Collider[] Colliders
        {
            get
            {
                if (colliders == null)
                    colliders = GetComponentsInChildren<Collider>(true);
                return colliders;
            }
        }

        /// <summary>Sets both poses and the move (used by the movement test builder).</summary>
        public void Configure(Vector3 fromPosition, Vector3 fromRotation, Vector3 toPosition, Vector3 toRotation,
            float moveDuration, AnimationCurve curve)
        {
            initialPosition = fromPosition;
            initialRotation = fromRotation;
            destinationPosition = toPosition;
            destinationRotation = toRotation;
            Duration = moveDuration;
            easing = curve;
        }

        /// <summary>Wires up the placeholder look (used by the movement test builder).</summary>
        public void SetLook(Renderer[] accents, Material dormant, Material warning, Material shifting, Material settled)
        {
            accentRenderers = accents;
            dormantMaterial = dormant;
            warningMaterial = warning;
            shiftingMaterial = shifting;
            settledMaterial = settled;
        }

        /// <summary>Back to the initial pose and look at once, with nothing left half-way.</summary>
        public void SnapToInitial()
        {
            ApplyPose(0f, Vector3.zero);
            appliedProgress = 0f;
            SetMaterial(dormantMaterial);
        }

        /// <summary>
        /// Puts the piece where it is <paramref name="time"/> seconds into its own move (negative = still waiting; the last
        /// Anticipation seconds of the wait are the warning) and updates its look. <paramref name="pulse"/> lights it up
        /// while it waits. Returns true when the transform changed. Run by <see cref="RealityTransformSequence"/>;
        /// allocation-free.
        /// </summary>
        public bool Sample(float time, float durationScale, bool pulse, float landingFlash, out bool finished)
        {
            float length = Mathf.Max(0.01f, duration * durationScale);
            finished = time >= length + landingFlash;

            bool waiting = time < 0f;
            bool warning = waiting && time >= -anticipation;
            Material look = warning ? warningMaterial
                : waiting ? (pulse ? shiftingMaterial : dormantMaterial)
                : time < length + landingFlash ? shiftingMaterial
                : settledMaterial;
            SetMaterial(look);

            if (warning && tremble > 0f)
            {
                // Shaking in place: rewritten every frame, and the real pose is written again once the move starts.
                float shake = Mathf.Sin((time + anticipation) * TrembleRate * 2f * Mathf.PI) * tremble;
                ApplyPose(0f, TrembleDirection() * shake);
                appliedProgress = -1f;
                return true;
            }

            float progress = Mathf.Clamp01(time / length);
            if (progress == appliedProgress)
                return false;
            ApplyPose(progress, Vector3.zero);
            appliedProgress = progress;
            return true;
        }

        void Awake()
        {
            // Driven by its transform only. Should someone add a Rigidbody, physics still must never move it.
            if (TryGetComponent(out Rigidbody body))
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
            }
            colliders = GetComponentsInChildren<Collider>(true);
        }

        void ApplyPose(float progress, Vector3 offset)
        {
            // The ends are exact, so pieces always meet flush and Reset leaves nothing a hair off.
            Vector3 position;
            Quaternion rotation;
            if (progress <= 0f)
            {
                position = initialPosition;
                rotation = Quaternion.Euler(initialRotation);
            }
            else if (progress >= 1f)
            {
                position = destinationPosition;
                rotation = Quaternion.Euler(destinationRotation);
            }
            else
            {
                float e = easing != null && easing.length > 0 ? easing.Evaluate(progress) : progress;
                position = Vector3.LerpUnclamped(initialPosition, destinationPosition, e);
                rotation = Quaternion.Euler(Vector3.LerpUnclamped(initialRotation, destinationRotation, e));
            }
            transform.SetLocalPositionAndRotation(position + offset, rotation);
        }

        Vector3 TrembleDirection()
        {
            Vector3 travel = destinationPosition - initialPosition;
            return travel.sqrMagnitude > 1e-4f ? travel.normalized : Vector3.right;
        }

        void SetMaterial(Material material)
        {
            if (material == null || material == shownMaterial || accentRenderers == null)
                return;
            foreach (Renderer r in accentRenderers)
            {
                if (r != null)
                    r.sharedMaterial = material;
            }
            shownMaterial = material;
        }

        /// <summary>Outlines the piece's colliders at both poses: violet = initial, cyan = destination.</summary>
        void OnDrawGizmosSelected()
        {
            Matrix4x4 parentToWorld = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
            Vector3 scale = transform.localScale;
            DrawPose(parentToWorld * Matrix4x4.TRS(initialPosition, Quaternion.Euler(initialRotation), scale), InitialGizmoColor);
            DrawPose(parentToWorld * Matrix4x4.TRS(destinationPosition, Quaternion.Euler(destinationRotation), scale), DestinationGizmoColor);
            Gizmos.matrix = Matrix4x4.identity;
        }

        void DrawPose(Matrix4x4 pivotToWorld, Color color)
        {
            Gizmos.color = color;
            Gizmos.matrix = pivotToWorld;
            Gizmos.DrawWireSphere(Vector3.zero, 0.5f);

            Matrix4x4 worldToPivot = transform.worldToLocalMatrix;
            foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>())
            {
                Gizmos.matrix = pivotToWorld * worldToPivot * box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}
