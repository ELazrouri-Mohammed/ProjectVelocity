using UnityEngine;
using DeviceScreen = UnityEngine.Device.Screen;

namespace ProjectVelocity
{
    /// <summary>
    /// Tiny developer readout (speed, state, boost, target) to help put numbers on how movement feels.
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

        [Tooltip("Player controller whose input source supplies the control reminder. Optional.")]
        [SerializeField] VelocityPlayerController player;

        [Tooltip("Show the control reminder at the bottom of the screen (keyboard/mouse only; touch controls are labelled).")]
        [SerializeField] bool showControls = true;

        GUIStyle style;
        float smoothedFrameTime;
        float refreshTimer;
        string stats;

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

        void Awake()
        {
            if (targeting == null)
                targeting = GetComponent<TraversalTargeting>();
            if (player == null)
                player = GetComponent<VelocityPlayerController>();

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

            return
                $"Speed  {speed:0.0} m/s  ({speed * 3.6f:0} km/h)   vertical {motor.Velocity.y:+0.0;-0.0;0.0}\n" +
                $"State  {state}\n" +
                $"Boost  {boost}   air boosts left {motor.AirBoostsRemaining}\n" +
                $"Last wall jump  {wallJump}\n" +
                $"Target  {target}\n" +
                $"FPS    {fps:0}";
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
            DrawShadowed(new Rect(left, screenHeight - safe.yMax + 12f, width, lineHeight * 6.5f), stats);

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
