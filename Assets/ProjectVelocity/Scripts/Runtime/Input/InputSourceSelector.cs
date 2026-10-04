using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

namespace ProjectVelocity
{
    /// <summary>
    /// Picks the player's input source at startup: touch controls on Android/iOS, keyboard/mouse (or gamepad) everywhere else,
    /// so the same scene works on every platform without rewiring. In the Editor the choice can be forced from the Inspector.
    /// Also lifts the frame-rate cap on phones, which otherwise run at 30 FPS.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class InputSourceSelector : MonoBehaviour
    {
        public enum EditorControls
        {
            /// <summary>Keyboard/mouse in the Game view; touch controls in the Device Simulator when it simulates a phone.</summary>
            Auto,
            Desktop,
            /// <summary>Touch controls in the Game view, with the mouse acting as one finger.</summary>
            Mobile,
        }

        [SerializeField] VelocityPlayerController player;
        [SerializeField] DesktopInputSource desktopInput;
        [SerializeField] MobileInputSource mobileInput;

        [Tooltip("Editor only (builds always pick by platform). Auto: keyboard/mouse in the Game view, touch controls in the " +
                 "Device Simulator. Mobile: touch controls in the Game view, the mouse acting as one finger. Read when Play starts.")]
        [SerializeField] EditorControls editorControls = EditorControls.Auto;

        [Tooltip("Frame-rate cap on Android/iOS, which default to 30 FPS. 0 keeps the platform default.")]
        [SerializeField, Min(0)] int mobileFrameRate = 60;

#if UNITY_EDITOR
        bool simulatingTouch;
#endif

        public VelocityPlayerController Player
        {
            get => player;
            set => player = value;
        }

        public DesktopInputSource DesktopInput
        {
            get => desktopInput;
            set => desktopInput = value;
        }

        public MobileInputSource MobileInput
        {
            get => mobileInput;
            set => mobileInput = value;
        }

        /// <summary>Editor-only override, read when Play starts. Builds always pick by platform.</summary>
        public EditorControls EditorOverride
        {
            get => editorControls;
            set => editorControls = value;
        }

        /// <summary>True when the touch controls were picked.</summary>
        public bool UsingTouchControls { get; private set; }

        void Awake()
        {
            if (player == null)
                player = GetComponent<VelocityPlayerController>();
            if (desktopInput == null)
                desktopInput = GetComponent<DesktopInputSource>();
            if (mobileInput == null)
                mobileInput = GetComponent<MobileInputSource>();

            bool touch = mobileInput != null && (WantsTouchControls() || desktopInput == null);
            UsingTouchControls = touch;

            // Runs before the sources wake up, so the unused one never grabs the cursor or shows its controls.
            if (desktopInput != null)
                desktopInput.enabled = !touch;
            if (mobileInput != null)
                mobileInput.enabled = touch;
            if (player != null)
                player.InputSource = touch ? mobileInput : desktopInput;

            if (Application.isMobilePlatform && mobileFrameRate > 0)
                Application.targetFrameRate = mobileFrameRate;

#if UNITY_EDITOR
            // No touchscreen in the Game view: let the mouse play one finger. The Device Simulator brings its own.
            if (touch && Touchscreen.current == null)
            {
                TouchSimulation.Enable();
                simulatingTouch = true;
            }
#endif
        }

#if UNITY_EDITOR
        void OnDestroy()
        {
            if (simulatingTouch)
                TouchSimulation.Disable();
        }
#endif

        bool WantsTouchControls()
        {
#if UNITY_EDITOR
            if (editorControls == EditorControls.Desktop)
                return false;
            if (editorControls == EditorControls.Mobile)
                return true;
#endif
            // The Device namespace reports the simulated phone in the Device Simulator, and the real platform elsewhere.
            return UnityEngine.Device.Application.isMobilePlatform;
        }
    }
}
