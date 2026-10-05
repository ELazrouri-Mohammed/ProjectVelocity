using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// A checkpoint gate on the route (or the finish line). Running through its volume along the route makes it the restart
    /// point: dying after it puts you back here in a fraction of a second, with everything past it reset. Like a reality trigger
    /// it is a plain box checked against the path the player moved each frame, so no speed can skip it.
    /// This object marks the gate and faces the way the route runs; the volume is centred on it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliceCheckpoint : MonoBehaviour
    {
        // A move longer than this (m) in one frame is a teleport, not running through.
        const float MaxStepDistance = 25f;

        [Tooltip("Order along the route (0 = the start). Matches the Slice Segment that begins here.")]
        [SerializeField, Min(0)] int index;

        [Tooltip("Short name shown when it's reached.")]
        [SerializeField] string label = "CHECKPOINT";

        [Tooltip("Where you restart, facing along its forward (blue) axis.")]
        [SerializeField] Transform restartPoint;

        [Tooltip("Size of the volume (m): width across the route, height, depth along it. Centred on this object.")]
        [SerializeField] Vector3 volumeSize = new Vector3(40f, 40f, 4f);

        [Tooltip("Falling below this height after reaching this checkpoint kills.")]
        [SerializeField] float killHeight = -60f;

        [Tooltip("The finish line rather than a checkpoint.")]
        [SerializeField] bool isFinish;

        [Header("Look")]
        [Tooltip("Glowing parts that light up once reached.")]
        [SerializeField] Renderer[] beacons;
        [SerializeField] Material dormantMaterial;
        [SerializeField] Material litMaterial;

        Transform player;
        Vector3 lastPosition;
        bool hasLastPosition;
        bool lit;

        /// <summary>Raised when the player runs through it (every time; the director decides whether it counts).</summary>
        public event Action<SliceCheckpoint> Crossed;

        public int Index
        {
            get => index;
            set => index = Mathf.Max(0, value);
        }

        public string Label
        {
            get => label;
            set => label = value;
        }

        public Transform RestartPoint
        {
            get => restartPoint;
            set => restartPoint = value;
        }

        public Vector3 VolumeSize
        {
            get => volumeSize;
            set => volumeSize = Vector3.Max(value, Vector3.zero);
        }

        public float KillHeight
        {
            get => killHeight;
            set => killHeight = value;
        }

        public bool IsFinish
        {
            get => isFinish;
            set => isFinish = value;
        }

        public bool IsLit => lit;

        /// <summary>Wires up the look (used by the scene builder).</summary>
        public void SetLook(Renderer[] glowing, Material dormant, Material litLook)
        {
            beacons = glowing;
            dormantMaterial = dormant;
            litMaterial = litLook;
        }

        public void SetPlayer(Transform target)
        {
            player = target;
            hasLastPosition = false;
        }

        public void SetLit(bool value)
        {
            lit = value;
            Material look = value ? litMaterial : dormantMaterial;
            if (look == null || beacons == null)
                return;
            foreach (Renderer r in beacons)
            {
                if (r != null)
                    r.sharedMaterial = look;
            }
        }

        void Update()
        {
            if (player == null)
                return;
            Vector3 position = player.position;
            Vector3 previous = lastPosition;
            bool had = hasLastPosition;
            lastPosition = position;
            hasLastPosition = true;
            if (!had)
                return;

            Vector3 step = position - previous;
            if (step.sqrMagnitude > MaxStepDistance * MaxStepDistance || Vector3.Dot(step, transform.forward) <= 0f)
                return;
            if (SegmentCrossesBox(transform.InverseTransformPoint(previous), transform.InverseTransformPoint(position), volumeSize * 0.5f))
                Crossed?.Invoke(this);
        }

        /// <summary>Whether the segment between two local points passes through the box of half size <paramref name="half"/> (slab test).</summary>
        static bool SegmentCrossesBox(Vector3 a, Vector3 b, Vector3 half)
        {
            Vector3 d = b - a;
            float enter = 0f;
            float exit = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(d[axis]) < 1e-6f)
                {
                    if (a[axis] < -half[axis] || a[axis] > half[axis])
                        return false;
                    continue;
                }
                float t1 = (-half[axis] - a[axis]) / d[axis];
                float t2 = (half[axis] - a[axis]) / d[axis];
                enter = Mathf.Max(enter, Mathf.Min(t1, t2));
                exit = Mathf.Min(exit, Mathf.Max(t1, t2));
                if (enter > exit)
                    return false;
            }
            return true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = isFinish ? new Color(1f, 0.85f, 0.3f, 0.6f) : new Color(0.3f, 1f, 0.9f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, volumeSize);
            Gizmos.matrix = Matrix4x4.identity;
            if (restartPoint != null)
            {
                Gizmos.DrawWireSphere(restartPoint.position, 0.8f);
                Gizmos.DrawLine(restartPoint.position, restartPoint.position + restartPoint.forward * 4f);
            }
        }
    }
}
