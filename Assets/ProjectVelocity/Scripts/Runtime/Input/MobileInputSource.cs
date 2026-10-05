using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.Utilities;
using DeviceScreen = UnityEngine.Device.Screen;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ProjectVelocity
{
    /// <summary>
    /// Two-thumb touch controls, producing the same <see cref="PlayerIntent"/> as the desktop input: a movement stick for the
    /// left thumb, camera drag everywhere else, and JUMP / BOOST / ACTION (LINK) / ATTACK around the right thumb. Landscape and
    /// portrait screens each get their own layout (Touch Controls Tuning); in portrait the whole upper screen is the look pad.
    /// Every touch belongs to whatever it started on until it lifts, so thumbs never steal each other's controls
    /// and any combination (move + look + button) works at once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileInputSource : VelocityInputSource
    {
        const int NoTouch = -1;
        const int ButtonCount = TouchControlsView.ButtonCount;

        // A fixed stick can be grabbed this far from its centre (in stick radii).
        const float FixedStickGrabRadius = 1.5f;

        // Swipe speed (short screen sides per second) that gets the full look acceleration.
        const float FastSwipeSpeed = 4f;

        [Tooltip("Touch control feel and layout. Changes made to it in Play Mode are kept when you stop playing.")]
        [SerializeField] TouchControlsTuning tuning;

        [Tooltip("On-screen visuals for the controls. Shown only while this input source is in use.")]
        [SerializeField] TouchControlsView view;

        TouchControlsTuning fallbackTuning;

        // Layout in screen pixels, rebuilt when the screen, safe area or tuning changes.
        int layoutWidth;
        int layoutHeight;
        Rect layoutSafeArea;
        TouchControlsTuning layoutTuning;
        int layoutVersion = -1;
        float shortSide = 1f;
        Vector2 stickHome;
        float stickRadius = 1f;
        float movementZoneRight;
        float movementZoneTop;
        readonly Vector2[] buttonCentres = new Vector2[ButtonCount];
        readonly float[] buttonRadii = new float[ButtonCount];
        readonly bool[] buttonVisible = new bool[ButtonCount];

        // Touch ownership: the touch id that owns each control, or NoTouch.
        int stickTouch = NoTouch;
        Vector2 stickCentre;
        Vector2 stickValue;
        int lookTouch = NoTouch;
        Vector2 lookLastPosition;
        readonly int[] buttonTouches = { NoTouch, NoTouch, NoTouch, NoTouch, NoTouch };
        int pressedThisFrame;   // bit per TouchButton

        int processedFrame = -1;
        PlayerIntent intent;

        public TouchControlsTuning Tuning
        {
            get => tuning;
            set => tuning = value;
        }

        public TouchControlsView View
        {
            get => view;
            set => view = value;
        }

        TouchControlsTuning Settings
        {
            get
            {
                if (tuning != null)
                    return tuning;
                if (fallbackTuning == null)
                    fallbackTuning = ScriptableObject.CreateInstance<TouchControlsTuning>();
                return fallbackTuning;
            }
        }

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            ReleaseAll();
            processedFrame = -1;
            intent = default;
            RefreshLayout(Settings, true);
            if (view != null)
                view.SetVisible(true);
        }

        void OnDisable()
        {
            ReleaseAll();
            intent = default;
            if (view != null)
                view.SetVisible(false);
            EnhancedTouchSupport.Disable();
        }

        void OnDestroy()
        {
            if (fallbackTuning != null)
                Destroy(fallbackTuning);
        }

        public override PlayerIntent ReadIntent()
        {
            if (!isActiveAndEnabled)
                return default;
            if (processedFrame == Time.frameCount)
                return intent;
            processedFrame = Time.frameCount;

            TouchControlsTuning t = Settings;
            RefreshLayout(t, false);

            pressedThisFrame = 0;
            Vector2 lookPixels = Vector2.zero;
            ReadOnlyArray<Touch> touches = Touch.activeTouches;

            // Lifted touches let go first, so a touch id the system reuses in the same frame is a clean new touch.
            for (int i = 0; i < touches.Count; i++)
            {
                Touch touch = touches[i];
                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                    continue;
                if (touch.touchId == lookTouch)
                {
                    // The last bit of camera drag still counts on the frame the finger lifts.
                    lookPixels += touch.screenPosition - lookLastPosition;
                    lookTouch = NoTouch;
                }
                ReleaseTouch(touch.touchId);
            }

            // New touches claim the control they started on; held touches drive theirs.
            bool stickSeen = false;
            bool lookSeen = false;
            int buttonsSeen = 0;
            for (int i = 0; i < touches.Count; i++)
            {
                Touch touch = touches[i];
                TouchPhase phase = touch.phase;
                if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
                    continue;

                int id = touch.touchId;
                Vector2 position = touch.screenPosition;
                if (phase == TouchPhase.Began)
                    Claim(id, position, t);

                if (id == stickTouch)
                {
                    UpdateStick(position, t);
                    stickSeen = true;
                }
                else if (id == lookTouch)
                {
                    lookPixels += position - lookLastPosition;
                    lookLastPosition = position;
                    lookSeen = true;
                }
                else
                {
                    for (int b = 0; b < ButtonCount; b++)
                    {
                        if (buttonTouches[b] == id)
                            buttonsSeen |= 1 << b;
                    }
                }
            }

            // Owners whose touch vanished without lifting (focus loss, system gesture) let go too.
            if (!stickSeen && stickTouch != NoTouch)
                ReleaseStick();
            if (!lookSeen)
                lookTouch = NoTouch;
            for (int b = 0; b < ButtonCount; b++)
            {
                if (buttonTouches[b] != NoTouch && (buttonsSeen & (1 << b)) == 0)
                    ReleaseButton(b);
            }

            intent = new PlayerIntent
            {
                Move = stickValue,
                LookDegrees = LookDegrees(lookPixels, t),
                JumpPressed = WasPressed(TouchButton.Jump),
                JumpHeld = buttonTouches[(int)TouchButton.Jump] != NoTouch,
                BoostPressed = WasPressed(TouchButton.Boost),
                TargetPressed = WasPressed(TouchButton.Action),
                TargetHeld = buttonTouches[(int)TouchButton.Action] != NoTouch,
                AttackPressed = WasPressed(TouchButton.Attack),
                RespawnPressed = WasPressed(TouchButton.Reset),
            };
            return intent;
        }

        /// <summary>Gives a new touch to the control it started on: a button, else the stick, else the camera.</summary>
        void Claim(int id, Vector2 position, TouchControlsTuning t)
        {
            // Touch ids get reused; an owner still holding this id missed its release.
            ReleaseTouch(id);

            int button = ButtonAt(position, t);
            if (button >= 0)
            {
                // The newest press owns the button (a second finger on a held button presses it again).
                buttonTouches[button] = id;
                pressedThisFrame |= 1 << button;
                if (view != null)
                    view.SetButtonPressed((TouchButton)button, true);
                return;
            }

            if (stickTouch == NoTouch && InMovementZone(position, t))
            {
                stickTouch = id;
                stickCentre = t.stickMode == TouchControlsTuning.StickMode.Floating ? position : stickHome;
                return;
            }

            if (lookTouch == NoTouch && (position.x >= movementZoneRight || position.y >= movementZoneTop))
            {
                lookTouch = id;
                lookLastPosition = position;
            }
        }

        bool InMovementZone(Vector2 position, TouchControlsTuning t)
        {
            if (t.stickMode == TouchControlsTuning.StickMode.Floating)
                return position.x < movementZoneRight && position.y < movementZoneTop;
            return (position - stickHome).sqrMagnitude <= Sq(stickRadius * FixedStickGrabRadius);
        }

        /// <summary>The visible button under the position (the nearest one if touch areas overlap), or -1.</summary>
        int ButtonAt(Vector2 position, TouchControlsTuning t)
        {
            int best = -1;
            float bestDistance = 1f;
            for (int b = 0; b < ButtonCount; b++)
            {
                if (!buttonVisible[b])
                    continue;
                float distance = (position - buttonCentres[b]).magnitude / (buttonRadii[b] * t.buttonHitScale);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = b;
                }
            }
            return best;
        }

        void UpdateStick(Vector2 position, TouchControlsTuning t)
        {
            Vector2 offset = position - stickCentre;
            float distance = offset.magnitude;
            if (t.stickFollowsThumb && distance > stickRadius)
            {
                stickCentre += offset * ((distance - stickRadius) / distance);
                offset = position - stickCentre;
                distance = stickRadius;
            }

            // Radial dead zone, full tilt slightly before the edge, then the response curve.
            float fullTilt = Mathf.Max(t.fullTiltAt, t.deadZone + 0.01f);
            float amount = Mathf.Clamp01((distance / stickRadius - t.deadZone) / (fullTilt - t.deadZone));
            if (amount > 0f && !Mathf.Approximately(t.responseExponent, 1f))
                amount = Mathf.Pow(amount, t.responseExponent);
            stickValue = distance > 1e-3f ? offset * (amount / distance) : Vector2.zero;

            if (view != null)
                view.SetStick(stickCentre, Vector2.ClampMagnitude(offset, stickRadius), true);
        }

        Vector2 LookDegrees(Vector2 pixels, TouchControlsTuning t)
        {
            if (pixels == Vector2.zero)
                return Vector2.zero;

            // Measure the drag in screen proportions, not pixels, so every phone turns the same for the same thumb travel.
            Vector2 swipe = pixels / shortSide;
            float gain = 1f;
            float dt = Time.unscaledDeltaTime;
            if (t.lookAcceleration > 0f && dt > 0f)
                gain += t.lookAcceleration * Mathf.Clamp01(swipe.magnitude / dt / FastSwipeSpeed);
            return new Vector2(swipe.x, swipe.y * t.verticalLookScale) * (t.lookSensitivity * gain);
        }

        bool WasPressed(TouchButton button)
        {
            return (pressedThisFrame & (1 << (int)button)) != 0;
        }

        void ReleaseTouch(int id)
        {
            if (stickTouch == id)
                ReleaseStick();
            if (lookTouch == id)
                lookTouch = NoTouch;
            for (int b = 0; b < ButtonCount; b++)
            {
                if (buttonTouches[b] == id)
                    ReleaseButton(b);
            }
        }

        void ReleaseStick()
        {
            stickTouch = NoTouch;
            stickValue = Vector2.zero;
            stickCentre = stickHome;
            if (view != null)
                view.SetStick(stickHome, Vector2.zero, false);
        }

        void ReleaseButton(int button)
        {
            buttonTouches[button] = NoTouch;
            if (view != null)
                view.SetButtonPressed((TouchButton)button, false);
        }

        void ReleaseAll()
        {
            ReleaseStick();
            lookTouch = NoTouch;
            for (int b = 0; b < ButtonCount; b++)
                ReleaseButton(b);
            pressedThisFrame = 0;
        }

        /// <summary>Recomputes where everything is, in screen pixels, when the screen, safe area or tuning changes.</summary>
        void RefreshLayout(TouchControlsTuning t, bool force)
        {
            int width = DeviceScreen.width;
            int height = DeviceScreen.height;
            Rect safe = DeviceScreen.safeArea;
            bool screenChanged = width != layoutWidth || height != layoutHeight || safe != layoutSafeArea;
            if (!force && !screenChanged && t == layoutTuning && t.Version == layoutVersion)
                return;

            layoutWidth = width;
            layoutHeight = height;
            layoutSafeArea = safe;
            layoutTuning = t;
            layoutVersion = t.Version;

            // Touch coordinates jump when the screen changes (e.g. flipping between the two landscape sides): start over.
            if (screenChanged && !force)
                ReleaseAll();

            shortSide = Mathf.Max(1f, Mathf.Min(width, height));
            if (width <= 0 || height <= 0)
                return;
            if (safe.width <= 0f || safe.height <= 0f)
                safe = new Rect(0f, 0f, width, height);

            // Same inset on both sides, so the layout doesn't shift when the phone flips and the camera cutout changes side.
            float sideInset = Mathf.Max(safe.xMin, width - safe.xMax, 0f);
            float left = sideInset;
            float right = width - sideInset;
            float bottom = safe.yMin;
            float top = safe.yMax;

            float unit = shortSide / TouchControlsTuning.ReferenceShortSide * t.controlScale;
            bool portrait = height > width;

            Vector2 stickAt = portrait ? t.portraitStickPosition : t.stickPosition;
            stickHome = new Vector2(left + stickAt.x * unit, bottom + stickAt.y * unit);
            stickRadius = Mathf.Max(1f, t.stickRadius * unit);
            movementZoneRight = left + (right - left) * (portrait ? t.portraitMovementZoneWidth : t.movementZoneWidth);
            movementZoneTop = portrait ? bottom + (top - bottom) * t.portraitMovementZoneHeight : float.MaxValue;

            if (portrait)
            {
                SetButton(TouchButton.Jump, BottomRight(right, bottom, t.portraitJumpPosition, unit), t.portraitJumpRadius * unit, true);
                SetButton(TouchButton.Boost, BottomRight(right, bottom, t.portraitBoostPosition, unit), t.portraitBoostRadius * unit, true);
                SetButton(TouchButton.Action, BottomRight(right, bottom, t.portraitActionPosition, unit), t.portraitActionRadius * unit, true);
                SetButton(TouchButton.Attack, BottomRight(right, bottom, t.portraitAttackPosition, unit), t.portraitAttackRadius * unit, true);
                SetButton(TouchButton.Reset, new Vector2(right - t.portraitResetPosition.x * unit, top - t.portraitResetPosition.y * unit),
                    t.resetRadius * unit, t.showResetButton);
            }
            else
            {
                SetButton(TouchButton.Jump, BottomRight(right, bottom, t.jumpPosition, unit), t.jumpRadius * unit, true);
                SetButton(TouchButton.Boost, BottomRight(right, bottom, t.boostPosition, unit), t.boostRadius * unit, true);
                SetButton(TouchButton.Action, BottomRight(right, bottom, t.actionPosition, unit), t.actionRadius * unit, true);
                SetButton(TouchButton.Attack, BottomRight(right, bottom, t.attackPosition, unit), t.attackRadius * unit, true);
                SetButton(TouchButton.Reset, new Vector2(right - t.resetPosition.x * unit, top - t.resetPosition.y * unit), t.resetRadius * unit, t.showResetButton);
            }

            if (view != null)
            {
                view.SetPixelsPerUnit(unit);
                view.SetStickRadius(stickRadius);
                for (int b = 0; b < ButtonCount; b++)
                    view.PlaceButton((TouchButton)b, buttonCentres[b], buttonRadii[b], buttonVisible[b]);
            }

            // A held stick is redrawn by its touch this frame; an idle one goes to its new rest position.
            if (stickTouch == NoTouch)
                ReleaseStick();
        }

        static Vector2 BottomRight(float right, float bottom, Vector2 offset, float unit)
        {
            return new Vector2(right - offset.x * unit, bottom + offset.y * unit);
        }

        void SetButton(TouchButton button, Vector2 centre, float radius, bool visible)
        {
            int index = (int)button;
            buttonCentres[index] = centre;
            buttonRadii[index] = Mathf.Max(1f, radius);
            buttonVisible[index] = visible;
            if (!visible && buttonTouches[index] != NoTouch)
                ReleaseButton(index);
        }

        static float Sq(float value)
        {
            return value * value;
        }
    }
}
