using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Tiny developer readout (speed, state, boost) to help put numbers on how movement feels.
    /// Not game UI: disable the component to hide it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementDebugHUD : MonoBehaviour
    {
        [SerializeField] VelocityMotor motor;

        [Tooltip("Show the control reminder at the bottom of the screen.")]
        [SerializeField] bool showControls = true;

        GUIStyle style;
        float smoothedFrameTime;

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            smoothedFrameTime = smoothedFrameTime <= 0f ? dt : Mathf.Lerp(smoothedFrameTime, dt, 0.05f);
        }

        void OnGUI()
        {
            if (motor == null)
                return;

            if (style == null)
                style = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = false };
            style.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 45f));

            float speed = motor.Speed;
            string state = motor.State switch
            {
                MotorState.Wall => $"WALL   ({motor.WallRunTimeRemaining:0.0}s left)",
                MotorState.Boost => "BOOST",
                MotorState.Ground => "GROUND",
                _ => "AIR",
            };
            string boost = motor.BoostCooldownRemaining > 0f ? $"{motor.BoostCooldownRemaining:0.00}s" : "ready";
            string wallJump = motor.LastWallJumpUpSpeed < 0f ? "-"
                : $"up {motor.LastWallJumpUpSpeed:0.0} m/s" + (motor.LastWallJumpAssisted ? "  (assisted: lowered to reach a lower wall)" : "");
            float fps = smoothedFrameTime > 0f ? 1f / smoothedFrameTime : 0f;

            string stats =
                $"Speed  {speed:0.0} m/s  ({speed * 3.6f:0} km/h)   vertical {motor.Velocity.y:+0.0;-0.0;0.0}\n" +
                $"State  {state}\n" +
                $"Boost  {boost}   air boosts left {motor.AirBoostsRemaining}\n" +
                $"Last wall jump  {wallJump}\n" +
                $"FPS    {fps:0}";

            float lineHeight = style.fontSize * 1.5f;
            DrawShadowed(new Rect(16f, 12f, Screen.width - 32f, lineHeight * 5.5f), stats);

            if (showControls)
            {
                const string controls = "WASD move   Mouse look   Space jump / wall jump   Shift boost   R respawn   Esc free cursor";
                DrawShadowed(new Rect(16f, Screen.height - lineHeight - 12f, Screen.width - 32f, lineHeight), controls);
            }
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
