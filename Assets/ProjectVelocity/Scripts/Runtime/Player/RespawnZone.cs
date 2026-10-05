using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// A stretch of the level with its own restart point: failing inside it (falling off, being hit or crushed by moving
    /// architecture, or pressing R / RESET) puts you back at its start instead of at the level spawn, so a failed attempt at
    /// a section restarts that section. The zone is a box centred on this object, in its own axes; several zones may share a
    /// restart point to cover an irregular area.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RespawnZone : MonoBehaviour
    {
        static readonly List<RespawnZone> active = new List<RespawnZone>();

        [Tooltip("Size (m) of the zone: a box centred on this object, in its own axes.")]
        [SerializeField] Vector3 size = new Vector3(100f, 100f, 100f);

        [Tooltip("Where you restart, facing along its forward (blue) axis.")]
        [SerializeField] Transform restartPoint;

        public Vector3 Size
        {
            get => size;
            set => size = Vector3.Max(value, Vector3.zero);
        }

        public Transform RestartPoint
        {
            get => restartPoint;
            set => restartPoint = value;
        }

        // Survives play mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            active.Clear();
        }

        /// <summary>The restart point of the zone containing <paramref name="position"/>, if any.</summary>
        public static bool TryGetRestart(Vector3 position, out Vector3 point, out float yaw)
        {
            for (int i = 0; i < active.Count; i++)
            {
                RespawnZone zone = active[i];
                if (zone.restartPoint == null || !zone.Contains(position))
                    continue;
                point = zone.restartPoint.position;
                yaw = zone.restartPoint.eulerAngles.y;
                return true;
            }
            point = Vector3.zero;
            yaw = 0f;
            return false;
        }

        bool Contains(Vector3 position)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        void OnEnable()
        {
            if (!active.Contains(this))
                active.Add(this);
        }

        void OnDisable()
        {
            active.Remove(this);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = Matrix4x4.identity;
            if (restartPoint != null)
            {
                Gizmos.DrawWireSphere(restartPoint.position, 1f);
                Gizmos.DrawLine(restartPoint.position, restartPoint.position + restartPoint.forward * 4f);
            }
        }
    }
}
