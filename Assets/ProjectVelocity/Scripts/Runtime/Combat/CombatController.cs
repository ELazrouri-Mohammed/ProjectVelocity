using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// The blade, built to keep you moving. Attack swings at once, or, when <see cref="CombatTargeting"/> has an enemy selected
    /// beyond the blade's reach, asks the motor to lunge at it (your velocity is turned toward it and never slowed) and swings
    /// on contact. What the swing is depends on how you move: on the ground a quick slash, in the air a wide vertical arc
    /// (an aerial kill pops you up to keep chaining), boosting or at speed a dash strike that cuts straight through.
    /// With nothing in blade or lunge range, Attack fires the pulse at a selected enemy or switch further away.
    /// One hit kills unless armour turns it away (a deflect knocks you back). A kill keeps your exit momentum, gives back one
    /// air boost, readies the boost and the pulse and adds a little speed; a miss is a quick slash that leaves movement alone.
    /// The player controller runs it each frame: <see cref="Tick"/> before the motor moves, <see cref="AfterMove"/> after.
    /// Feel values live in Combat Tuning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatController : MonoBehaviour
    {
        public enum AttackPhase
        {
            Ready,
            Lunging,
            Slashing,
            Cooldown,
        }

        public enum AttackKind
        {
            Slash,
            Aerial,
            Dash,
            /// <summary>A tether strike arriving at its enemy.</summary>
            Strike,
        }

        // Below this horizontal speed (m/s) an untargeted swing goes where the camera looks rather than where you're moving.
        const float MovingSpeed = 2f;
        // Upward part of the knock from a deflected hit (m/s).
        const float DeflectLift = 6f;

        [Tooltip("Combat tuning asset. Changes made to it in Play Mode are kept when you stop playing.")]
        [SerializeField] CombatTuning tuning;

        [SerializeField] VelocityMotor motor;

        [Tooltip("Picks the enemy Attack lunges at, and the pulse target.")]
        [SerializeField] CombatTargeting targeting;

        [Tooltip("Placeholder blade and slash effect. Optional.")]
        [SerializeField] BladeVisual blade;

        [Tooltip("Camera for the tiny kill feedback (Kill Fov Kick in Combat Tuning). Optional.")]
        [SerializeField] VelocityCamera cameraRig;

        [Tooltip("Fires the ranged pulse. Optional: without it, Attack is blade only.")]
        [SerializeField] PulseLauncher pulse;

        CombatTuning fallbackTuning;
        float cooldownTimer;
        float bufferTimer;
        float activeTimer;
        float pulseCooldownTimer;
        bool attacking;
        CombatEnemy lungeTarget;
        Vector3 attackDirection = Vector3.forward;
        AttackKind kind;
        float swingReach;
        float swingAngle;
        // Body centre before this frame's move: hits are checked along the whole path, so speed never skips an enemy.
        Vector3 frameStart;
        int killsThisAttack;

        int killCount;
        float lastKillTime = -1f;
        bool lastKillRefreshedAirBoost;

        /// <summary>Raised on every kill the player makes (blade, strike or pulse).</summary>
        public event Action<CombatEnemy> Killed;
        /// <summary>Raised when the blade swings, with the kind of attack and its direction.</summary>
        public event Action<AttackKind, Vector3> Swung;
        /// <summary>Raised when armour turns the blade away, with the contact point.</summary>
        public event Action<CombatEnemy, Vector3> Deflected;
        /// <summary>Raised when the pulse fires, from where.</summary>
        public event Action<Vector3> PulseFired;

        public CombatTuning Tuning
        {
            get => tuning;
            set => tuning = value;
        }

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        public CombatTargeting Targeting
        {
            get => targeting;
            set => targeting = value;
        }

        public BladeVisual Blade
        {
            get => blade;
            set => blade = value;
        }

        public VelocityCamera CameraRig
        {
            get => cameraRig;
            set => cameraRig = value;
        }

        public PulseLauncher Pulse
        {
            get => pulse;
            set => pulse = value;
        }

        public AttackPhase Phase =>
            lungeTarget != null ? AttackPhase.Lunging
            : activeTimer > 0f ? AttackPhase.Slashing
            : cooldownTimer > 0f ? AttackPhase.Cooldown
            : AttackPhase.Ready;

        /// <summary>The kind of the current (or last) swing.</summary>
        public AttackKind CurrentKind => kind;

        public float CooldownRemaining => cooldownTimer;
        /// <summary>Whether the pulse can fire right now.</summary>
        public bool PulseReady => pulse != null && pulseCooldownTimer <= 0f;
        /// <summary>0 just fired, 1 ready.</summary>
        public float PulseCharge01 => pulse == null ? 0f : 1f - Mathf.Clamp01(pulseCooldownTimer / Mathf.Max(0.01f, Settings.pulseCooldown));
        /// <summary>Kills since the scene started. Debug readout.</summary>
        public int KillCount => killCount;
        /// <summary>Time.time of the last kill, or negative before the first. Debug readout.</summary>
        public float LastKillTime => lastKillTime;
        /// <summary>Whether the last kill gave back a spent air boost. Debug readout.</summary>
        public bool LastKillRefreshedAirBoost => lastKillRefreshedAirBoost;

        /// <summary>The tuning in use: the assigned asset, or built-in defaults when none is assigned.</summary>
        public CombatTuning Settings
        {
            get
            {
                if (tuning != null)
                    return tuning;
                if (fallbackTuning == null)
                    fallbackTuning = ScriptableObject.CreateInstance<CombatTuning>();
                return fallbackTuning;
            }
        }

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<VelocityMotor>();
            if (targeting == null)
                targeting = GetComponent<CombatTargeting>();
            if (blade == null)
                blade = GetComponent<BladeVisual>();
            if (pulse == null)
                pulse = GetComponent<PulseLauncher>();
            if (cameraRig == null && Camera.main != null)
                cameraRig = Camera.main.GetComponent<VelocityCamera>();
        }

        void OnDisable()
        {
            StopAttack();
        }

        void OnDestroy()
        {
            if (fallbackTuning != null)
                Destroy(fallbackTuning);
        }

        /// <summary>
        /// Called once per frame by the player controller, before the motor moves. Updates the selection and starts an attack
        /// when asked. Returns the lunge for the motor to start this frame, or none.
        /// </summary>
        public MotorLunge Tick(bool attackPressed, Vector3 viewForward, float deltaTime)
        {
            if (!isActiveAndEnabled || motor == null)
                return default;

            CombatTuning t = Settings;
            cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);
            activeTimer = Mathf.Max(0f, activeTimer - deltaTime);
            pulseCooldownTimer = Mathf.Max(0f, pulseCooldownTimer - deltaTime);
            bufferTimer = attackPressed ? t.attackBufferTime : Mathf.Max(0f, bufferTimer - deltaTime);
            frameStart = motor.BodyCenter;

            if (targeting != null)
                targeting.Tick(t, lungeTarget, pulse != null);

            if (!attackPressed && bufferTimer <= 0f)
                return default;
            if (cooldownTimer > 0f || lungeTarget != null)
                return default;

            bufferTimer = 0f;
            return StartAttack(t, viewForward);
        }

        /// <summary>Called once per frame by the player controller, right after the motor moves: lands the blade.</summary>
        public void AfterMove()
        {
            if (!attacking || motor == null)
                return;

            CombatTuning t = Settings;
            Vector3 from = frameStart;
            Vector3 to = motor.BodyCenter;

            if (lungeTarget != null)
            {
                if (lungeTarget.IsAlive && InReach(lungeTarget, from, to, t.hitReach))
                {
                    // Contact: the lunge ends right here with all its speed, and the blade swings through the enemy.
                    attackDirection = DirectionOr(lungeTarget.HurtboxCenter - from, attackDirection);
                    motor.EndLunge();
                    lungeTarget = null;
                    Swing(t);
                }
                else if (!lungeTarget.IsAlive || !motor.IsLunging)
                {
                    // The lunge ran out (or a boost, a target or a wall cut it short) before reaching it: swing anyway.
                    motor.EndLunge();
                    lungeTarget = null;
                    Swing(t);
                }
            }

            if (activeTimer > 0f)
                CutInReach(t, from, to);

            if (lungeTarget == null && activeTimer <= 0f)
                attacking = false;
        }

        /// <summary>Clears the attack and brings every enemy back that comes back with the player (called on respawn).</summary>
        public void ResetCombat()
        {
            StopAttack();
            cooldownTimer = 0f;
            bufferTimer = 0f;
            pulseCooldownTimer = 0f;
            if (blade != null)
                blade.ResetPose();
            if (pulse != null)
                pulse.Clear();
            CombatEnemy.ReviveAll();
        }

        /// <summary>A tether strike arrives at <paramref name="enemy"/>: the blade cuts it down in passing (a movement kill).</summary>
        public void StrikeKill(CombatEnemy enemy)
        {
            if (enemy == null || !enemy.IsAlive || motor == null)
                return;
            CombatTuning t = Settings;
            attackDirection = DirectionOr(motor.Velocity, DirectionOr(enemy.HurtboxCenter - motor.BodyCenter, transform.forward));
            kind = AttackKind.Strike;
            if (blade != null)
                blade.PlaySlash(attackDirection, kind);
            Swung?.Invoke(kind, attackDirection);
            killsThisAttack = 0;
            ApplyHit(t, enemy, HitKind.Strike, attackDirection);
            cooldownTimer = Mathf.Min(cooldownTimer, t.killCooldown);
        }

        /// <summary>The pulse reached <paramref name="enemy"/> travelling along <paramref name="direction"/>.</summary>
        public void PulseLanded(CombatEnemy enemy, Vector3 direction)
        {
            if (enemy == null || !enemy.IsAlive)
                return;
            killsThisAttack = 0;
            ApplyHit(Settings, enemy, HitKind.Pulse, direction);
        }

        MotorLunge StartAttack(CombatTuning t, Vector3 viewForward)
        {
            attacking = true;
            killsThisAttack = 0;
            cooldownTimer = t.attackCooldown;

            CombatEnemy target = targeting != null ? targeting.Selected : null;
            if (target != null && target.IsAlive)
            {
                attackDirection = DirectionOr(target.HurtboxCenter - frameStart, FacingDirection(viewForward));

                // Out of reach: lunge at it with the blade drawn back, and swing on contact (see AfterMove).
                if (!InReach(target, frameStart, frameStart, t.hitReach) && motor.CanLunge)
                {
                    lungeTarget = target;
                    activeTimer = 0f;
                    if (blade != null)
                        blade.PlayWindup();
                    return new MotorLunge
                    {
                        Goal = target.HurtboxCenter,
                        Speed = t.lungeSpeed,
                        Duration = t.lungeMaxDuration,
                        TurnRate = t.lungeTurnRate,
                    };
                }
            }
            else
            {
                // Nothing for the blade: the pulse, at something further away.
                CombatEnemy far = targeting != null ? targeting.PulseSelected : null;
                if (far != null && far.IsAlive && pulse != null && pulseCooldownTimer <= 0f)
                {
                    Vector3 origin = frameStart + Vector3.up * 0.3f;
                    pulse.Fire(origin, far, t.pulseSpeed, this);
                    pulseCooldownTimer = t.pulseCooldown;
                    cooldownTimer = t.killCooldown;
                    attacking = false;
                    PulseFired?.Invoke(origin);
                    return default;
                }
                attackDirection = FacingDirection(viewForward);
            }

            // In reach, or nothing to lunge at: a swing that leaves your movement alone.
            Swing(t);
            return default;
        }

        void Swing(CombatTuning t)
        {
            bool dashing = motor.IsBoosting || motor.Speed >= t.dashSpeed;
            if (dashing)
            {
                kind = AttackKind.Dash;
                attackDirection = DirectionOr(motor.Velocity, attackDirection);
                activeTimer = t.dashActiveTime;
                swingReach = t.dashReach;
                swingAngle = t.dashHitAngle;
            }
            else if (!motor.IsGrounded)
            {
                kind = AttackKind.Aerial;
                activeTimer = t.slashActiveTime;
                swingReach = t.aerialReach;
                swingAngle = t.aerialHitAngle;
            }
            else
            {
                kind = AttackKind.Slash;
                activeTimer = t.slashActiveTime;
                swingReach = t.hitReach;
                swingAngle = t.hitAngle;
            }

            if (blade != null)
                blade.PlaySlash(attackDirection, kind);
            Swung?.Invoke(kind, attackDirection);
        }

        /// <summary>Cuts every live enemy the blade reaches along this frame's path, in front of the swing.</summary>
        void CutInReach(CombatTuning t, Vector3 from, Vector3 to)
        {
            float minDot = Mathf.Cos(swingAngle * Mathf.Deg2Rad);
            IReadOnlyList<CombatEnemy> enemies = CombatEnemy.Active;
            for (int i = 0; i < enemies.Count; i++)
            {
                CombatEnemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || !InReach(enemy, from, to, swingReach))
                    continue;

                Vector3 toEnemy = enemy.HurtboxCenter - from;
                float distance = toEnemy.magnitude;
                if (swingAngle < 180f && distance > 1e-3f && Vector3.Dot(toEnemy / distance, attackDirection) < minDot)
                    continue;

                if (ApplyHit(t, enemy, HitKind.Blade, attackDirection) == HitResult.Deflected)
                    return; // armour stops the swing
            }
        }

        HitResult ApplyHit(CombatTuning t, CombatEnemy enemy, HitKind hitKind, Vector3 direction)
        {
            var hit = new HitInfo
            {
                Origin = motor.BodyCenter,
                Direction = DirectionOr(direction, transform.forward),
                AttackerVelocity = motor.Velocity,
                Kind = hitKind,
            };
            HitResult result = enemy.TryHit(hit);
            if (result == HitResult.Killed)
                RegisterKill(t, enemy, hitKind);
            else if (result == HitResult.Deflected && hitKind != HitKind.Pulse)
                Deflect(t, enemy);
            return result;
        }

        void RegisterKill(CombatTuning t, CombatEnemy enemy, HitKind hitKind)
        {
            killCount++;
            killsThisAttack++;
            lastKillTime = Time.time;

            // The reward is movement: one air boost back (never more than the maximum), the boost ready, a little speed.
            bool refreshed = t.killRefreshesAirBoost && motor.RefreshAirBoost();
            lastKillRefreshedAirBoost = killsThisAttack == 1 ? refreshed : lastKillRefreshedAirBoost || refreshed;
            if (t.killReadiesBoost)
                motor.ReadyBoost();
            if (t.killRechargesPulse)
                pulseCooldownTimer = 0f;
            if (killsThisAttack == 1 && hitKind != HitKind.Pulse)
            {
                motor.AddMomentum(t.killSpeedBonus);
                if (cameraRig != null)
                    cameraRig.KickFieldOfView(t.killFovKick);
                if (blade != null)
                    blade.ShowHit();
                if (kind == AttackKind.Aerial && !motor.IsGrounded && t.aerialKillPop > 0f)
                    motor.PopUp(t.aerialKillPop);
            }

            GameTime.HitStop(enemy.IsLight ? t.killHitStop : t.heavyKillHitStop);
            cooldownTimer = Mathf.Min(cooldownTimer, t.killCooldown);
            Killed?.Invoke(enemy);
        }

        void Deflect(CombatTuning t, CombatEnemy enemy)
        {
            activeTimer = 0f;
            cooldownTimer = t.attackCooldown;
            if (lungeTarget != null)
            {
                motor.EndLunge();
                lungeTarget = null;
            }

            Vector3 away = motor.BodyCenter - enemy.HurtboxCenter;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -attackDirection;
            motor.Knock(away * t.deflectKnockback + Vector3.up * DeflectLift);
            GameTime.HitStop(t.deflectHitStop, 0.05f);
            Deflected?.Invoke(enemy, Vector3.Lerp(motor.BodyCenter, enemy.HurtboxCenter, 0.5f));
        }

        void StopAttack()
        {
            if (lungeTarget != null && motor != null)
                motor.EndLunge();
            attacking = false;
            lungeTarget = null;
            activeTimer = 0f;
            killsThisAttack = 0;
        }

        /// <summary>Where an untargeted swing goes: the way you're moving, or where the camera looks when standing still.</summary>
        Vector3 FacingDirection(Vector3 viewForward)
        {
            Vector3 travel = motor.PlanarVelocity;
            if (travel.sqrMagnitude > MovingSpeed * MovingSpeed)
                return travel.normalized;
            viewForward.y = 0f;
            return viewForward.sqrMagnitude > 1e-4f ? viewForward.normalized : Vector3.forward;
        }

        /// <summary>Whether the blade reaches the enemy's hurtbox anywhere along the path from <paramref name="from"/> to <paramref name="to"/>.</summary>
        static bool InReach(CombatEnemy enemy, Vector3 from, Vector3 to, float reach)
        {
            float limit = reach + enemy.HurtboxRadius;
            Vector3 centre = enemy.HurtboxCenter;
            Vector3 path = to - from;
            float length = path.sqrMagnitude;
            float along = length > 1e-8f ? Mathf.Clamp01(Vector3.Dot(centre - from, path) / length) : 0f;
            return (from + path * along - centre).sqrMagnitude <= limit * limit;
        }

        static Vector3 DirectionOr(Vector3 vector, Vector3 fallback)
        {
            float length = vector.magnitude;
            return length > 1e-4f ? vector / length : fallback;
        }
    }
}
