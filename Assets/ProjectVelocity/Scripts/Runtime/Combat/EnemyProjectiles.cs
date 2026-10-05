using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Every enemy bolt in the scene: a small fixed pool of glowing orange spheres moved by hand (no physics, no allocation).
    /// Bolts are slow and bright on purpose: they are read and dodged, and standing still is what gets you hit. A swing of the
    /// blade that meets one cuts it out of the air.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyProjectiles : MonoBehaviour
    {
        [Tooltip("Bolt objects (renderer and trail as children). Hidden while unused.")]
        [SerializeField] Transform[] bolts;

        [Tooltip("Radius (m) a bolt hits within.")]
        [SerializeField, Min(0.05f)] float radius = 0.45f;

        [Tooltip("Seconds a bolt flies before fading out.")]
        [SerializeField, Min(0.1f)] float lifetime = 3f;

        [Tooltip("Knock (m/s) a bolt gives on hit, along its flight, plus a little up.")]
        [SerializeField, Min(0f)] float knockback = 9f;

        [Tooltip("A blade swing cuts bolts out of the air within this distance (m) of your body.")]
        [SerializeField, Min(0f)] float parryReach = 2.6f;

        [Tooltip("Layers that stop a bolt. The player sits on Ignore Raycast and is checked separately.")]
        [SerializeField] LayerMask blockingLayers = ~(1 << 2);

        [SerializeField] VelocityPlayerController player;

        Vector3[] velocities;
        float[] ages;
        bool[] flying;
        TrailRenderer[] trails;
        PlayerHealth health;
        VelocityMotor motor;
        CharacterController body;
        CombatController combat;

        public static EnemyProjectiles Instance { get; private set; }

        /// <summary>Raised where a bolt ends: hit the player (true) or something else, cut, or faded (false).</summary>
        public event Action<Vector3, bool> Impact;
        /// <summary>Raised when the blade cuts a bolt out of the air.</summary>
        public event Action<Vector3> Parried;

        public void SetBolts(Transform[] boltObjects, VelocityPlayerController target)
        {
            bolts = boltObjects;
            player = target;
        }

        void Awake()
        {
            Instance = this;
            int count = bolts != null ? bolts.Length : 0;
            velocities = new Vector3[count];
            ages = new float[count];
            flying = new bool[count];
            trails = new TrailRenderer[count];
            for (int i = 0; i < count; i++)
            {
                if (bolts[i] == null)
                    continue;
                trails[i] = bolts[i].GetComponentInChildren<TrailRenderer>(true);
                bolts[i].gameObject.SetActive(false);
            }
        }

        void Start()
        {
            if (player == null)
                player = FindAnyObjectByType<VelocityPlayerController>();
            if (player != null)
            {
                health = player.GetComponent<PlayerHealth>();
                motor = player.GetComponent<VelocityMotor>();
                body = player.GetComponent<CharacterController>();
                combat = player.GetComponent<CombatController>();
                player.Respawned += Clear;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (player != null)
                player.Respawned -= Clear;
        }

        /// <summary>Fires a bolt. Returns false when every bolt is already flying.</summary>
        public bool Fire(Vector3 origin, Vector3 velocity)
        {
            if (bolts == null)
                return false;
            for (int i = 0; i < bolts.Length; i++)
            {
                if (bolts[i] == null || flying[i])
                    continue;
                bolts[i].position = origin;
                bolts[i].gameObject.SetActive(true);
                if (trails[i] != null)
                    trails[i].Clear();
                velocities[i] = velocity;
                ages[i] = 0f;
                flying[i] = true;
                return true;
            }
            return false;
        }

        public void Clear()
        {
            if (bolts == null || flying == null)
                return;
            for (int i = 0; i < bolts.Length; i++)
                End(i);
        }

        void Update()
        {
            if (bolts == null || flying == null)
                return;
            float dt = Time.deltaTime;
            bool hasPlayer = body != null && health != null && !player.Frozen;
            Vector3 top = Vector3.zero;
            Vector3 bottom = Vector3.zero;
            float playerRadius = 0.4f;
            if (hasPlayer)
                RealityContact.Capsule(body, out top, out bottom, out playerRadius);
            bool parrying = combat != null && combat.Phase == CombatController.AttackPhase.Slashing;
            Vector3 bodyCentre = motor != null ? motor.BodyCenter : Vector3.zero;

            for (int i = 0; i < bolts.Length; i++)
            {
                if (!flying[i])
                    continue;
                ages[i] += dt;
                Vector3 from = bolts[i].position;
                Vector3 to = from + velocities[i] * dt;

                if (parrying && (to - bodyCentre).sqrMagnitude < parryReach * parryReach)
                {
                    Parried?.Invoke(to);
                    End(i);
                    continue;
                }

                if (hasPlayer && Distance(from, to, bottom, top) < radius + playerRadius)
                {
                    Vector3 knock = velocities[i].normalized * knockback + Vector3.up * 4f;
                    health.TakeHit(from, knock, "shot");
                    Impact?.Invoke(to, true);
                    End(i);
                    continue;
                }

                Vector3 step = to - from;
                if (Physics.Raycast(from, step, out RaycastHit hit, step.magnitude, blockingLayers, QueryTriggerInteraction.Ignore))
                {
                    Impact?.Invoke(hit.point, false);
                    End(i);
                    continue;
                }

                bolts[i].position = to;
                if (ages[i] >= lifetime)
                    End(i);
            }
        }

        void End(int i)
        {
            flying[i] = false;
            if (bolts[i] != null)
                bolts[i].gameObject.SetActive(false);
        }

        static float Distance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            // Sampled segment-segment distance: bolts move a fraction of a metre per frame, so a few samples are exact enough.
            float best = float.MaxValue;
            Vector3 axis = q2 - p2;
            float axisSq = Mathf.Max(axis.sqrMagnitude, 1e-6f);
            for (int k = 0; k <= 4; k++)
            {
                Vector3 p = Vector3.Lerp(p1, q1, k * 0.25f);
                float t = Mathf.Clamp01(Vector3.Dot(p - p2, axis) / axisSq);
                float d = (p - (p2 + axis * t)).sqrMagnitude;
                if (d < best)
                    best = d;
            }
            return Mathf.Sqrt(best);
        }
    }
}
