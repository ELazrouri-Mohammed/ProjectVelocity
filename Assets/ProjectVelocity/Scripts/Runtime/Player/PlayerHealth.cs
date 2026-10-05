using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// The player can take one hit. An enemy hit breaks the shield (a knock and a red flash); a second hit before it comes back
    /// kills. The shield recharges after a few seconds without being hit, and at once on every kill, so aggression keeps you
    /// alive. Moving architecture, lethal hazards and falls kill outright.
    /// Death is a beat, not a loading screen: the world drops into slow motion for a moment, then you're back at the last
    /// checkpoint (see <see cref="VelocityPlayerController.Respawn"/>). There is no health bar: the HUD shows the shield as a
    /// screen-edge glow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField] VelocityPlayerController player;
        [SerializeField] VelocityMotor motor;

        [Tooltip("Kills restore the shield. Optional.")]
        [SerializeField] CombatController combat;

        [Header("Shield")]
        [Tooltip("Seconds without being hit before a broken shield comes back.")]
        [SerializeField, Min(0f)] float shieldRechargeDelay = 3.5f;

        [Tooltip("A kill brings the shield back at once.")]
        [SerializeField] bool killRestoresShield = true;

        [Tooltip("After a hit, further hits are ignored for this long (s), so one attack never counts twice.")]
        [SerializeField, Min(0f)] float hitInvulnerability = 0.6f;

        [Tooltip("After respawning, hits are ignored for this long (s).")]
        [SerializeField, Min(0f)] float respawnInvulnerability = 1f;

        [Tooltip("A hit freezes the world this long (s, real time).")]
        [SerializeField, Min(0f)] float hitStop = 0.06f;

        [Header("Death")]
        [Tooltip("Real seconds between dying and being back at the checkpoint. Keep it short: fail → retry → learn.")]
        [SerializeField, Min(0f)] float deathTime = 0.4f;

        [Tooltip("World speed while the death beat plays.")]
        [SerializeField, Range(0.01f, 1f)] float deathTimeScale = 0.12f;

        bool shieldUp = true;
        bool dead;
        float deathTimer;
        float lastHitTime = float.NegativeInfinity;
        float invulnerableUntil;
        string lastDeathReason;
        int deaths;

        /// <summary>Raised when an enemy hit breaks the shield (source position).</summary>
        public event Action<Vector3> ShieldBroken;
        public event Action ShieldRestored;
        /// <summary>Raised the moment the player dies (the reason, for the HUD).</summary>
        public event Action<string> Died;

        public bool ShieldUp => shieldUp;
        public bool IsDead => dead;
        public int Deaths => deaths;
        public string LastDeathReason => lastDeathReason;

        /// <summary>0 just after the shield broke, 1 when it's about to come back (1 while it's up).</summary>
        public float ShieldRecharge01 => shieldUp ? 1f : Mathf.Clamp01((Time.time - lastHitTime) / Mathf.Max(0.01f, shieldRechargeDelay));

        public VelocityPlayerController Player
        {
            get => player;
            set => player = value;
        }

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        public CombatController Combat
        {
            get => combat;
            set => combat = value;
        }

        void Awake()
        {
            if (player == null)
                player = GetComponent<VelocityPlayerController>();
            if (motor == null)
                motor = GetComponent<VelocityMotor>();
            if (combat == null)
                combat = GetComponent<CombatController>();
        }

        void OnEnable()
        {
            if (player != null)
                player.Respawned += OnRespawned;
            if (combat != null)
                combat.Killed += OnKill;
        }

        void OnDisable()
        {
            if (player != null)
                player.Respawned -= OnRespawned;
            if (combat != null)
                combat.Killed -= OnKill;
        }

        /// <summary>
        /// An enemy attack connects. Breaks the shield (knocking you by <paramref name="knockback"/>), or kills if it's already
        /// down. Returns false when the hit was ignored (invulnerable, already dead).
        /// </summary>
        public bool TakeHit(Vector3 sourcePosition, Vector3 knockback, string source)
        {
            if (dead || Time.unscaledTime < invulnerableUntil)
                return false;

            if (!shieldUp)
            {
                Kill(source);
                return true;
            }

            shieldUp = false;
            lastHitTime = Time.time;
            invulnerableUntil = Time.unscaledTime + hitInvulnerability;
            if (motor != null && knockback.sqrMagnitude > 0.01f)
                motor.Knock(knockback);
            GameTime.HitStop(hitStop, 0.05f);
            ShieldBroken?.Invoke(sourcePosition);
            return true;
        }

        /// <summary>Dies at once (falls, crushing architecture, lethal hazards, or a hit with the shield down).</summary>
        public void Kill(string reason)
        {
            if (dead)
                return;
            dead = true;
            deaths++;
            deathTimer = deathTime;
            lastDeathReason = reason;
            if (player != null)
                player.Frozen = true;
            GameTime.SlowMotion(deathTimeScale, deathTime);
            Died?.Invoke(reason);
            if (deathTime <= 0f)
                FinishDeath();
        }

        /// <summary>Back to full: shield up, not dead (the director calls this when restarting the whole run).</summary>
        public void ResetHealth()
        {
            dead = false;
            shieldUp = true;
            deaths = 0;
            if (player != null)
                player.Frozen = false;
        }

        void Update()
        {
            if (dead)
            {
                deathTimer -= Time.unscaledDeltaTime;
                if (deathTimer <= 0f)
                    FinishDeath();
                return;
            }

            if (!shieldUp && Time.time - lastHitTime >= shieldRechargeDelay)
                RestoreShield();
        }

        void FinishDeath()
        {
            dead = false;
            GameTime.ClearSlowMotion();
            if (player != null)
            {
                player.Frozen = false;
                player.Respawn();
            }
        }

        void RestoreShield()
        {
            if (shieldUp)
                return;
            shieldUp = true;
            ShieldRestored?.Invoke();
        }

        void OnKill(CombatEnemy enemy)
        {
            if (killRestoresShield && !dead)
                RestoreShield();
        }

        void OnRespawned()
        {
            dead = false;
            invulnerableUntil = Time.unscaledTime + respawnInvulnerability;
            RestoreShield();
        }
    }
}
