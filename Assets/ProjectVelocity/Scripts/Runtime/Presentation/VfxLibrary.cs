using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// The slice's effects, kept cheap for phones: one world-space particle system per kind of effect, emitted on demand with
    /// explicit positions and velocities (no instantiation, no allocation), plus a small pool of physical debris. Restrained on
    /// purpose: at phone scale clarity beats particle count. Gameplay never calls this directly; <see cref="PlayerFeedback"/>
    /// turns gameplay events into effects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxLibrary : MonoBehaviour
    {
        [Tooltip("Soft puffs: landing dust, architecture dust.")]
        [SerializeField] ParticleSystem dust;

        [Tooltip("Stretched sparks: hits, deflects, bolt impacts.")]
        [SerializeField] ParticleSystem sparks;

        [Tooltip("Single bright glow: hit flashes, tether connect, deaths.")]
        [SerializeField] ParticleSystem flash;

        [Tooltip("Small solid shards (mesh particles): enemy deaths, the player's shatter.")]
        [SerializeField] ParticleSystem shards;

        [Tooltip("Flat expanding rings on the ground or facing the camera: landings, boosts, slams, checkpoints.")]
        [SerializeField] ParticleSystem rings;

        [Tooltip("Speed streaks: boosts and high-speed flight.")]
        [SerializeField] ParticleSystem streaks;

        [Tooltip("Physical debris from breaking architecture and heavy deaths.")]
        [SerializeField] DebrisPool debris;

        public static VfxLibrary Instance { get; private set; }

        public DebrisPool Debris => debris;

        public void SetSystems(ParticleSystem dustSystem, ParticleSystem sparkSystem, ParticleSystem flashSystem,
            ParticleSystem shardSystem, ParticleSystem ringSystem, ParticleSystem streakSystem, DebrisPool debrisPool)
        {
            dust = dustSystem;
            sparks = sparkSystem;
            flash = flashSystem;
            shards = shardSystem;
            rings = ringSystem;
            streaks = streakSystem;
            debris = debrisPool;
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>A ring of soft dust puffs on a surface.</summary>
        public void Dust(Vector3 position, int count, float radius, float speed, Color color, float size = 1f)
        {
            if (dust == null)
                return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                p.position = position + outward * (radius * Random.Range(0.2f, 1f)) + Vector3.up * 0.2f;
                p.velocity = outward * (speed * Random.Range(0.5f, 1f)) + Vector3.up * (speed * Random.Range(0.1f, 0.4f));
                p.startSize = size * Random.Range(0.7f, 1.3f);
                p.startColor = color;
                dust.Emit(p, 1);
            }
        }

        /// <summary>A puff cloud filling a box (architecture breaking, a block landing).</summary>
        public void DustCloud(Bounds area, int count, float speed, Color color, float size)
        {
            if (dust == null)
                return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            Vector3 min = area.min;
            Vector3 extent = area.size;
            for (int i = 0; i < count; i++)
            {
                p.position = min + new Vector3(Random.value * extent.x, Random.value * extent.y, Random.value * extent.z);
                p.velocity = Random.insideUnitSphere * speed + Vector3.down * (speed * 0.3f);
                p.startSize = size * Random.Range(0.6f, 1.4f);
                p.startColor = color;
                dust.Emit(p, 1);
            }
        }

        /// <summary>A spray of sparks along <paramref name="direction"/>, spread by <paramref name="spread"/> (0 = a line, 1 = all round).</summary>
        public void Sparks(Vector3 position, Vector3 direction, float spread, int count, float speed, Color color)
        {
            if (sparks == null)
                return;
            Vector3 forward = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.up;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false, position = position, startColor = color };
            for (int i = 0; i < count; i++)
            {
                Vector3 d = Vector3.Slerp(forward, Random.onUnitSphere, spread * Random.value).normalized;
                p.velocity = d * (speed * Random.Range(0.4f, 1f));
                sparks.Emit(p, 1);
            }
        }

        /// <summary>One bright glow.</summary>
        public void Flash(Vector3 position, float size, Color color)
        {
            if (flash == null)
                return;
            var p = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = position,
                velocity = Vector3.zero,
                startSize = size,
                startColor = color,
            };
            flash.Emit(p, 1);
        }

        /// <summary>Solid shards bursting out (carrying <paramref name="carry"/>, e.g. the attacker's velocity).</summary>
        public void Shards(Vector3 position, Vector3 carry, int count, float speed, Color color, float size = 1f)
        {
            if (shards == null)
                return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false, position = position, startColor = color };
            for (int i = 0; i < count; i++)
            {
                Vector3 d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.8f + 0.2f;
                p.velocity = d.normalized * (speed * Random.Range(0.5f, 1f)) + carry;
                p.startSize = size * Random.Range(0.6f, 1.2f);
                p.rotation3D = Random.insideUnitSphere * 180f;
                shards.Emit(p, 1);
            }
        }

        /// <summary>An expanding ring (flat on the ground, or facing the camera, as the ring system is set up).</summary>
        public void Ring(Vector3 position, float size, Color color)
        {
            if (rings == null)
                return;
            var p = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = position,
                velocity = Vector3.zero,
                startSize = size,
                startColor = color,
            };
            rings.Emit(p, 1);
        }

        /// <summary>Speed streaks flowing past a point along <paramref name="velocity"/>.</summary>
        public void Streaks(Vector3 position, Vector3 velocity, int count, float radius, Color color)
        {
            if (streaks == null)
                return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false, startColor = color };
            Vector3 back = -velocity;
            for (int i = 0; i < count; i++)
            {
                p.position = position + Random.insideUnitSphere * radius;
                p.velocity = back * Random.Range(0.25f, 0.5f);
                streaks.Emit(p, 1);
            }
        }

        /// <summary>Clears every effect in flight (on respawn).</summary>
        public void ClearAll()
        {
            if (dust != null) dust.Clear();
            if (sparks != null) sparks.Clear();
            if (flash != null) flash.Clear();
            if (shards != null) shards.Clear();
            if (rings != null) rings.Clear();
            if (streaks != null) streaks.Clear();
            if (debris != null) debris.ClearAll();
        }
    }
}
