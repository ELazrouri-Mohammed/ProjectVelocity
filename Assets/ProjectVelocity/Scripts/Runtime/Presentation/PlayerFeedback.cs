using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Turns gameplay events into feedback: effects (<see cref="VfxLibrary"/>), sounds (<see cref="SliceAudio"/>) and small,
    /// restrained camera impulses. It only listens: movement, combat, the tether, enemies, the director and moving architecture
    /// raise events, and nothing in gameplay knows this exists, so presentation can be replaced without touching gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFeedback : MonoBehaviour
    {
        [SerializeField] VelocityPlayerController player;
        [SerializeField] VelocityMotor motor;
        [SerializeField] CombatController combat;
        [SerializeField] PulseLauncher pulse;
        [SerializeField] PlayerHealth health;
        [SerializeField] VelocityCamera cameraRig;

        [Tooltip("Hidden while the player is dead (the body shatters), shown again on respawn.")]
        [SerializeField] GameObject[] hideOnDeath;

        [Header("Palette")]
        [SerializeField] Color energy = new Color(0.25f, 0.9f, 1f, 1f);
        [SerializeField] Color danger = new Color(1f, 0.45f, 0.12f, 1f);
        [SerializeField] Color stoneDust = new Color(0.86f, 0.8f, 0.72f, 0.55f);
        [SerializeField] Color graphite = new Color(0.12f, 0.13f, 0.15f, 1f);
        [SerializeField] Color bone = new Color(0.92f, 0.9f, 0.84f, 1f);

        [Header("Footsteps")]
        [SerializeField, Min(0.5f)] float strideLength = 2.6f;

        [Header("World")]
        [Tooltip("Architecture moving further than this (m) from the player makes no camera impulse.")]
        [SerializeField, Min(0f)] float impulseRange = 55f;

        VfxLibrary vfx;
        SliceAudio audioOut;
        float strideDistance;
        float streakTimer;
        float rumbleCooldown;
        float impactCooldown;
        float warningCooldown;
        RealityChunk[] chunks;
        LoopingMotion[] loops;
        EnemyProjectiles bolts;
        SliceDirector director;

        public void Configure(VelocityPlayerController playerController, VelocityMotor playerMotor, CombatController playerCombat,
            PulseLauncher playerPulse, PlayerHealth playerHealth, VelocityCamera rig, GameObject[] hidden)
        {
            player = playerController;
            motor = playerMotor;
            combat = playerCombat;
            pulse = playerPulse;
            health = playerHealth;
            cameraRig = rig;
            hideOnDeath = hidden;
        }

        void Start()
        {
            vfx = VfxLibrary.Instance;
            audioOut = SliceAudio.Instance;
            if (motor != null)
            {
                motor.Jumped += OnJumped;
                motor.Landed += OnLanded;
                motor.BoostStarted += OnBoost;
                motor.WallRunStarted += OnWallRun;
                motor.WallJumped += OnWallJump;
                motor.TargetLaunched += OnTargetLaunched;
                motor.TetherAttached += OnTetherAttached;
                motor.TetherReleased += OnTetherReleased;
            }
            if (combat != null)
            {
                combat.Swung += OnSwung;
                combat.Killed += OnKilled;
                combat.Deflected += OnDeflected;
                combat.PulseFired += OnPulseFired;
            }
            if (pulse != null)
                pulse.Impact += OnPulseImpact;
            if (health != null)
            {
                health.ShieldBroken += OnShieldBroken;
                health.ShieldRestored += OnShieldRestored;
                health.Died += OnDied;
            }
            if (player != null)
                player.Respawned += OnRespawned;

            SliceEnemy.AnyCue += OnEnemyCue;
            bolts = EnemyProjectiles.Instance;
            if (bolts != null)
            {
                bolts.Impact += OnBoltImpact;
                bolts.Parried += OnParried;
            }
            director = SliceDirector.Current;
            if (director != null)
            {
                director.CheckpointReached += OnCheckpoint;
                director.Finished += OnFinished;
            }

            chunks = FindObjectsByType<RealityChunk>(FindObjectsSortMode.None);
            foreach (RealityChunk chunk in chunks)
            {
                chunk.WarningStarted += OnChunkWarning;
                chunk.MoveStarted += OnChunkMoveStarted;
                chunk.MoveFinished += OnChunkMoveFinished;
            }
            loops = FindObjectsByType<LoopingMotion>(FindObjectsSortMode.None);
            foreach (LoopingMotion loop in loops)
            {
                loop.Warned += OnLoopWarned;
                loop.Landed += OnLoopLanded;
            }
        }

        void OnDestroy()
        {
            SliceEnemy.AnyCue -= OnEnemyCue;
            if (motor != null)
            {
                motor.Jumped -= OnJumped;
                motor.Landed -= OnLanded;
                motor.BoostStarted -= OnBoost;
                motor.WallRunStarted -= OnWallRun;
                motor.WallJumped -= OnWallJump;
                motor.TargetLaunched -= OnTargetLaunched;
                motor.TetherAttached -= OnTetherAttached;
                motor.TetherReleased -= OnTetherReleased;
            }
            if (combat != null)
            {
                combat.Swung -= OnSwung;
                combat.Killed -= OnKilled;
                combat.Deflected -= OnDeflected;
                combat.PulseFired -= OnPulseFired;
            }
            if (pulse != null)
                pulse.Impact -= OnPulseImpact;
            if (health != null)
            {
                health.ShieldBroken -= OnShieldBroken;
                health.ShieldRestored -= OnShieldRestored;
                health.Died -= OnDied;
            }
            if (player != null)
                player.Respawned -= OnRespawned;
            if (bolts != null)
            {
                bolts.Impact -= OnBoltImpact;
                bolts.Parried -= OnParried;
            }
            if (director != null)
            {
                director.CheckpointReached -= OnCheckpoint;
                director.Finished -= OnFinished;
            }
            if (chunks != null)
            {
                foreach (RealityChunk chunk in chunks)
                {
                    if (chunk == null)
                        continue;
                    chunk.WarningStarted -= OnChunkWarning;
                    chunk.MoveStarted -= OnChunkMoveStarted;
                    chunk.MoveFinished -= OnChunkMoveFinished;
                }
            }
            if (loops != null)
            {
                foreach (LoopingMotion loop in loops)
                {
                    if (loop == null)
                        continue;
                    loop.Warned -= OnLoopWarned;
                    loop.Landed -= OnLoopLanded;
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            rumbleCooldown -= Time.unscaledDeltaTime;
            impactCooldown -= Time.unscaledDeltaTime;
            warningCooldown -= Time.unscaledDeltaTime;
            if (motor == null || player == null || player.Frozen)
                return;

            // Footsteps on the ground and on walls.
            if (motor.IsGrounded || motor.IsWallRunning)
            {
                float speed = motor.IsWallRunning ? motor.Velocity.magnitude : motor.Speed;
                strideDistance += speed * dt;
                float stride = strideLength * Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(10f, 40f, speed));
                if (speed > 3f && strideDistance >= stride)
                {
                    strideDistance = 0f;
                    Play(SoundId.Footstep, Mathf.Lerp(0.35f, 0.7f, speed / 40f));
                }
            }

            // Speed streaks at very high speed: a few, flowing past.
            float velocity = motor.Velocity.magnitude;
            streakTimer -= dt;
            if (vfx != null && velocity > 36f && streakTimer <= 0f)
            {
                streakTimer = 0.05f;
                Vector3 v = motor.Velocity;
                vfx.Streaks(motor.BodyCenter + v.normalized * 6f, v, motor.IsBoosting || motor.IsTethering ? 3 : 1, 3.5f, new Color(1f, 1f, 1f, 0.35f));
            }
        }

        // ------------------------------------------------------------------ Movement

        void OnJumped()
        {
            Play(SoundId.Jump, 0.7f);
            if (vfx != null && motor.IsGrounded)
                vfx.Dust(motor.transform.position, 5, 0.6f, 3f, stoneDust, 0.7f);
        }

        void OnLanded(float impact)
        {
            if (impact < 6f)
                return;
            bool hard = impact > 26f;
            Play(hard ? SoundId.LandHard : SoundId.Land, Mathf.Clamp01(impact / 30f) * 0.9f + 0.2f);
            if (vfx != null)
            {
                Vector3 feet = motor.transform.position;
                vfx.Dust(feet, hard ? 14 : 7, hard ? 1.8f : 1f, hard ? 7f : 4f, stoneDust, hard ? 1.4f : 0.9f);
                if (hard)
                    vfx.Ring(feet + Vector3.up * 0.05f, 4f, new Color(stoneDust.r, stoneDust.g, stoneDust.b, 0.5f));
            }
            if (hard && cameraRig != null)
                cameraRig.AddImpulse(Vector3.down * Mathf.Clamp(impact / 120f, 0.05f, 0.3f));
        }

        void OnBoost()
        {
            Play(SoundId.Boost, 0.9f);
            if (vfx == null)
                return;
            Vector3 v = motor.Velocity;
            Vector3 back = v.sqrMagnitude > 1f ? -v.normalized : -motor.transform.forward;
            vfx.Sparks(motor.BodyCenter, back, 0.35f, 10, 14f, energy);
            vfx.Flash(motor.BodyCenter, 2.2f, new Color(energy.r, energy.g, energy.b, 0.7f));
            vfx.Streaks(motor.BodyCenter + v.normalized * 4f, v, 6, 2.5f, new Color(1f, 1f, 1f, 0.45f));
        }

        void OnWallRun()
        {
            Play(SoundId.WallRun, 0.6f);
            if (vfx != null)
            {
                Vector3 contact = motor.BodyCenter - motor.WallNormal * 0.45f;
                vfx.Sparks(contact, motor.WallNormal + Vector3.up * 0.3f, 0.4f, 6, 6f, bone);
            }
        }

        void OnWallJump()
        {
            Play(SoundId.WallJump, 0.8f);
            if (vfx != null)
                vfx.Dust(motor.BodyCenter, 6, 0.4f, 3f, stoneDust, 0.6f);
        }

        void OnTargetLaunched(TraversalTarget target)
        {
            Play(SoundId.TargetLaunch, 0.9f);
            if (vfx != null && target != null)
            {
                vfx.Flash(target.Position, 4f, energy);
                vfx.Sparks(target.Position, motor.Velocity, 0.5f, 12, 18f, energy);
            }
        }

        void OnTetherAttached(TetherAnchor anchor)
        {
            Play(SoundId.TetherFire, 0.8f);
            if (anchor == null)
                return;
            PlayAt(SoundId.TetherConnect, anchor.Position, 1f);
            if (vfx != null)
            {
                vfx.Flash(anchor.Position, 3.5f, energy);
                vfx.Sparks(anchor.Position, motor.BodyCenter - anchor.Position, 0.6f, 10, 10f, energy);
            }
        }

        void OnTetherReleased(TetherAnchor anchor, VelocityMotor.TetherExit exit)
        {
            if (exit == VelocityMotor.TetherExit.Lost || exit == VelocityMotor.TetherExit.Landed)
                return;
            Play(SoundId.TetherRelease, 0.8f);
            if (vfx != null)
                vfx.Streaks(motor.BodyCenter + motor.Velocity.normalized * 3f, motor.Velocity, 5, 2f, new Color(energy.r, energy.g, energy.b, 0.5f));
        }

        // ------------------------------------------------------------------ Combat

        void OnSwung(CombatController.AttackKind kind, Vector3 direction)
        {
            float pitch = kind == CombatController.AttackKind.Dash ? 1.2f : kind == CombatController.AttackKind.Aerial ? 0.9f : 1f;
            Play(SoundId.Slash, 0.8f, pitch);
        }

        void OnKilled(CombatEnemy enemy)
        {
            if (enemy == null)
                return;
            Vector3 at = enemy.HurtboxCenter;
            bool heavy = !enemy.IsLight;
            PlayAt(heavy ? SoundId.HeavyKill : SoundId.Kill, at, 1f);
            Play(SoundId.SlashHit, 0.9f);
            if (vfx != null)
            {
                vfx.Flash(at, heavy ? 7f : 3.5f, Color.white);
                vfx.Sparks(at, motor.Velocity, 0.7f, heavy ? 24 : 14, heavy ? 22f : 16f, danger);
                vfx.Shards(at, motor.Velocity * 0.3f, heavy ? 16 : 9, heavy ? 12f : 9f, bone, heavy ? 1.6f : 1f);
                vfx.Shards(at, motor.Velocity * 0.3f, heavy ? 10 : 5, 10f, graphite, heavy ? 1.4f : 0.9f);
                if (heavy && vfx.Debris != null)
                {
                    for (int i = 0; i < 7; i++)
                        vfx.Debris.Spawn(at + Random.insideUnitSphere * 1.5f, Random.insideUnitSphere * 9f + Vector3.up * 6f, Random.Range(0.5f, 1.1f));
                }
            }
            if (cameraRig != null)
                cameraRig.AddImpulse((at - motor.BodyCenter).normalized * (heavy ? 0.25f : 0.08f));
        }

        void OnDeflected(CombatEnemy enemy, Vector3 point)
        {
            PlayAt(SoundId.Deflect, point, 1f);
            if (vfx != null)
            {
                vfx.Sparks(point, motor.BodyCenter - point, 0.6f, 20, 18f, danger);
                vfx.Flash(point, 3f, new Color(1f, 0.8f, 0.5f, 1f));
            }
            if (cameraRig != null)
                cameraRig.AddImpulse((motor.BodyCenter - point).normalized * 0.15f);
        }

        void OnPulseFired(Vector3 origin)
        {
            Play(SoundId.PulseFire, 0.8f);
            if (vfx != null)
                vfx.Flash(origin, 1.6f, energy);
        }

        void OnPulseImpact(Vector3 position, bool hitEnemy)
        {
            PlayAt(SoundId.PulseHit, position, hitEnemy ? 1f : 0.6f);
            if (vfx != null)
                vfx.Sparks(position, Vector3.up, 1f, hitEnemy ? 14 : 6, 10f, energy);
        }

        // ------------------------------------------------------------------ Health

        void OnShieldBroken(Vector3 source)
        {
            Play(SoundId.ShieldBreak, 1f);
            if (vfx != null)
                vfx.Sparks(motor.BodyCenter, motor.BodyCenter - source, 0.8f, 18, 12f, new Color(1f, 0.25f, 0.2f, 1f));
            if (cameraRig != null)
                cameraRig.AddImpulse((motor.BodyCenter - source).normalized * 0.2f);
        }

        void OnShieldRestored()
        {
            Play(SoundId.ShieldRestore, 0.5f);
        }

        void OnDied(string reason)
        {
            Play(SoundId.Death, 1f);
            if (vfx != null)
            {
                Vector3 at = motor.BodyCenter;
                vfx.Flash(at, 5f, energy);
                vfx.Shards(at, motor.Velocity * 0.4f, 14, 10f, graphite, 1.2f);
                vfx.Shards(at, motor.Velocity * 0.4f, 8, 12f, energy, 0.8f);
            }
            SetBodyVisible(false);
        }

        void OnRespawned()
        {
            SetBodyVisible(true);
            if (vfx != null)
            {
                vfx.ClearAll();
                vfx.Flash(motor.BodyCenter, 3f, energy);
                vfx.Ring(motor.transform.position + Vector3.up * 0.05f, 3f, energy);
            }
        }

        void SetBodyVisible(bool visible)
        {
            if (hideOnDeath == null)
                return;
            foreach (GameObject go in hideOnDeath)
            {
                if (go != null && go.activeSelf != visible)
                    go.SetActive(visible);
            }
        }

        // ------------------------------------------------------------------ Enemies

        void OnEnemyCue(SliceEnemy enemy, EnemyCue cue, Vector3 position)
        {
            switch (cue)
            {
                case EnemyCue.Woke:
                    if (enemy is PursuerDrone)
                        PlayAt(SoundId.HoundScreech, position, 0.8f);
                    break;
                case EnemyCue.Telegraph:
                    if (enemy is RangedDrone)
                        PlayAt(SoundId.EnemyCharge, position, 0.9f);
                    else if (enemy is PursuerDrone)
                        PlayAt(SoundId.HoundScreech, position, 1f, 1.15f);
                    else
                        PlayAt(SoundId.WardenCharge, position, 1f);
                    break;
                case EnemyCue.Fire:
                    PlayAt(SoundId.EnemyFire, position, 0.9f);
                    if (vfx != null)
                        vfx.Flash(position, 1.8f, danger);
                    break;
                case EnemyCue.Dash:
                    PlayAt(SoundId.HoundDash, position, 1f);
                    break;
                case EnemyCue.Slam:
                    PlayAt(SoundId.WardenSlam, position, 1f);
                    if (vfx != null)
                    {
                        vfx.Dust(position, 18, 3f, 10f, stoneDust, 1.8f);
                        vfx.Ring(position + Vector3.up * 0.1f, 6f, danger);
                    }
                    ImpulseFrom(position, 0.3f);
                    break;
                case EnemyCue.Died:
                    PlayAt(SoundId.EnemyDeath, position, 1f);
                    break;
            }
        }

        void OnBoltImpact(Vector3 position, bool hitPlayer)
        {
            PlayAt(SoundId.BoltImpact, position, hitPlayer ? 1f : 0.6f);
            if (vfx != null)
                vfx.Sparks(position, Vector3.up, 1f, 8, 8f, danger);
        }

        void OnParried(Vector3 position)
        {
            PlayAt(SoundId.Parry, position, 1f);
            if (vfx != null)
            {
                vfx.Sparks(position, position - motor.BodyCenter, 0.5f, 14, 14f, danger);
                vfx.Flash(position, 2f, energy);
            }
        }

        // ------------------------------------------------------------------ Director

        void OnCheckpoint(SliceCheckpoint checkpoint)
        {
            Play(SoundId.Checkpoint, 0.9f);
            if (vfx != null && checkpoint != null)
            {
                Vector3 at = checkpoint.transform.position;
                vfx.Flash(at, 8f, energy);
                vfx.Ring(motor.transform.position + Vector3.up * 0.05f, 5f, energy);
            }
        }

        void OnFinished()
        {
            Play(SoundId.Finish, 1f);
            if (vfx != null)
                vfx.Flash(motor.BodyCenter, 12f, Color.white);
        }

        // ------------------------------------------------------------------ Architecture

        void OnChunkWarning(RealityChunk chunk)
        {
            if (warningCooldown > 0f || !Near(chunk.BoundsCenter, 120f))
                return;
            warningCooldown = 0.25f;
            PlayAt(SoundId.WorldWarning, chunk.BoundsCenter, chunk.IsLethal ? 1f : 0.55f);
        }

        void OnChunkMoveStarted(RealityChunk chunk)
        {
            Bounds b = chunk.WorldBounds;
            float size = b.size.magnitude;
            if (!Near(b.center, 160f))
                return;
            if (rumbleCooldown <= 0f)
            {
                rumbleCooldown = 0.12f;
                PlayAt(SoundId.WorldRumble, b.center, Mathf.Clamp(size / 40f, 0.4f, 1f), Mathf.Lerp(1.15f, 0.75f, Mathf.InverseLerp(10f, 80f, size)));
            }
            if (vfx == null)
                return;
            vfx.DustCloud(b, Mathf.Clamp((int)(size / 5f), 4, 16), 2.5f, stoneDust, Mathf.Clamp(size / 12f, 1.2f, 4f));
            if (vfx.Debris != null && size > 15f)
            {
                int count = Mathf.Clamp((int)(size / 12f), 2, 6);
                for (int i = 0; i < count; i++)
                {
                    Vector3 at = new Vector3(Random.Range(b.min.x, b.max.x), b.max.y, Random.Range(b.min.z, b.max.z));
                    vfx.Debris.Spawn(at, Random.insideUnitSphere * 4f + Vector3.up * 2f, Random.Range(0.4f, 1.3f));
                }
            }
        }

        void OnChunkMoveFinished(RealityChunk chunk)
        {
            Bounds b = chunk.WorldBounds;
            float size = b.size.magnitude;
            if (!Near(b.center, 160f))
                return;
            if (impactCooldown <= 0f)
            {
                impactCooldown = 0.1f;
                PlayAt(SoundId.WorldImpact, b.center, Mathf.Clamp(size / 50f, 0.35f, 1f), Mathf.Lerp(1.1f, 0.7f, Mathf.InverseLerp(10f, 80f, size)));
            }
            if (vfx != null)
                vfx.DustCloud(b, Mathf.Clamp((int)(size / 6f), 3, 12), 4f, stoneDust, Mathf.Clamp(size / 14f, 1f, 3.5f));
            ImpulseFrom(b.center, Mathf.Clamp(size / 200f, 0.03f, 0.25f));
        }

        void OnLoopWarned(LoopingMotion loop)
        {
            if (warningCooldown > 0f || !Near(loop.transform.position, 70f))
                return;
            warningCooldown = 0.2f;
            PlayAt(SoundId.WorldWarning, loop.transform.position, 0.6f, 1.2f);
        }

        void OnLoopLanded(LoopingMotion loop)
        {
            Vector3 at = loop.transform.position;
            if (!Near(at, 80f))
                return;
            PlayAt(SoundId.Crusher, at, 0.9f);
            if (vfx != null)
                vfx.Dust(at, 8, 2f, 5f, stoneDust, 1.4f);
            ImpulseFrom(at, 0.12f);
        }

        // ------------------------------------------------------------------ Helpers

        bool Near(Vector3 position, float range)
        {
            return motor != null && (position - motor.BodyCenter).sqrMagnitude < range * range;
        }

        void ImpulseFrom(Vector3 position, float strength)
        {
            if (cameraRig == null || motor == null)
                return;
            float distance = Vector3.Distance(position, motor.BodyCenter);
            if (distance > impulseRange)
                return;
            float falloff = 1f - distance / impulseRange;
            cameraRig.AddImpulse(Random.onUnitSphere * (strength * falloff));
        }

        void Play(SoundId id, float volume, float pitch = 1f)
        {
            if (audioOut != null)
                audioOut.Play(id, volume, pitch);
        }

        void PlayAt(SoundId id, Vector3 position, float volume, float pitch = 1f)
        {
            if (audioOut != null)
                audioOut.PlayAt(id, position, volume, pitch);
        }
    }
}
