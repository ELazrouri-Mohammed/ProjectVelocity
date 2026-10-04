using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Touch control feel and layout. Stored as an asset so tweaks made while playing are kept after leaving Play Mode.
    /// Sizes and positions are in layout units: 1080 units = the short side of the screen (its height in landscape),
    /// so the controls keep the same proportions on every phone, whatever its resolution.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Velocity/Touch Controls Tuning", fileName = "TouchControlsTuning")]
    public sealed class TouchControlsTuning : ScriptableObject
    {
        /// <summary>Layout units across the short side of the screen.</summary>
        public const float ReferenceShortSide = 1080f;

        public enum StickMode
        {
            /// <summary>The stick centres itself wherever the left thumb lands in the movement zone.</summary>
            Floating,
            /// <summary>The stick stays at its rest position; grab it there.</summary>
            Fixed,
        }

        [Header("Layout")]
        [Tooltip("Scales every control and its distance from the screen corners.")]
        [Range(0.5f, 2f)] public float controlScale = 1f;

        [Header("Movement Stick (left thumb)")]
        [Tooltip("Floating: the stick centres itself wherever the left thumb lands in the movement zone, and returns to its rest " +
                 "position on release. Fixed: the stick stays at its rest position; grab it there.")]
        public StickMode stickMode = StickMode.Floating;

        [Tooltip("Rest position of the stick's centre, from the bottom-left corner of the safe area (units).")]
        public Vector2 stickPosition = new Vector2(300f, 280f);

        [Tooltip("Thumb travel from the centre to full tilt (units).")]
        [Min(10f)] public float stickRadius = 150f;

        [Tooltip("Movement zone width, as a fraction of the safe area from its left edge. Touches that start inside it move; " +
                 "touches that start to its right turn the camera (or press a button).")]
        [Range(0.2f, 0.6f)] public float movementZoneWidth = 0.45f;

        [Tooltip("Fraction of the radius around the centre that is ignored.")]
        [Range(0f, 0.5f)] public float deadZone = 0.1f;

        [Tooltip("Fraction of the radius at which the stick reaches full tilt. Below 1, full speed is easier to hold.")]
        [Range(0.5f, 1f)] public float fullTiltAt = 0.9f;

        [Tooltip("Response curve: 1 = linear. Above 1 = finer control near the centre. Below 1 = high speed sooner.")]
        [Range(0.3f, 3f)] public float responseExponent = 1f;

        [Tooltip("When the thumb goes past the edge, drag the stick along with it, so reversing is always a short move.")]
        public bool stickFollowsThumb;

        [Header("Camera Drag (right thumb)")]
        [Tooltip("Degrees the camera turns for a drag across the whole short side of the screen (its height in landscape). " +
                 "Measured in screen proportions, not pixels, so it feels the same on every phone.")]
        [Min(0f)] public float lookSensitivity = 300f;

        [Tooltip("Vertical look speed relative to horizontal.")]
        [Range(0f, 2f)] public float verticalLookScale = 0.7f;

        [Tooltip("Extra sensitivity for fast swipes. 0 = off (linear). 1 = up to double speed for a very fast flick, " +
                 "so slow drags stay precise while quick turns need less thumb travel.")]
        [Range(0f, 2f)] public float lookAcceleration;

        [Header("Buttons (right thumb)")]
        [Tooltip("Button centres are measured from the bottom-right corner of the safe area: x = leftward, y = upward (units).")]
        public Vector2 jumpPosition = new Vector2(250f, 240f);
        [Min(10f)] public float jumpRadius = 105f;

        public Vector2 boostPosition = new Vector2(520f, 180f);
        [Min(10f)] public float boostRadius = 82f;

        public Vector2 actionPosition = new Vector2(225f, 495f);
        [Min(10f)] public float actionRadius = 82f;

        [Tooltip("Touch area relative to each button's drawn size, so slightly-off presses still count.")]
        [Range(1f, 1.5f)] public float buttonHitScale = 1.15f;

        [Header("Debug")]
        [Tooltip("Small RESET button in the top-right corner: back to the start, like R on desktop.")]
        public bool showResetButton = true;

        [Tooltip("Reset button centre, from the top-right corner of the safe area: x = leftward, y = downward (units).")]
        public Vector2 resetPosition = new Vector2(90f, 80f);
        [Min(10f)] public float resetRadius = 46f;

        [System.NonSerialized] int version;

        /// <summary>Bumped whenever a value is edited in the Inspector, so the layout can refresh live.</summary>
        public int Version => version;

        void OnValidate()
        {
            version++;
        }
    }
}
