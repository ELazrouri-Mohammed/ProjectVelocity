using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>Moments in an enemy's behaviour that presentation (sound, effects) reacts to.</summary>
    public enum EnemyCue
    {
        Woke,
        Telegraph,
        Fire,
        Dash,
        Slam,
        Died,
        Deflect,
    }

    /// <summary>
    /// Shared behaviour for the slice's enemies: finds the player, wakes up when the player comes close, checks line of sight,
    /// and puts itself back at its post when its <see cref="CombatEnemy"/> revives (a checkpoint restart). The enemy's hittable
    /// body (hurtbox, selection, death) is the <see cref="CombatEnemy"/> on the same object; this only decides what it does.
    /// Presentation listens to <see cref="AnyCue"/>.
    /// </summary>
    [RequireComponent(typeof(CombatEnemy))]
    public abstract class SliceEnemy : MonoBehaviour
    {
        [Tooltip("Wakes up when the player is this close (m) and in sight.")]
        [SerializeField, Min(0f)] protected float wakeRange = 55f;

        [Tooltip("Layers that block its line of sight. The player itself sits on Ignore Raycast.")]
        [SerializeField] protected LayerMask sightLayers = ~(1 << 2);

        [Tooltip("The visible body: turned and animated by the behaviour. The hurtbox stays on this object.")]
        [SerializeField] protected Transform visual;

        protected CombatEnemy body;
        protected VelocityPlayerController player;
        protected VelocityMotor playerMotor;
        protected PlayerHealth playerHealth;
        protected CharacterController playerBody;
        protected Vector3 home;
        protected Quaternion homeRotation;
        protected bool awake;

        /// <summary>Raised for every enemy cue (who, what, where).</summary>
        public static event Action<SliceEnemy, EnemyCue, Vector3> AnyCue;

        public CombatEnemy Body => body;
        public bool IsAwake => awake;

        public Transform Visual
        {
            get => visual;
            set => visual = value;
        }

        public float WakeRange
        {
            get => wakeRange;
            set => wakeRange = Mathf.Max(0f, value);
        }

        // Survives play mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            AnyCue = null;
        }

        protected virtual void Awake()
        {
            body = GetComponent<CombatEnemy>();
            home = transform.position;
            homeRotation = transform.rotation;
        }

        protected virtual void Start()
        {
            player = SliceDirector.Current != null ? SliceDirector.Current.Player : null;
            if (player == null)
                player = FindAnyObjectByType<VelocityPlayerController>();
            if (player != null)
            {
                playerMotor = player.GetComponent<VelocityMotor>();
                playerHealth = player.GetComponent<PlayerHealth>();
                playerBody = player.GetComponent<CharacterController>();
            }
        }

        protected virtual void OnEnable()
        {
            if (body == null)
                body = GetComponent<CombatEnemy>();
            body.Died += HandleDied;
            body.Revived += HandleRevived;
        }

        protected virtual void OnDisable()
        {
            body.Died -= HandleDied;
            body.Revived -= HandleRevived;
        }

        /// <summary>Alive, the player exists and isn't frozen (dead, or the run is over).</summary>
        protected bool CanAct => body.IsAlive && player != null && playerMotor != null && !player.Frozen;

        protected Vector3 PlayerCenter => playerMotor.BodyCenter;

        /// <summary>Nothing but its own body (a heavy's shield) between the two points.</summary>
        protected bool CanSee(Vector3 from, Vector3 to)
        {
            if (!Physics.Linecast(from, to, out RaycastHit hit, sightLayers, QueryTriggerInteraction.Ignore))
                return true;
            return hit.collider.transform.IsChildOf(transform);
        }

        /// <summary>Wakes when the player comes within range and in sight. Returns whether it is awake.</summary>
        protected bool UpdateWake(Vector3 eye)
        {
            if (awake)
                return true;
            Vector3 to = PlayerCenter - eye;
            if (to.sqrMagnitude > wakeRange * wakeRange || !CanSee(eye, PlayerCenter))
                return false;
            awake = true;
            OnWoke();
            Raise(EnemyCue.Woke, eye);
            return true;
        }

        protected void Raise(EnemyCue cue, Vector3 position)
        {
            AnyCue?.Invoke(this, cue, position);
        }

        /// <summary>Breaks the player's shield (or kills), knocking them by <paramref name="knockback"/>.</summary>
        protected bool HitPlayer(Vector3 from, Vector3 knockback, string source)
        {
            return playerHealth != null && playerHealth.TakeHit(from, knockback, source);
        }

        protected virtual void OnWoke()
        {
        }

        /// <summary>Back at its post, asleep, ready to go again.</summary>
        protected virtual void ResetBehaviour()
        {
        }

        protected virtual void OnDeath(Vector3 attackerVelocity)
        {
        }

        void HandleDied(CombatEnemy enemy, Vector3 attackerVelocity)
        {
            Raise(EnemyCue.Died, transform.position);
            OnDeath(attackerVelocity);
        }

        void HandleRevived(CombatEnemy enemy)
        {
            awake = false;
            transform.SetPositionAndRotation(home, homeRotation);
            ResetBehaviour();
        }

        /// <summary>Closest distance between segments p1-q1 and p2-q2.</summary>
        protected static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            float s;
            float t;
            if (a <= 1e-8f && e <= 1e-8f)
                return r.magnitude;
            if (a <= 1e-8f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-8f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > 1e-8f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }
            return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
        }

        /// <summary>The player's capsule core segment (feet sphere centre to head sphere centre) and radius.</summary>
        protected void PlayerCapsule(out Vector3 bottom, out Vector3 top, out float radius)
        {
            if (playerBody == null)
            {
                bottom = top = PlayerCenter;
                radius = 0.4f;
                return;
            }
            RealityContact.Capsule(playerBody, out top, out bottom, out radius);
        }
    }
}
