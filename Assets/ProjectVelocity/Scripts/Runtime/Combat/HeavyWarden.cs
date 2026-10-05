using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Enemy 3, space controller (the "Warden"): a heavy that stands in the way. Its body and tower shield physically block
    /// the route, and the shield turns away any hit from the front (the deflect knocks you back). It turns slowly to keep the
    /// shield on you, so go around it fast, over it, or come down on it from above: a hit from behind or from above kills it.
    /// Get close on the ground and it raises its fists (a glowing ring on the floor shows the reach), then slams: a shockwave
    /// rolls out along the ground. Be in the air when it passes. On death its colliders drop and the route opens.
    /// </summary>
    public sealed class HeavyWarden : SliceEnemy, IHitFilter
    {
        enum State
        {
            Idle,
            Telegraph,
            Shockwave,
            Cooldown,
        }

        [Header("Guard")]
        [Tooltip("Turn rate (degrees per second) keeping the shield on the player. Slow on purpose: outrun it.")]
        [SerializeField, Min(0f)] float turnRate = 55f;

        [Tooltip("Width of the shield's cover (degrees, centred on its facing). Hits from inside it are deflected.")]
        [SerializeField, Range(0f, 360f)] float guardArc = 120f;

        [Tooltip("A hit from at least this far (m) above its hurtbox centre comes from above and kills.")]
        [SerializeField] float aboveHeight = 1.4f;

        [Header("Slam")]
        [Tooltip("Slams when the player is this close (m) on the ground.")]
        [SerializeField, Min(0f)] float slamRange = 13f;

        [Tooltip("Readable wind-up (s).")]
        [SerializeField, Min(0.05f)] float telegraphTime = 0.9f;

        [SerializeField, Min(0f)] float cooldown = 1.5f;

        [Tooltip("Speed (m/s) the shockwave rolls out at.")]
        [SerializeField, Min(1f)] float shockwaveSpeed = 24f;

        [SerializeField, Min(0f)] float shockwaveRadius = 17f;

        [Tooltip("Height (m) above its feet the shockwave reaches: higher than that, it passes under you.")]
        [SerializeField, Min(0f)] float shockwaveHeight = 1.3f;

        [Tooltip("How thick (m) the rolling wave is.")]
        [SerializeField, Min(0.1f)] float shockwaveThickness = 1.6f;

        [Header("Parts")]
        [Tooltip("Height (m) of its feet below this object (the hurtbox centre).")]
        [SerializeField] float feetDepth = 2.3f;

        [Tooltip("Raised during the wind-up.")]
        [SerializeField] Transform arms;

        [Tooltip("Flat ring on the floor: shows the slam's reach during the wind-up, then rolls out as the shockwave.")]
        [SerializeField] Transform shockwaveRing;

        [SerializeField] Renderer[] glow;
        [SerializeField] Material glowIdle;
        [SerializeField] Material glowCharge;

        [Tooltip("Colliders that block the route while it stands.")]
        [SerializeField] Collider[] blockers;

        State state;
        float timer;
        float waveRadius;
        bool waveHit;
        Quaternion armsRest = Quaternion.identity;

        public void SetParts(Transform visualRoot, Transform armPivot, Transform ring, Renderer[] glowing, Material idle,
            Material charging, Collider[] blocking, float feet)
        {
            visual = visualRoot;
            arms = armPivot;
            shockwaveRing = ring;
            glow = glowing;
            glowIdle = idle;
            glowCharge = charging;
            blockers = blocking;
            feetDepth = feet;
            if (arms != null)
                armsRest = arms.localRotation;
        }

        protected override void Awake()
        {
            base.Awake();
            if (arms != null)
                armsRest = arms.localRotation;
            ResetBehaviour();
        }

        public HitResult Filter(in HitInfo hit)
        {
            Vector3 centre = transform.position;
            if (hit.Origin.y >= centre.y + aboveHeight)
                return HitResult.Killed; // from above

            Vector3 toAttacker = hit.Origin - centre;
            toAttacker.y = 0f;
            Vector3 facing = transform.forward;
            facing.y = 0f;
            if (toAttacker.sqrMagnitude < 1e-4f || facing.sqrMagnitude < 1e-4f)
                return HitResult.Killed;
            float angle = Vector3.Angle(facing, toAttacker);
            if (angle <= guardArc * 0.5f)
            {
                Raise(EnemyCue.Deflect, Vector3.Lerp(centre, hit.Origin, 0.5f));
                return HitResult.Deflected;
            }
            return HitResult.Killed;
        }

        protected override void ResetBehaviour()
        {
            state = State.Idle;
            timer = 0.5f;
            waveRadius = 0f;
            SetBlockers(true);
            SetGlow(false);
            if (arms != null)
                arms.localRotation = armsRest;
            if (shockwaveRing != null)
                shockwaveRing.gameObject.SetActive(false);
        }

        protected override void OnDeath(Vector3 attackerVelocity)
        {
            SetBlockers(false);
            if (shockwaveRing != null)
                shockwaveRing.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!CanAct)
            {
                if (shockwaveRing != null && shockwaveRing.gameObject.activeSelf && state != State.Shockwave)
                    shockwaveRing.gameObject.SetActive(false);
                return;
            }

            float dt = Time.deltaTime;
            Vector3 centre = transform.position;
            if (!UpdateWake(centre))
                return;

            Vector3 target = PlayerCenter;
            Vector3 flat = target - centre;
            flat.y = 0f;
            float distance = flat.magnitude;
            Vector3 feet = centre + Vector3.down * feetDepth;

            // Keep the shield on the player, slowly (not while it slams).
            if ((state == State.Idle || state == State.Cooldown) && flat.sqrMagnitude > 1e-3f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), turnRate * dt);

            timer -= dt;
            switch (state)
            {
                case State.Idle:
                case State.Cooldown:
                    if (timer <= 0f && distance < slamRange && target.y < feet.y + 6f && CanSee(centre, target))
                    {
                        state = State.Telegraph;
                        timer = telegraphTime;
                        SetGlow(true);
                        if (shockwaveRing != null)
                        {
                            shockwaveRing.gameObject.SetActive(true);
                            shockwaveRing.position = feet + Vector3.up * 0.05f;
                            shockwaveRing.localScale = new Vector3(slamRange, 1f, slamRange);
                        }
                        Raise(EnemyCue.Telegraph, centre);
                    }
                    break;
                case State.Telegraph:
                {
                    float u = 1f - Mathf.Clamp01(timer / telegraphTime);
                    if (arms != null)
                        arms.localRotation = armsRest * Quaternion.Euler(-110f * Mathf.Sin(u * Mathf.PI * 0.5f), 0f, 0f);
                    if (shockwaveRing != null)
                    {
                        float pulse = 1f + 0.04f * Mathf.Sin(u * 30f);
                        shockwaveRing.localScale = new Vector3(slamRange * pulse, 1f, slamRange * pulse);
                    }
                    if (timer <= 0f)
                    {
                        state = State.Shockwave;
                        waveRadius = 1.5f;
                        waveHit = false;
                        if (arms != null)
                            arms.localRotation = armsRest * Quaternion.Euler(30f, 0f, 0f);
                        Raise(EnemyCue.Slam, feet);
                    }
                    break;
                }
                case State.Shockwave:
                {
                    waveRadius += shockwaveSpeed * dt;
                    if (shockwaveRing != null)
                        shockwaveRing.localScale = new Vector3(waveRadius, 1f, waveRadius);
                    if (!waveHit && playerMotor != null)
                    {
                        Vector3 playerFeet = playerMotor.transform.position;
                        Vector3 offset = playerFeet - feet;
                        float height = offset.y;
                        offset.y = 0f;
                        float r = offset.magnitude;
                        if (Mathf.Abs(r - waveRadius) < shockwaveThickness && height < shockwaveHeight && height > -1.5f)
                        {
                            Vector3 outward = r > 1e-3f ? offset / r : transform.forward;
                            HitPlayer(feet, outward * 14f + Vector3.up * 9f, "shockwave");
                            waveHit = true;
                        }
                    }
                    if (waveRadius >= shockwaveRadius)
                    {
                        state = State.Cooldown;
                        timer = cooldown;
                        SetGlow(false);
                        if (arms != null)
                            arms.localRotation = armsRest;
                        if (shockwaveRing != null)
                            shockwaveRing.gameObject.SetActive(false);
                    }
                    break;
                }
            }
        }

        void SetGlow(bool charging)
        {
            Material m = charging ? glowCharge : glowIdle;
            if (glow == null || m == null)
                return;
            foreach (Renderer r in glow)
            {
                if (r != null && r.sharedMaterial != m)
                    r.sharedMaterial = m;
            }
        }

        void SetBlockers(bool on)
        {
            if (blockers == null)
                return;
            foreach (Collider c in blockers)
            {
                if (c != null)
                    c.enabled = on;
            }
        }
    }
}
