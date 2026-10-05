using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Enemy 2, fast pursuer (the "Hound"): a blade-winged hunter that wakes when you pass and chases you through the air,
    /// faster than a plain run. Within striking distance it telegraphs (its blades flare and a red line shows the path), then
    /// dashes straight through where you're heading. Keep boosting and it can't keep up; stop and it cuts you. After a dash it
    /// drifts for a moment, open to a counter-cut. It gives up once you're far enough ahead. One cut kills it.
    /// Flies by hand (no physics), rising over anything in its way.
    /// </summary>
    public sealed class PursuerDrone : SliceEnemy
    {
        enum State
        {
            Dormant,
            Chase,
            Telegraph,
            Dash,
            Recover,
        }

        [Header("Chase")]
        [Tooltip("Top chase speed (m/s). Faster than a plain run (22), slower than a boost.")]
        [SerializeField, Min(0f)] float chaseSpeed = 27f;

        [SerializeField, Min(0f)] float acceleration = 45f;

        [Tooltip("Height (m) above the player's centre it flies at while chasing.")]
        [SerializeField] float chaseHeight = 1.5f;

        [Tooltip("Gives up (hovers in place, asleep) once the player is this far away (m).")]
        [SerializeField, Min(0f)] float giveUpDistance = 85f;

        [Header("Strike")]
        [Tooltip("Starts its strike within this distance (m).")]
        [SerializeField, Min(0f)] float strikeDistance = 11f;

        [Tooltip("Readable wind-up (s).")]
        [SerializeField, Min(0.05f)] float telegraphTime = 0.45f;

        [SerializeField, Min(0f)] float dashSpeed = 46f;
        [SerializeField, Min(0.05f)] float dashTime = 0.34f;

        [Tooltip("Distance (m) from the dash path within which it cuts the player.")]
        [SerializeField, Min(0f)] float hitRadius = 1.2f;

        [Tooltip("Seconds it drifts after a dash before chasing again.")]
        [SerializeField, Min(0f)] float recoverTime = 0.8f;

        [Tooltip("How far ahead of the player (s of their motion) the dash aims.")]
        [SerializeField, Range(0f, 1f)] float dashLead = 0.25f;

        [Header("Parts")]
        [Tooltip("Blades that spread while it winds up a strike.")]
        [SerializeField] Transform[] blades;
        [SerializeField] Renderer[] glow;
        [SerializeField] Material glowIdle;
        [SerializeField] Material glowStrike;
        [SerializeField] LineRenderer aimLine;

        State state;
        float timer;
        Vector3 velocity;
        Vector3 dashDirection;
        bool dashHit;
        float orbit;

        public void SetParts(Transform visualRoot, Transform[] bladeParts, Renderer[] glowing, Material idle, Material striking, LineRenderer line)
        {
            visual = visualRoot;
            blades = bladeParts;
            glow = glowing;
            glowIdle = idle;
            glowStrike = striking;
            aimLine = line;
        }

        protected override void Awake()
        {
            base.Awake();
            Vector3 p = transform.position;
            orbit = Mathf.Repeat(p.x * 0.13f + p.z * 0.29f, 1f) > 0.5f ? 1f : -1f;
            ResetBehaviour();
        }

        protected override void ResetBehaviour()
        {
            state = State.Dormant;
            velocity = Vector3.zero;
            timer = 0f;
            SetGlow(false);
            SetBlades(0f);
            if (aimLine != null)
                aimLine.enabled = false;
        }

        protected override void OnWoke()
        {
            state = State.Chase;
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
            Vector3 position = transform.position;
            if (state == State.Dormant)
            {
                // Perched: a slow idle bob until it sees you.
                if (UpdateWake(position))
                    state = State.Chase;
                else
                    return;
            }

            Vector3 target = PlayerCenter;
            Vector3 toPlayer = target - position;
            float distance = toPlayer.magnitude;
            if (distance > giveUpDistance && state == State.Chase)
            {
                awake = false;
                state = State.Dormant;
                velocity = Vector3.zero;
                return;
            }

            timer -= dt;
            switch (state)
            {
                case State.Chase:
                {
                    // Come in from slightly above and to one side, so it reads against the sky and doesn't hide behind you.
                    Vector3 side = Vector3.Cross(Vector3.up, toPlayer.sqrMagnitude > 1e-4f ? toPlayer.normalized : Vector3.forward);
                    Vector3 goal = target + Vector3.up * chaseHeight + side * (orbit * 3f);
                    Vector3 wanted = goal - position;
                    wanted = wanted.sqrMagnitude > 1e-4f ? wanted.normalized * chaseSpeed : Vector3.zero;
                    velocity = Vector3.MoveTowards(velocity, wanted, acceleration * dt);
                    if (distance < strikeDistance && CanSee(position, target))
                    {
                        state = State.Telegraph;
                        timer = telegraphTime;
                        SetGlow(true);
                        Raise(EnemyCue.Telegraph, position);
                    }
                    break;
                }
                case State.Telegraph:
                {
                    // Brakes hard, aims where you're going.
                    velocity = Vector3.MoveTowards(velocity, Vector3.zero, acceleration * 2f * dt);
                    Vector3 aim = target + playerMotor.Velocity * dashLead - position;
                    dashDirection = aim.sqrMagnitude > 1e-4f ? aim.normalized : transform.forward;
                    SetBlades(1f - Mathf.Clamp01(timer / telegraphTime));
                    if (aimLine != null)
                    {
                        aimLine.enabled = true;
                        aimLine.SetPosition(0, position);
                        aimLine.SetPosition(1, position + dashDirection * (dashSpeed * dashTime));
                    }
                    if (timer <= 0f)
                    {
                        state = State.Dash;
                        timer = dashTime;
                        dashHit = false;
                        velocity = dashDirection * dashSpeed;
                        if (aimLine != null)
                            aimLine.enabled = false;
                        Raise(EnemyCue.Dash, position);
                    }
                    break;
                }
                case State.Dash:
                {
                    if (!dashHit)
                    {
                        PlayerCapsule(out Vector3 bottom, out Vector3 top, out float radius);
                        Vector3 next = position + velocity * dt;
                        if (SegmentDistance(position, next, bottom, top) < hitRadius + radius)
                        {
                            HitPlayer(position, dashDirection * 12f + Vector3.up * 5f, "cut by a hound");
                            dashHit = true;
                        }
                    }
                    if (timer <= 0f)
                    {
                        state = State.Recover;
                        timer = recoverTime;
                        SetGlow(false);
                    }
                    break;
                }
                case State.Recover:
                    velocity = Vector3.MoveTowards(velocity, Vector3.zero, acceleration * dt);
                    SetBlades(Mathf.Clamp01(timer / recoverTime));
                    if (timer <= 0f)
                        state = State.Chase;
                    break;
            }

            // Fly, rising over whatever is in the way instead of passing through it.
            Vector3 step = velocity * dt;
            if (step.sqrMagnitude > 1e-6f &&
                Physics.SphereCast(position, 0.6f, step, out RaycastHit hit, step.magnitude + 0.3f, sightLayers, QueryTriggerInteraction.Ignore))
            {
                step = Vector3.ProjectOnPlane(step, hit.normal) + Vector3.up * (chaseSpeed * 0.5f * dt);
                if (state == State.Dash)
                {
                    state = State.Recover;
                    timer = recoverTime;
                    SetGlow(false);
                    velocity = Vector3.zero;
                }
            }
            transform.position = position + step;

            Vector3 facing = state == State.Telegraph ? dashDirection : velocity;
            if (visual != null && facing.sqrMagnitude > 0.25f)
                visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.LookRotation(facing), 540f * dt);
        }

        void SetGlow(bool striking)
        {
            Material m = striking ? glowStrike : glowIdle;
            if (glow == null || m == null)
                return;
            foreach (Renderer r in glow)
            {
                if (r != null && r.sharedMaterial != m)
                    r.sharedMaterial = m;
            }
        }

        void SetBlades(float spread)
        {
            if (blades == null)
                return;
            for (int i = 0; i < blades.Length; i++)
            {
                if (blades[i] == null)
                    continue;
                float side = i % 2 == 0 ? 1f : -1f;
                blades[i].localRotation = Quaternion.Euler(0f, side * (12f + 38f * spread), side * -20f * spread);
            }
        }
    }
}
