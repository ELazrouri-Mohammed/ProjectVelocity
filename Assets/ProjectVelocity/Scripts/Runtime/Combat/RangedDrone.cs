using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Enemy 1, ranged pressure (the "Lantern"): a light drone hovering at its post. It turns to track you, charges for a
    /// readable moment (its eye flares and a thin aiming line shows where the shot will go), then fires a slow, bright bolt
    /// at where you'll be if you keep doing what you're doing. Keep changing what you're doing and it misses; stand still or
    /// run in a straight line and it hits. One cut kills it, and the tether can strike it.
    /// </summary>
    public sealed class RangedDrone : SliceEnemy
    {
        enum State
        {
            Idle,
            Charging,
            Cooldown,
        }

        [Header("Attack")]
        [Tooltip("Fires at the player within this distance (m).")]
        [SerializeField, Min(0f)] float range = 55f;

        [Tooltip("Readable wind-up before each shot (s).")]
        [SerializeField, Min(0.05f)] float chargeTime = 0.8f;

        [Tooltip("Seconds between shots.")]
        [SerializeField, Min(0f)] float cooldown = 1.9f;

        [Tooltip("Bolt speed (m/s). Slow enough to read and dodge.")]
        [SerializeField, Min(1f)] float boltSpeed = 34f;

        [Tooltip("How much it leads a moving target: 0 = fires at where you are, 1 = where you'd be if you kept going exactly.")]
        [SerializeField, Range(0f, 1f)] float lead = 0.6f;

        [Tooltip("Turn rate while tracking (degrees per second).")]
        [SerializeField, Min(0f)] float turnRate = 200f;

        [Header("Hover")]
        [SerializeField, Min(0f)] float bobHeight = 0.35f;
        [SerializeField, Min(0f)] float bobRate = 0.6f;

        [Header("Parts")]
        [Tooltip("Where bolts leave from.")]
        [SerializeField] Transform muzzle;

        [Tooltip("The eye: switches to the charge material while winding up.")]
        [SerializeField] Renderer eye;
        [SerializeField] Material eyeIdle;
        [SerializeField] Material eyeCharging;

        [Tooltip("Thin line showing where the shot will go while charging. Optional.")]
        [SerializeField] LineRenderer aimLine;

        State state;
        float timer;
        float bobPhase;
        Vector3 aimPoint;

        public void SetParts(Transform visualRoot, Transform boltOrigin, Renderer eyeRenderer, Material idle, Material charging, LineRenderer line)
        {
            visual = visualRoot;
            muzzle = boltOrigin;
            eye = eyeRenderer;
            eyeIdle = idle;
            eyeCharging = charging;
            aimLine = line;
        }

        protected override void Awake()
        {
            base.Awake();
            Vector3 p = transform.position;
            bobPhase = Mathf.Repeat(p.x * 0.31f + p.z * 0.17f, 1f) * Mathf.PI * 2f;
            ResetBehaviour();
        }

        protected override void ResetBehaviour()
        {
            state = State.Idle;
            // Out of step with its neighbours, so a group never fires as one.
            timer = 0.4f + Mathf.Repeat(bobPhase, 1f) * 0.8f;
            SetEye(false);
            if (aimLine != null)
                aimLine.enabled = false;
        }

        protected override void OnDeath(Vector3 attackerVelocity)
        {
            if (aimLine != null)
                aimLine.enabled = false;
        }

        void Update()
        {
            if (!CanAct)
            {
                if (aimLine != null && aimLine.enabled)
                    aimLine.enabled = false;
                return;
            }

            float dt = Time.deltaTime;
            float bob = bobHeight * Mathf.Sin(Time.time * bobRate * 2f * Mathf.PI + bobPhase);
            transform.position = home + Vector3.up * bob;

            Vector3 eyePosition = muzzle != null ? muzzle.position : transform.position;
            if (!UpdateWake(eyePosition))
                return;

            Vector3 target = PlayerCenter;
            Vector3 toPlayer = target - eyePosition;
            float distance = toPlayer.magnitude;
            bool inRange = distance <= range && CanSee(eyePosition, target);

            // Track: the predicted point while charging, the player otherwise.
            if (state == State.Charging)
            {
                float flight = distance / boltSpeed;
                aimPoint = target + playerMotor.Velocity * (flight * lead);
            }
            Vector3 look = (state == State.Charging ? aimPoint : target) - transform.position;
            if (visual != null && look.sqrMagnitude > 1e-4f)
                visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.LookRotation(look), turnRate * dt);

            timer -= dt;
            switch (state)
            {
                case State.Idle:
                case State.Cooldown:
                    if (timer <= 0f && inRange)
                    {
                        state = State.Charging;
                        timer = chargeTime;
                        SetEye(true);
                        aimPoint = target;
                        Raise(EnemyCue.Telegraph, eyePosition);
                    }
                    break;
                case State.Charging:
                    if (aimLine != null)
                    {
                        aimLine.enabled = true;
                        aimLine.SetPosition(0, eyePosition);
                        aimLine.SetPosition(1, eyePosition + (aimPoint - eyePosition).normalized * Mathf.Min(distance, range));
                    }
                    if (!inRange)
                    {
                        state = State.Cooldown;
                        timer = cooldown * 0.5f;
                        SetEye(false);
                        if (aimLine != null)
                            aimLine.enabled = false;
                        break;
                    }
                    if (timer <= 0f)
                    {
                        Vector3 direction = aimPoint - eyePosition;
                        direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : transform.forward;
                        if (EnemyProjectiles.Instance != null)
                            EnemyProjectiles.Instance.Fire(eyePosition + direction * 0.6f, direction * boltSpeed);
                        Raise(EnemyCue.Fire, eyePosition);
                        state = State.Cooldown;
                        timer = cooldown;
                        SetEye(false);
                        if (aimLine != null)
                            aimLine.enabled = false;
                    }
                    break;
            }
        }

        void SetEye(bool charging)
        {
            if (eye == null)
                return;
            Material m = charging ? eyeCharging : eyeIdle;
            if (m != null && eye.sharedMaterial != m)
                eye.sharedMaterial = m;
        }
    }
}
