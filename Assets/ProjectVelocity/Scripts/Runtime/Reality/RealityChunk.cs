using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// One piece of reality-changing architecture. When its <see cref="RealityTransformSequence"/> plays, it moves its own
    /// transform from an initial pose to a destination pose (local position and rotation, relative to its parent) over
    /// <see cref="Duration"/> seconds along an easing curve, then stays there until the sequence is reset.
    /// This object is the pivot: put its origin where the piece should hinge, fold or roll around (it can be in mid-air),
    /// and its geometry and colliders in children. Moved only by its transform, on a kinematic Rigidbody, so it never pushes
    /// anything with physics forces: the player is never launched by it and simply stands on wherever it is.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RealityChunk : MonoBehaviour
    {
        static readonly Color InitialGizmoColor = new Color(0.75f, 0.45f, 1f, 0.8f);
        static readonly Color DestinationGizmoColor = new Color(0.3f, 0.95f, 1f, 0.9f);

        [Header("Poses (local to the parent; this object is the pivot)")]
        [Tooltip("Where the pivot starts, and where Reset puts it back.")]
        [SerializeField] Vector3 initialPosition;

        [Tooltip("Rotation it starts at (Euler degrees). Interpolated per axis, so turns past 180° work.")]
        [SerializeField] Vector3 initialRotation;

        [Tooltip("Where the pivot ends up: the piece's place in the finished path.")]
        [SerializeField] Vector3 destinationPosition;

        [Tooltip("Rotation it ends at (Euler degrees).")]
        [SerializeField] Vector3 destinationRotation;

        [Header("Timing")]
        [Tooltip("Extra wait (s) on top of this piece's slot in the sequence (Start Delay + its place in the order × Stagger). Can be negative to start early.")]
        [SerializeField] float extraDelay;

        [Tooltip("How long the move takes (s). The sequence's Duration Scale scales it.")]
        [SerializeField, Min(0.01f)] float duration = 1.5f;

        [Tooltip("Progress over the move: time 0-1 → pose 0 (initial) - 1 (destination). Values past 1 overshoot. The move always ends exactly on the destination.")]
        [SerializeField] AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Placeholder Look")]
        [Tooltip("Glowing edges that switch material with the piece's state.")]
        [SerializeField] Renderer[] accentRenderers;

        [Tooltip("Before it moves.")]
        [SerializeField] Material dormantMaterial;

        [Tooltip("While it moves, and in brief pulses (when the sequence starts and when the piece lands).")]
        [SerializeField] Material shiftingMaterial;

        [Tooltip("Once it has landed in place.")]
        [SerializeField] Material settledMaterial;

        // Progress of the pose last written to the transform (0-1), or negative when nothing has been written yet.
        float appliedProgress = -1f;
        Material shownMaterial;

        public float ExtraDelay
        {
            get => extraDelay;
            set => extraDelay = value;
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
        public void SetLook(Renderer[] accents, Material dormant, Material shifting, Material settled)
        {
            accentRenderers = accents;
            dormantMaterial = dormant;
            shiftingMaterial = shifting;
            settledMaterial = settled;
        }

        /// <summary>Back to the initial pose and look at once, with nothing left half-way.</summary>
        public void SnapToInitial()
        {
            ApplyPose(0f);
            appliedProgress = 0f;
            SetMaterial(dormantMaterial);
        }

        /// <summary>
        /// Puts the piece where it is <paramref name="time"/> seconds into its own move (negative = not started yet) and
        /// updates its look. <paramref name="pulse"/> lights it up while it waits. Returns true when the transform changed.
        /// Run by <see cref="RealityTransformSequence"/>; allocation-free.
        /// </summary>
        public bool Sample(float time, float durationScale, bool pulse, float landingFlash, out bool finished)
        {
            float length = Mathf.Max(0.01f, duration * durationScale);
            finished = time >= length + landingFlash;

            bool waiting = time < 0f;
            bool shifting = waiting ? pulse : time < length + landingFlash;
            SetMaterial(shifting ? shiftingMaterial : waiting ? dormantMaterial : settledMaterial);

            float progress = Mathf.Clamp01(time / length);
            if (progress == appliedProgress)
                return false;
            ApplyPose(progress);
            appliedProgress = progress;
            return true;
        }

        void Awake()
        {
            // Driven by its transform only: physics must never move it, or push the player with it.
            if (TryGetComponent(out Rigidbody body))
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        void ApplyPose(float progress)
        {
            // The ends are exact, so pieces always meet flush and Reset leaves nothing a hair off.
            if (progress <= 0f)
            {
                transform.SetLocalPositionAndRotation(initialPosition, Quaternion.Euler(initialRotation));
                return;
            }
            if (progress >= 1f)
            {
                transform.SetLocalPositionAndRotation(destinationPosition, Quaternion.Euler(destinationRotation));
                return;
            }

            float e = easing != null && easing.length > 0 ? easing.Evaluate(progress) : progress;
            transform.SetLocalPositionAndRotation(Vector3.LerpUnclamped(initialPosition, destinationPosition, e),
                Quaternion.Euler(Vector3.LerpUnclamped(initialRotation, destinationRotation, e)));
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
