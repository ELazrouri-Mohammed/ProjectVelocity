using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectVelocity
{
    /// <summary>
    /// Keyboard + mouse input for desktop testing (a gamepad also works), built on the Input System.
    /// This is the only place that knows about physical devices.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DesktopInputSource : VelocityInputSource
    {
        [Tooltip("Lock and hide the cursor while playing. Esc frees it, left click captures it again.")]
        [SerializeField] bool lockCursor = true;

        InputAction move;
        InputAction mouseLook;
        InputAction stickLook;
        InputAction jump;
        InputAction boost;
        InputAction activateTarget;
        InputAction attack;
        InputAction respawn;
        InputAction freeCursor;
        InputAction captureCursor;

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            mouseLook = new InputAction("MouseLook", InputActionType.PassThrough, "<Mouse>/delta", expectedControlType: "Vector2");
            stickLook = new InputAction("StickLook", InputActionType.Value, "<Gamepad>/rightStick", expectedControlType: "Vector2");

            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            boost = new InputAction("Boost", InputActionType.Button, "<Keyboard>/leftShift");
            boost.AddBinding("<Gamepad>/rightShoulder");
            boost.AddBinding("<Gamepad>/buttonEast");

            // Desktop binding for the traversal-target button (ACTION on touch screens).
            activateTarget = new InputAction("ActivateTarget", InputActionType.Button, "<Keyboard>/e");
            activateTarget.AddBinding("<Gamepad>/buttonWest");

            // Blade attack (ATTACK on touch screens). The left click that recaptures a freed cursor doesn't attack.
            attack = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            attack.AddBinding("<Keyboard>/f");
            attack.AddBinding("<Gamepad>/rightTrigger");
            attack.AddBinding("<Gamepad>/buttonNorth");

            respawn = new InputAction("Respawn", InputActionType.Button, "<Keyboard>/r");
            respawn.AddBinding("<Gamepad>/select");

            freeCursor = new InputAction("FreeCursor", InputActionType.Button, "<Keyboard>/escape");
            captureCursor = new InputAction("CaptureCursor", InputActionType.Button, "<Mouse>/leftButton");
        }

        void OnEnable()
        {
            SetActionsEnabled(true);
            if (lockCursor)
                CaptureCursor();
        }

        void OnDisable()
        {
            SetActionsEnabled(false);
            FreeCursor();
        }

        void OnDestroy()
        {
            move?.Dispose();
            mouseLook?.Dispose();
            stickLook?.Dispose();
            jump?.Dispose();
            boost?.Dispose();
            activateTarget?.Dispose();
            attack?.Dispose();
            respawn?.Dispose();
            freeCursor?.Dispose();
            captureCursor?.Dispose();
        }

        public override string ControlsHint =>
            "WASD move   Mouse look   Space jump / wall jump   Shift boost   LMB / F attack   E link (hold to swing)   R respawn   Esc free cursor";

        public override PlayerIntent ReadIntent()
        {
            bool clickCapturedCursor = false;
            if (freeCursor.WasPressedThisFrame())
                FreeCursor();
            else if (lockCursor && captureCursor.WasPressedThisFrame())
            {
                clickCapturedCursor = Cursor.lockState != CursorLockMode.Locked;
                CaptureCursor();
            }

            // While the cursor is free the mouse is being used for the editor, not the camera.
            bool mouseLookActive = !lockCursor || Cursor.lockState == CursorLockMode.Locked;

            return new PlayerIntent
            {
                Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f),
                LookDelta = mouseLookActive ? mouseLook.ReadValue<Vector2>() : Vector2.zero,
                LookRate = stickLook.ReadValue<Vector2>(),
                JumpPressed = jump.WasPressedThisFrame(),
                JumpHeld = jump.IsPressed(),
                BoostPressed = boost.WasPressedThisFrame(),
                TargetPressed = activateTarget.WasPressedThisFrame(),
                TargetHeld = activateTarget.IsPressed(),
                AttackPressed = attack.WasPressedThisFrame() && !clickCapturedCursor,
                RespawnPressed = respawn.WasPressedThisFrame(),
            };
        }

        void SetActionsEnabled(bool active)
        {
            foreach (InputAction action in new[] { move, mouseLook, stickLook, jump, boost, activateTarget, attack, respawn, freeCursor, captureCursor })
            {
                if (action == null)
                    continue;
                if (active)
                    action.Enable();
                else
                    action.Disable();
            }
        }

        static void CaptureCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        static void FreeCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
