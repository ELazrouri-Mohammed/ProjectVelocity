using UnityEngine;
using DeviceScreen = UnityEngine.Device.Screen;

namespace ProjectVelocity
{
    /// <summary>
    /// Tiny developer readout (speed, state, boost, target, attack, reality sequence) to help put numbers on how movement feels.
    /// Not game UI: disable the component to hide it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementDebugHUD : MonoBehaviour
    {
        // The text is rebuilt this often (s) rather than every frame, so the readout doesn't churn garbage on phones.
        const float RefreshInterval = 0.1f;

        [SerializeField] VelocityMotor motor;

        [Tooltip("Traversal target selection to report. Optional.")]
        [SerializeField] TraversalTargeting targeting;

        [Tooltip("Blade combat to report (attack state, selected enemy, last kill). Optional.")]
        [SerializeField] CombatController combat;

        [Tooltip("Player controller whose input source supplies the control reminder. Optional.")]
        [SerializeField] VelocityPlayerController player;

        [Tooltip("Show the control reminder at the bottom of the screen (keyboard/mouse only; touch controls are labelled).")]
        [SerializeField] bool showControls = true;

        GUIStyle style;
        float smoothedFrameTime;
        float refreshTimer;
        string stats;
        bool showsReality;

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        public TraversalTargeting Targeting
        {
            get => targeting;
            set => targeting = value;
        }

        public VelocityPlayerController Player
        {
            get => player;
            set => player = value;
        }

        public CombatController Combat
        {
            get => combat;
            set => combat = value;
        }

        void Awake()
        {
            if (targeting == null)
                targeting = GetComponent<TraversalTargeting>();
            if (player == null)
                player = GetComponent<VelocityPlayerController>();
            if (combat == null)
                combat = GetComponent<CombatController>();

            // Labels only: skip the IMGUI layout pass.
            useGUILayout = false;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            smoothedFrameTime = smoothedFrameTime <= 0f ? dt : Mathf.Lerp(smoothedFrameTime, dt, 0.05f);

            refreshTimer -= dt;
            if (refreshTimer <= 0f || stats == null)
            {
                refreshTimer = RefreshInterval;
                stats = motor != null ? BuildStats() : null;
            }
        }

        string BuildStats()
        {
            float speed = motor.Speed;
            string state = motor.State switch
            {
                MotorState.Target => "TARGET (pulled through)",
                MotorState.Lunge => "LUNGE",
                MotorState.Wall => $"WALL   ({motor.WallRunTimeRemaining:0.0}s left)",
                MotorState.Boost => "BOOST",
                MotorState.Ground => "GROUND",
                _ => "AIR",
            };
            string boost = motor.BoostCooldownRemaining > 0f ? $"{motor.BoostCooldownRemaining:0.00}s" : "ready";
            string wallJump = motor.LastWallJumpUpSpeed < 0f ? "-"
                : $"up {motor.LastWallJumpUpSpeed:0.0} m/s" + (motor.LastWallJumpAssisted ? "  (assisted: lowered to reach a lower wall)" : "");
            float fps = smoothedFrameTime > 0f ? 1f / smoothedFrameTime : 0f;

            string target = "none";
            if (targeting != null && targeting.Selected != null)
            {
                target = motor.IsTargetPulling ? $"launching   {targeting.SelectedDistance:0.0} m"
                    : $"selected   {targeting.SelectedDistance:0.0} m   {targeting.SelectedAngle:0}°" + (motor.CanActivateTarget ? "" : "   (cooldown)");
            }

            string reality = BuildReality();
            showsReality = reality != null;

            return
                $"Speed  {speed:0.0} m/s  ({speed * 3.6f:0} km/h)   vertical {motor.Velocity.y:+0.0;-0.0;0.0}\n" +
                $"State  {state}\n" +
                $"Boost  {boost}   air boosts left {motor.AirBoostsRemaining}\n" +
                $"Last wall jump  {wallJump}\n" +
                $"Target  {target}\n" +
                (combat != null ? BuildCombat() + "\n" : "") +
                (reality != null ? reality + "\n" : "") +
                $"FPS    {fps:0}";
        }

        /// <summary>
        /// How many reality stages have gone off, and the one moving right now (the latest to start); for a few seconds after
        /// moving architecture resets you, what did it instead. Null when the scene has none.
        /// </summary>
        static string BuildReality()
        {
            var sequences = RealityTransformSequence.Active;
            if (sequences.Count == 0)
                return null;

            string failure = RealityTransformSequence.RecentFailure;
            if (failure != null)
                return $"Reality  RESET: {failure}";

            int started = 0;
            RealityTransformSequence moving = null;
            for (int i = 0; i < sequences.Count; i++)
            {
                RealityTransformSequence sequence = sequences[i];
                if (sequence.State == RealityTransformSequence.Phase.Dormant)
                    continue;
                started++;
                if (sequence.State == RealityTransformSequence.Phase.Playing && (moving == null || sequence.Elapsed < moving.Elapsed))
                    moving = sequence;
            }

            string stages = $"Reality  {started}/{sequences.Count} stages";
            if (moving != null)
                return $"{stages}   {moving.name}  {moving.Elapsed:0.0}s";
            return started == sequences.Count ? stages + "   all in place (R / RESET puts it back)" : stages;
        }

        string BuildCombat()
        {
            string attack = combat.Phase switch
            {
                CombatController.AttackPhase.Lunging => "LUNGE",
                CombatController.AttackPhase.Slashing => "SLASH",
                CombatController.AttackPhase.Cooldown => $"{combat.CooldownRemaining:0.00}s",
                _ => "ready",
            };

            CombatTargeting enemies = combat.Targeting;
            string enemy = enemies != null && enemies.Selected != null
                ? $"{enemies.SelectedDistance:0.0} m  {enemies.SelectedAngle:0}°"
                : "none";

            string kill = combat.LastKillTime < 0f ? "-"
                : $"{Time.time - combat.LastKillTime:0.0}s ago" +
                  (combat.LastKillRefreshedAirBoost ? "  (+1 air boost)" : "  (air boost was already ready)");

            return $"Attack  {attack}   enemy {enemy}   kills {combat.KillCount}   last kill {kill}";
        }

        void OnGUI()
        {
            if (motor == null || stats == null || Event.current.type != EventType.Repaint)
                return;

            if (style == null)
                style = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = false };
            int screenHeight = DeviceScreen.height;
            style.fontSize = Mathf.Max(12, Mathf.RoundToInt(screenHeight / 45f));

            // Stay inside the safe area (camera cutouts, rounded corners). GUI y runs top-down, the safe area bottom-up.
            Rect safe = DeviceScreen.safeArea;
            float left = safe.xMin + 16f;
            float width = safe.width - 32f;
            float lineHeight = style.fontSize * 1.5f;
            float lines = (combat != null ? 7.5f : 6.5f) + (showsReality ? 1f : 0f);
            DrawShadowed(new Rect(left, screenHeight - safe.yMax + 12f, width, lineHeight * lines), stats);

            string controls = showControls && player != null && player.InputSource != null ? player.InputSource.ControlsHint : null;
            if (!string.IsNullOrEmpty(controls))
                DrawShadowed(new Rect(left, screenHeight - safe.yMin - lineHeight - 12f, width, lineHeight), controls);
        }

        void DrawShadowed(Rect rect, string text)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }
    }
}
