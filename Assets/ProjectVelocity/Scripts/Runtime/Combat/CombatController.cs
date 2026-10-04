using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// The blade attack: one slash, built to keep you moving. Attack swings at once, or, when <see cref="CombatTargeting"/>
    /// has an enemy selected beyond the blade's reach, asks the motor to lunge at it (your velocity is turned toward it and
    /// never slowed) and swings on contact. One hit kills. A kill keeps your exit momentum, gives back one air boost and adds
    /// a little speed. A miss is a quick slash that leaves your movement alone.
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

        // Below this horizontal speed (m/s) an untargeted swing goes where the camera looks rather than where you're moving.
        const float MovingSpeed = 2f;

        [Tooltip("Combat tuning asset. Changes made to it in Play Mode are kept when you stop playing.")]
        [SerializeField] CombatTuning tuning;

        [SerializeField] VelocityMotor motor;

        [Tooltip("Picks the enemy Attack lunges at.")]
        [SerializeField] CombatTargeting targeting;

        [Tooltip("Placeholder blade and slash effect. Optional.")]
        [SerializeField] BladeVisual blade;

        [Tooltip("Camera for the tiny kill feedback (Kill Fov Kick in Combat Tuning). Optional.")]
        [SerializeField] VelocityCamera cameraRig;

        CombatTuning fallbackTuning;
        float cooldownTimer;
        float bufferTimer;
        float activeTimer;
        bool attacking;
        CombatEnemy lungeTarget;
        Vector3 attackDirection = Vector3.forward;
        // Body centre before this frame's move: hits are checked along the whole path, so speed never skips an enemy.
        Vector3 frameStart;
        int killsThisAttack;

        int killCount;
        float lastKillTime = -1f;
        bool lastKillRefreshedAirBoost;

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

        public AttackPhase Phase =>
            lungeTarget != null ? AttackPhase.Lunging
            : activeTimer > 0f ? AttackPhase.Slashing
            : cooldownTimer > 0f ? AttackPhase.Cooldown
            : AttackPhase.Ready;

        public float CooldownRemaining => cooldownTimer;
        /// <summary>Kills since the scene started. Debug readout.</summary>
        public int KillCount => killCount;
        /// <summary>Time.time of the last kill, or negative before the first. Debug readout.</summary>
        public float LastKillTime => lastKillTime;
        /// <summary>Whether the last kill gave back a spent air boost. Debug readout.</summary>
        public bool LastKillRefreshedAirBoost => lastKillRefreshedAirBoost;

        /// <summary>The tuning in use: the assigned asset, or built-in defaults when none is assigned.</summary>
        CombatTuning Settings
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
            bufferTimer = attackPressed ? t.attackBufferTime : Mathf.Max(0f, bufferTimer - deltaTime);
            frameStart = motor.BodyCenter;

            if (targeting != null)
                targeting.Tick(t, lungeTarget);

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

        /// <summary>Clears the attack and brings every enemy back (called when the player respawns).</summary>
        public void ResetCombat()
        {
            StopAttack();
            cooldownTimer = 0f;
            bufferTimer = 0f;
            if (blade != null)
                blade.ResetPose();
            CombatEnemy.ReviveAll();
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
                attackDirection = FacingDirection(viewForward);
            }

            // In reach, or nothing to lunge at: a quick swing that leaves your movement alone.
            Swing(t);
            return default;
        }

        void Swing(CombatTuning t)
        {
            activeTimer = t.slashActiveTime;
            if (blade != null)
                blade.PlaySlash(attackDirection);
        }

        /// <summary>Kills every live enemy the blade reaches along this frame's path, in front of the swing.</summary>
        void CutInReach(CombatTuning t, Vector3 from, Vector3 to)
        {
            float minDot = Mathf.Cos(t.hitAngle * Mathf.Deg2Rad);
            IReadOnlyList<CombatEnemy> enemies = CombatEnemy.Active;
            for (int i = 0; i < enemies.Count; i++)
            {
                CombatEnemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || !InReach(enemy, from, to, t.hitReach))
                    continue;

                Vector3 toEnemy = enemy.HurtboxCenter - from;
                float distance = toEnemy.magnitude;
                if (t.hitAngle < 180f && distance > 1e-3f && Vector3.Dot(toEnemy / distance, attackDirection) < minDot)
                    continue;

                Kill(enemy, t);
            }
        }

        void Kill(CombatEnemy enemy, CombatTuning t)
        {
            if (!enemy.Kill(motor.Velocity))
                return;

            killCount++;
            killsThisAttack++;
            lastKillTime = Time.time;

            // The reward is movement: one air boost back (never more than the maximum), the boost ready, a little speed.
            bool refreshed = t.killRefreshesAirBoost && motor.RefreshAirBoost();
            lastKillRefreshedAirBoost = killsThisAttack == 1 ? refreshed : lastKillRefreshedAirBoost || refreshed;
            if (t.killReadiesBoost)
                motor.ReadyBoost();
            if (killsThisAttack == 1)
            {
                motor.AddMomentum(t.killSpeedBonus);
                if (cameraRig != null)
                    cameraRig.KickFieldOfView(t.killFovKick);
                if (blade != null)
                    blade.ShowHit();
            }

            cooldownTimer = Mathf.Min(cooldownTimer, t.killCooldown);
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
