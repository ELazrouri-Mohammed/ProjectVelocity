using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Starts a <see cref="RealityTransformSequence"/> the first time the player runs through its volume, heading along the
    /// route. Automatic: no button, and nothing about the player, the camera or time changes.
    /// This object marks where the transformed route has to be ready (the edge of the gap) and faces the way the route
    /// runs; the volume sits Lead Distance metres before it. It is a plain box checked against the path the player moved
    /// each frame, not a physics trigger, so no speed can skip it and nothing else ever bumps into it.
    /// When the player respawns (R / RESET, or a fall), it snaps the sequence back to its start and can fire again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RealityTrigger : MonoBehaviour
    {
        // A move longer than this (m) in one frame is a teleport (a respawn), not running through the volume.
        const float MaxStepDistance = 20f;
        // How far the floor marker floats above the surface it marks (m).
        const float MarkerLift = 0.03f;

        [Tooltip("The transformation this starts.")]
        [SerializeField] RealityTransformSequence sequence;

        [Tooltip("Whose run sets it off, and whose respawn resets it. Found automatically when empty.")]
        [SerializeField] VelocityPlayerController player;

        [Tooltip("Lead distance: how far (m) before this point (back along its forward axis) the volume sits. Larger = the world starts changing earlier, with more time to watch it before you get there.")]
        [SerializeField, Min(0f)] float leadDistance = 80f;

        [Tooltip("Size of the volume (m): width across the route, height (centred on this object's height), depth along the route.")]
        [SerializeField] Vector3 volumeSize = new Vector3(16f, 30f, 4f);

        [Tooltip("Optional line on the floor showing where the volume is. Laid on the floor under it when play starts and whenever Lead Distance changes.")]
        [SerializeField] Transform marker;

        bool armed = true;
        bool hasLastPosition;
        Vector3 lastPosition;
        // Lead distance the marker was last laid at, or negative when it hasn't been yet.
        float markerLead = -1f;

        /// <summary>True until it fires; true again after the player respawns.</summary>
        public bool IsArmed => armed;

        public RealityTransformSequence Sequence
        {
            get => sequence;
            set => sequence = value;
        }

        public VelocityPlayerController Player
        {
            get => player;
            set
            {
                if (player == value)
                    return;
                bool listening = Application.isPlaying && isActiveAndEnabled;
                if (listening && player != null)
                    player.Respawned -= Rearm;
                player = value;
                hasLastPosition = false;
                if (listening && player != null)
                    player.Respawned += Rearm;
            }
        }

        public float LeadDistance
        {
            get => leadDistance;
            set => leadDistance = Mathf.Max(0f, value);
        }

        public Vector3 VolumeSize
        {
            get => volumeSize;
            set => volumeSize = Vector3.Max(value, Vector3.zero);
        }

        public Transform Marker
        {
            get => marker;
            set
            {
                marker = value;
                markerLead = -1f;
            }
        }

        /// <summary>World-space centre of the volume.</summary>
        public Vector3 VolumeCenter => transform.TransformPoint(VolumeLocalCenter);

        Vector3 VolumeLocalCenter => new Vector3(0f, 0f, -leadDistance);

        /// <summary>Snaps the sequence back to its start and lets the trigger fire again.</summary>
        public void Rearm()
        {
            armed = true;
            hasLastPosition = false;
            if (sequence != null)
                sequence.ResetToStart();
        }

        void Awake()
        {
            if (player == null)
                player = FindAnyObjectByType<VelocityPlayerController>();
        }

        void OnEnable()
        {
            hasLastPosition = false;
            if (player != null)
                player.Respawned += Rearm;
        }

        void OnDisable()
        {
            if (player != null)
                player.Respawned -= Rearm;
        }

        void Update()
        {
            if (marker != null && markerLead != leadDistance)
                LayMarker();
            if (player == null)
                return;

            Vector3 position = player.transform.position;
            Vector3 previous = lastPosition;
            bool hadPosition = hasLastPosition;
            lastPosition = position;
            hasLastPosition = true;
            if (!armed || !hadPosition)
                return;

            // Only running through it along the route counts, so crossing it sideways or being put back at the start never does.
            Vector3 step = position - previous;
            if (step.sqrMagnitude > MaxStepDistance * MaxStepDistance || Vector3.Dot(step, transform.forward) <= 0f)
                return;
            if (!PathCrossesVolume(transform.InverseTransformPoint(previous), transform.InverseTransformPoint(position)))
                return;

            armed = false;
            if (sequence != null)
                sequence.Play();
        }

        /// <summary>Whether the segment between two local points passes through the volume (slab test).</summary>
        bool PathCrossesVolume(Vector3 a, Vector3 b)
        {
            Vector3 centre = VolumeLocalCenter;
            Vector3 half = volumeSize * 0.5f;
            Vector3 d = b - a;
            float enter = 0f;
            float exit = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float start = a[axis] - centre[axis];
                if (Mathf.Abs(d[axis]) < 1e-6f)
                {
                    if (start < -half[axis] || start > half[axis])
                        return false;
                    continue;
                }

                float t1 = (-half[axis] - start) / d[axis];
                float t2 = (half[axis] - start) / d[axis];
                enter = Mathf.Max(enter, Mathf.Min(t1, t2));
                exit = Mathf.Min(exit, Mathf.Max(t1, t2));
                if (enter > exit)
                    return false;
            }
            return true;
        }

        void LayMarker()
        {
            markerLead = leadDistance;
            Vector3 centre = VolumeCenter;
            Vector3 top = centre + Vector3.up * (volumeSize.y * 0.5f);
            if (Physics.Raycast(top, Vector3.down, out RaycastHit floor, volumeSize.y, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Vector3 along = Vector3.ProjectOnPlane(transform.forward, floor.normal);
                if (along.sqrMagnitude < 1e-4f)
                    along = transform.forward;
                marker.SetPositionAndRotation(floor.point + floor.normal * MarkerLift, Quaternion.LookRotation(along, floor.normal));
            }
            else
            {
                marker.SetPositionAndRotation(centre, transform.rotation);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.95f, 1f, 0.6f);
            Gizmos.DrawLine(VolumeCenter, transform.position);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(VolumeLocalCenter, volumeSize);
            Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
