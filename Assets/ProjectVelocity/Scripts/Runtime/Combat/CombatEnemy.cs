using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Sentinel: the one placeholder enemy. It hovers in place, can be selected and lunged at, and one hit kills it with a
    /// cheap burst (flash, collapse, a few shards). It never attacks and has no collider: its hurtbox is a sphere that
    /// <see cref="CombatController"/> measures the blade's reach against, so running into one never stops you.
    /// It comes back a few seconds after dying, and every one comes back when the player respawns.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEnemy : MonoBehaviour
    {
        enum State
        {
            Alive,
            Dying,
            Dead,
        }

        // Shards fall a little slower than the player, so the burst hangs for a moment.
        const float ShardGravity = 30f;
        // Size of the body while it flashes, relative to idle, before collapsing.
        const float FlashSwell = 1.15f;

        static readonly List<CombatEnemy> active = new List<CombatEnemy>();

        [Header("Hurtbox")]
        [Tooltip("Radius (m) of the sphere the blade has to reach, centred on this object (shown when selected in the Scene view).")]
        [SerializeField, Min(0.05f)] float hurtboxRadius = 0.7f;

        [Header("Respawn")]
        [Tooltip("Seconds after dying before it comes back. 0 = stays down until the player respawns (R / RESET).")]
        [SerializeField, Min(0f)] float respawnDelay = 4f;

        [Header("Idle Motion (visual only: the hurtbox stays put)")]
        [Tooltip("Hover bob height (m).")]
        [SerializeField, Min(0f)] float hoverHeight = 0.15f;

        [Tooltip("Hover bobs per second.")]
        [SerializeField, Min(0f)] float hoverRate = 0.5f;

        [Tooltip("Spin speed (degrees per second).")]
        [SerializeField] float spinSpeed = 60f;

        [Header("Placeholder Look")]
        [Tooltip("Hovers, spins, pulses while selected and collapses on death.")]
        [SerializeField] Transform visualRoot;

        [Tooltip("Renderers that switch material with the enemy's state.")]
        [SerializeField] Renderer[] stateRenderers;
        [SerializeField] Material idleMaterial;
        [SerializeField] Material selectedMaterial;

        [Tooltip("Shown from the hit until it has collapsed.")]
        [SerializeField] Material hitMaterial;

        [Tooltip("Size while selected, relative to idle.")]
        [SerializeField, Min(0f)] float selectedScale = 1.2f;

        [Tooltip("Pulse size while selected, as a share of the selected size.")]
        [SerializeField, Range(0f, 0.5f)] float pulseAmount = 0.08f;

        [Tooltip("Pulses per second while selected.")]
        [SerializeField, Min(0f)] float pulseRate = 4f;

        [Header("Death Feedback")]
        [Tooltip("Small pieces thrown out on death (hidden until then).")]
        [SerializeField] Transform[] shards;

        [Tooltip("How long (s) it flashes before collapsing.")]
        [SerializeField, Min(0f)] float flashTime = 0.05f;

        [Tooltip("How long (s) the collapse to nothing takes.")]
        [SerializeField, Min(0.01f)] float collapseTime = 0.12f;

        [Tooltip("Speed (m/s) the shards burst out at.")]
        [SerializeField, Min(0f)] float shardSpeed = 9f;

        [Tooltip("Share of the attacker's velocity the shards carry, so they spray the way you cut through.")]
        [SerializeField, Range(0f, 1f)] float shardCarry = 0.35f;

        [Tooltip("How long (s) the shards last, shrinking away.")]
        [SerializeField, Min(0.05f)] float shardLifetime = 0.6f;

        [Tooltip("How long (s) it takes to grow back in when it respawns.")]
        [SerializeField, Min(0.01f)] float reviveTime = 0.25f;

        State state = State.Alive;
        float stateTime;
        bool selected;
        Material shownMaterial;
        Vector3 visualBasePosition;
        Vector3 visualBaseScale = Vector3.one;
        float pulse = 1f;
        float grow = 1f;
        float hoverPhase;
        float spin;
        Vector3[] shardVelocities;
        Vector3[] shardSpins;
        Vector3 shardBaseScale = Vector3.one;
        // Seconds since the shards burst out, or negative when none are flying.
        float shardTime = -1f;

        /// <summary>Every enabled enemy, alive or not. Small enough to scan each frame.</summary>
        public static IReadOnlyList<CombatEnemy> Active => active;

        public bool IsAlive => state == State.Alive && isActiveAndEnabled;

        /// <summary>World-space centre of the hurtbox: what the lunge aims at and the blade's reach is measured to.</summary>
        public Vector3 HurtboxCenter => transform.position;

        public float HurtboxRadius
        {
            get => hurtboxRadius;
            set => hurtboxRadius = Mathf.Max(0.05f, value);
        }

        public float RespawnDelay
        {
            get => respawnDelay;
            set => respawnDelay = Mathf.Max(0f, value);
        }

        public bool IsSelected => selected;

        // Survives play mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            active.Clear();
        }

        /// <summary>Brings back every enemy that's down (called when the player respawns).</summary>
        public static void ReviveAll()
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null && active[i].state != State.Alive)
                    active[i].Revive();
            }
        }

        /// <summary>Wires up the placeholder look (used by the movement test builder).</summary>
        public void SetLook(Transform visual, Renderer[] renderers, Material idle, Material selectedLook, Material hit, Transform[] pieces)
        {
            visualRoot = visual;
            stateRenderers = renderers;
            idleMaterial = idle;
            selectedMaterial = selectedLook;
            hitMaterial = hit;
            shards = pieces;
        }

        /// <summary>Set by <see cref="CombatTargeting"/>: this is the enemy Attack will lunge at.</summary>
        public void SetSelected(bool value)
        {
            selected = value;
        }

        /// <summary>One hit kills. <paramref name="attackerVelocity"/> sprays the shards. False if it was already down.</summary>
        public bool Kill(Vector3 attackerVelocity)
        {
            if (!IsAlive)
                return false;

            state = State.Dying;
            stateTime = 0f;
            selected = false;
            SetMaterial(hitMaterial);
            BurstShards(attackerVelocity);
            return true;
        }

        public void Revive()
        {
            state = State.Alive;
            stateTime = 0f;
            grow = 0f;
            pulse = 1f;
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(true);
        }

        void Awake()
        {
            if (visualRoot != null)
            {
                visualBasePosition = visualRoot.localPosition;
                visualBaseScale = visualRoot.localScale;
            }

            // Bob and spin out of step with the others.
            Vector3 p = transform.position;
            float offset = Mathf.Repeat(p.x * 0.37f + p.z * 0.61f, 1f);
            hoverPhase = offset * 2f * Mathf.PI;
            spin = offset * 360f;

            int count = shards != null ? shards.Length : 0;
            shardVelocities = new Vector3[count];
            shardSpins = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                if (shards[i] == null)
                    continue;
                shardBaseScale = shards[i].localScale;
                shards[i].gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            if (!active.Contains(this))
                active.Add(this);
        }

        void OnDisable()
        {
            active.Remove(this);
            selected = false;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            stateTime += dt;

            switch (state)
            {
                case State.Alive:
                    UpdateAlive(dt);
                    break;
                case State.Dying:
                    UpdateDying();
                    break;
                case State.Dead:
                    if (respawnDelay > 0f && stateTime >= respawnDelay)
                        Revive();
                    break;
            }

            if (shardTime >= 0f)
                UpdateShards(dt);
        }

        void UpdateAlive(float dt)
        {
            SetMaterial(selected ? selectedMaterial : idleMaterial);
            if (visualRoot == null)
                return;

            grow = Mathf.MoveTowards(grow, 1f, dt / reviveTime);
            float goal = selected ? selectedScale * (1f + pulseAmount * Mathf.Sin(Time.time * pulseRate * 2f * Mathf.PI)) : 1f;
            pulse = Mathf.Lerp(pulse, goal, 1f - Mathf.Exp(-20f * dt));
            spin = Mathf.Repeat(spin + spinSpeed * dt, 360f);
            float bob = hoverHeight * Mathf.Sin(Time.time * hoverRate * 2f * Mathf.PI + hoverPhase);

            visualRoot.localPosition = visualBasePosition + Vector3.up * bob;
            visualRoot.localRotation = Quaternion.Euler(0f, spin, 0f);
            visualRoot.localScale = visualBaseScale * (pulse * grow * (2f - grow)); // eases in
        }

        /// <summary>Flash, then crush flat and shrink away. Then it's gone until it respawns.</summary>
        void UpdateDying()
        {
            float collapse = Mathf.Clamp01((stateTime - flashTime) / collapseTime);
            if (collapse >= 1f || visualRoot == null)
            {
                state = State.Dead;
                stateTime = 0f;
                if (visualRoot != null)
                    visualRoot.gameObject.SetActive(false);
                return;
            }

            float wide = FlashSwell * (1f - collapse * collapse);
            float tall = FlashSwell * (1f - collapse) * (1f - collapse);
            visualRoot.localScale = Vector3.Scale(visualBaseScale * pulse, new Vector3(wide, tall, wide));
        }

        void BurstShards(Vector3 attackerVelocity)
        {
            if (shards == null || shards.Length == 0)
                return;

            Vector3 centre = HurtboxCenter;
            Vector3 carry = attackerVelocity * shardCarry;
            for (int i = 0; i < shards.Length; i++)
            {
                Transform shard = shards[i];
                if (shard == null)
                    continue;

                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.2f; // mostly up and out
                direction.Normalize();
                shard.SetPositionAndRotation(centre + direction * 0.3f, Random.rotation);
                shard.localScale = shardBaseScale;
                shard.gameObject.SetActive(true);
                shardVelocities[i] = direction * (shardSpeed * Random.Range(0.6f, 1f)) + carry;
                shardSpins[i] = Random.insideUnitSphere * 720f;
            }
            shardTime = 0f;
        }

        void UpdateShards(float dt)
        {
            shardTime += dt;
            float life = shardTime / shardLifetime;
            bool done = life >= 1f;
            float size = 1f - life * life;

            for (int i = 0; i < shards.Length; i++)
            {
                Transform shard = shards[i];
                if (shard == null)
                    continue;
                if (done)
                {
                    shard.gameObject.SetActive(false);
                    continue;
                }

                shardVelocities[i].y -= ShardGravity * dt;
                shard.position += shardVelocities[i] * dt;
                shard.Rotate(shardSpins[i] * dt, Space.World);
                shard.localScale = shardBaseScale * size;
            }

            if (done)
                shardTime = -1f;
        }

        void SetMaterial(Material material)
        {
            if (material == null || material == shownMaterial || stateRenderers == null)
                return;
            foreach (Renderer r in stateRenderers)
            {
                if (r != null)
                    r.sharedMaterial = material;
            }
            shownMaterial = material;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(HurtboxCenter, hurtboxRadius);
        }
    }
}
